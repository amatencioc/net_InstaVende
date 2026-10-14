/**
 * InstaVende — WhatsApp Web QR Client  v3.0 (multi-sesión)
 * =========================================================
 * Servidor Express que gestiona una sesión de whatsapp-web.js POR NEGOCIO.
 * Normalmente lo lanza y supervisa la app .NET, que le pasa la configuración
 * por variables de entorno.
 *
 * Endpoints (todos salvo /health exigen la cabecera X-WA-Key):
 *   GET  /health                            → { ok, sessions, uptime }
 *   GET  /sessions/:businessId/status       → { state, connected, qrDataUrl, qrExpiresAt, info, qrHash?, unchanged? }
 *   POST /sessions/:businessId/send         → { to, message } → { ok, messageId }
 *   POST /sessions/:businessId/disconnect   → logout + borra la sesión del negocio
 *   POST /sessions/:businessId/reset        → borra la sesión y genera un QR nuevo
 *
 * Estados (state) de cada sesión:
 *   "initializing"   → Chrome arrancando / WhatsApp Web cargando
 *   "qr"             → QR generado, esperando escaneo
 *   "authenticating" → QR escaneado, verificando
 *   "connected"      → Sesión activa
 *   "disconnected"   → Sesión cerrada o error (se reintenta sola salvo cierre intencional)
 *
 * Mensajes entrantes → POST {DOTNET_BASE_URL}/api/webhooks/whatsapp-local/{businessId}
 *   cuerpo JSON { from, body, pushname, messageId }
 *   cabecera X-WA-Signature: sha256=<HMAC-SHA256(cuerpo, WA_SHARED_SECRET)>
 */

'use strict';

const { Client, LocalAuth } = require('whatsapp-web.js');
const express  = require('express');
const qrcode   = require('qrcode');
const axios    = require('axios');
const crypto   = require('crypto');
const fs       = require('fs');
const path     = require('path');

// ── .env opcional ───────────────────────────────────────────────────────────
// process.loadEnvFile no sobrescribe variables ya definidas: las que pasa el
// supervisor .NET tienen prioridad sobre el archivo.
try {
    process.loadEnvFile(path.join(__dirname, '.env'));
} catch (e) {
    if (e.code !== 'ENOENT') log(`Could not load .env: ${e.message}`, 'warn');
}

// ── Config ──────────────────────────────────────────────────────────────────
const PORT          = parseInt(process.env.WA_PORT || '3001', 10);
const HOST          = process.env.WA_HOST || '127.0.0.1';
const DOTNET_BASE   = (process.env.DOTNET_BASE_URL || 'http://localhost:5221').replace(/\/+$/, '');
const SHARED_SECRET = process.env.WA_SHARED_SECRET || '';
const SESSION_DIR   = process.env.WA_SESSION_DIR || path.join(__dirname, 'session');

if (!SHARED_SECRET) {
    log('FATAL: WA_SHARED_SECRET is not set — refusing to start without authentication', 'error');
    process.exit(1);
}

// QR validity on WhatsApp side ~20 s; we set 18 s to have margin
const QR_TTL_MS = 18_000;

// Watchdog: if a session is still "initializing" after this, force a reinit
const WATCHDOG_MS = 90_000;

// An unlinked session showing QRs that nobody polls is retired after this
// (avoids one idle Chrome per business that opened the QR page once).
const QR_IDLE_MS = 5 * 60_000;

// Delay between auto-restored sessions at startup (avoid N Chromes at once)
const RESTORE_STAGGER_MS = 3_000;

// Max time to wait for logout()/destroy() before giving up
const CLIENT_OP_TIMEOUT_MS = 15_000;

// Webhook forwarding: 15 s timeout, up to 3 retries with exponential backoff
const FWD_TIMEOUT_MS = 15_000;
const FWD_RETRIES    = 3;
const FWD_DELAY_MS   = 1_500;

const CHROME_LOCK_FILES = ['lockfile', 'SingletonLock', 'SingletonCookie', 'SingletonSocket'];

