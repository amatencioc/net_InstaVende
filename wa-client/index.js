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
};

function clearQrTimer() {
    if (state.qrTimer) { clearTimeout(state.qrTimer); state.qrTimer = null; }
}

// ?? WhatsApp client ??????????????????????????????????????????????????????????
const waClient = new Client({
    authStrategy: new LocalAuth({ dataPath: SESSION_DIR }),
    puppeteer: {
        headless: true,
        args: [
            '--no-sandbox',
            '--disable-setuid-sandbox',
            '--disable-dev-shm-usage',
            '--disable-gpu',
            '--disable-extensions',
        ],
    },
});

waClient.on('loading_screen', (percent, message) => {
    log(`Loading ${percent}% — ${message}`);
});

waClient.on('qr', async (qr) => {
    log('QR received — scan with WhatsApp');
    clearQrTimer();
    state.phase       = 'qr';
    state.clientInfo  = null;

    try {
        state.qrDataUrl   = await qrcode.toDataURL(qr, { width: 280, margin: 2 });
        state.qrExpiresAt = new Date(Date.now() + QR_TTL_MS).toISOString();
    } catch (e) {
        log(`QR encode error: ${e.message}`, 'error');
        state.qrDataUrl   = null;
        state.qrExpiresAt = null;
    }

    // Auto-clear QR after TTL so browser shows "Generating..." again
    state.qrTimer = setTimeout(() => {
        if (state.phase === 'qr') {
            log('QR expired — waiting for new QR');
            state.qrDataUrl   = null;
            state.qrExpiresAt = null;
            state.phase       = 'initializing';
        }
    }, QR_TTL_MS);
});

waClient.on('authenticated', () => {
    log('Authenticated ?');
    clearQrTimer();
    state.phase       = 'authenticating';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
});

waClient.on('auth_failure', (msg) => {
    log(`Auth failure: ${msg}`, 'error');
    clearQrTimer();
    state.phase       = 'disconnected';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
    state.clientInfo  = null;
    clearSession();
    log('Session cleared — restart the server to get a new QR');
});

waClient.on('ready', () => {
    const me = waClient.info;
    state.phase       = 'connected';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
    state.clientInfo  = {
        wid:      me.wid.user,
        phone:    '+' + me.wid.user,
        pushname: me.pushname,
    };
    log(`Connected as ${state.clientInfo.pushname} (${state.clientInfo.phone}) ?`);
});

waClient.on('disconnected', (reason) => {
    log(`Disconnected: ${reason}`, 'warn');
    clearQrTimer();
    state.phase       = 'disconnected';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
    state.clientInfo  = null;
});

// ?? Incoming message ? forward to .NET with retries ??????????????????????????
waClient.on('message', async (msg) => {
    if (msg.isGroupMsg || msg.from === 'status@broadcast') return;

    const from = msg.from.replace('@c.us', '');
    const body = msg.body?.trim();
    if (!body) return;

    log(`Incoming from ${from}: ${body.substring(0, 100)}`);
    await forwardWithRetry({ from, body, businessId: Number(BUSINESS_ID) });
});

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
waClient.initialize().catch(err => {
    log(`Initialize error: ${err.message}`, 'error');
    state.phase = 'disconnected';
});

// ?? Express API ???????????????????????????????????????????????????????????????
const app = express();
app.use(express.json());
app.disable('x-powered-by');

// Status — polled by .NET proxy every ~1-3 s
app.get('/status', (req, res) => {
    res.json({
        state:       state.phase,
        connected:   state.phase === 'connected',
        qrDataUrl:   state.qrDataUrl,
        qrExpiresAt: state.qrExpiresAt,
        info:        state.clientInfo,
    });
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
    state.phase       = 'disconnected';
    state.qrDataUrl   = null;
    state.qrExpiresAt = null;
    state.clientInfo  = null;
    try { await waClient.logout(); } catch (_) {}
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
    try { await waClient.destroy(); } catch (_) {}
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

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function log(msg, level = 'info') {
    const ts    = new Date().toISOString().replace('T', ' ').slice(0, 19);
    const label = level === 'error' ? '?' : level === 'warn' ? '?' : '?';
    console.log(`[${ts}] [WA] ${label} ${msg}`);
}
