/**
 /**
 * InstaVende — WhatsApp Web QR Client  v2.0
 * ==========================================
 * Express server que gestiona la sesión de whatsapp-web.js.
 *
 * Endpoints:
 *   GET  /status        ? { state, connected, qrDataUrl, qrExpiresAt, info }
 *   POST /send          ? { to, message }
 *   POST /disconnect    ? cierra sesión
 *   GET  /health        ? { ok: true }
 *
 * Estados (state):
 *   "initializing"  ? Puppeteer/Chrome arrancando
 *   "qr"            ? QR generado, esperando escaneo
 *   "authenticating"? QR escaneado, verificando
 *   "connected"     ? Sesión activa
 *   "disconnected"  ? Sesión cerrada o error
 */

'use strict';

const { Client, LocalAuth } = require('whatsapp-web.js');
const express  = require('express');
const qrcode   = require('qrcode');
const axios    = require('axios');
const fs       = require('fs');
const path     = require('path');

// ?? Config ??????????????????????????????????????????????????????????????????
const PORT        = parseInt(process.env.WA_PORT        || '3001', 10);
const DOTNET_BASE = process.env.DOTNET_BASE_URL          || 'http://localhost:5221';
const BUSINESS_ID = process.env.BUSINESS_ID              || '1';
const WEBHOOK_URL = `${DOTNET_BASE}/api/webhooks/whatsapp-local/${BUSINESS_ID}`;
const SESSION_DIR = path.join(__dirname, 'session');

// QR validity on WhatsApp side ? 20 s; we set 18 s to have margin
const QR_TTL_MS  = 18_000;

// Message forward retry config
const FWD_RETRIES  = 3;
const FWD_DELAY_MS = 1500;

// ?? State ????????????????????????????????????????????????????????????????????
const state = {
    phase:        'initializing', // initializing | qr | authenticating | connected | disconnected
    qrDataUrl:    null,
    qrExpiresAt:  null,           // ISO string — browser shows countdown
    clientInfo:   null,           // { wid, phone, pushname }
    qrTimer:      null,           // handle to clear QR after TTL
    intentionalDisconnect: false, // set true before logout() to suppress auto-reinit
};

function clearQrTimer() {
    if (state.qrTimer) { clearTimeout(state.qrTimer); state.qrTimer = null; }
}

// ?? WhatsApp client factory ???????????????????????????????????????????????????
// We recreate the Client on each reinitialise because calling initialize()
// twice on the same instance throws in whatsapp-web.js.
let waClient = null;

