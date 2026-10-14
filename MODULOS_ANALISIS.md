# Análisis de Módulos y Funcionalidades — InstaVende
## Basado en: Referencia visual (ya.onl / yavendio) vs. Proyecto actual

> **Fuente de análisis:** 39 capturas de pantalla de la app de referencia + revisión completa del código fuente del proyecto InstaVende.
> **Fecha:** 2025

---

## RESUMEN EJECUTIVO

| Estado | Cantidad | Detalle |
|--------|----------|---------|
| ? Implementado | 7 módulos | Funcional, puede mejorar UI |
| ?? Parcial | 5 módulos | Existe la base pero faltan funcionalidades clave |
| ? Faltante | 6 módulos | No existe en el proyecto, debe construirse |

---

## MÓDULOS PRIORITARIOS (solicitados)

---

### 1. ?? CONFIGURA TU VENDEDOR (Bot Assistant)
**Pantallas de referencia:** `dashboard-v2-assistant` (2 pantallas)

#### Estado actual: ?? PARCIAL
- ? Existe `Bot/Index.cshtml` con tabs (Configuración, Intenciones, Conocimiento, Probar)
- ? Nombre del bot, personalidad, prompt base, nivel de interacción
- ? Handoff a humano, frase de transferencia
- ? Descuentos por nivel (1, 2, 3), margen mínimo, umbral envío gratis

#### ? FALTA implementar:
- [ ] **Wizard de onboarding** para configurar el bot paso a paso (4 pasos como en la referencia)
  - Paso 1: Información del negocio (sector, horario, zona)
  - Paso 2: Tono y personalidad del bot (formal, amigable, vendedor)
  - Paso 3: Políticas (devoluciones, garantía, envíos)
  - Paso 4: Activación y prueba
- [ ] **Sección visual de "Políticas"** dedicada (ReturnPolicy, WarrantyPolicy, ShippingTimes están en BD pero sin UI propia)
- [ ] **Campo de horario de atención** con selector visual (hoy solo es texto libre `BusinessHours`)
- [ ] **Sección "Promociones activas"** (`ActivePromotions` existe en BD pero sin UI)
- [ ] **Preview del bot** en tiempo real mientras se configura (tipo chat embed)
- [ ] **Indicador visual de estado** del bot (activo / pausado / en entrenamiento)

---

### 2. ?? BASE DE CONOCIMIENTOS
**Pantallas de referencia:** `dashboard-v2-assistant` (tab dentro de assistant)

#### Estado actual: ?? PARCIAL
- ? Tab "Base de Conocimiento" dentro de `Bot/Index.cshtml`
- ? Entidad `BotKnowledge` con Question, Answer, Category, IsActive
- ? CRUD básico vía AJAX

#### ? FALTA implementar:
- [ ] **Página dedicada** (no un tab secundario) — la referencia lo muestra como módulo independiente
- [ ] **Categorías visuales** para organizar el conocimiento (ej: Precios, Envíos, Garantías)
- [ ] **Import masivo** desde archivo CSV / Excel
- [ ] **Búsqueda y filtro** por categoría y texto
- [ ] **Contador de uso** — cuántas veces el bot usó cada respuesta
- [ ] **Sugerencias automáticas** basadas en preguntas frecuentes de conversaciones

---

### 3. ?? MÉTODOS DE PAGO
**Pantallas de referencia:** dentro de `dashboard-v2-assistant` y `welcome-form`

#### Estado actual: ? FALTANTE COMO UI
- ? Campo `PaymentMethods` existe en `BotConfig` (solo texto libre en BD)
- ? **No existe pantalla dedicada** de configuración de métodos de pago

#### ? FALTA implementar (módulo completo):
- [ ] **Pantalla "Métodos de Pago"** con lista visual de opciones:
  - Transferencia bancaria (datos de cuenta)
  - Tarjeta de crédito/débito
  - Efectivo / contra entrega
  - Mercado Pago / PayPal / Stripe (integración futura)
  - Criptomonedas (opcional)
- [ ] **Por cada método**: nombre, instrucciones, CBU/alias/link, activo/inactivo
- [ ] **El bot debe usar estos datos** al responder "¿Cómo pago?"
- [ ] Entidad nueva: `PaymentMethod` (Id, BusinessId, Type, Name, Instructions, IsActive)

---

