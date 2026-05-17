# InstaVende — WhatsApp QR Client

Servidor Node.js local que gestiona la sesión de WhatsApp Web mediante QR.

## Requisitos

- Node.js 18+
- Google Chrome o Chromium instalado (lo usa Puppeteer internamente)

## Instalación

```bash
cd wa-client
npm install
```

## Configuración

Variables de entorno (opcionales, tienen valores por defecto):

| Variable | Default | Descripción |
|---|---|---|
| `WA_PORT` | `3001` | Puerto del servidor |
| `DOTNET_BASE_URL` | `http://localhost:5221` | URL del backend .NET |
| `BUSINESS_ID` | `1` | ID del negocio en la DB |

Para cambiarlas crea un archivo `.env` en esta carpeta:

```
WA_PORT=3001
DOTNET_BASE_URL=http://localhost:5221
BUSINESS_ID=1
```

O instala `dotenv` y agrégalo al inicio de `index.js`:
```bash
npm install dotenv
```
```js
require('dotenv').config();  // primera línea de index.js
```

## Uso

```bash
npm start
```

El servidor expone:

| Endpoint | Método | Descripción |
|---|---|---|
| `/status` | GET | Estado de conexión + QR en base64 |
| `/send` | POST | Envía un mensaje `{ to, message }` |
| `/disconnect` | POST | Cierra la sesión de WhatsApp |

## Flujo completo

1. `npm start` ? genera QR
2. El backend .NET hace polling a `/status` cada 3 s (proxy para el browser)
3. El usuario escanea el QR ? cliente se autentica
4. Mensajes entrantes se reenvían al .NET: `POST /api/webhooks/whatsapp-local/{businessId}`
5. El .NET responde con el bot y llama a `POST /send` para enviar la respuesta

## Solución de problemas

**"Session closed" / fallo al autenticar**
- Borra la carpeta `session/` y reinicia: el QR se regenera limpio.

**Error de Puppeteer / Chrome**
- En Windows: Chrome debe estar instalado en la ruta estándar.
- En Linux: `apt install chromium-browser` y agrega `executablePath: '/usr/bin/chromium-browser'` en las opciones de Puppeteer dentro de `index.js`.

**Puerto ocupado**
- Cambia `WA_PORT` a otro valor (ej. `3002`) y actualiza `appsettings.json`.
