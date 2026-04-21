# Análisis UI de Referencia (ya! / InstaVende)

> Este archivo documenta el análisis progresivo de imágenes de la app de referencia
> para guiar la implementación de funcionalidades en InstaVende.

---

## LOTE 1 – Configuración de la cuenta (`/Configuracion/Cuenta`)

### Estructura general
- Breadcrumb: `Configuración > Configuración de la cuenta`
- Botón superior derecho: `Guardar cambios` (primario, deshabilitado hasta editar)
- 4 tabs horizontales:
  1. Organización
  2. Usuarios
  3. Notificaciones por email
  4. Métodos de pago

---

### Tab 1 – Organización
| Campo | Tipo | Detalle |
|---|---|---|
| Nombre de la organización | Input text | Placeholder: nombre del negocio (ej. "Fresh Boy Store") |
| País | Select/Dropdown | Ej. "Perú" |
| Moneda | Select/Dropdown | Ej. "PEN - Sol Peruano" |

- Nota informativa: _"Incluye el país y la moneda para personalizar el vendedor inteligente."_

**Entidades/campos a crear o extender:**
- `Business.Name` (ya existe como `BusinessName`)
- `Business.Country` ? agregar campo
- `Business.Currency` ? agregar campo

---

### Tab 2 – Usuarios (Gestión de usuarios)
- Sección **Invitar a tu equipo**: input email + botón `Invitar`
- Sección **Invitaciones pendientes**: lista vacía con estado "No hay invitaciones pendientes"
- Sección **Usuarios**: lista con avatar, email, teléfono, rol (Admin/Member)
  - Ejemplo: `freshboystore@gmail.com | +51992260979 | Admin`
  - Límite visible: "1 de 1 usuarios disponibles en tu plan"

**Funcionalidades a implementar:**
- Invitación de usuarios por email
- Tabla de invitaciones pendientes (con posibilidad de cancelar)
- Lista de usuarios del negocio con rol
- Control de límite según plan

---

### Tab 3 – Notificaciones por email
- Descripción: _"Recibe avisos cuando tu vendedor IA cierre una venta o cuando sea necesario que un humano tome la conversación."_
- Lista de emails (input + botón `+ Añadir email`)
- Permite múltiples emails de notificación

**Entidades/campos a crear:**
- `NotificationEmail` (tabla): `BusinessId`, `Email`, `IsActive`

---

### Tab 4 – Métodos de pago
- Tab visible en las imágenes pero contenido no mostrado aún.
- Pendiente de análisis en próximo lote.

---

## LOTE 2 – Pedidos, Métricas y Métodos de pago

### Imagen 1 – Gestión de pedidos (`/Pedidos`)

**Título:** Gestión de pedidos  
**Botón superior derecho:** `? Exportar pedidos ?` (dropdown)  
**Barra de búsqueda:** input "Buscar pedidos..." + icono filtro  

#### Tabs de estado (con contador numérico)
| Tab | Badge |
|---|---|
| Nuevos pedidos | 0 |
| En curso | 0 |
| Completados | 0 |
| Cancelados | 0 |

#### Sub-sección dentro de "Nuevos pedidos"
- **En validación** `0` (badge morado) con botón `···` (opciones)
- Estado vacío: _"Sin pedidos"_

**Entidades/campos implicados:**
- `Order` ? estados: `NuevoPedido`, `EnCurso`, `Completado`, `Cancelado`
- Sub-estado interno: `EnValidacion` (dentro de Nuevo)
- Funcionalidad de exportación (CSV/Excel)
- Búsqueda + filtro avanzado

**Funcionalidades a implementar:**
- [ ] `PedidosController` con acciones Index, Exportar
- [ ] Tabs filtrados por estado con contadores
- [ ] Sub-agrupación "En validación"
- [ ] Exportar pedidos (dropdown: CSV / Excel)
- [ ] Buscador + filtros

---

### Imagen 2 – Métricas y resultados (`/Metricas`)

**Título:** Métricas y resultados  
**Contenido:** Panel difuminado (bloqueado) con modal de upgrade encima

#### Modal "Métricas Pro" (paywall)
- Ícono: candado (feature locked)
- Título: _"¿Sabes dónde pierdes ventas?"_
- Subtítulo: _"Descúbrelo con Métricas Pro"_
- Descripción: _"Accede al panel completo de métricas de tu tienda en WhatsApp. Toma decisiones en tiempo real con datos, no con intuición."_
- Features listadas:
  | Feature | Descripción |
  |---|---|
  | Conversión en tiempo real | Conversaciones, ventas y % de conversión al instante |
  | Análisis de órdenes | Ranking de productos, ticket promedio y combos ganadores |
  | Rendimiento de campañas | Mide qué anuncios generan ventas reales, no solo clics |
  | Base de datos descargable | Exporta tu base de clientes y segmenta como quieras |
- CTA: botón primario `Ver planes y precios ?`
- Nota: _"? Disponible desde el Plan Pro"_

**Funcionalidades a implementar:**
- [ ] `MetricasController` con vista base (difuminada para plan Free)
- [ ] Modal/overlay de upgrade según plan activo
- [ ] Componente reutilizable `_UpgradeModal.cshtml`
- [ ] Lógica de acceso por plan: `PlanFeatureService.HasAccess(feature)`
- [ ] KPIs básicos: conversiones, órdenes, campañas

---

### Imagen 3 – Tab "Métodos de pago" (dentro de Configuración de la cuenta)

**Descripción:** _"Gestiona tus métodos de pago a través de nuestro portal de pagos seguro."_  
**Único elemento:** Botón `?? Gestionar métodos de pago` ? redirige a portal externo (ej. Stripe Customer Portal)

**Funcionalidades a implementar:**
- [ ] Acción `MetodosPago` en `AccountConfigController`
- [ ] Botón que redirige al portal externo de pagos (Stripe/MercadoPago)
- [ ] No requiere formulario propio, solo enlace seguro al portal del proveedor

---

## LOTE 3 – Sub-estados completos de Gestión de pedidos

Estas imágenes completan el detalle de los sub-estados de cada tab en `/Pedidos`.
Cada tab tiene columnas Kanban con badge de color y botón `···` (opciones).

### Mapa completo de estados y sub-estados

| Tab principal | Sub-estado | Color badge | Ícono |
|---|---|---|---|
| Nuevos pedidos | En validación | Morado | ? |
| En curso | En preparación | Azul/gris | ? |
| En curso | Listo para envío | Naranja | ? |
| En curso | Enviado | Azul claro | ? |
| Completados | Entregado | Verde | ? |
| Completados | Finalizado | Verde | ? |
| Cancelados | Rechazado | Rojo | ? |
| Cancelados | Cancelado | Rojo | ? |

