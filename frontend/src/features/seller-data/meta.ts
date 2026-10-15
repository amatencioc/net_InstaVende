import type { LucideIcon } from 'lucide-react'
import {
  Banknote,
  CircleDollarSign,
  Clock,
  CreditCard,
  Landmark,
  Layers,
  Link2,
  MessageCircleQuestion,
  Package,
  Smartphone,
  Truck,
  Undo2,
  Wallet,
} from 'lucide-react'
import type { KnowledgeCategory, PaymentMethodType } from './api'

// ── Base de conocimiento ─────────────────────────────────────────────────────

export interface CategoryMeta {
  label: string
  icon: LucideIcon
  /** Clases de color (tokens del tema) para insignias e iconos. */
  tone: string
}

export const knowledgeCategories: KnowledgeCategory[] = ['Productos', 'Horarios', 'Envios', 'Pagos', 'Devoluciones', 'Otros']

export const categoryMeta: Record<KnowledgeCategory, CategoryMeta> = {
  Productos: { label: 'Productos', icon: Package, tone: 'bg-info/10 text-info' },
  Horarios: { label: 'Horarios', icon: Clock, tone: 'bg-warning/15 text-warning' },
  Envios: { label: 'Envíos', icon: Truck, tone: 'bg-primary/10 text-primary' },
  Pagos: { label: 'Pagos', icon: CreditCard, tone: 'bg-brand-accent/10 text-brand-accent' },
  Devoluciones: { label: 'Devoluciones', icon: Undo2, tone: 'bg-destructive/10 text-destructive' },
  Otros: { label: 'Otros', icon: MessageCircleQuestion, tone: 'bg-muted text-muted-foreground' },
}

export const allCategoriesMeta = { label: 'Todas', icon: Layers }

/** Temas que conviene cubrir; se ofrecen en el estado vacío y abren el diálogo prellenado. */
export const knowledgeSuggestions: { title: string; category: KnowledgeCategory }[] = [
  { title: '¿Cuál es su horario de atención?', category: 'Horarios' },
  { title: '¿Hacen envíos a provincia?', category: 'Envios' },
  { title: '¿Aceptan pago con tarjeta?', category: 'Pagos' },
  { title: '¿Cuál es la política de cambios y devoluciones?', category: 'Devoluciones' },
  { title: '¿Los productos tienen garantía?', category: 'Productos' },
  { title: '¿Tienen tienda física?', category: 'Otros' },
]

export const KNOWLEDGE_TITLE_MAX = 200
export const KNOWLEDGE_CONTENT_MAX = 2000
export const KNOWLEDGE_IMPORT_MAX_ROWS = 500

// ── Métodos de pago ──────────────────────────────────────────────────────────

export interface PaymentTypeMeta {
  label: string
  description: string
  icon: LucideIcon
  tone: string
  /** Ejemplos para orientar el llenado según el tipo. */
  namePlaceholder: string
  accountPlaceholder: string
  aliasPlaceholder: string
  instructionsPlaceholder: string
  /** El enlace de pago es el dato principal (pasarelas en línea). */
  linkFirst?: boolean
}

export const paymentTypes: PaymentMethodType[] = [
  'BankTransfer',
  'DigitalWallet',
  'Cash',
  'CreditCard',
  'MercadoPago',
  'PayPal',
  'Stripe',
  'Other',
]