// ── Session ─────────────────────────────────────────────────────────────────
// One WaSession per businessId. Each one owns its own Client, LocalAuth folder
// (<SESSION_DIR>/session-<id>), state, timers, watchdog and reinit guard, so
// a failure or logout in one business never touches another.
class WaSession {
    constructor(businessId) {
        this.businessId = businessId;
        this.authDir    = authDirFor(businessId);
        this.client     = null;

        this.state = {
            phase:       'initializing', // initializing | qr | authenticating | connected | disconnected
            qrDataUrl:   null,
            qrExpiresAt: null,           // ISO string — browser shows countdown
            qrHash:      null,           // first 16 hex of sha1(raw QR)
            clientInfo:  null,           // { wid, phone, pushname }
        };

        this.gen            = 0;     // bumps on every new Client; events from old clients are ignored
        this.qrSeq          = 0;     // bumps on every QR; stale async encodes are discarded
        this.closed         = false; // true once removed (disconnect/reset/shutdown) → never reinit
        this.restarting     = null;  // promise of the in-flight (re)initialisation, if any
        this.wipeAuthOnInit = false; // set after auth_failure/LOGOUT → start from a clean folder
        this.lastPolledAt   = Date.now();

        this.reinitTimer = null;     // single pending reinit timer
        this.watchdog    = null;
        this.qrTimer     = null;
    }

    log(msg, level = 'info') { log(`[biz ${this.businessId}] ${msg}`, level); }

    start(delayMs = 0) { this.scheduleReinit(delayMs); }

    // Only one pending reinit timer per session: extra requests are dropped.
    scheduleReinit(delayMs) {
        if (this.closed) return;
        if (this.reinitTimer) {
            this.log(`Reinit already scheduled — ignoring extra request (${delayMs}ms)`);
            return;
        }
        if (delayMs > 0) this.log(`Scheduling (re)initialise in ${delayMs}ms...`);
        this.reinitTimer = setTimeout(() => {
            this.reinitTimer = null;
            this.init().catch(err => this.log(`Init error: ${err.message}`, 'error'));
        }, delayMs);
    }

    // Reinit guard: never two initialisations of the same session at once.
    async init() {
        if (this.closed) return;
        if (this.restarting) {
            this.log('Initialisation already in progress — skipping');
            return;
        }
        this.restarting = this._init().finally(() => { this.restarting = null; });
        return this.restarting;
    }

    async _init() {
        const gen = ++this.gen;
        this.clearTimers();
        this.resetState('initializing');

        // We recreate the Client on each reinitialise because calling initialize()
        // twice on the same instance throws in whatsapp-web.js. The old Chrome must
        // be fully closed before a new one opens the same profile folder.
        await this.teardownClient();
        if (this.closed || gen !== this.gen) return;

        if (this.wipeAuthOnInit) {
            this.wipeAuthOnInit = false;
            this.log('Clearing stale auth folder before reinit');
            await removeDir(this.authDir);
        }
        clearChromeLocks(this.authDir, this.log.bind(this));

        const client = this.createClient(gen);
        this.client = client;
        this.armWatchdog(gen, 'initializing');
        this.log('Client instance created OK, calling initialize()...');

        client.initialize()
            .then(() => {
                if (gen === this.gen) this.log('initialize() resolved — waiting for QR or auth events...');
            })
            .catch(err => {
                if (gen !== this.gen || this.closed) return; // superseded or closed on purpose
                this.log(`Initialize error: ${err.message}`, 'error');
                this.clearTimers();
                this.resetState('disconnected');
                this.scheduleReinit(10_000); // back-off and retry
            });
    }