### Layout de cada tab (vista Kanban)
- Las columnas se muestran **en paralelo** (lado a lado), no apiladas
- Cada columna tiene: título + badge contador + botón `···`
- Estado vacío por columna: _"Sin pedidos"_
- Máximo 3 columnas visibles simultáneamente (ej. "En curso" tiene 3)

### Enum `OrderSubStatus` a crear
```csharp
public enum OrderSubStatus
{
    // Nuevos pedidos
    EnValidacion,
    // En curso
    EnPreparacion,
    ListoParaEnvio,
    Enviado,
    // Completados
    Entregado,
    Finalizado,
    // Cancelados
    Rechazado,
    Cancelado
}
```

### Relación con `OrderStatus` (tab padre)
```csharp
public enum OrderStatus
{
    NuevoPedido,   // sub: EnValidacion
    EnCurso,       // sub: EnPreparacion, ListoParaEnvio, Enviado
    Completado,    // sub: Entregado, Finalizado
    Cancelado      // sub: Rechazado, Cancelado
}
```

**Funcionalidades adicionales confirmadas:**
- [ ] Vista Kanban por columnas de sub-estados
- [ ] Transiciones de estado (mover pedido entre columnas)
- [ ] Badge con color semántico por sub-estado (rojo/verde/azul/naranja)
- [ ] Botón `···` por columna (opciones de columna: ocultar, ordenar, etc.)

---

## LOTE 4 – Módulo de Mensajes / Conversaciones (`/Mensajes`)

> Imágenes 2 y 3 de este lote son duplicados del Lote 3 (ya documentados).
> Solo se analiza la imagen nueva.

### Imagen 1 – Vista principal de Mensajes

**Layout de 3 paneles:**

```
[ Panel izquierdo ]  [ Panel centro ]  [ Panel derecho ]
   Navegación          Lista conv.        Chat abierto
```

---

#### Panel izquierdo – Menú de Mensajes
| Sección | Subsección | Contador |
|---|---|---|
| Mensajes | Todas | 4540 |
| Mensajes | Ventas | 33 |
| Mensajes | No atendidas | 10 |
| Recordatorios | — | — |

- "Mensajes" es la sección activa con indicador visual izquierdo
- "Recordatorios" es una sección colapsable separada

#### Panel central – Lista de Conversaciones
- **Título:** "Conversaciones" + badge `10` (no atendidas)
- **Filtros superiores (3 dropdowns):**
  - `??? Etiquetas ?`
  - `?? Calendario ?`
  - `?? Filtros ?`
- **Buscador:** input "Buscar conversaciones..."
- **Lista de conversaciones:**
  - Avatar con iniciales + badge número (mensajes no leídos) + punto verde WhatsApp
  - Nombre del contacto
  - Preview del último mensaje (truncado)
  - Fecha/hora relativa (ej. "Jueves", "26/03/2026")
  - Botón `···` (opciones por conversación)

**Ejemplos de conversaciones visibles:**
| Contacto | Último mensaje | Fecha |
|---|---|---|
| Jordan Matencio | ??? (imagen) | Jueves |
| Valeris Chincha Alta (43) | "Muchas gracias amigo" | 26/03/2026 |
| Angelo Matencio Caj... (14) | "Pt recien me levanto" | 26/03/2026 |
| Carlos C.S VMT | "Si tienes alguna consulta, no du..." | 26/03/2026 |
| Mn'r RI (19) | ?? (emoji) | 30/12/2025 |
| Dios Y Mi Familia (30) | "Donde se encuentra usted amigo" | 17/12/2025 |
| JHONATAN (19) | "Brindame un momento para po..." | 04/11/2025 |
| Jorge luis | "Hola muchas gracias" | 19/10/2025 |
| (sin nombre) | "Hola buen día ??. Hoy por la tar..." | 04/10/2025 |
| Jair Kaled | ?? (emoji) | 29/09/2025 |

#### Panel derecho – Estado vacío
- Ilustración decorativa (IA/chat)
- Título: _"Selecciona una conversación"_
- Subtítulo: _"Selecciona una conversación desde la barra lateral para gestionar las interacciones con los clientes"_
- Nota: _"Las conversaciones aparecerán aquí"_

---

### Entidades/campos implicados
- `Conversation` ? `ContactId`, `BusinessId`, `ChannelType`, `LastMessageAt`, `UnreadCount`, `Label`
- `Contact` ? `Name`, `Phone`, `AvatarInitials`
- `Message` ? `ConversationId`, `Body`, `MediaUrl`, `SentAt`, `Direction` (Inbound/Outbound)
- `ConversationLabel` ? para filtro por etiquetas
- `Reminder` ? para sección "Recordatorios"

### Funcionalidades a implementar
- [ ] `MensajesController` ? Index, GetConversaciones (con filtros), GetMensajes
- [ ] Panel 3 columnas (sidebar + lista + chat) con layout responsive
- [ ] Filtros: por etiqueta, por rango de fecha, filtros avanzados
- [ ] Buscador de conversaciones (por nombre/teléfono/mensaje)
- [ ] Badge de mensajes no leídos por conversación
- [ ] Categorías: Todas / Ventas / No atendidas (con contadores)
- [ ] Indicador de canal (punto verde = WhatsApp)
- [ ] Preview del último mensaje (texto o ícono si es media)
- [ ] Estado vacío animado cuando no hay conversación seleccionada
- [ ] Sección "Recordatorios" (ver Lote 5)

---

## LOTE 5 – Recordatorios automáticos por segmento (`/Mensajes/Recordatorios`)

### Estructura general
- Ruta: sub-sección dentro de **Mensajes ? Recordatorios**
- Título de página: **"Recordatorios"**
- Subtítulo de sección: **"Segmento del cliente"**
- Descripción: _"Usamos modelos por segmento (Frío, Tibio, Caliente) con ventanas de 2-3h y una segunda de 22-23h cuando aplica."_
- Layout: panel principal (editor) + panel lateral derecho (Previsualización)

### Tabs de segmento
| Tab | Recordatorios | Nota |
|---|---|---|
| **Frío** | Solo 1 | _"Para contactos fríos es mejor no insistir demasiado."_ |
| **Tibio** | 2 (2do opcional) | _"Sin segundo recordatorio - Un único recordatorio estratégico..."_ |
| **Caliente** | 2 activos | — |

