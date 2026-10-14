# Arquitectura de InstaVende

InstaVende es un SaaS de **vendedor con IA** que atiende y vende por WhatsApp, Instagram y Messenger.
El sistema está dividido en piezas desacopladas que se comunican por contratos explícitos.

```
┌──────────────┐  HTTP JSON + cookie   ┌───────────────────────┐   SQL   ┌────────────┐
│  frontend/   │ ───────────────────▶ │  src/InstaVende.Web   │ ──────▶ │ SQL Server │
│  SPA React   │ ◀─── SignalR ─────── │  API REST .NET 10     │         └────────────┘
└──────────────┘   /hubs/inbox         │  + workers en 2º plano│
                                       └──────┬──────────▲─────┘
                         HTTP + X-WA-Key      │          │ webhook firmado (X-WA-Signature)
                                       ┌──────▼──────────┴─────┐       ┌──────────────────┐
                                       │ wa-client/ (Node)     │       │ Meta Graph API   │
                                       │ sesiones QR por negocio│      │ (Cloud/Msgr/IG)  │
                                       └───────────────────────┘       └──────────────────┘
```

| Carpeta | Rol |
|---|---|
| `src/InstaVende.Core` | Dominio: entidades, enums, reglas puras (`Domain/OrderWorkflow`, `Domain/BusinessLocale`) e interfaces. Sin dependencias de infraestructura. |
| `src/InstaVende.Infrastructure` | EF Core (`AppDbContext`, migraciones), cifrado de tokens (`DataProtectionService`). |
| `src/InstaVende.Web` | API REST, autenticación, SignalR, motor del bot, canales y procesos en segundo plano. |
| `frontend/` | SPA (React 19 + TypeScript + Vite). Solo depende del contrato OpenAPI. |
| `wa-client/` | Proceso Node que gestiona las sesiones de WhatsApp Web (QR) por negocio. |
| `legacy/` | UI MVC anterior, solo como referencia funcional. No se compila. |

## Backend (`src/InstaVende.Web`)

### Organización
- `Controllers/` — infraestructura de la API: `TenantController` (base), `AuthController`, `WebhooksController`.
- `Features/{Modulo}/` — un módulo funcional por carpeta: controlador + DTOs del módulo.
  Namespace `InstaVende.Web.Features.{Modulo}`.
- `Services/Bot/` — motor del vendedor IA (contexto cacheado, comandos, carrito, herramientas, prompts).
- `Services/Messaging/` — pipeline de mensajes: cola de entrada, manejador idempotente, despachador de salida, recordatorios, notificaciones SignalR.
- `Services/Channels/` — emisores por canal (WhatsApp Cloud/QR, Messenger, Instagram), cliente de wa-client y su supervisor.
- `Infrastructure/` — CSRF, rate limiting, roles, cabeceras de seguridad.

### Convenciones de la API
- Rutas bajo `/api/{recurso}` en kebab-case y plural (`/api/products`, `/api/delivery-zones`).
- Todo controlador del panel hereda de `TenantController` (`[ApiController]`, `[Authorize]`, tenant resuelto).
  **Toda consulta filtra por `BusinessId`** (`this.BusinessId`). Las entidades hijas se validan contra el negocio
  (p.ej. un `ContactId` recibido debe pertenecer al negocio).
- Acciones de administración: `[RequireBusinessAdmin]`.
- DTOs como `record` con nombre prefijado por el módulo (`ProductDto`, `ProductUpsertRequest`), nunca entidades EF.
  Los nombres deben ser únicos en toda la API (el esquema OpenAPI usa el nombre del tipo).
- Validación con DataAnnotations en los DTOs (mensajes en español). `[ApiController]` devuelve 400
  `ValidationProblemDetails` automáticamente.
