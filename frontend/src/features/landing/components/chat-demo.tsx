import { BadgePercent, CheckCheck, ChevronLeft, Package, Receipt, RotateCcw, ShoppingBag } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { WhatsAppIcon } from '@/components/shared/brand-icons'
import { cn } from '@/lib/utils'
import { useInView, usePrefersReducedMotion } from '../hooks'

type Attachment = 'product' | 'order' | 'receipt'

interface ChatStep {
  from: 'customer' | 'seller'
  text: string
  time: string
  attachment?: Attachment
}

/** Guion de la demo: el vendedor IA recomienda, negocia dentro de las reglas y cierra el pedido. */
const script: ChatStep[] = [
  { from: 'customer', text: 'Hola 👋 ¿tienen las zapatillas urbanas blancas en talla 40?', time: '21:47' },
  {
    from: 'seller',
    text: '¡Hola! Soy Sofía de Urban Step 😊 Sí, tenemos las Urban Classic en talla 40:',
    time: '21:47',
    attachment: 'product',
  },
  {
    from: 'customer',
    text: 'Me encantan, pero se me pasa un poco del presupuesto 😅 ¿algún descuento?',
    time: '21:48',
  },
  {
    from: 'seller',
    text: 'Te entiendo. Por ser tu primera compra te descuento S/ 20: quedan en S/ 179.90 y el envío a Miraflores es gratis 🎁',
    time: '21:48',
  },
  { from: 'customer', text: '¡Trato hecho! ¿Cómo pago?', time: '21:49' },
  {
    from: 'seller',
    text: '¡Genial! Registré tu pedido. Puedes pagar por Yape, Plin o transferencia y enviarme tu comprobante aquí 👇',
    time: '21:49',
    attachment: 'order',
  },
  { from: 'customer', text: 'Listo, ya te envié el comprobante 🙌', time: '21:51', attachment: 'receipt' },
  {
    from: 'seller',
    text: '¡Gracias! Tu pedido quedó en validación ✅ Te aviso apenas salga a reparto.',
    time: '21:51',
  },
]

const DISCOUNT_STEP = 3
const ORDER_STEP = 5

const transcript = script
  .map((s) => `${s.from === 'customer' ? 'Cliente' : 'Vendedor IA'}: ${s.text}`)
  .join(' ')

function ProductCard() {
  return (
    <div className="bg-background/70 mt-2 flex gap-3 rounded-lg border p-2">
      <svg viewBox="0 0 64 64" className="bg-accent size-14 shrink-0 rounded-md" aria-hidden>
        <path
          d="M8 40c6 0 12-4 16-10l6 4c4 3 10 4 16 4h6c3 0 6 3 6 6v2H8z"
          className="fill-card stroke-brand-accent"
          strokeWidth="2.5"
          strokeLinejoin="round"
        />
        <path d="M8 46h50" className="stroke-brand-accent" strokeWidth="2.5" strokeLinecap="round" />
        <path d="M30 34l3-4M35 36l3-4" className="stroke-primary" strokeWidth="2.5" strokeLinecap="round" />
      </svg>
      <div className="min-w-0 text-xs">
        <p className="font-semibold">Urban Classic Blanca</p>
        <p className="text-muted-foreground">Tallas 38–43 · Blanco, negro</p>
        <p className="mt-1 flex items-center gap-2">
          <span className="font-semibold">S/ 199.90</span>
          <span className="bg-success/15 text-success rounded px-1.5 py-0.5 text-[10px] font-semibold">
            En stock
          </span>
        </p>
      </div>
    </div>
  )
}