### Estructura de cada tarjeta de Recordatorio
| Elemento | Detalle |
|---|---|
| Número de orden | `1`, `2` |
| Título | "Primer recordatorio" / "Segundo recordatorio" |
| Ventana de envío | `2-3h` / `22-23h` (badge gris) |
| Toggle | Switch on/off (morado) |
| Textarea | Editable, máx 200 chars, contador visible |
| Botón Plantillas | `?? Plantillas` ? sugerencias IA |
| Banner multimedia | `?? Adjuntar imagen, video o PDF — Disponible desde el plan Gold` + botón `Actualizar plan` |

### Mensajes de ejemplo por segmento

#### Frío (1 recordatorio)
- **1er (2-3h):** _"Hola ??, gracias por contactarnos. ¿En qué puedo ayudarte hoy?..."_

#### Tibio (2 recordatorios)
- **1er (2-3h):** _"Gracias por escribirnos ?? ¿Tienes alguna duda sobre nuestros productos?..."_
- **2do (22-23h):** _"¡Hola! Ayer hablamos un poquito. ¿Te animas a revisar de nuevo?..."_

#### Caliente (2 recordatorios)
- **1er (2-3h):** _"Gracias por tu interés ?? Estás a un paso de tener tu pedido listo..."_
- **2do (22-23h):** _"¡Hola! Solo queríamos recordarte que tu pedido sigue apartado ??..."_

### Panel Previsualización (lateral derecho)
- Burbujas estilo WhatsApp (fondo azul claro)
- Etiquetas: "Primer recordatorio (2-3h)", "Segundo recordatorio (22-23h)"
- Hora simulada: `18:20 ??`

### Entidad `ReminderTemplate`
```csharp
public enum CustomerSegment { Frio, Tibio, Caliente }

public class ReminderTemplate
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public CustomerSegment Segment { get; set; }
    public int Order { get; set; }          // 1 o 2
    public string Message { get; set; }     // máx 200 chars
    public string TimeWindow { get; set; }  // "2-3h", "22-23h"
    public bool IsActive { get; set; }
    public string? MediaUrl { get; set; }   // plan Gold+
}
```

### Funcionalidades a implementar
- [ ] `RecordatoriosController` ? Index (GET/POST por segmento)
- [ ] 3 tabs: Frío / Tibio / Caliente con mensajes diferenciados
- [ ] Editor textarea con contador en tiempo real (máx 200 chars)
- [ ] Toggle activo/inactivo por recordatorio
- [ ] Modal "Plantillas" con sugerencias IA por segmento
- [ ] Panel previsualización lateral con burbujas WhatsApp reactivas
- [ ] Banner paywall multimedia (plan Gold+) ? botón Actualizar plan
- [ ] Frío: ocultar 2do recordatorio; Tibio/Caliente: mostrar ambos
- [ ] Guardado por segmento (botón Guardar o auto-save)

---

## Plan de implementación (se actualiza con cada lote)

### Modelos / Entidades
- [ ] `Business` ? agregar `Country`, `Currency`
- [ ] `BusinessUser` ? gestión de miembros del equipo con rol
- [ ] `UserInvitation` ? email, token, estado, fechaExpiracion
- [ ] `NotificationEmail` ? `BusinessId`, `Email`, `IsActive`
- [ ] `Conversation` ? `ContactId`, `BusinessId`, `UnreadCount`, `Label`, `LastMessageAt`
- [ ] `Contact` ? `Name`, `Phone`, `AvatarInitials`
- [ ] `Message` ? `ConversationId`, `Body`, `MediaUrl`, `SentAt`, `Direction`
- [ ] `ConversationLabel` ? etiquetas para filtro
- [ ] `ReminderTemplate` ? `BusinessId`, `Segment` (Frío/Tibio/Caliente), `Order`, `Message`, `TimeWindow`, `IsActive`, `MediaUrl`
- [ ] `Order` ? `OrderStatus` (tab) + `OrderSubStatus` (columna Kanban)
- [ ] Colores de badge: Morado=EnValidacion, Azul=EnPreparacion/Enviado, Naranja=ListoParaEnvio, Verde=Entregado/Finalizado, Rojo=Rechazado/Cancelado

### Controladores
- [ ] `AccountConfigController` ? Organizacion, Usuarios, Notificaciones, MetodosPago
- [ ] `PedidosController` ? Index (Kanban tabs+columnas), Exportar
- [ ] `MensajesController` ? Index, GetConversaciones, GetMensajes
- [ ] `RecordatoriosController` ? Index, Guardar (por segmento)
- [ ] `MetricasController` ? Index (con paywall por plan)

### Vistas
- [ ] `Views/AccountConfig/Index.cshtml` ? tabs Bootstrap (Org/Usuarios/Notif/Pago)
- [ ] `Views/Pedidos/Index.cshtml` ? Kanban por sub-estado
- [ ] `Views/Mensajes/Index.cshtml` ? layout 3 paneles
- [ ] `Views/Mensajes/Recordatorios.cshtml` ? editor + previsualización
- [ ] `Views/Metricas/Index.cshtml` ? panel + paywall modal
- [ ] `Views/Shared/_UpgradeModal.cshtml` ? componente reutilizable paywall

### ViewModels
- [ ] `OrganizacionViewModel`
- [ ] `UsuariosViewModel` (con invitaciones + lista)
- [ ] `NotificacionesEmailViewModel`
- [ ] `MetodosPagoViewModel`
- [ ] `PedidosKanbanViewModel`
- [ ] `ConversacionListViewModel`
- [ ] `ReminderTemplateViewModel`
- [ ] `VendedorPagosViewModel`
- [ ] `CanalWhatsAppViewModel`
- [ ] `PaymentMethodViewModel`

---

## LOTE 6 – Vendedor (Pagos) y Canales (WhatsApp conectado)

### Imagen 1 – Configuración del Vendedor > Tab "Pagos"

**Título:** Configuración de tu vendedor  
**Botón superior derecho:** `?? Guardar Cambios`

#### Banner superior – Importar desde redes sociales
- Tabs: `@ Instagram` · `f Facebook` · `?? Web`
- Input URL: `https://instagram.com/tu-negocio`
- Botón: `?? Importar`
- Descripción: _"Pega el enlace de tu negocio para extraer información automáticamente"_
- Hint: _"Presiona Enter o Espacio para agregar múltiples enlaces"_