export const paymentTypeMeta: Record<PaymentMethodType, PaymentTypeMeta> = {
  BankTransfer: {
    label: 'Transferencia bancaria',
    description: 'Depósito o transferencia a tu cuenta.',
    icon: Landmark,
    tone: 'bg-info/10 text-info',
    namePlaceholder: 'BCP – Cuenta de ahorros',
    accountPlaceholder: '191-12345678-0-12',
    aliasPlaceholder: 'CCI 002-191-112345678012-55',
    instructionsPlaceholder: 'Titular: Tu Negocio SAC. Envía la constancia por este chat para confirmar tu pedido.',
  },
  DigitalWallet: {
    label: 'Billetera digital',
    description: 'Yape, Plin, Nequi u otra billetera.',
    icon: Smartphone,
    tone: 'bg-brand-accent/10 text-brand-accent',
    namePlaceholder: 'Yape / Plin',
    accountPlaceholder: '994 832 877',
    aliasPlaceholder: 'Titular: Ana Pérez',
    instructionsPlaceholder: 'Envía el pago al número indicado y comparte la captura por este chat.',
  },
  Other: {
    label: 'Otro medio',
    description: 'Cualquier otra forma de pago.',
    icon: Smartphone,
    tone: 'bg-brand-accent/10 text-brand-accent',
    namePlaceholder: 'Yape / Plin',
    accountPlaceholder: '994 832 877',
    aliasPlaceholder: 'Titular: Ana Pérez',
    instructionsPlaceholder: 'Envía el pago al número indicado y comparte la captura por este chat.',
  },
  Cash: {
    label: 'Efectivo / contraentrega',
    description: 'El cliente paga al recibir el pedido.',
    icon: Banknote,
    tone: 'bg-success/10 text-success',
    namePlaceholder: 'Pago contraentrega',
    accountPlaceholder: '',
    aliasPlaceholder: '',
    instructionsPlaceholder: 'Paga en efectivo al recibir tu pedido. Ten el monto exacto, el motorizado no lleva cambio.',
  },
  CreditCard: {
    label: 'Tarjeta de crédito o débito',
    description: 'POS en la entrega o cobro con tarjeta.',
    icon: CreditCard,
    tone: 'bg-warning/15 text-warning',
    namePlaceholder: 'Tarjeta Visa / Mastercard',
    accountPlaceholder: '',
    aliasPlaceholder: '',
    instructionsPlaceholder: 'Aceptamos Visa y Mastercard. El motorizado lleva POS.',
  },
  MercadoPago: {
    label: 'Mercado Pago',
    description: 'Link de pago o QR de Mercado Pago.',
    icon: Wallet,
    tone: 'bg-info/10 text-info',
    namePlaceholder: 'Mercado Pago',
    accountPlaceholder: '',
    aliasPlaceholder: 'tu.negocio.mp',
    instructionsPlaceholder: 'Paga con tarjeta o saldo desde el enlace.',
    linkFirst: true,
  },
  PayPal: {
    label: 'PayPal',
    description: 'Pagos internacionales con PayPal.',
    icon: CircleDollarSign,
    tone: 'bg-info/10 text-info',
    namePlaceholder: 'PayPal',
    accountPlaceholder: '',
    aliasPlaceholder: 'pagos@tunegocio.com',
    instructionsPlaceholder: 'Paga de forma segura desde el enlace de PayPal.',
    linkFirst: true,
  },
  Stripe: {
    label: 'Stripe',
    description: 'Link de pago con tarjeta vía Stripe.',
    icon: Link2,
    tone: 'bg-brand-accent/10 text-brand-accent',
    namePlaceholder: 'Pago con tarjeta en línea',
    accountPlaceholder: '',
    aliasPlaceholder: '',
    instructionsPlaceholder: 'Completa el pago con tu tarjeta desde el enlace.',
    linkFirst: true,
  },
}

/** Etiquetas de los campos de cuenta según el tipo (formulario, tarjeta y vista previa). */
export function paymentFieldLabels(type: PaymentMethodType): { account: string; alias: string } {
  switch (type) {
    case 'BankTransfer':
      return { account: 'Número de cuenta', alias: 'CCI o titular' }
    case 'DigitalWallet':
    case 'Other':
      return { account: 'Número', alias: 'Titular' }
    case 'MercadoPago':
    case 'PayPal':
      return { account: 'Cuenta', alias: 'Usuario o correo' }
    default:
      return { account: 'Cuenta', alias: 'Alias' }
  }
}

export const PAYMENT_INSTRUCTIONS_MAX = 1000
export const PAYMENT_IMAGES_MAX = 5
export const PAYMENT_IMAGE_MAX_BYTES = 5 * 1024 * 1024