    createClient(gen) {
        const client = new Client({
            authStrategy: new LocalAuth({ clientId: String(this.businessId), dataPath: SESSION_DIR }),
            puppeteer: {
                headless: true,
                // No executablePath: whatsapp-web.js uses puppeteer's bundled Chrome
                args: [
                    '--no-sandbox',
                    '--disable-setuid-sandbox',
                    '--disable-dev-shm-usage',
                    '--disable-gpu',
                    '--disable-extensions',
                    '--disable-background-networking',
                    '--disable-default-apps',
                    '--disable-sync',
                    '--no-first-run',
                    '--mute-audio',
                    '--disable-features=TranslateUI,BlinkGenPropertyTrees',
                    '--disable-ipc-flooding-protection',
                    '--disable-renderer-backgrounding',
                    '--disable-backgrounding-occluded-windows',
                    '--disable-client-side-phishing-detection',
                    '--disable-hang-monitor',
                    '--disable-popup-blocking',
                    '--disable-prompt-on-repost',
                    '--disable-domain-reliability',
                    '--disable-component-update',
                    '--no-default-browser-check',
                    '--metrics-recording-only',
                    '--safebrowsing-disable-auto-update',
                    '--password-store=basic',
                    '--use-mock-keychain',
                    // Keep disk cache warm for faster subsequent startups
                    '--disk-cache-size=67108864',          // 64 MB cache
                    '--media-cache-size=16777216',          // 16 MB media cache
                ],
            },
        });

        // Every handler is bound to this generation: once the client is replaced
        // or the session closed, late events from the old instance are ignored.
        const on = (event, handler) => client.on(event, (...args) => {
            if (gen !== this.gen || this.closed) return;
            try {
                const r = handler(...args);
                if (r && typeof r.catch === 'function') {
                    r.catch(err => this.log(`Handler '${event}' failed: ${err.message}`, 'error'));
                }
            } catch (err) {
                this.log(`Handler '${event}' failed: ${err.message}`, 'error');
            }
        });

        on('loading_screen', (percent, message) => {
            this.log(`[Chrome] Loading ${percent}% — ${message}`);
            if (percent === 100) this.log('[Chrome] WhatsApp Web fully loaded');
        });

        on('qr', (qr) => this.onQr(qr));

        on('authenticated', () => {
            this.log('Authenticated');
            this.clearQr();
            this.state.phase = 'authenticating';
            // Syncing after the scan can take a while: give it a fresh watchdog window
            this.armWatchdog(gen, 'authenticating');
        });

        on('auth_failure', (msg) => {
            this.log(`Auth failure: ${msg}`, 'error');
            this.clearTimers();
            this.resetState('disconnected');
            this.wipeAuthOnInit = true;
            this.scheduleReinit(3_000);
        });

        on('ready', () => {
            const me = client.info;
            this.clearTimers();
            this.resetState('connected');
            this.state.clientInfo = {
                wid:      me.wid.user,
                phone:    '+' + me.wid.user,
                pushname: me.pushname,
            };
            this.log(`Connected as ${maskPhone(me.wid.user)} — uptime ${Math.floor(process.uptime())}s`);
        });

        on('disconnected', (reason) => {
            this.log(`Disconnected: ${reason}`, 'warn');
            this.clearTimers();
            this.resetState('disconnected');
            // Logged out from the phone → the stored session is useless, start clean
            if (reason === 'LOGOUT') this.wipeAuthOnInit = true;
            // Unexpected disconnect — init() destroys and recreates the client
            this.scheduleReinit(5_000);
        });

        on('message', (msg) => this.onMessage(msg));

        return client;
    }

    onQr(qr) {
        const seq = ++this.qrSeq;
        const authExists = fs.existsSync(this.authDir);
        this.log(`[QR] QR event received (auth folder: ${authExists ? 'yes (may be stale)' : 'none'})`);

        // Nobody has looked at this QR for a while → free the Chrome
        if (Date.now() - this.lastPolledAt > QR_IDLE_MS) {
            this.log(`[QR] No status polls for ${QR_IDLE_MS / 60_000} min — retiring idle session`, 'warn');
            retireIdleSession(this);
            return;
        }

        this.clearQr();
        this.state.phase       = 'qr';
        this.state.clientInfo  = null;
        this.state.qrExpiresAt = new Date(Date.now() + QR_TTL_MS).toISOString();
        const hash = crypto.createHash('sha1').update(qr).digest('hex').slice(0, 16);

        // Encode in background — setImmediate yields to the event loop first.
        // qrHash is published together with the image so the .NET side never
        // caches a hash without its picture.
        setImmediate(async () => {
            try {
                const dataUrl = await qrcode.toDataURL(qr, { width: 280, margin: 2 });
                if (seq !== this.qrSeq || this.state.phase !== 'qr') return; // superseded
                this.state.qrDataUrl = dataUrl;
                this.state.qrHash    = hash;
                const sizeKb = Math.round(dataUrl.length / 1024);
                this.log(`[QR] Encoded OK — ${sizeKb} KB, expires at ${this.state.qrExpiresAt}`);
            } catch (e) {
                if (seq !== this.qrSeq) return;
                this.log(`[QR] Encode error: ${e.message}`, 'error');
                this.clearQr();
            }
        });

        // Auto-clear QR after TTL so browser shows "Generating..." again
        this.qrTimer = setTimeout(() => {
            this.qrTimer = null;
            if (this.state.phase === 'qr' && seq === this.qrSeq) {
                this.log('[QR] QR expired — waiting for new QR from client');
                this.state.qrDataUrl   = null;
                this.state.qrExpiresAt = null;
                this.state.qrHash      = null;
                // stay in 'qr' phase; new QR event will arrive
            }
        }, QR_TTL_MS);
    }

