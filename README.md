# InstaVende

**Vendedor con IA que atiende y vende 24/7 por WhatsApp, Instagram y Messenger.**
Responde con el catálogo real, negocia dentro de los límites configurados, arma el carrito, registra pedidos,
envía recordatorios y deriva a un humano cuando hace falta. El comercio lo gestiona todo desde un panel web.

## Componentes

| | Tecnología | Carpeta |
|---|---|---|
| **API** | .NET 10 · ASP.NET Core · EF Core 10 · SQL Server · SignalR · OpenAI | `src/` |
| **Panel web (SPA)** | React 19 · TypeScript · Vite · TanStack Router/Query · Tailwind CSS v4 | `frontend/` |
| **WhatsApp QR** | Node.js · whatsapp-web.js (una sesión por negocio) | `wa-client/` |
| **Pruebas** | xUnit v3 · Vitest | `tests/`, `frontend/src/**/__tests__` |

La arquitectura, convenciones y decisiones están en **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.
El análisis funcional de referencia está en [docs/producto/](docs/producto/).

## Requisitos

- .NET SDK 10 (ver `global.json`)
- Node.js 22+
- SQL Server 2019+ (local, contenedor o Azure SQL)
- Clave de OpenAI para el vendedor IA

## Desarrollo local

```bash
# 1. Configuración sensible con user-secrets (nunca en appsettings)
dotnet user-secrets --project src/InstaVende.Web set "ConnectionStrings:DefaultConnection" "Server=.;Database=InstaVendeDb_Dev;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets --project src/InstaVende.Web set "OpenAI:ApiKey" "sk-..."

# 2. API — aplica migraciones al arrancar · http://localhost:5221 · documentación en /api/docs
dotnet run --project src/InstaVende.Web

# 3. wa-client — la API lo lanza y supervisa; solo hay que instalar dependencias una vez
cd wa-client && npm ci

# 4. Panel — http://localhost:5173 (proxy de /api y /hubs hacia la API)
cd frontend && npm ci && npm run dev
```

### Todo con Docker

```bash
cp deploy/.env.example deploy/.env   # completa las claves
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up --build
# Panel en http://localhost:8080
```

## Comandos útiles

| Tarea | Comando |
|---|---|
| Compilar todo (genera el contrato OpenAPI) | `dotnet build InstaVende.slnx` |
| Pruebas del backend | `dotnet test --solution InstaVende.slnx` |
| Regenerar tipos del cliente desde la API | `cd frontend && npm run gen:api` |
| Verificar el frontend | `cd frontend && npx tsc -b && npm run lint && npm test && npm run build` |
| Nueva migración | `dotnet ef migrations add <Nombre> --project src/InstaVende.Infrastructure --startup-project src/InstaVende.Web --output-dir Data/Migrations` |

## Configuración

| Clave | Descripción |
|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server |
| `OpenAI:ApiKey`, `OpenAI:Model` | Credenciales y modelo del vendedor IA (por defecto `gpt-4o`) |
| `App:PublicBaseUrl` | URL pública (enlaces de imágenes, webhooks e invitaciones) |
| `WhatsAppClient:*` | Proceso wa-client: URL, ruta, autoarranque y secreto compartido |
| `Spa:RootPath` | Opcional: carpeta con el build del SPA para servirlo desde la propia API |
| `Cors:AllowedOrigins` | Solo si el SPA se sirve desde otro origen |
| `Serilog` | Niveles y destinos de log |

## Webhooks de Meta

- WhatsApp Cloud API: `{App:PublicBaseUrl}/api/webhooks/whatsapp/{businessId}`
- Messenger e Instagram: `{App:PublicBaseUrl}/api/webhooks/meta/{businessId}`

El *verify token* y el *App Secret* se configuran por negocio en **Canales**. Sin App Secret los eventos se rechazan.