function OrderCard() {
  return (
    <div className="bg-background/70 mt-2 rounded-lg border p-2.5 text-xs">
      <p className="flex items-center gap-1.5 font-semibold">
        <ShoppingBag className="text-primary size-3.5" aria-hidden /> Pedido #1048
      </p>
      <dl className="mt-1.5 space-y-0.5">
        <div className="flex justify-between gap-3">
          <dt className="text-muted-foreground">Urban Classic T40</dt>
          <dd className="whitespace-nowrap tabular-nums">S/ 179.90</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-muted-foreground">Envío · Miraflores</dt>
          <dd className="text-success font-medium">Gratis</dd>
        </div>
        <div className="flex justify-between gap-3 border-t pt-1 font-semibold">
          <dt>Total</dt>
          <dd className="whitespace-nowrap tabular-nums">S/ 179.90</dd>
        </div>
      </dl>
    </div>
  )
}

function ReceiptCard() {
  return (
    <div className="bg-background/60 mt-2 flex items-center gap-2 rounded-lg border p-2 text-xs">
      <span className="bg-primary/15 text-primary flex size-8 items-center justify-center rounded-md">
        <Receipt className="size-4" aria-hidden />
      </span>
      <span>
        <span className="block font-medium">comprobante.jpg</span>
        <span className="text-muted-foreground">Imagen · 184 KB</span>
      </span>
    </div>
  )
}

const attachmentCards: Record<Attachment, () => ReactNode> = {
  product: ProductCard,
  order: OrderCard,
  receipt: ReceiptCard,
}

function Bubble({ step, animate }: { step: ChatStep; animate: boolean }) {
  const mine = step.from === 'seller'
  const AttachmentCard = step.attachment ? attachmentCards[step.attachment] : null
  return (
    <div className={cn('flex', mine ? 'justify-end' : 'justify-start', animate && 'lp-message')}>
      <div
        className={cn(
          'max-w-[85%] rounded-2xl px-3 py-2 text-[13px] leading-snug shadow-sm',
          mine ? 'bg-primary/15 dark:bg-primary/20 rounded-br-md' : 'bg-card rounded-bl-md',
        )}
      >
        <p>{step.text}</p>
        {AttachmentCard && <AttachmentCard />}
        <p className="text-muted-foreground mt-1 flex items-center justify-end gap-1 text-[10px]">
          {step.time}
          {mine && <CheckCheck className="text-info size-3" aria-hidden />}
        </p>
      </div>
    </div>
  )
}

function TypingBubble() {
  return (
    <div className="lp-message flex justify-end">
      <div className="bg-primary/15 dark:bg-primary/20 flex items-center gap-1 rounded-2xl rounded-br-md px-3 py-3 shadow-sm">
        <span className="lp-typing-dot bg-primary size-1.5 rounded-full" />
        <span className="lp-typing-dot bg-primary size-1.5 rounded-full" />
        <span className="lp-typing-dot bg-primary size-1.5 rounded-full" />
      </div>
    </div>
  )
}

/** Tarjeta flotante junto al teléfono (posición absoluta: no provoca saltos de diseño). */
function FloatingCard({
  show,
  className,
  children,
}: {
  show: boolean
  className?: string
  children: ReactNode
}) {
  return (
    <div
      className={cn(
        'bg-card/90 absolute z-10 hidden rounded-xl border p-3 text-xs shadow-xl backdrop-blur transition-all duration-500 sm:block',
        show ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-2 opacity-0',
        className,
      )}
    >
      <div className="lp-float flex items-center gap-2.5">{children}</div>
    </div>
  )
}

/**
 * Maqueta de una conversación de WhatsApp animada paso a paso.
 * Arranca al entrar en pantalla; con "reducir movimiento" muestra la conversación completa sin animar.
 */