    onMessage(msg) {
        if (msg.fromMe) return;
        const rawFrom = msg.from || '';
        if (rawFrom.endsWith('@g.us'))       return; // group chat
        if (rawFrom === 'status@broadcast')  return; // statuses
        if (rawFrom.endsWith('@broadcast'))  return; // broadcast lists
        if (rawFrom.endsWith('@newsletter')) return; // channels
        if (msg.type !== 'chat')             return; // text only
        const body = typeof msg.body === 'string' ? msg.body.trim() : '';
        if (!body) return;

        const payload = {
            from:      rawFrom.endsWith('@c.us') ? rawFrom.slice(0, -'@c.us'.length) : rawFrom,
            body,
            pushname:  msg._data?.notifyName || null,
            messageId: msg.id?._serialized || null,
        };
        // Never log message content; phone masked
        this.log(`Incoming from ${maskPhone(payload.from)} (${body.length} chars) — forwarding`);
        forwardWithRetry(this.businessId, payload)
            .catch(err => this.log(`Forward error: ${err.message}`, 'error'));
    }

    async send(to, message) {
        const chatId = to.includes('@') ? to : `${to}@c.us`;
        const sent = await this.client.sendMessage(chatId, message);
        this.log(`Sent to ${maskPhone(to)} (${message.length} chars)`);
        return sent?.id?._serialized ?? null;
    }

    // Stops the session for good (disconnect/reset/shutdown). With logout=true
    // the phone link is also revoked. Does not touch any other session.
    async close({ logout = false } = {}) {
        this.closed = true;
        this.clearTimers();
        if (this.reinitTimer) { clearTimeout(this.reinitTimer); this.reinitTimer = null; }
        if (this.restarting) await this.restarting.catch(() => {});
        this.gen++; // ignore any event still coming from the current client

        const client = this.client;
        this.client = null;
        if (client && logout) {
            try { await withTimeout(client.logout(), CLIENT_OP_TIMEOUT_MS, 'logout'); }
            catch (e) { this.log(`Logout failed (ignored): ${e.message}`, 'warn'); }
        }
        if (client) {
            try { await withTimeout(client.destroy(), CLIENT_OP_TIMEOUT_MS, 'destroy'); }
            catch (_) { /* best effort: may already be closed after logout */ }
        }
        this.resetState('disconnected');
        this.log(logout ? 'Session logged out and closed' : 'Session closed');
    }

    async teardownClient() {
        const client = this.client;
        this.client = null;
        if (!client) return;
        try { await withTimeout(client.destroy(), CLIENT_OP_TIMEOUT_MS, 'destroy'); }
        catch (e) { this.log(`Destroy of previous client failed (ignored): ${e.message}`, 'warn'); }
    }

    // Watchdog: if still stuck in `phase` after 90 s something went wrong
    armWatchdog(gen, phase) {
        if (this.watchdog) clearTimeout(this.watchdog);
        this.watchdog = setTimeout(() => {
            this.watchdog = null;
            if (gen === this.gen && !this.closed && this.state.phase === phase) {
                this.log(`Watchdog: still ${phase} after ${WATCHDOG_MS / 1000} s — forcing reinit`, 'warn');
                this.scheduleReinit(3_000);
            }
        }, WATCHDOG_MS);
        this.watchdog.unref(); // don't keep the process alive just for the watchdog
    }

    clearQr() {
        if (this.qrTimer) { clearTimeout(this.qrTimer); this.qrTimer = null; }
        this.state.qrDataUrl   = null;
        this.state.qrExpiresAt = null;
        this.state.qrHash      = null;
    }

    clearTimers() {
        this.clearQr();
        if (this.watchdog) { clearTimeout(this.watchdog); this.watchdog = null; }
    }

    resetState(phase) {
        this.state.phase       = phase;
        this.state.qrDataUrl   = null;
        this.state.qrExpiresAt = null;
        this.state.qrHash      = null;
        this.state.clientInfo  = null;
    }