### 4. ?? CONEXIÓN WHATSAPP Y META
**Pantallas de referencia:** `dashboard-v2-channels-whatsapp` (2 pantallas), `setup-session` (2 pantallas)

#### Estado actual: ?? PARCIAL
- ? `ChannelConfig/Index.cshtml` con tabs WhatsApp / Facebook / Instagram
- ? Formulario manual: PhoneNumberId, AccessToken, AppSecret, WebhookVerifyToken
- ? Webhook handler en `/api/webhooks/`
- ? Tokens encriptados en BD

#### ? FALTA implementar:
- [ ] **Estado de conexión en tiempo real** — indicador verde/rojo con último mensaje recibido
- [ ] **Botón "Probar conexión"** — envía mensaje de prueba y confirma que el webhook funciona
- [ ] **Guía paso a paso** visual para obtener el AccessToken desde Meta Business Manager
  - Links directos a Meta Developer Console
  - Instrucciones con screenshots para cada campo
- [ ] **Sync / QR WhatsApp** (pantallas `setup-session`):
  - Si usan WhatsApp Web API (no oficial): mostrar QR para vincular sesión
  - Status de la sesión: conectado / desconectado / esperando QR
- [ ] **Webhook URL copiable** con un click (actualmente es texto estático)
- [ ] **Test de webhook** desde el panel (verificar que Meta puede hacer ping)
- [ ] **Instagram**: el form actual tiene `InstagramAccountId` pero la conexión real requiere OAuth de Meta — **falta el flujo OAuth**
- [ ] **Facebook Messenger**: igual, requiere Page Token renovable — falta indicador de expiración
- [ ] **Historial de conexiones** — log de cuándo se conectó/desconectó cada canal

---

### 5. ?? PANEL DE MENSAJES Y CONVERSACIONES
**Pantallas de referencia:** `dashboard-v2-conversations` (1 pantalla + detalles)

#### Estado actual: ?? PARCIAL
- ? `InboxController` y `Inbox/Index.cshtml` existen
- ? Entidades `Conversation`, `Message`, `Contact` en BD
- ? SignalR Hub (`InboxHub`) para tiempo real

#### ? FALTA implementar:
- [ ] **Layout split-panel** (lista izquierda + chat derecho) — la referencia muestra este patrón
- [ ] **Filtros de conversación**: Por canal (WA/IG/FB), por estado (Bot / Agente / Resuelto), por fecha
- [ ] **Búsqueda** de contacto o mensaje dentro del inbox
- [ ] **Vista de chat completa** con burbujas de mensaje (actualmente puede ser básico)
- [ ] **Indicador de canal** por conversación (ícono WhatsApp/Instagram/Facebook)
- [ ] **Tomar control / devolver al bot** — botón de handoff visible en cada conversación
- [ ] **Notas internas** del agente por conversación (no se envían al cliente)
- [ ] **Información del contacto** en panel lateral (nombre, historial de compras, conversaciones previas)
- [ ] **Estado de lectura** de mensajes (leído / no leído)
- [ ] **Respuestas rápidas** predefinidas (shortcuts para el agente humano)
- [ ] **Contador de conversaciones** sin atender en el sidebar (badge)

---

### 6. ?? PRODUCTOS (Registrados / Editar / Agregar)
**Pantallas de referencia:** `dashboard-v2-products` (4 pantallas), `products-new-product`, `products-edit-13797`

#### Estado actual: ?? PARCIAL
- ? `Products/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`
- ? Entidad `Product` con Name, Description, Price, Stock, ImageUrl, IsActive, IsFeatured
- ? Categorías básicas

#### ? FALTA implementar:
- [ ] **Variantes de producto** — la referencia muestra tallas, colores, etc.
  - Entidad nueva: `ProductVariant` (Id, ProductId, AttributeName, AttributeValue, PriceModifier, Stock)
  - Ejemplos: Talla S/M/L/XL, Color Rojo/Azul/Negro, Material
- [ ] **Galería de imágenes** — actualmente solo 1 imagen URL, la referencia muestra múltiples fotos
- [ ] **SKU / Código de producto** — campo faltante en entidad
- [ ] **Precio de oferta / tachado** — PriceOriginal vs PriceSale
- [ ] **Gestión visual de categorías** — CRUD de categorías con ícono/color
- [ ] **Import masivo CSV** — para cargar catálogo de golpe
- [ ] **Filtros y búsqueda avanzada** en la lista (por categoría, estado, stock bajo)
- [ ] **Vista de cards vs. tabla** — toggle de visualización
- [ ] **Indicador de stock bajo** con alerta visual (ej: menos de 5 unidades)
- [ ] **Historial de precios** — registrar cambios de precio

