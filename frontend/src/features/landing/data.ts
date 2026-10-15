import { Bot, Plug, Rocket, type LucideIcon } from 'lucide-react'
import type { SectionId } from './scroll'

/** Correo de contacto público (cambiar aquí si se usa otro buzón). */
export const CONTACT_EMAIL = 'hola@instavende.app'

export const navLinks: { id: SectionId; label: string }[] = [
  { id: 'como-funciona', label: 'Cómo funciona' },
  { id: 'funciones', label: 'Funciones' },
  { id: 'beneficios', label: 'Beneficios' },
  { id: 'precios', label: 'Precios' },
  { id: 'faq', label: 'Preguntas' },
]

export const steps: { icon: LucideIcon; title: string; text: string; detail: string }[] = [
  {
    icon: Plug,
    title: 'Conecta tus canales',
    text: 'Vincula WhatsApp escaneando un código QR (o con la API oficial de WhatsApp Cloud) y suma Instagram y Messenger.',
    detail: 'Una sola bandeja para todo',
  },
  {
    icon: Bot,
    title: 'Entrena a tu vendedor',
    text: 'Carga tu catálogo con variantes y stock, y define personalidad, reglas de descuento, métodos de pago y zonas de entrega.',
    detail: 'Sin código ni plantillas rígidas',
  },
  {
    icon: Rocket,
    title: 'Vende en piloto automático',
    text: 'Tu vendedor IA responde, recomienda, negocia y registra cada pedido. Tú supervisas y tomas el control cuando quieras.',
    detail: 'Atención 24/7, todos los días',
  },
]

export const stats: { value: string; label: string; text: string }[] = [
  {
    value: '24/7',
    label: 'Atención sin pausas',
    text: 'Responde de madrugada, en feriados y en plena campaña.',
  },
  {
    value: '3',
    label: 'Canales en una bandeja',
    text: 'WhatsApp, Instagram y Messenger, con historial unificado.',
  },
  {
    value: '3',
    label: 'Niveles de descuento',
    text: 'Negocia solo dentro de los límites y el margen que definas.',
  },
  {
    value: '8',
    label: 'Estados de pedido',
    text: 'Desde “En validación” hasta “Entregado”, en un tablero kanban.',
  },
]

export const comparison = {
  without: [
    'Respondes horas después y el cliente ya compró en otro lado.',
    'Copias y pegas precios, fotos y tallas a mano, chat por chat.',
    'Das descuentos improvisados que se comen tu margen.',
    'Los pedidos se pierden entre chats, notas y capturas.',
    'No sabes qué canal ni qué producto te genera más ventas.',
    'Los interesados que no compraron nunca vuelven a saber de ti.',
  ],
  with: [
    'Respuesta inmediata, de día y de noche, en todos tus canales.',
    'Recomienda productos de tu catálogo con variantes, precios y stock.',
    'Negocia con niveles de descuento y margen mínimo bajo tu control.',
    'Cada venta queda registrada como pedido en un tablero kanban.',
    'Métricas de ventas, conversiones, canales y productos.',
    'Recordatorios automáticos según el interés de cada cliente.',
  ],
}

export interface Testimonial {
  quote: string
  role: string
  place: string
  initials: string
}

/** Testimonios ilustrativos (personas y negocios genéricos, no reales). */
export const testimonials: Testimonial[] = [
  {
    quote:
      'Antes pasaba la noche contestando “¿precio?” y “¿hay en talla M?”. Ahora el vendedor responde solo y yo reviso los pedidos en la mañana.',
    role: 'Dueña de una tienda de moda',
    place: 'Lima',
    initials: 'TM',
  },
  {
    quote:
      'Lo que más me convenció fue poner un tope a los descuentos. Negocia con el cliente, pero nunca baja de mi margen.',
    role: 'Emprendedor de accesorios tecnológicos',
    place: 'Arequipa',
    initials: 'AT',
  },
  {
    quote:
      'Cuando alguien pide algo especial, el bot me lo pasa con todo el contexto. El cliente no siente el cambio y yo no pierdo la venta.',
    role: 'Fundadora de una marca de cosmética natural',
    place: 'Trujillo',
    initials: 'CN',
  },
]