    statusPayload(prevHash) {
        this.lastPolledAt = Date.now();
        const s = this.state;
        const payload = {
            state:       s.phase,
            connected:   s.phase === 'connected',
            qrDataUrl:   s.qrDataUrl,
            qrExpiresAt: s.qrExpiresAt,
            info:        s.clientInfo,
        };
        if (s.qrDataUrl && s.qrHash) {
            payload.qrHash = s.qrHash;
            // If the caller already has this QR, strip the heavy base64 payload.
            if (prevHash && prevHash === s.qrHash) {
                payload.qrDataUrl = null;
                payload.unchanged = true;
            }
        }
        return payload;
    }
}

// ── Session registry ────────────────────────────────────────────────────────
const sessions = new Map(); // businessId (number) → WaSession
const opLocks  = new Map(); // businessId → tail promise of disconnect/reset/retire ops

// Serialises lifecycle operations (disconnect/reset/retire) of one business.
function withLock(businessId, fn) {
    const prev = opLocks.get(businessId) || Promise.resolve();
    const run  = prev.then(fn);
    const tail = run.catch(() => {});
    opLocks.set(businessId, tail);
    tail.then(() => { if (opLocks.get(businessId) === tail) opLocks.delete(businessId); });
    return run;
}

function getOrCreateSession(businessId, startDelayMs = 0) {
    let session = sessions.get(businessId);
    if (!session) {
        session = new WaSession(businessId);
        sessions.set(businessId, session);
        session.log('Session created');
        session.start(startDelayMs);
    }
    return session;
}

function retireIdleSession(session) {
    withLock(session.businessId, async () => {
        if (sessions.get(session.businessId) === session) sessions.delete(session.businessId);
        await session.close();
        // Never authenticated in this run: the folder holds no valid link, so
        // remove it to avoid auto-restoring an unlinked business on next start.
        await removeDir(session.authDir);
    }).catch(err => session.log(`Retire failed: ${err.message}`, 'error'));
}

// Restore businesses already linked before a restart: one session per
// existing <SESSION_DIR>/session-<id> folder.
function restoreSessions() {
    let entries = [];
    try {
        entries = fs.readdirSync(SESSION_DIR, { withFileTypes: true });
    } catch (e) {
        log(`Could not scan session dir: ${e.message}`, 'warn');
        return;
    }
    let i = 0;
    for (const entry of entries) {
        if (!entry.isDirectory()) continue;
        const m = /^session-(\d+)$/.exec(entry.name);
        if (!m) {
            if (entry.name === 'session') log("[Restore] Legacy single-tenant folder 'session/session' found — ignored", 'warn');
            continue;
        }
        const id = Number(m[1]);
        if (!Number.isSafeInteger(id) || id <= 0) continue;
        log(`[Restore] Restoring session for business ${id}`);
        getOrCreateSession(id, i * RESTORE_STAGGER_MS);
        i++;
    }
    log(`[Restore] ${i} session(s) restored`);
}

// ── Webhook forwarding ──────────────────────────────────────────────────────
// The body is serialised ONCE and that exact string is both signed and sent,
// so the .NET side can verify the HMAC over the raw request body.
async function forwardWithRetry(businessId, payload) {
    const url  = `${DOTNET_BASE}/api/webhooks/whatsapp-local/${businessId}`;
    const body = JSON.stringify(payload);
    const signature = 'sha256=' + crypto.createHmac('sha256', SHARED_SECRET).update(body, 'utf8').digest('hex');
    const tag = `[biz ${businessId}] [Fwd]`;

    for (let attempt = 1; attempt <= FWD_RETRIES + 1; attempt++) {
        let retryable;
        let reason;
        try {
            const res = await axios.post(url, body, {
                timeout: FWD_TIMEOUT_MS,
                headers: { 'Content-Type': 'application/json', 'X-WA-Signature': signature },
                transformRequest: [(data) => data], // send the exact signed string
                validateStatus: () => true,
                maxRedirects: 0,
            });
            if (res.status >= 200 && res.status < 300) {
                if (attempt > 1) log(`${tag} Delivered on attempt ${attempt}`);
                return;
            }
            retryable = res.status >= 500;
            reason    = `HTTP ${res.status}`;
        } catch (err) {
            retryable = true; // network error / timeout
            reason    = err.code || err.message;
        }

        if (!retryable) {
            log(`${tag} Rejected by .NET (${reason}) — not retrying`, 'warn');
            return;
        }
        if (attempt > FWD_RETRIES) {
            log(`${tag} Failed permanently after ${attempt} attempts (${reason})`, 'error');
            return;
        }
        const delay = FWD_DELAY_MS * 2 ** (attempt - 1);
        log(`${tag} Attempt ${attempt} failed (${reason}) — retrying in ${delay}ms`, 'warn');
        await sleep(delay);
    }
}