---

## MÓDULOS COMPLETAMENTE FALTANTES

---

### 7. ?? PEDIDOS / ÓRDENES
**Pantallas de referencia:** `dashboard-v2-orders` (**6 pantallas!** — el módulo más complejo faltante)

#### Estado actual: ? NO EXISTE
No hay entidad `Order`, ni controller, ni vista.

#### ? Debe construirse desde cero:
- [ ] **Entidad `Order`**: Id, BusinessId, ContactId, ConversationId, Status, Total, PaymentMethod, ShippingAddress, Notes, CreatedAt
- [ ] **Entidad `OrderItem`**: OrderId, ProductId, VariantId, Quantity, UnitPrice, Subtotal
- [ ] **Lista de pedidos** con filtros (estado, fecha, canal, monto)
- [ ] **Estados del pedido**: Pendiente ? Confirmado ? En preparación ? Enviado ? Entregado ? Cancelado
- [ ] **Detalle del pedido** — items, datos de contacto, método de pago, dirección
- [ ] **Cambio de estado** con notificación automática al cliente via bot
- [ ] **Generación de orden desde conversación** — el bot crea la orden al cerrar venta
- [ ] **Exportar pedidos** a CSV/Excel
- [ ] **Métricas de pedidos** en dashboard (pedidos hoy, semana, mes)

---

### 8. ? RECORDATORIOS Y SEGUIMIENTO
**Pantallas de referencia:** `dashboard-v2-reminders` (3 pantallas)

#### Estado actual: ? NO EXISTE

#### ? Debe construirse desde cero:
- [ ] **Entidad `Reminder`**: Id, BusinessId, ContactId, Message, ScheduledAt, ChannelType, Status, CreatedAt
- [ ] **Crear recordatorio** — seleccionar contacto, fecha/hora, mensaje personalizado, canal
- [ ] **Lista de recordatorios** programados con estado (pendiente / enviado / fallido)
- [ ] **Recordatorio automático** — el bot puede programar follow-ups al cerrar conversación
  - Ej: "Te recuerdo mañana sobre tu pedido"
  - Ej: "Seguimiento 48hs después si no compró"
- [ ] **Templates de mensaje** para recordatorios frecuentes
- [ ] **Historial** de recordatorios enviados por contacto

---

### 9. ?? MÉTRICAS Y ANALYTICS AVANZADO
**Pantallas de referencia:** `dashboard-v2-metrics` (1 pantalla dedicada)

#### Estado actual: ?? BÁSICO (solo en Dashboard)
- ? 4 KPIs básicos en dashboard (Total, Resueltas, Bot Activo, Tasa)
- ? Gráfico línea de conversaciones diarias
- ? Gráfico dona por canal

#### ? FALTA implementar (página dedicada de métricas):
- [ ] **Métricas de ventas**: Total facturado, ticket promedio, conversiones por canal
- [ ] **Métricas del bot**: Tasa de respuesta automática, tiempo promedio de respuesta, escalaciones
- [ ] **Funnel de conversión**: Contactos ? Interesados ? Ofertas ? Ventas cerradas
- [ ] **Top productos** más consultados y más vendidos
- [ ] **Ranking de clientes** (más activos, mayor gasto)
- [ ] **Comparativa de períodos**: semana vs semana anterior, mes vs mes anterior
- [ ] **Exportar reporte** en PDF o Excel

---

### 10. ?? CONFIGURACIÓN DE CUENTA
**Pantallas de referencia:** `dashboard-v2-settings-account` (4 pantallas)

#### Estado actual: ? NO EXISTE (solo Login/Register/Logout)

#### ? Debe construirse desde cero:
- [ ] **Perfil personal**: FirstName, LastName, Email, cambio de contraseña, foto de perfil
- [ ] **Perfil del negocio**: Nombre, descripción, logo, teléfono, email, website, sector
- [ ] **Plan y facturación**: plan actual, próxima fecha de cobro, historial de pagos
- [ ] **Equipo / Agentes**: agregar agentes humanos que reciben handoffs del bot
  - Nombre, email, rol (Admin / Agente)
  - Activar/desactivar acceso