#### Tabs del vendedor
| Tab | Estado |
|---|---|
| Personalidad | — |
| Base de conocimiento | — |
| Entrega | — |
| **Pagos** | **Activo** |

#### Sección "Métodos de pago"
- Ícono: ? checkmark (morado)
- Descripción: _"Selecciona si aceptas pago adelantado, contraentrega o ambos"_

**Tarjeta de método de pago:**
| Campo | Tipo | Ejemplo |
|---|---|---|
| Toggle activo | Switch (on) | activado |
| Nombre visual | Badge | "Billeteras virtuales" |
| Botón eliminar | `?` | eliminar tarjeta |
| Nombre del método | Input text | "YAPE, PLIN" |
| Tipo de método | Select/Dropdown | "Billeteras virtuales" (sub: _"Billeteras digitales como Yape, Plin, etc."_) |
| Instrucciones de pago | Textarea | "Enviar al número: 994 832 877 / Titular: Jordan Matencio / Enviar captura del pago" |
| Hint | Texto | _"Proporciona todos los datos necesarios para que el cliente pueda realizar el pago correctamente"_ |
| Vista previa | Texto renderizado | muestra las instrucciones tal como las verá el cliente |

**Botón:** `+ Agregar método de pago` (permite múltiples métodos)

#### Sección "Imágenes de pago (opcional)"
- Descripción: _"Sube imágenes como códigos QR, capturas de cuenta o referencias visuales para facilitar el pago"_
- Dropzone: `? Haz clic para subir imágenes`
- Restricciones: PNG, JPG o JPEG · Máx. 5 imágenes · 5MB por imagen

#### Tipos de método de pago (dropdown)
- Billeteras virtuales (Yape, Plin, etc.)
- (pendiente ver más opciones)

**Entidades a crear:**
```csharp
public class PaymentMethod
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; }           // "YAPE, PLIN"
    public string Type { get; set; }           // "BilleterasVirtuales", "Contraentrega", etc.
    public string Instructions { get; set; }
    public bool IsActive { get; set; }
    public int Order { get; set; }
}
public class PaymentImage
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string ImageUrl { get; set; }
}
```

**Funcionalidades a implementar:**
- [ ] Tab "Pagos" dentro de `VendedorController`
- [ ] CRUD de métodos de pago (agregar/editar/eliminar/reordenar)
- [ ] Tipos de método: dropdown con categorías predefinidas
- [ ] Toggle activo/inactivo por método
- [ ] Vista previa en tiempo real de las instrucciones
- [ ] Upload de imágenes de pago (QR, capturas) — máx 5, 5MB c/u
- [ ] Banner "Importar desde redes sociales" (Instagram/Facebook/Web) compartido entre todos los tabs del vendedor

---

### Imágenes 2 y 3 – Canales > WhatsApp conectado (`/Canales/WhatsApp`)

> Estas imágenes muestran el estado **post-conexión** del canal WhatsApp.
> Complementan lo ya implementado en `ChannelConfigController.WhatsApp()`.

#### Banner de éxito (estado conectado)
- Título: **"¡Perfecto! Tu canal está listo"**
- Subtítulo: _"Ahora puedes empezar a recibir y gestionar conversaciones de tus clientes"_
- Botón CTA: `Ir a Conversaciones` (primario teal)
- Ilustración decorativa (lado derecho)

#### Tarjeta del canal activo
| Campo | Valor ejemplo |
|---|---|
| Ícono | WhatsApp verde |
| Nombre | "WhatsApp Business API" |
| Badge estado | `Activo` (verde) |
| Número | `51994832877` |
| Fecha conexión | "18 de septiembre de 2025" |
| Botón | `Probar vendedor` |

#### Sección "Dudas frecuentes" (accordion) + link "Explorar la guía detallada ?"
| # | Pregunta |
|---|---|
| 1 | ¿Qué tipo de conexión elijo? |
| 2 | ¿Necesito tener una cuenta en Meta Business? |
| 3 | ¿Qué es el Portfolio Meta y cómo dejarlo listo? |
| 4 | ¿Qué pasa si no conecto mi WhatsApp? |
| 5 | ¿Debo seleccionar 'Compartir chats'? |
| 6 | ¿Cuánto cuesta usar la API de WhatsApp Business? |
| 7 | ¿Puedo usar un WhatsApp normal? |
| 8 | ¿Qué pasa si cometo un error al conectar? |

**Respuesta expandida de "¿Qué tipo de conexión elijo?"** incluye:
- Opción 1: Crear nueva cuenta WhatsApp Business (nombre de presentación vía Facebook/SMS)
- Opción 2: ? Vincular cuenta existente con QR ("Conexión Coexistencia") — **RECOMENDADA**
- Notas importantes: verificación azul, tiempo de espera 24-72h para Portfolio nuevo, límites

**Funcionalidades a agregar en `ChannelConfig/WhatsApp.cshtml`:**
- [ ] Banner de éxito con botón "Ir a Conversaciones" (cuando `IsActive == true`)
- [ ] Tarjeta del canal con número, fecha y botón "Probar vendedor"
- [ ] Sección FAQ accordion con las 8 preguntas documentadas
- [ ] Link "Explorar la guía detallada" (URL externa o página interna)
- [ ] Ilustración decorativa en el banner de éxito (ya existe parcialmente)

---

## LOTE 7 – Vendedor: Personalidad, Base de conocimiento y Entrega

### Imagen 1 – Tab "Personalidad" (`/Vendedor/Configuracion`)

**Sección 1 – Información básica**

| Campo | Tipo | Ejemplo |
|---|---|---|
| Nombre del vendedor* | Input | "Favio" |
| Género del vendedor* | Radio | Masculino / Femenino / No especificado / Otro |
| Nombre de la empresa* | Input | "Fresh Boy Store" |
| País de operación* | Select | Perú |
| Descripción de la empresa* | Textarea | texto largo del negocio |

**Sección 2 – Audiencia e instrucciones**
- ¿A quién le vendes?*: Input — "broad consumer audience"
- Reglas para mi vendedor*: Textarea — "general product support"
- Hint: _"Siempre agrega las reglas usando términos absolutos como SIEMPRE y NUNCA."_

**Sección 3 – Personalidad** (botón `Plantillas`)
| Campo | Tipo | Límite |
|---|---|---|
| Estilo de comunicación | Textarea | 200 chars |
| Estilo de ventas | Textarea | 210 chars |
| Longitud de respuesta | Slider 5 posiciones | Muy conciso / Conciso / Equilibrado / Detallado / Muy detallado |
| ¿Usar emojis? | Toggle | default: on |
| ¿Usar signos de apertura ¿¡? | Toggle | default: off |
| Palabras a evitar | Input tags | separadas por comas |
| Paleta de emojis permitidos | Emoji picker | selección visual + añadir |