// ── Express API ─────────────────────────────────────────────────────────────
const app = express();
app.disable('x-powered-by');

// Health check — the only unauthenticated route
app.get('/health', (req, res) => {
    res.json({ ok: true, sessions: sessions.size, uptime: Math.floor(process.uptime()) });
});

// Shared-secret auth: compare sha256 digests so lengths always match and the
// comparison is constant-time.
const SECRET_DIGEST = sha256(SHARED_SECRET);
app.use((req, res, next) => {
    const key = req.get('X-WA-Key');
    if (typeof key !== 'string' || !crypto.timingSafeEqual(sha256(key), SECRET_DIGEST)) {
        return res.status(401).json({ error: 'unauthorized' });
    }
    next();
});

app.use(express.json());

// Status — polled by .NET proxy every ~1-3 s. Creates the session lazily.
// Supports ?qrHash=<prev> to skip re-sending the QR base64 if unchanged.
app.get('/sessions/:businessId/status', async (req, res) => {
    const id = parseBusinessId(req, res);
    if (id === null) return;
    try {
        // Wait for an in-flight disconnect/reset so we don't race its teardown
        if (opLocks.has(id)) await opLocks.get(id);
        const session = getOrCreateSession(id);
        const prevHash = typeof req.query.qrHash === 'string' ? req.query.qrHash : null;
        res.json(session.statusPayload(prevHash));
    } catch (err) {
        log(`[biz ${id}] Status error: ${err.message}`, 'error');
        res.status(500).json({ error: 'status_failed' });
    }
});

// Send a message (called by .NET WhatsAppService)
app.post('/sessions/:businessId/send', async (req, res) => {
    const id = parseBusinessId(req, res);
    if (id === null) return;
    const { to, message } = req.body ?? {};
    if (typeof to !== 'string' || !to.trim() || typeof message !== 'string' || !message) {
        return res.status(400).json({ error: 'to and message are required' });
    }
    const session = sessions.get(id);
    if (!session) return res.status(404).json({ error: 'session not found' });
    if (session.state.phase !== 'connected' || !session.client) {
        return res.status(503).json({ error: 'WhatsApp not connected', state: session.state.phase });
    }
    try {
        const messageId = await session.send(to.trim(), message);
        res.json({ ok: true, messageId });
    } catch (err) {
        session.log(`Send error: ${err.message}`, 'error');
        res.status(500).json({ error: 'send_failed' });
    }
});

// Logout + remove this business's session. Other sessions are untouched.
app.post('/sessions/:businessId/disconnect', async (req, res) => {
    const id = parseBusinessId(req, res);
    if (id === null) return;
    try {
        await withLock(id, async () => {
            const session = sessions.get(id);
            sessions.delete(id);
            if (session) await session.close({ logout: true });
            await removeDir(authDirFor(id));
            log(`[biz ${id}] Disconnected and auth folder removed`);
        });
        res.json({ ok: true });
    } catch (err) {
        log(`[biz ${id}] Disconnect error: ${err.message}`, 'error');
        res.status(500).json({ error: 'disconnect_failed' });
    }
});

// Destroy, wipe auth and start again from scratch (fresh QR).
app.post('/sessions/:businessId/reset', async (req, res) => {
    const id = parseBusinessId(req, res);
    if (id === null) return;
    try {
        await withLock(id, async () => {
            const session = sessions.get(id);
            sessions.delete(id);
            if (session) await session.close();
            await removeDir(authDirFor(id));
            log(`[biz ${id}] Reset — auth folder removed, starting fresh session`);
            getOrCreateSession(id);
        });
        res.json({ ok: true });
    } catch (err) {
        log(`[biz ${id}] Reset error: ${err.message}`, 'error');
        res.status(500).json({ error: 'reset_failed' });
    }
});

app.use((req, res) => res.status(404).json({ error: 'not_found' }));

// eslint-disable-next-line no-unused-vars
app.use((err, req, res, next) => {
    if (err.type === 'entity.parse.failed') return res.status(400).json({ error: 'invalid_json' });
    log(`HTTP error: ${err.message}`, 'error');
    res.status(err.status || 500).json({ error: 'internal_error' });
});