function createClient() {
    const client = new Client({
        authStrategy: new LocalAuth({ dataPath: SESSION_DIR }),
        puppeteer: {
            headless: true,
            executablePath: require('puppeteer').executablePath(),
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

    client.on('loading_screen', (percent, message) => {
        log(`[Chrome] Loading ${percent}% — ${message}`);
        if (percent === 0)  log('[Chrome] Browser launched, WhatsApp Web loading...');
        if (percent === 100) log('[Chrome] WhatsApp Web fully loaded');
    });

    client.on('qr', async (qr) => {
        const sessionExists = fs.existsSync(path.join(SESSION_DIR, 'session'));
        log(`[QR] QR event received — uptime ${Math.floor(process.uptime())}s (prior session: ${sessionExists ? 'yes (may be stale)' : 'none'})`);
        clearQrTimer();
        state.phase      = 'qr';
        state.clientInfo = null;
        state.qrDataUrl   = null;
        state.qrExpiresAt = new Date(Date.now() + QR_TTL_MS).toISOString();

        // Encode in background — setImmediate yields to the event loop first
        setImmediate(async () => {
            try {
                state.qrDataUrl = await qrcode.toDataURL(qr, { width: 280, margin: 2 });
                const sizeKb    = Math.round(state.qrDataUrl.length / 1024);
                log(`[QR] Encoded OK — ${sizeKb} KB, expires at ${state.qrExpiresAt} — ready for browser poll`);
            } catch (e) {
                log(`[QR] Encode error: ${e.message}`, 'error');
                state.qrDataUrl   = null;
                state.qrExpiresAt = null;
            }
        });

        // Auto-clear QR after TTL so browser shows "Generating..." again
        state.qrTimer = setTimeout(() => {
            if (state.phase === 'qr') {
                log('QR expired — waiting for new QR from client');
                state.qrDataUrl   = null;
                state.qrExpiresAt = null;
                // stay in 'qr' phase; new QR event will arrive
            }
        }, QR_TTL_MS);
    });

    client.on('authenticated', () => {
        log('Authenticated ?');
        clearQrTimer();
        state.phase       = 'authenticating';
        state.qrDataUrl   = null;
        state.qrExpiresAt = null;
    });

    client.on('auth_failure', (msg) => {
        log(`Auth failure: ${msg}`, 'error');
        clearQrTimer();
        state.phase       = 'disconnected';
        state.qrDataUrl   = null;
        state.qrExpiresAt = null;
        state.clientInfo  = null;
        clearSession();
        scheduleReinit(3000);
    });

    client.on('ready', () => {
        const me = client.info;
        state.phase       = 'connected';
        state.qrDataUrl   = null;
        state.qrExpiresAt = null;
        state.clientInfo  = {
            wid:      me.wid.user,
            phone:    '+' + me.wid.user,
            pushname: me.pushname,
        };
        log(`Connected as ${state.clientInfo.pushname} (${state.clientInfo.phone}) — uptime ${Math.floor(process.uptime())}s`);
    });

    client.on('disconnected', (reason) => {
        log(`Disconnected: ${reason}`, 'warn');
        clearQrTimer();
        state.phase       = 'disconnected';
        state.qrDataUrl   = null;
        state.qrExpiresAt = null;
        state.clientInfo  = null;

        if (state.intentionalDisconnect) {
            state.intentionalDisconnect = false;
            log('Intentional disconnect — not reinitialising.');
            return;
        }

        // Unexpected disconnect — destroy and recreate after short delay
        log('Scheduling reinitialise in 5s...');
        try { client.destroy().catch(() => {}); } catch (_) {}
        scheduleReinit(5000);
    });

    client.on('message', async (msg) => {
        if (msg.isGroupMsg || msg.from === 'status@broadcast') return;
        const from = msg.from.replace('@c.us', '');
        const body = msg.body?.trim();
        if (!body) return;
        log(`Incoming from ${from}: ${body.substring(0, 100)}`);
        await forwardWithRetry({ from, body, businessId: Number(BUSINESS_ID) });
    });

    return client;
}

function scheduleReinit(delayMs) {
    setTimeout(() => {
        log(`Reinitialising client in ${delayMs}ms (new instance)...`);
        state.phase = 'initializing';
        clearChromeLocks();
        log(`[Reinit] Chrome locks cleared, creating new client instance`);
        waClient = createClient();
        if (!waClient) {
            log('[Reinit] FATAL: createClient() returned null/undefined — aborting reinit', 'error');
            return;
        }
        log(`[Reinit] waClient OK — calling initialize()...`);
        // Per-reinit watchdog
        const _reinitWatchdog = setTimeout(() => {
            if (state.phase === 'initializing') {
                log('Watchdog: reinit still stuck after 90 s — retrying', 'warn');
                try { waClient.destroy().catch(() => {}); } catch (_) {}
                scheduleReinit(10000);
            }
        }, 90_000);
        _reinitWatchdog.unref();
        waClient.initialize().catch(err => {
            clearTimeout(_reinitWatchdog);
            log(`Reinitialize error: ${err.message}`, 'error');
            state.phase = 'disconnected';
            scheduleReinit(10000); // back-off and retry
        });
    }, delayMs);
}

// ?? Incoming message helpers ?????????????????????????????????????????????????
async function forwardWithRetry(payload, attempt = 1) {
    try {
        await axios.post(WEBHOOK_URL, payload, { timeout: 8000 });
    } catch (err) {
        if (attempt < FWD_RETRIES) {
            log(`Forward failed (attempt ${attempt}/${FWD_RETRIES}): ${err.message} — retrying in ${FWD_DELAY_MS * attempt}ms`, 'warn');
            await sleep(FWD_DELAY_MS * attempt);
            return forwardWithRetry(payload, attempt + 1);
        }
        log(`Forward failed permanently after ${FWD_RETRIES} attempts: ${err.message}`, 'error');
    }
}

// ?? Initialize ????????????????????????????????????????????????????????????????
log(`Node.js ${process.version} — PID ${process.pid}`);
log(`Session dir : ${SESSION_DIR}`);
try {
    const chromePath = require('puppeteer').executablePath();
    log(`Chrome path : ${chromePath}`);
    const fs2 = require('fs');
    log(`Chrome exists: ${fs2.existsSync(chromePath)}`);
} catch (e) {
    log(`Could not resolve Chrome path: ${e.message}`, 'warn');
}
clearChromeLocks();
waClient = createClient();

// Watchdog null-check: createClient() should never return null/undefined,
// but if it does (e.g. future refactor error) we log clearly instead of
// crashing with a cryptic "Cannot read properties of undefined" error.
if (!waClient) {
    log('FATAL: createClient() returned null/undefined — cannot initialize. Check createClient() for early returns.', 'error');
    process.exit(1);
}
log(`[Init] waClient instance created OK, calling initialize()...`);

// Watchdog: if still in 'initializing' after 90 s something went wrong
const _initWatchdog = setTimeout(() => {
    if (state.phase === 'initializing') {
        log('Watchdog: still initializing after 90 s — forcing reinit', 'warn');
        try { waClient.destroy().catch(() => {}); } catch (_) {}
        scheduleReinit(3000);
    }
}, 90_000);
_initWatchdog.unref(); // don't keep the process alive just for the watchdog

waClient.initialize()
    .then(() => log('[Init] initialize() resolved — waiting for QR or auth events...'))
    .catch(err => {
        clearTimeout(_initWatchdog);
        log(`Initialize error: ${err.message}`, 'error');
        state.phase = 'disconnected';
        scheduleReinit(5000);
    });

// ?? Express API ???????????????????????????????????????????????????????????????
const app = express();
app.use(express.json());
app.disable('x-powered-by');

// Status — polled by .NET proxy every ~1-3 s
// Supports ETag via ?qrHash=<prev> to skip re-sending the QR base64 if unchanged.
app.get('/status', (req, res) => {
    const payload = {
        state:       state.phase,
        connected:   state.phase === 'connected',
        qrDataUrl:   state.qrDataUrl,
        qrExpiresAt: state.qrExpiresAt,
        info:        state.clientInfo,
    };

    // If the caller already has this QR, strip the heavy base64 payload.
    // The browser compares qrHash; if equal it keeps the cached <img> src.
    const prevHash = req.query.qrHash;
    if (prevHash && state.qrDataUrl) {
        const curHash = Buffer.from(state.qrDataUrl).length.toString(16); // fast surrogate hash
        if (prevHash === curHash) {
            res.json({ ...payload, qrDataUrl: null, qrHash: curHash, unchanged: true });
            return;
        }
        res.json({ ...payload, qrHash: curHash });
        return;
    }
    if (state.qrDataUrl) {
        const qrHash = Buffer.from(state.qrDataUrl).length.toString(16);
        res.json({ ...payload, qrHash });
        return;
    }
    res.json(payload);
});

// Health check
app.get('/health', (req, res) => {
    res.json({ ok: true, state: state.phase, uptime: Math.floor(process.uptime()) });
});

// Send a message (called by .NET WhatsAppService)
app.post('/send', async (req, res) => {
    const { to, message } = req.body ?? {};
    if (!to || !message)             return res.status(400).json({ error: 'to and message are required' });
    if (state.phase !== 'connected') return res.status(503).json({ error: 'WhatsApp not connected', state: state.phase });

    try {
        const chatId = to.includes('@') ? to : `${to}@c.us`;
        await waClient.sendMessage(chatId, message);
        log(`Sent to ${to}: ${message.substring(0, 60)}`);
        res.json({ ok: true });
    } catch (err) {
        log(`Send error: ${err.message}`, 'error');
        res.status(500).json({ error: err.message });
    }
});

// Logout
app.post('/disconnect', async (req, res) => {
    clearQrTimer();
    state.intentionalDisconnect = true;  // prevent auto-reinit on the disconnected event
    state.phase       = 'disconnected';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
    state.clientInfo  = null;
    try { await waClient.logout(); } catch (_) {}
    try { await waClient.destroy(); } catch (_) {}
    res.json({ ok: true });
});

const server = app.listen(PORT, () => {
    log(`Server listening on http://localhost:${PORT}`);
    log(`Messages will forward to ${WEBHOOK_URL}`);
});

// ?? Graceful shutdown ?????????????????????????????????????????????????????????
async function shutdown(signal) {
    log(`${signal} received — shutting down gracefully`);
    server.close();
    try { if (waClient) await waClient.destroy(); } catch (_) {}
    process.exit(0);
}
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT',  () => shutdown('SIGINT'));
process.on('uncaughtException',  (err)    => log(`Uncaught exception: ${err.message}`, 'error'));
process.on('unhandledRejection', (reason) => log(`Unhandled rejection: ${reason}`, 'error'));

// ?? Helpers ???????????????????????????????????????????????????????????????????
function clearSession() {
    try {
        if (fs.existsSync(SESSION_DIR)) fs.rmSync(SESSION_DIR, { recursive: true, force: true });
    } catch (e) { log(`Could not clear session: ${e.message}`, 'warn'); }
}

// Remove Chrome/Chromium lock files that get left behind after a crash or
// an unclean shutdown. These cause "browser already running" errors on the
// next initialize() without losing the WhatsApp auth session.
function clearChromeLocks() {
    const lockFiles = ['lockfile', 'SingletonLock', 'SingletonCookie', 'SingletonSocket'];
    // LocalAuth nests the profile under SESSION_DIR/session/
    const profileDir = path.join(SESSION_DIR, 'session');
    let cleared = 0;
    for (const name of lockFiles) {
        const p = path.join(profileDir, name);
        try {
            if (fs.existsSync(p)) {
                fs.rmSync(p, { force: true });
                log(`[Locks] Removed stale lock: ${name}`);
                cleared++;
            }
        } catch (e) {
            log(`[Locks] Could not remove lock ${name}: ${e.message}`, 'warn');
        }
    }
    if (cleared === 0) log('[Locks] No stale Chrome locks found');
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function log(msg, level = 'info') {
    const ts    = new Date().toISOString().replace('T', ' ').slice(0, 19);
    const label = level === 'error' ? '?' : level === 'warn' ? '?' : '?';
    console.log(`[${ts}] [WA] ${label} ${msg}`);
}