- Errores de negocio: `ProblemDetails` con `detail` legible (`NotFoundProblem()`, `BadRequestProblem("…")`, `ConflictProblem("…")`).
- Respuestas tipadas: `ActionResult<T>` + `[ProducesResponseType]` para que el contrato OpenAPI sea preciso.
- Listados grandes: `PagedResult<T>` (`Features/Shared/PagedResult.cs`) con `page`/`pageSize`.
- Fechas siempre en UTC (ISO-8601 con `Z`). Para "hoy" o agrupar por día usar `Tenant.TimeZone` / `BusinessLocale`.
- Enums se serializan como texto (`"WhatsApp"`, `"EnValidacion"`).
- Lecturas con `AsNoTracking()` y proyecciones `Select` en SQL; sin N+1 ni `ToListAsync` antes de filtrar/paginar.
- Pedidos: `OrderWorkflow` es la única fuente de verdad (sub-estado → estado, ingresos, etiquetas). Los carritos
  del bot (`OrderStatus.Cart`) nunca aparecen como pedidos.
- La caché del bot se invalida sola al guardar entidades relevantes (`BotContextInvalidationInterceptor`).

### Seguridad
- Autenticación por cookie HttpOnly (`iv.auth`) + CSRF double-submit: cookie `XSRF-TOKEN` → cabecera `X-XSRF-TOKEN`.
- Rate limiting: `auth` (login/registro), `webhooks`, `llm` (endpoints que llaman al modelo).
- Webhooks: firma HMAC sobre bytes crudos, *fail-closed*. wa-client usa secreto compartido en ambos sentidos.
- Tokens de canal cifrados con Data Protection; claves persistidas en la BD.

### Contrato OpenAPI
`dotnet build` genera `frontend/openapi/instavende-api.json`. El frontend lo convierte en tipos con
`npm run gen:api` → `frontend/src/lib/api/schema.d.ts`. Documentación interactiva en desarrollo: `/api/docs`.

## Frontend (`frontend/`)

| Tecnología | Uso |
|---|---|
| React 19 + TypeScript 5.9 + Vite 8 | Base |
| TanStack Router (rutas por archivo, tipadas) | Navegación, `beforeLoad` para protección de rutas |
| TanStack Query | Datos del servidor, caché e invalidación |
| openapi-fetch + tipos generados | Cliente HTTP 100 % tipado desde el contrato |
| Tailwind CSS v4 + componentes Radix (estilo shadcn/ui) | Sistema de diseño accesible, modo claro/oscuro |
| React Hook Form + Zod 4 | Formularios y validación |
| Recharts | Gráficos |
| @microsoft/signalr | Tiempo real (bandeja de mensajes) |
| @dnd-kit, @tanstack/react-table, react-dropzone | Kanban, tablas, subida de archivos |

### Organización
- `src/routes/` — rutas por archivo. `_auth/*` páginas públicas de acceso, `_app/*` panel protegido.
- `src/features/{modulo}/` — lógica y componentes del módulo: `api.ts` (queryOptions y mutaciones), `components/`, `schemas.ts`.
- `src/components/ui/` — componentes base (no modificar salvo mejoras globales).
- `src/components/shared/` — piezas reutilizables del producto (`PageHeader`, `EmptyState`, `StatCard`, `useConfirm`, `ChannelIcon`…).
- `src/lib/` — cliente de API, sesión, formato, tema, tiempo real.

### Convenciones
- Datos: `queryOptions` por recurso con claves jerárquicas (`['products', 'list', params]`); las mutaciones invalidan su raíz.
- Llamadas: `unwrap(api.GET('/api/...'))` — lanza `ApiError` con el mensaje del servidor; el toast de error es global
  (desactivable con `meta: { silent: true }`), el de éxito con `meta: { successMessage }`.
- Dinero: `useMoney()` (usa el símbolo de moneda del negocio). Fechas: helpers de `lib/format.ts`.
- Accesibilidad: labels asociados (`FormField`), `aria-label` en botones de solo icono, foco visible, teclado.
- Textos de la interfaz en español neutro con tuteo.

## Puesta en marcha (desarrollo)

```bash
# API (http://localhost:5221, docs en /api/docs)
dotnet run --project src/InstaVende.Web

# wa-client (lo lanza y supervisa la API automáticamente; solo instalar dependencias)
cd wa-client && npm ci

# SPA (http://localhost:5173, proxy a la API)
cd frontend && npm ci && npm run dev
```

Configuración sensible por variables de entorno o *user secrets*: `ConnectionStrings__DefaultConnection`,
`OpenAI__ApiKey`, `WhatsAppClient__SharedSecret` (opcional si la API lanza wa-client), `App__PublicBaseUrl`.