// ── Startup ─────────────────────────────────────────────────────────────────
log(`Node.js ${process.version} — PID ${process.pid}`);
log(`Session dir : ${SESSION_DIR}`);
fs.mkdirSync(SESSION_DIR, { recursive: true });

const server = app.listen(PORT, HOST, () => {
    log(`Server listening on http://${HOST}:${PORT}`);
    log(`Messages will forward to ${DOTNET_BASE}/api/webhooks/whatsapp-local/{businessId}`);
    restoreSessions();
});
server.on('error', (err) => {
    log(`HTTP server error: ${err.message}`, 'error');
    process.exit(1);
});

// ── Graceful shutdown ───────────────────────────────────────────────────────
let shuttingDown = false;
async function shutdown(signal) {
    if (shuttingDown) return;
    shuttingDown = true;
    log(`${signal} received — shutting down gracefully (${sessions.size} session(s))`);
    server.close();
    const all = [...sessions.values()];
    sessions.clear();
    // Hard deadline in case a Chrome refuses to die
    setTimeout(() => process.exit(0), CLIENT_OP_TIMEOUT_MS + 5_000).unref();
    await Promise.allSettled(all.map(s => s.close()));
    process.exit(0);
}
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT',  () => shutdown('SIGINT'));

// Crash loudly: the .NET supervisor restarts the process
process.on('uncaughtException', (err) => {
    log(`Uncaught exception: ${err?.stack || err}`, 'error');
    process.exit(1);
});
process.on('unhandledRejection', (reason) => {
    log(`Unhandled rejection: ${reason?.stack || reason}`, 'error');
});

// ── Helpers ─────────────────────────────────────────────────────────────────
function authDirFor(businessId) {
    // LocalAuth stores the profile at <dataPath>/session-<clientId>
    return path.join(SESSION_DIR, `session-${businessId}`);
}

// Returns a positive integer id, or null after answering 400.
function parseBusinessId(req, res) {
    const raw = req.params.businessId;
    const id  = /^\d+$/.test(raw) ? Number(raw) : NaN;
    if (!Number.isSafeInteger(id) || id <= 0) {
        res.status(400).json({ error: 'invalid businessId' });
        return null;
    }
    return id;
}

async function removeDir(dir) {
    try {
        // Retries cover Windows EBUSY while Chrome releases its files
        await fs.promises.rm(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 500 });
    } catch (e) {
        log(`Could not remove ${dir}: ${e.message}`, 'warn');
    }
}

// Remove Chrome/Chromium lock files that get left behind after a crash or
// an unclean shutdown. These cause "browser already running" errors on the
// next initialize() without losing the WhatsApp auth session.
function clearChromeLocks(profileDir, logFn = log) {
    let cleared = 0;
    for (const name of CHROME_LOCK_FILES) {
        const p = path.join(profileDir, name);
        try {
            if (fs.existsSync(p)) {
                fs.rmSync(p, { force: true });
                logFn(`[Locks] Removed stale lock: ${name}`);
                cleared++;
            }
        } catch (e) {
            logFn(`[Locks] Could not remove lock ${name}: ${e.message}`, 'warn');
        }
    }
    if (cleared === 0) logFn('[Locks] No stale Chrome locks found');
}

function withTimeout(promise, ms, label) {
    let timer;
    const timeout = new Promise((_, reject) => {
        timer = setTimeout(() => reject(new Error(`${label} timed out after ${ms}ms`)), ms);
    });
    return Promise.race([Promise.resolve(promise), timeout]).finally(() => clearTimeout(timer));
}

function sha256(value) {
    return crypto.createHash('sha256').update(String(value), 'utf8').digest();
}

// Privacy: show only the last 4 digits of a phone / WhatsApp id
function maskPhone(value) {
    const digits = String(value ?? '').replace(/\D/g, '');
    return digits.length > 4 ? `***${digits.slice(-4)}` : '****';
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function log(msg, level = 'info') {
    const ts    = new Date().toISOString().replace('T', ' ').slice(0, 19);
    const label = level === 'error' ? 'ERROR' : level === 'warn' ? 'WARN ' : 'INFO ';
    // Everything to stdout: the .NET supervisor captures it as one stream
    console.log(`[${ts}] [WA] ${label} ${msg}`);
}
