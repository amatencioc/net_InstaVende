# InstaVende — Cliente WhatsApp (QR, multi-sesión)

Servidor Node.js que mantiene **una sesión de WhatsApp Web por negocio** (whatsapp-web.js + Chrome headless),
expone una API HTTP local autenticada y reenvía los mensajes entrantes al backend .NET mediante un webhook firmado.

## Requisitos

- Node.js **20.12 o superior** (usa `process.loadEnvFile`).
- `npm install` en esta carpeta. Puppeteer (dependencia de whatsapp-web.js) descarga su propio Chrome; no hace falta tener Chrome instalado.

## Cómo se ejecuta

Normalmente **no se lanza a mano**: la app .NET lo arranca, le pasa las variables de entorno y lo reinicia si el proceso termina
(ante un error no controlado el cliente hace `process.exit(1)` a propósito).

Para pruebas locales:

```bash
cd wa-client
npm install
npm start
```

Si existe un archivo `.env` en esta carpeta se carga al inicio. Las variables ya definidas en el entorno (las que pasa el .NET) tienen prioridad.

## Variables de entorno

| Variable | Por defecto | Descripción |
|---|---|---|
| `WA_SHARED_SECRET` | — (**obligatoria**) | Secreto compartido con el .NET. Sin ella el proceso no arranca. |
| `WA_PORT` | `3001` | Puerto HTTP. |
| `WA_HOST` | `127.0.0.1` | Interfaz de escucha. Mantener en loopback salvo necesidad real. |
| `DOTNET_BASE_URL` | `http://localhost:5221` | URL base del backend .NET (sin barra final). |
| `WA_SESSION_DIR` | `./session` | Carpeta donde se guardan las sesiones de todos los negocios. |

## API

Todas las rutas excepto `/health` exigen la cabecera `X-WA-Key: <WA_SHARED_SECRET>`; si falta o no coincide → `401 {"error":"unauthorized"}`.
`:businessId` debe ser un entero positivo (si no → `400`).

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/health` (sin auth) | `{ ok, sessions, uptime }` |
| GET | `/sessions/:businessId/status?qrHash=<h>` | `{ state, connected, qrDataUrl, qrExpiresAt, info, qrHash?, unchanged? }`. Crea e inicia la sesión si no existe. Si `qrHash` coincide con el QR actual, `qrDataUrl` es `null` y `unchanged: true`. |
| POST | `/sessions/:businessId/send` | Cuerpo `{ to, message }` → `200 { ok, messageId }`; `400` si faltan campos; `404` si la sesión no existe; `503 { error, state }` si no está conectada; `500 { error: "send_failed" }`. |
| POST | `/sessions/:businessId/disconnect` | Cierra sesión en WhatsApp, borra su carpeta y la quita → `{ ok: true }`. No afecta a otros negocios. |
| POST | `/sessions/:businessId/reset` | Destruye la sesión, borra su carpeta y arranca de cero (QR nuevo) → `{ ok: true }`. |

Estados (`state`): `initializing`, `qr`, `authenticating`, `connected`, `disconnected`.
`info` = `{ wid, phone, pushname }` cuando está conectada. `qrHash` = 16 primeros hex del SHA-1 del QR.

## Webhook de mensajes entrantes

Solo se reenvían mensajes de texto (`chat`) de chats individuales; se ignoran mensajes propios, grupos, estados, listas de difusión y canales.

```
POST {DOTNET_BASE_URL}/api/webhooks/whatsapp-local/{businessId}
Content-Type: application/json
X-WA-Signature: sha256=<hex HMAC-SHA256 del cuerpo exacto con WA_SHARED_SECRET>

{"from":"51987654321","body":"Hola","pushname":"Ana","messageId":"false_51987654321@c.us_3EB0..."}
```

- `from`: remitente sin el sufijo `@c.us` (otros sufijos, p. ej. `@lid`, se conservan completos).
- El .NET debe verificar la firma sobre el cuerpo **crudo** recibido.
- Timeout de 15 s; hasta 3 reintentos con backoff ante error de red, timeout o `5xx` (no ante `4xx`). El .NET deduplica por `messageId`.

## Sesiones por negocio

Cada negocio guarda su autenticación en `WA_SESSION_DIR/session-<businessId>/` (perfil de Chrome de `LocalAuth`).
Al arrancar, el cliente restaura automáticamente una sesión por cada carpeta `session-<id>` existente.
Una sesión sin vincular que muestra QR durante 5 min sin que nadie consulte su estado se cierra para liberar Chrome
(se vuelve a crear con la siguiente consulta de estado).

Los logs no incluyen el contenido de los mensajes y enmascaran los teléfonos (solo últimos 4 dígitos).

## Solución de problemas

- **`WA_SHARED_SECRET is not set`**: falta la variable; el .NET debe pasarla (o defínela en `.env` para pruebas).
- **`401 unauthorized`**: la cabecera `X-WA-Key` no coincide con el secreto con el que arrancó el proceso.
- **Fallo al autenticar / QR que no aparece**: `POST /sessions/<id>/reset`, o con el proceso detenido borra `session/session-<id>/`.
- **"browser already running" / perfil bloqueado**: los locks de Chrome se limpian solos al (re)iniciar cada sesión; si persiste, detén el proceso y comprueba que no queden `chrome.exe` huérfanos.
- **Sesión atascada en `initializing`**: un watchdog de 90 s reinicia el cliente automáticamente.
- **Error al descargar/arrancar Chrome**: reinstala dependencias (`npm install`) para que Puppeteer descargue su navegador. En Linux pueden faltar librerías del sistema de Chrome.
- **Puerto ocupado**: cambia `WA_PORT` y la configuración correspondiente en el .NET.
- **Carpeta antigua `session/session/`** (versión de un solo negocio): se ignora; puede borrarse.