export function ChatDemo() {
  const reducedMotion = usePrefersReducedMotion()
  const [ref, inView] = useInView<HTMLDivElement>({ threshold: 0.3, rootMargin: '0px' })
  const [count, setCount] = useState(0)
  const [typing, setTyping] = useState(false)

  const shown = reducedMotion ? script.length : count
  const finished = shown >= script.length

  useEffect(() => {
    const next = script[count]
    if (reducedMotion || !inView || !next) return
    if (next.from === 'seller' && !typing) {
      const t = setTimeout(() => setTyping(true), 600)
      return () => clearTimeout(t)
    }
    const delay = next.from === 'seller' ? 1500 : count === 0 ? 500 : 1300
    const t = setTimeout(() => {
      setTyping(false)
      setCount((c) => c + 1)
    }, delay)
    return () => clearTimeout(t)
  }, [count, typing, inView, reducedMotion])

  const replay = () => {
    setTyping(false)
    setCount(0)
  }

  return (
    <figure ref={ref} className="relative mx-auto w-full max-w-[22.5rem]">
      <figcaption className="sr-only">
        Ejemplo de conversación por WhatsApp entre un cliente y el vendedor IA de InstaVende. {transcript}
      </figcaption>

      <FloatingCard show={shown > DISCOUNT_STEP} className="top-28 -left-12 lg:-left-20">
        <span className="bg-brand-accent/15 text-brand-accent flex size-8 items-center justify-center rounded-lg">
          <BadgePercent className="size-4" aria-hidden />
        </span>
        <span>
          <span className="block font-semibold">Descuento de S/ 20 aplicado</span>
          <span className="text-muted-foreground">Dentro de tu margen mínimo</span>
        </span>
      </FloatingCard>

      <FloatingCard show={shown > ORDER_STEP} className="-right-8 bottom-24 lg:-right-16">
        <span className="bg-primary/15 text-primary flex size-8 items-center justify-center rounded-lg">
          <Package className="size-4" aria-hidden />
        </span>
        <span>
          <span className="block font-semibold">Pedido #1048 creado</span>
          <span className="text-muted-foreground">Estado: En validación</span>
        </span>
      </FloatingCard>

      {/* Teléfono */}
      <div
        aria-hidden
        className="bg-foreground/90 dark:bg-card ring-border rounded-[2.5rem] p-2.5 shadow-2xl ring-1"
      >
        <div className="bg-background overflow-hidden rounded-[2rem]">
          <div className="bg-primary text-primary-foreground flex items-center gap-2.5 px-3 pt-4 pb-3">
            <ChevronLeft className="size-5 opacity-80" />
            <span className="bg-primary-foreground/20 flex size-9 items-center justify-center rounded-full">
              <ShoppingBag className="size-4" />
            </span>
            <span className="min-w-0 flex-1 leading-tight">
              <span className="block text-sm font-semibold">Urban Step</span>
              <span className="block text-[11px] opacity-85">{typing ? 'escribiendo…' : 'en línea'}</span>
            </span>
            <WhatsAppIcon className="size-5 opacity-90" />
          </div>

          <div className="lp-chat-bg relative flex h-[30rem] flex-col justify-end gap-2 overflow-hidden px-3 pt-8 pb-3">
            <div className="from-muted pointer-events-none absolute inset-x-0 top-0 z-10 h-10 bg-gradient-to-b to-transparent" />
            {script.slice(0, shown).map((step, i) => (
              <Bubble key={i} step={step} animate={!reducedMotion} />
            ))}
            {typing && !reducedMotion && <TypingBubble />}
          </div>

          <div className="bg-background flex items-center gap-2 border-t px-3 py-2.5">
            <span className="bg-muted text-muted-foreground flex-1 rounded-full px-3 py-1.5 text-xs">
              Escribe un mensaje
            </span>
            <span className="bg-primary text-primary-foreground flex size-8 items-center justify-center rounded-full">
              <WhatsAppIcon className="size-4" />
            </span>
          </div>
        </div>
      </div>

      <div className="mt-4 flex h-8 justify-center">
        {finished && !reducedMotion && (
          <button
            type="button"
            onClick={replay}
            className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors outline-none focus-visible:ring-[3px]"
          >
            <RotateCcw className="size-3.5" aria-hidden /> Repetir conversación
          </button>
        )}
      </div>
    </figure>
  )
}