export type BillingCycle = 'monthly' | 'yearly'

/** Meses que se pagan al elegir facturación anual. */
export const YEARLY_PAID_MONTHS = 10

export interface Plan {
  id: 'free' | 'pro' | 'business'
  name: string
  description: string
  /** Precio mensual en soles. El pago anual equivale a 10 meses (2 meses gratis). */
  monthlyPrice: number
  cta: string
  highlighted?: boolean
  features: string[]
}

export const plans: Plan[] = [
  {
    id: 'free',
    name: 'Gratis',
    description: 'Para probar tu vendedor IA con tus primeros clientes.',
    monthlyPrice: 0,
    cta: 'Empezar gratis',
    features: [
      '1 canal (WhatsApp)',
      '100 conversaciones con IA al mes',
      'Hasta 30 productos',
      'Bandeja de mensajes y pedidos',
      'Personalidad y base de conocimiento',
    ],
  },
  {
    id: 'pro',
    name: 'Pro',
    description: 'Para negocios que ya venden todos los días por chat.',
    monthlyPrice: 149,
    cta: 'Probar Pro',
    highlighted: true,
    features: [
      'WhatsApp, Instagram y Messenger',
      '2 000 conversaciones con IA al mes',
      'Productos ilimitados con variantes',
      'Negociación con descuentos controlados',
      'Recordatorios por segmento',
      'Métricas de ventas y del vendedor',
      'Hasta 3 usuarios',
    ],
  },
  {
    id: 'business',
    name: 'Business',
    description: 'Para equipos con alto volumen de conversaciones.',
    monthlyPrice: 349,
    cta: 'Elegir Business',
    features: [
      'Todo lo del plan Pro',
      '10 000 conversaciones con IA al mes',
      'Usuarios ilimitados con roles',
      'Derivación a tu equipo con pausa automática',
      'Acompañamiento en la configuración',
      'Soporte prioritario',
    ],
  },
]

export const faqs: { q: string; a: string }[] = [
  {
    q: '¿Necesito la API oficial de WhatsApp Business?',
    a: 'No es obligatorio. Puedes vincular tu número escaneando un código QR desde el panel, igual que en WhatsApp Web. Si ya tienes la API oficial de WhatsApp Cloud, también puedes conectarla.',
  },
  {
    q: '¿El vendedor IA puede inventar precios o regalar descuentos?',
    a: 'No. Responde con la información de tu catálogo y tu base de conocimiento, y solo ofrece descuentos dentro de los niveles, el tope máximo y el margen mínimo que tú configures.',
  },
  {
    q: '¿Qué pasa si un cliente quiere hablar con una persona?',
    a: 'Defines en qué situaciones debe derivar (reclamos, pedidos especiales, etc.). El vendedor avisa al cliente, se pausa automáticamente en esa conversación y tú continúas desde la bandeja con todo el historial.',
  },
  {
    q: '¿Puedo probarlo antes de conectarlo a mis clientes?',
    a: 'Sí. Desde “Mi vendedor” tienes un chat de prueba para conversar con él, ajustar su tono y revisar sus respuestas antes de activarlo en tus canales.',
  },
  {
    q: '¿Cómo cobro las ventas?',
    a: 'InstaVende no procesa pagos: tú configuras tus métodos (transferencia, Yape, Plin, contra entrega, link de pago, etc.) y el vendedor los comparte al cerrar la venta. El pedido queda “En validación” hasta que confirmes el pago.',
  },
  {
    q: '¿Puedo trabajar con mi equipo?',
    a: 'Sí. Invita a tu equipo por correo y asigna roles de administrador o miembro para que atiendan la bandeja y gestionen pedidos.',
  },
  {
    q: '¿Hay contratos o permanencia?',
    a: 'No. Puedes empezar gratis, sin tarjeta de crédito, y cambiar de plan o cancelar cuando quieras.',
  },
  {
    q: '¿Mis datos y los de mis clientes están seguros?',
    a: 'Cada negocio tiene sus datos aislados, las credenciales de tus canales se guardan cifradas y las sesiones están protegidas. Nunca vendemos tu información.',
  },
]