---

### 11. ?? ONBOARDING / BIENVENIDA (Flujo inicial)
**Pantallas de referencia:** `welcome-form` (4 pasos), `onboarding-shopify-setup`, `onboarding-contact-support`, `get-started`

#### Estado actual: ? NO EXISTE
El usuario llega al dashboard directamente después de registrarse.

#### ? Debe construirse:
- [ ] **Wizard de bienvenida** (4 pasos tras el registro):
  1. ¿A qué se dedica tu negocio? (sector, descripción)
  2. ¿Qué canales quieres conectar? (WA/IG/FB)
  3. Configura tu primer producto
  4. Personaliza tu bot
- [ ] **Pantalla "Get Started"** con checklist de tareas pendientes:
  - [ ] Conectar WhatsApp
  - [ ] Agregar 5 productos
  - [ ] Configurar métodos de pago
  - [ ] Probar el bot
- [ ] **Barra de progreso de configuración** visible en el dashboard

---

## GAPS TÉCNICOS IDENTIFICADOS EN EL CÓDIGO

### Base de datos — Entidades faltantes:
| Entidad | Motivo |
|---------|--------|
| `Order` | Módulo de pedidos completo faltante |
| `OrderItem` | Detalle de cada pedido |
| `ProductVariant` | Variantes de talla/color por producto |
| `ProductImage` | Galería multi-imagen por producto |
| `PaymentMethod` | Métodos de pago configurables del negocio |
| `Reminder` | Recordatorios y seguimientos programados |
| `AgentProfile` | Agentes humanos adicionales por negocio |
| `OnboardingProgress` | Control del wizard de configuración inicial |

### Campos faltantes en entidades existentes:
| Entidad | Campo faltante |
|---------|---------------|
| `Product` | SKU, PriceOriginal, PriceSale, SortOrder |
| `ApplicationUser` | AvatarUrl, LastLoginAt |
| `Business` | Sector, LogoUrl (existe), CompletionPercent |
| `Contact` | TotalPurchases, TotalSpent, LastPurchaseAt |
| `Conversation` | UnreadCount, LastMessagePreview |

### Funcionalidades técnicas faltantes:
| Funcionalidad | Prioridad |
|--------------|-----------|
| WhatsApp QR / Session sync (no-oficial API) | Alta |
| Meta OAuth flow para Facebook/Instagram | Alta |
| Job scheduler para Reminders (Hangfire / Quartz) | Media |
| Sistema de notificaciones push en el panel | Media |
| Export a CSV/Excel (pedidos, productos, métricas) | Media |
| Upload múltiple de imágenes (galería productos) | Media |
| Importación masiva de productos CSV | Baja |

---

## ORDEN DE IMPLEMENTACIÓN SUGERIDO

### Fase 1 — Núcleo operativo (inmediato)
1. **Métodos de Pago** — UI + entidad (requieren el bot para responder correctamente)
2. **Mejora Canal WhatsApp** — estado de conexión + botón test + guía visual
3. **Panel de Conversaciones** — split-panel + filtros + handoff visual
4. **Configuración de Cuenta** — perfil + negocio (funcionalidad básica)

### Fase 2 — Módulos de negocio
5. **Pedidos / Órdenes** — módulo completo (es el más solicitado comercialmente)
6. **Variantes de Producto** — entidad + UI
7. **Métricas avanzadas** — página dedicada de analytics

### Fase 3 — Engagement y retención
8. **Recordatorios y Seguimiento** — programar follow-ups
9. **Onboarding Wizard** — flujo inicial para nuevos usuarios
10. **Base de Conocimiento** — página dedicada + import CSV

---

## NOTAS FINALES

- La app de referencia (yavendio) usa una arquitectura similar pero con más módulos maduros.
- El mayor gap comercial es el módulo de **Pedidos** — sin él, el bot puede vender pero no hay trazabilidad de las ventas.
- La **conexión WhatsApp** necesita mejorar la UX significativamente — actualmente es técnica y confusa para el usuario final.
- Los **Métodos de Pago** son urgentes porque el bot hoy no puede responder con datos concretos de pago.

---
*Documento generado por análisis de capturas de referencia + revisión de código fuente del proyecto InstaVende*