**Sección 4 – Inicio de conversación**
- Mensaje inicial*: "¡Hola! Soy Favio. ¿En qué puedo ayudarte hoy?"
- Archivos adjuntos al bienvenida: Dropzone (img 5MB / video 16MB / audio 16MB / doc 100MB)
- Instrucciones avanzadas: colapsable

**Sección 5 – Mensajes**
- Mensaje de confirmación de compra*: "¡Gracias por tu compra! Estamos procesando tu pedido..."

**Sección 6 – Derivación humana**
- Situaciones que requieren un humano: Textarea tags
- Pausar al vendedor de IA automáticamente: Toggle on
- Ejemplo de mensaje de derivación (opcional): "Brindame un momento para poder ayudarte."
- Botón al pie: `?? Configuración avanzada`

**Entidad `VendorConfig`:**
Campos: `BusinessId, VendorName, VendorGender, Country, BusinessDescription, TargetAudience, Rules, CommunicationStyle, SalesStyle, ResponseLength(1-5), UseEmojis, UseOpeningPunctuation, WordsToAvoid, EmojiPalette(JSON), WelcomeMessage, PurchaseConfirmationMessage, HumanHandoffSituations, AutoPauseOnHandoff, HandoffExampleMessage`

**Funcionalidades:**
- [ ] Form con 6 secciones + botón `Guardar` en topbar
- [ ] Slider longitud de respuesta con 5 posiciones y labels
- [ ] Emoji picker interactivo (paleta de emojis permitidos)
- [ ] Dropzone para archivos adjuntos al bienvenida
- [ ] Botón `Plantillas` con sugerencias IA por campo
- [ ] Sección `Configuración avanzada` colapsable

---

### Imagen 2 – Tab "Base de conocimiento"

**Sub-filtros de categoría (tabs):**
Todas | ?? Productos | ?? Horarios | ?? Envíos | ?? Pagos | ?? Devoluciones | ? Otros

- Botón: `+ Agregar tema`
- Layout: Grid 2 columnas de tarjetas

**Tarjeta de conocimiento:**
- Título de la pregunta/tema
- Badge con categoría
- Descripción/respuesta (preview truncada)
- Acciones: ? favorito · ?? editar · ??? eliminar

**Ejemplos de entradas reales observadas:**

| Título | Categoría |
|---|---|
| Aceptas pago con tarjeta de crédito | Pagos |
| ¿Realizan envíos a provincia? | Envíos |
| ¿Realizas pago contraentrega? | Envíos |
| No se realiza contraentrega en: (lista de distritos) | Envíos |
| ¿Donde estan ubicados? | Envíos |
| ¿Tienen tienda física? | Otros |
| ¿A qué hora llegaría el motorizado? | Envíos |
| ¿Puede venir el motorizado por la mañana? | Envíos |

**Entidad `KnowledgeEntry`:**
```csharp
public enum KnowledgeCategory { Productos, Horarios, Envios, Pagos, Devoluciones, Otros }
public class KnowledgeEntry
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public KnowledgeCategory Category { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Funcionalidades:**
- [ ] Grid 2 columnas con filtrado por categoría (tabs)
- [ ] CRUD completo: modal agregar/editar (título, contenido, categoría)
- [ ] Toggle favorito ? por tarjeta
- [ ] Eliminación con confirmación

---

### Imagen 3 – Tab "Entrega"

- Ícono: ? morado
- Título: **"Configuración de entrega"**
- Descripción: _"Define las zonas de entrega, opciones de envío y métodos de pago disponibles"_
- Estado vacío: solo botón `+ Agregar zona de entrega`

**Entidad `DeliveryZone`:**
```csharp
public class DeliveryZone
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; }
    public decimal? Cost { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
```

**Funcionalidades:**
- [ ] Lista de zonas + botón agregar ? modal/form inline
- [ ] Toggle activo/inactivo por zona
- [ ] Estado vacío con CTA hasta primera zona creada

---

### Actualización Plan de implementación (Lote 7)

**Modelos nuevos:** `VendorConfig`, `KnowledgeEntry`, `DeliveryZone`

**Controladores:**
- [ ] `VendedorController` ? Personalidad, BaseConocimiento, Entrega, Pagos

**Vistas:**
- [ ] `Views/Vendedor/Personalidad.cshtml` ? form 6 secciones
- [ ] `Views/Vendedor/BaseConocimiento.cshtml` ? grid filtrable + CRUD
- [ ] `Views/Vendedor/Entrega.cshtml` ? lista zonas + agregar

**ViewModels:**
- [ ] `VendorPersonalidadViewModel`
- [ ] `KnowledgeEntryViewModel`
- [ ] `DeliveryZoneViewModel`

---

## LOTE 7 – Módulo de Productos (`/Productos`)

### Imagen 1 – Lista de productos registrados

**Ruta:** `/Productos`  
**Título:** Productos registrados  
**Subtítulo:** _"Visualiza, edita y gestiona todos los productos de tu tienda."_  
**Botones superiores derechos:** `??` (refresh) · `? Carga masiva` · `+ Nuevo producto` (primario morado)

#### Barra de búsqueda y filtro de categorías
- Input: "Buscar producto..." (con ícono lupa)
- Dropdown: "Todas las categorías" ? abre panel con:
  - Input interno: "Buscar categorías..."
  - Opción: `Seleccionar todo` · contador `0 / 10` · enlace `Limpiar`
  - Lista de categorías con radio button: Limpieza, zapatillas, Pack, kit, sneakers

#### Tabla de productos
| Columna | Detalle |
|---|---|
| Producto | Thumbnail + Nombre + "Creado el DD-MM-YYYY" |
| Categorías | Badges de tags (ej. "Pack", "kit limpieza") |
| Disponibilidad | Badge verde "Disponible" |
| Precio | Formato "S/. 100.00" |
| Acciones | Ícono ?? editar · ??? eliminar por fila |

#### Footer / Paginación
- Texto: "Total de 9 productos"
- Selector: "Filas por página: 10 ?"
- Paginación numérica

#### Toast flotante (inferior derecho)
- Muestra nombre del negocio: `"Yago"` · Botón `Probar vendedor`

---

### Imagen 2 – Formulario de edición de producto

**Ruta:** `/Productos/Editar`  
**Breadcrumb:** `Productos > Editar producto`  
**Botones superiores derechos:** `Cancelar` · `Guardar` (primario morado)

#### Sección 1 – Información básica
| Campo | Tipo | Detalle |
|---|---|---|
| Nombre del producto * | Input text | Máx. 99 chars, contador visible (ej. `99/99`) |
| Disponible | Toggle switch | Activado/desactivado |
| Descripción del producto | Textarea | Con contador de chars; texto que usa el vendedor IA |
| Características del producto | Textarea (columna derecha) | Palabras clave y contexto para respuestas del vendedor |
| Categorías | Tag input | Separadas por coma; hint: _"También puedes pegar tu lista separado por comas. Ej: Ropa, Calzado, Accesorios."_ |

#### Sección 2 – Imágenes y videos
- Drag & drop zone: _"Arrastre y suelte los archivos o haga clic para enviar"_
- Límites: Máximo 3 archivos · Fotos: 2MB · Videos: 5MB (PNG, JPG, PNG, 3GPP, MP4)
- Botón: `Importar`
- Sub-sección **"Imágenes actuales"** con badge contador (ej. `4 imágenes`)
  - Thumbnails clickeables para eliminar o reemplazar
  - Hint: _"Click en las imágenes actuales del producto para eliminarlas o agregar nuevas."_

#### Sección 3 – Precio general
- Descripción: _"Este precio será usado para la variante principal del producto"_
- Campo: `Precio base` · prefijo `PEN` · valor numérico · sufijo `PEN`
- Hint: _"Este será el precio de tu producto. Se guardará internamente como una variante principal."_

#### Sección 4 – Agregar variaciones
- Descripción: _"Ofrece diferentes opciones de tu producto, como color, talla o material."_
- Indicador de almacenamiento: `0.0MB / 20MB` · `20/de respuestas`
- Botón: `+ Agregar opción`

---

### Imagen adicional – Formulario de creación de nuevo producto

**Ruta:** `/Productos/Nuevo`  
**Breadcrumb:** `Productos > Nuevo producto`  
**Título de página:** "Agregar nuevo producto"  
**Botones superiores derechos:** `?? Cambios sin guardar` (badge advertencia, aparece al detectar cambios) · `Cancelar` · `Guardar` (primario morado)

> Este formulario es la variante de **creación** vs. la de edición (misma estructura, diferencias documentadas a continuación).

#### Diferencias clave respecto al formulario de edición

| Campo | Editar | Crear |
|---|---|---|
| Título de página | "Editar producto" | "Agregar nuevo producto" |
| Nombre del producto (máx) | 99 chars (`99/99`) | 150 chars (`0/150`) |
| Descripción del producto (máx) | sin detalle visible | 500 chars (`0/500`) |
| Características del producto (máx) | sin detalle visible | 1000 chars (`0/1000`) |
| Categorías placeholder | — | _"Escribe tus categorías y presiona 'Enter' o 'coma(,)' para agregarlas."_ |
| Precio base valor inicial | valor existente | `0,00` |
| Indicador de cambios | — | Badge `?? Cambios sin guardar` (aparece automáticamente al editar) |
| Imágenes actuales | Sección presente con thumbnails | No aplica (sin imágenes previas) |

#### Sección Agregar variaciones (detalle extendido)
- Fila de opción visible por defecto:
  - Input "Ej. Color" ? nombre de la opción
  - Input "Agregar valores..." ? valores separados por coma (ej. "Blanco, Negro, Gris")
  - Ícono ??? para eliminar la fila de opción
  - Hint debajo: _"Añade valores separados por coma"_
- Indicador: `0.0MB / 20MB` · `20/MB disponible`
- Botón: `+ Agregar opción` (permite múltiples opciones: Color, Talla, Material, etc.)

#### Placeholders de ejemplo en Información básica
| Campo | Placeholder / Ejemplo visible |
|---|---|
| Nombre del producto | _"Ej. Camiseta de algodón"_ |
| Descripción del producto | _"?? Nuestra camiseta de algodón es suave, fresca y perfecta para el día a día. Disponible en varios colores para combinar con todo."_ |
| Características del producto | _"Camiseta básica de algodón 100% peinado, suave al tacto y transpirable. Ideal para uso casual o deportivo en climas cálidos. Su corte recto favorece todo tipo de cuerpo. Tallas: XS, S, M, L, XL. Colores: Blanco, Negro, Gris, Azul marino."_ |

**Funcionalidades adicionales confirmadas:**
- [ ] Badge `?? Cambios sin guardar` que aparece dinámicamente al detectar cambios en el formulario
- [ ] Contadores de caracteres actualizados: Nombre 150, Descripción 500, Características 1000
- [ ] Placeholder descriptivo con ejemplo de producto en cada campo de texto
- [ ] Fila de variación pre-renderizada con inputs de nombre + valores + botón eliminar
- [ ] Hint "Añade valores separados por coma" bajo el input de valores de variación

---

### Imagen 3 – Modal de confirmación de eliminación

**Trigger:** Clic en ícono ??? de la fila de producto  
**Overlay:** Fondo oscuro semi-transparente sobre la lista

| Elemento | Detalle |
|---|---|
| Título | `¿Estás seguro?` |
| Cuerpo | _"Estás a punto de eliminar el producto '[Nombre del producto]'. Esta acción no se puede deshacer y eliminará permanentemente el producto de tu catálogo."_ |
| Botón cancelar | `Cancelar` (secundario, borde gris) |
| Botón confirmar | `Eliminar producto` (destructivo, rojo) |

---

### Entidades/campos implicados
- `Product` ? `Name`, `Description`, `Characteristics`, `BasePrice`, `IsAvailable`, `CreatedAt`, `BusinessId`
- `ProductCategory` ? relación muchos-a-muchos con tags de texto libre
- `ProductImage` ? `ProductId`, `ImageUrl`, `Order` (máx 3 por producto)
- `ProductVariant` ? `ProductId`, `OptionName`, `Price`, `Stock` (para variaciones)

### ViewModels a crear
- `ProductListViewModel` ? lista paginada + filtro por categoría + buscador
- `ProductEditViewModel` ? formulario completo (info + imágenes + precio + variantes)

### Funcionalidades a implementar
- [ ] `ProductosController` ? Index (lista paginada), Create, Edit, Delete
- [ ] Tabla con columnas: Producto, Categorías, Disponibilidad, Precio, Acciones
- [ ] Búsqueda por nombre de producto (input live search o submit)
- [ ] Filtro por categoría con dropdown multi-select (radio/checkbox) + "Seleccionar todo" + "Limpiar"
- [ ] Paginación con selector "Filas por página"
- [ ] Botón `+ Nuevo producto` ? formulario de creación
- [ ] Botón `? Carga masiva` ? importación CSV/Excel (pendiente detalle)
- [ ] Formulario de edición con 4 secciones: Información básica, Imágenes, Precio, Variaciones
- [ ] Toggle "Disponible" en el formulario (actualiza `IsAvailable`)
- [ ] Contador de caracteres en tiempo real: Nombre (150 max), Descripción (500 max), Características (1000 max)
- [ ] Tag input de categorías (separadas por coma, con chips eliminables)
- [ ] Drag & drop de imágenes (máx 3, validación de tipo y tamaño)
- [ ] Gestión de imágenes actuales (eliminar/reordenar con click)
- [ ] Sección de variaciones (`+ Agregar opción`) con indicador de almacenamiento
- [ ] Modal de confirmación de eliminación con mensaje dinámico con nombre del producto
- [ ] Toast flotante inferior: nombre del negocio + botón "Probar vendedor"

---

## LOTE 8 – Flujo de conexión WhatsApp (`/Canales`) – Estado pre-conexión y OAuth Meta

> Estas imágenes documentan la pantalla de inicio de conexión del canal WhatsApp
> y los dos primeros pasos del popup OAuth de Facebook/Meta que se abre al pulsar "Conectar WhatsApp".

---

### Imagen 1 – Página principal de Canales (estado pre-conexión)

**Ruta:** `/Configuraciones/Canales`  
**Breadcrumb:** `Configuración > Canales` · botón `? Volver`  
**App visible:** yavendió! (referencia de marca equivalente a InstaVende/ya!)

#### Sidebar izquierdo (estructura de navegación completa visible)
| Sección | Ítem | Detalle |
|---|---|---|
| Principal | Productos | — |
| Principal | Métricas | ?? (bloqueado, plan superior) |
| Principal | Mi vendedor | — |
| Cuenta | Planes | — |
| Cuenta | Programa de referidos | — |
| Cuenta | Configuraciones ? | Expandido |
| Cuenta ? Config | Canales | Badge naranja "Conectar What..." (acción pendiente) |
| Cuenta ? Config | Idioma | ???? Español |
| — | Soporte | — |

**Banner inferior del sidebar:**
- Avatar/ilustración: "Yago — socio de tu negocio"
- Subtítulo: _"Te conecta con lo que genera resultados"_
- Botón: `Activar sociedad` (primario oscuro, ancho completo)

#### Panel principal – Conecta tu WhatsApp Business
- **Título:** "Conecta tu WhatsApp Business"
- **Subtítulo:** _"Es el primer paso para empezar a vender con tu vendedor inteligente."_
- **Botones:** `Conectar WhatsApp` (primario oscuro) · `Contactar soporte` (secundario sin fondo)
- **Ilustración:** personaje decorativo robot/mago (esquina superior derecha)

#### Sección "Pasos para conectar" (3 pasos numerados)
| # | Título | Descripción |
|---|---|---|
| 1 | Inicia sesión en Meta | Conéctate con tu cuenta de Facebook Business. |
| 2 | Completa la información | Nombre, sitio web y categoría del negocio. |
| 3 | Activa tu número de WhatsApp | Selecciona "Usar solo un nombre visible" y confirma. |

- Layout: 3 tarjetas en fila horizontal con número de paso en badge circular
- Texto completo paso 3: _"Activa tu número de WhatsApp: Selecciona 'Usar solo un nombre visible' y confirma."_

#### Sección "Dudas frecuentes"
- Link superior derecho: `Explorar la guía detallada ?`
- Primera pregunta visible (accordion): "¿Necesito tener una cuenta en Meta Business?" (colapsada)
- (Mismas preguntas documentadas en Lote 6 para el estado post-conexión)

---

### Imagen 2 – Popup OAuth Meta: Pantalla de bienvenida (paso 1)

**Contexto:** Ventana emergente nativa de Facebook que se abre al pulsar "Conectar WhatsApp"  
**URL:** `facebook.com/v23.0/dialog/oauth?app_id=1593662348031270&...`  
**Título de ventana:** "Inicio de sesión con Facebook para empresas"  
**Header del popup:** Logo Meta (`?`) · flecha ? · logo ya! · ícono de cuenta

#### Contenido
- **Ilustración:** banner superior con gráfico colorido (apretón de manos, personas, iconos de negocio)
- **Título:** _"Conecta tu cuenta fácilmente con YaVendió"_
- **Subtítulo:** _"En este proceso de registro, se te guiará para que registres tu cuenta de empresa y la conectes con tu socio."_

**Sección "Podrás hacer lo siguiente:"**
- Ícono ?? + título: **"Comunícate con tus clientes a gran escala"**
- Descripción: _"La API de la nube te permite enviar y recibir mensajes de forma segura y administrar las conversaciones de manera automática."_
- Bullet 1: _"Manejar grandes volúmenes de mensajes con facilidad"_
- Bullet 2: _"Reducir los costos asociados a los SMS o llamadas de voz tradicionales"_

**Footer legal:**
- _"Al continuar, aceptas las Condiciones de hospedaje de Meta para la API de la nube y las Condiciones de Meta para WhatsApp Business."_ (ambos como enlaces)
- Link: `Política de privacidad de YaVendio`
- Botones: `Cancelar` (secundario) · `Continuar` (primario azul)

---

### Imagen 3 – Popup OAuth Meta: Selección de activos comerciales (paso 2)

**Contexto:** Segundo paso dentro del mismo popup OAuth de Facebook  
**URL:** `facebook.com/v23.0/dialog/oauth?app_id=1593662348031270&cbt=...`

#### Contenido
- **Título:** _"Selecciona los activos comerciales que quieres compartir con YaVendio"_
- **Subtítulo:** _"Puedes usar activos existentes o crear nuevos."_

**Campos:**
| Campo | Tipo | Estado ejemplo |
|---|---|---|
| Portfolio comercial ?? | Dropdown | "Cecile Mellet - Joyas" (con ícono "C" rojo) |
| Cuenta de WhatsApp Business ?? | Dropdown | "Selecciona una cuenta de WhatsApp Business" (vacío) |

- Los íconos ?? indican tooltips de ayuda contextual
- El Portfolio comercial se pre-selecciona con el negocio de Facebook del usuario
- La Cuenta de WhatsApp Business se vincula al portfolio seleccionado

**Footer:**
- Link: `Política de privacidad de YaVendio`
- Botones: `Volver` (secundario) · `Siguiente` (primario azul)

---

### Imagen adicional A – Popup OAuth Meta: Dropdown "Portfolio comercial" expandido

**Contexto:** Paso 2 del popup, con el dropdown de Portfolio comercial abierto mostrando todas las opciones del usuario  

#### Opciones del dropdown Portfolio comercial
| Opción | Tipo | Identificador | Nota |
|---|---|---|---|
| Crea un portfolio comercial | Acción | — | Aparece en gris con advertencia: _"Alcanzaste el límite de portfolios comerciales."_ |
| Cecile Mellet - Joyas | Portfolio (seleccionado) | `359994644525367` | Ícono "C" rojo · resaltado en azul |
| Harmonia Do Sol | Portfolio | `655891477379543` | Ícono "H" gris |
| Patio Digital | Portfolio | — | Advertencia naranja: _"Business is not eligible for advertising based on Facebook Advertising P..."_ |
| solarixshop | Portfolio | `1418789775705678` | Ícono "S" azul |

**Detalles técnicos:**
- Cada portfolio muestra su **Identificador numérico** (ID de Business Manager de Meta) bajo el nombre
- El portfolio seleccionado queda resaltado en azul con bullet azul relleno
- Se puede buscar portfolio por nombre (input con ícono lupa)
- El aviso _"Alcanzaste el límite"_ bloquea la creación de portfolios adicionales

---

### Imagen adicional B – Popup OAuth Meta: Dropdown "Cuenta de WhatsApp Business" expandido

**Contexto:** Paso 2 del popup, con Portfolio "Harmonia Do Sol" seleccionado y dropdown de Cuenta WA abierto  

#### Opciones del dropdown Cuenta de WhatsApp Business
| Opción | Ícono | Descripción |
|---|---|---|
| Crear una cuenta de WhatsApp Business | ? (checkbox vacío) | Crea una WABA nueva vinculada al portfolio |
| Conecta una app de WhatsApp Business | ?? ícono WhatsApp verde | Vincula una app WA Business existente (Coexistencia) |

- Input de búsqueda disponible (lupa) dentro del dropdown
- La opción **"Conecta una app de WhatsApp Business"** corresponde a la conexión Coexistencia (recomendada, QR)
- Botón `Siguiente` permanece deshabilitado hasta seleccionar una opción

---

### Imagen adicional C – Popup OAuth Meta: Ingreso de número de teléfono (paso 3)

**Contexto:** Tercer paso del popup; aparece después de seleccionar el portfolio y la cuenta WA  
**Indicador de pasos:** 3 radio buttons (paso 3 activo = último)

#### Contenido
- **Título:** "Ingresa tu número de teléfono de WhatsApp Business"
- **Subtítulo (en inglés):** _"Select a country and enter your phone number."_

**Campo número de teléfono:**
- Selector de país: `PE +51` (dropdown con bandera de Perú, prefijo internacional)
- Input: campo vacío para el número local

**Sección informativa "?? Conecta tu app de WhatsApp Business existente":**
- Texto principal (en inglés): _"Share your WhatsApp Business account with YaVendió. You'll still have full access to your WhatsApp Business app and can continue using it."_
- **"Compartir acceso"**: _"Es posible que se comparta información de la cuenta, como tu número de teléfono, contactos, chats e historial de chat."_
- **"Protección de tus datos"**: _"Los chats individuales ahora se administrarán con un servicio seguro de Meta. Enviaremos una notificación a tus clientes sobre este cambio de privacidad."_

**Footer:**
- Link: `Política de privacidad de YaVendio`
- Botones: `Volver` (secundario) · `Siguiente` (primario azul, deshabilitado hasta ingresar número)

---

### Flujo completo de conexión WhatsApp (actualizado con pasos confirmados)

```
[Canales pre-conexión]
  ? Clic "Conectar WhatsApp"
    ? [Popup OAuth Meta – Paso 1: Bienvenida + descripción API / Continuar]
      ? [Popup OAuth Meta – Paso 2: Selección Portfolio comercial + Cuenta WA Business]
           ? Portfolio: lista con ID, búsqueda, opción crear (si no hay límite)
           ? Cuenta WA: "Crear nueva WABA" o "Conectar app existente (Coexistencia)"
        ? [Popup OAuth Meta – Paso 3: Ingreso número de teléfono con selector de país]
             ? Aviso: compartir acceso + protección de datos
          ? [Canales post-conexión: banner éxito + tarjeta activa] (documentado en Lote 6)
```

> **Nota:** El popup es 100% gestionado por Meta (Facebook OAuth). InstaVende solo controla:
> - La URL de inicio del OAuth (con `app_id`, `redirect_uri`, `scopes`)
> - El callback al que Meta redirige con el `code`
> - El procesamiento del `code` para obtener el `access_token` y guardar los datos del canal

### Funcionalidades a implementar / completar en `ChannelConfig`

- [ ] Vista pre-conexión (`IsActive == false`): título + subtítulo + botón "Conectar WhatsApp" + pasos + FAQ
- [ ] Botón `Contactar soporte` (secundario) ? enlace a WhatsApp/email de soporte
- [ ] Pasos de conexión (3 tarjetas numeradas) con descripciones completas
- [ ] Banner inferior sidebar: "Yago — socio de tu negocio" + botón "Activar sociedad"
- [ ] Badge de alerta en ítem "Canales" del sidebar cuando canal no conectado
- [ ] Acción `ConectarWhatsApp` ? redirige al OAuth URL de Meta con `app_id`, `redirect_uri`, `scopes` correctos
- [ ] Callback OAuth de Meta: recibir `code`, intercambiar por `access_token`, extraer `WabaId` y `PhoneNumberId`
- [ ] Guardar en BD: `PhoneNumber`, `WabaId`, `PhoneNumberId`, `AccessToken`, `ConnectedAt`
- [ ] Redirigir a vista post-conexión al completar el flujo exitosamente
- [ ] Manejo de errores OAuth (usuario cancela, permisos insuficientes, portfolio sin número WA)

### Entidades/campos a agregar
- `WhatsAppChannel` ? `BusinessId`, `PhoneNumber`, `WabaId`, `PhoneNumberId`, `AccessToken`, `ConnectedAt`, `IsActive`
- (O extender entidad de canal existente con estos campos)

---
