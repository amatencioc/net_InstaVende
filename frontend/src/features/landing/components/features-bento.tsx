import {
  BellRing,
  Bot,
  ChartColumn,
  CreditCard,
  Headset,
  MapPin,
  MessagesSquare,
  SquareKanban,
  Tags,
  Timer,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { InstagramIcon, MessengerIcon, WhatsAppIcon } from '@/components/shared/brand-icons'
import { cn } from '@/lib/utils'
import { GradientText, Reveal, Section, SectionHeading } from './primitives'

function BentoCard({
  icon: Icon,
  title,
  text,
  className,
  delay,
  children,
}: {
  icon: LucideIcon
  title: string
  text: string
  className?: string
  delay?: number
  children?: ReactNode
}) {
  return (
    <li className={cn('list-none', className)}>
      <Reveal delay={delay} className="h-full">
        <article className="group bg-card hover:border-primary/40 relative flex h-full flex-col overflow-hidden rounded-2xl border p-6 shadow-xs transition-[border-color,box-shadow] hover:shadow-lg">
          <div
            aria-hidden
            className="bg-primary/10 pointer-events-none absolute -top-20 -right-20 size-48 rounded-full opacity-0 blur-3xl transition-opacity duration-500 group-hover:opacity-100"
          />
          <span className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-xl">
            <Icon className="size-5" aria-hidden />
          </span>
          <h3 className="mt-4 text-lg font-semibold">{title}</h3>
          <p className="text-muted-foreground mt-2 text-sm leading-relaxed">{text}</p>
          {children && (
            <div aria-hidden className="mt-6 flex flex-1 flex-col justify-end">
              {children}
            </div>
          )}
        </article>
      </Reveal>
    </li>
  )
}

const Chip = ({ children, className }: { children: ReactNode; className?: string }) => (
  <span
    className={cn(
      'bg-muted inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium',
      className,
    )}
  >
    {children}
  </span>
)

function PersonalityVisual() {
  return (
    <div className="bg-muted/50 rounded-xl border p-4">
      <div className="flex items-center gap-3">
        <span className="from-primary to-brand-accent text-primary-foreground flex size-11 items-center justify-center rounded-full bg-gradient-to-br text-sm font-bold">
          S
        </span>
        <div>
          <p className="text-sm font-semibold">Sofía · Vendedora de Urban Step</p>
          <p className="text-muted-foreground text-xs">Activa en 3 canales</p>
        </div>
      </div>
      <div className="bg-primary/15 dark:bg-primary/20 mt-4 rounded-2xl rounded-br-md px-3 py-2 text-[13px] leading-snug">
        ¡Hola! 😊 Soy Sofía. Te cuento que las Urban Classic están con envío gratis en Lima esta semana. ¿Qué
        talla usas?
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        <Chip className="bg-primary/10 text-primary">Amigable</Chip>
        <Chip className="bg-brand-accent/10 text-brand-accent">Persuasiva</Chip>
        <Chip>Usa emojis 😊</Chip>
        <Chip>Respuestas breves</Chip>
      </div>
      <div className="mt-4 space-y-3 text-xs">
        {[
          { label: 'Descuento nivel 1', value: 5 },
          { label: 'Descuento nivel 2', value: 10 },
          { label: 'Tope máximo', value: 15 },
        ].map((d) => (
          <div key={d.label}>
            <div className="mb-1 flex justify-between">
              <span className="text-muted-foreground">{d.label}</span>
              <span className="font-semibold tabular-nums">{d.value} %</span>
            </div>
            <div className="bg-muted h-1.5 overflow-hidden rounded-full">
              <div
                className="from-primary to-brand-accent h-full rounded-full bg-gradient-to-r"
                style={{ width: `${(d.value / 15) * 100}%` }}
              />
            </div>
          </div>
        ))}
        <p className="text-muted-foreground pt-1">
          Margen mínimo protegido: <span className="text-foreground font-semibold">20 %</span>
        </p>
      </div>
      <div className="mt-4 border-t pt-4">
        <p className="text-muted-foreground mb-2 text-xs">Base de conocimiento</p>
        <div className="flex flex-wrap gap-2">
          <Chip>Envíos y plazos</Chip>
          <Chip>Cambios y devoluciones</Chip>
          <Chip>Garantía</Chip>
          <Chip>Horario de atención</Chip>
        </div>
      </div>
    </div>
  )
}

function InboxVisual() {
  const rows = [
    {
      name: 'Andrea',
      msg: '¿Hacen envíos a Surco?',
      icon: WhatsAppIcon,
      color: 'text-whatsapp',
      badge: 'IA',
    },
    {
      name: 'Luis',
      msg: 'Quiero la talla M en negro',
      icon: InstagramIcon,
      color: 'text-instagram',
      badge: 'IA',
    },
    {
      name: 'Carla',
      msg: 'Necesito factura con RUC',
      icon: MessengerIcon,
      color: 'text-messenger',
      badge: 'Tú',
    },
  ]
  return (
    <ul className="divide-y rounded-xl border">
      {rows.map((r) => (
        <li key={r.name} className="flex items-center gap-3 px-3 py-2.5">
          <span className="bg-muted relative flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
            {r.name[0]}
            <r.icon
              className={cn('bg-card absolute -right-1 -bottom-1 size-4 rounded-full p-0.5', r.color)}
            />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-xs font-semibold">{r.name}</span>
            <span className="text-muted-foreground block truncate text-xs">{r.msg}</span>
          </span>
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-[10px] font-semibold',
              r.badge === 'IA' ? 'bg-primary/10 text-primary' : 'bg-warning/20 text-foreground',
            )}
          >
            {r.badge}
          </span>
        </li>
      ))}
    </ul>
  )
}

function KanbanVisual() {
  const cols = [
    { title: 'Validación', dot: 'bg-brand-accent', cards: 2 },
    { title: 'Preparación', dot: 'bg-info', cards: 1 },
    { title: 'Enviado', dot: 'bg-primary', cards: 2 },
  ]
  return (
    <div className="grid grid-cols-3 gap-2">
      {cols.map((c) => (
        <div key={c.title} className="bg-muted/60 rounded-lg p-1.5">
          <p className="mb-1.5 flex min-w-0 items-center gap-1 text-[10px] leading-tight font-semibold">
            <span className={cn('size-1.5 shrink-0 rounded-full', c.dot)} /> {c.title}
          </p>
          <div className="space-y-1.5">
            {Array.from({ length: c.cards }, (_, i) => (
              <div key={i} className="bg-card space-y-1 rounded-md border p-1.5">
                <div className="bg-muted-foreground/30 h-1.5 w-3/4 rounded-full" />
                <div className="bg-muted-foreground/15 h-1.5 w-1/2 rounded-full" />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

function CatalogVisual() {
  return (
    <div className="space-y-2 text-xs">
      <div className="flex flex-wrap gap-1.5">
        {['38', '39', '40', '41', '42'].map((t, i) => (
          <span
            key={t}
            className={cn(
              'flex size-7 items-center justify-center rounded-md border font-medium',
              i === 2 && 'border-primary bg-primary/10 text-primary',
            )}
          >
            {t}
          </span>
        ))}
      </div>
      <div className="flex gap-1.5">
        <span className="bg-foreground size-5 rounded-full border" title="Negro" />
        <span className="bg-background size-5 rounded-full border-2" title="Blanco" />
        <span className="bg-brand-accent size-5 rounded-full border" title="Violeta" />
      </div>
    </div>
  )
}

function PaymentsVisual() {
  return (
    <div className="flex flex-wrap gap-1.5">
      <Chip>Transferencia</Chip>
      <Chip>Yape / Plin</Chip>
      <Chip>Contra entrega</Chip>
      <Chip className="bg-primary/10 text-primary">
        <MapPin className="size-3" /> Lima: S/ 10
      </Chip>
    </div>
  )
}

function RemindersVisual() {
  const segments = [
    { label: 'Frío', className: 'bg-info/15 text-info', window: '2–3 h' },
    { label: 'Tibio', className: 'bg-warning/20 text-foreground', window: '2–3 h · 22–23 h' },
    { label: 'Caliente', className: 'bg-destructive/15 text-destructive', window: '2–3 h · 22–23 h' },
  ]
  return (
    <ul className="space-y-1.5 text-xs">
      {segments.map((s) => (
        <li key={s.label} className="flex items-center justify-between gap-2">
          <span className={cn('rounded-full px-2 py-0.5 font-semibold', s.className)}>{s.label}</span>
          <span className="text-muted-foreground flex items-center gap-1">
            <Timer className="size-3" /> {s.window}
          </span>
        </li>
      ))}
    </ul>
  )
}

function MetricsVisual() {
  const bars = [35, 52, 44, 68, 60, 82, 95]
  return (
    <div>
      <div className="flex items-baseline justify-between">
        <p className="text-2xl font-semibold tabular-nums">S/ 12 480</p>
        <span className="bg-success/15 text-success rounded-full px-2 py-0.5 text-[10px] font-semibold">
          +18 %
        </span>
      </div>
      <p className="text-muted-foreground text-[11px]">Ventas de los últimos 7 días (ejemplo)</p>
      <div className="mt-3 flex h-16 items-end gap-1.5">
        {bars.map((h, i) => (
          <div
            key={i}
            className={cn('flex-1 rounded-t-sm', i === bars.length - 1 ? 'bg-primary' : 'bg-primary/25')}
            style={{ height: `${h}%` }}
          />
        ))}
      </div>
    </div>
  )
}

function HandoffVisual() {
  return (
    <div className="bg-warning/10 border-warning/40 rounded-xl border p-3 text-xs">
      <p className="flex items-center gap-1.5 font-semibold">
        <Headset className="size-3.5" /> Derivado a tu equipo
      </p>
      <p className="text-muted-foreground mt-1">
        “Quiere 20 unidades para su tienda”. El vendedor IA se pausó en este chat.
      </p>
    </div>
  )
}

export function FeaturesBento() {
  return (
    <Section id="funciones" labelledBy="features-title" className="bg-muted/30">
      <SectionHeading
        id="features-title"
        eyebrow="Funciones"
        title={
          <>
            Todo lo que necesitas para <GradientText>vender por chat</GradientText>
          </>
        }
        description="Un vendedor que conoce tu catálogo y tus reglas, y un panel para que tú tengas el control de cada conversación y cada pedido."
      />
      <ul className="grid auto-rows-auto gap-4 md:grid-cols-2 lg:grid-cols-4">
        <BentoCard
          icon={Bot}
          title="Vendedor IA con personalidad"
          text="Nombre, tono, estilo de venta, emojis y palabras a evitar. Conoce tus políticas y tu base de conocimiento, y negocia con descuentos por niveles sin bajar de tu margen."
          className="md:col-span-2 lg:row-span-2"
        >
          <PersonalityVisual />
        </BentoCard>
        <BentoCard
          icon={MessagesSquare}
          title="Bandeja unificada"
          text="WhatsApp, Instagram y Messenger en tiempo real, con etiquetas, filtros por canal y el historial de compras de cada cliente."
          className="md:col-span-2"
          delay={80}
        >
          <InboxVisual />
        </BentoCard>
        <BentoCard
          icon={SquareKanban}
          title="Pedidos en kanban"
          text="Cada venta del chat se convierte en pedido. Muévelo de “En validación” a “Entregado”."
          delay={120}
        >
          <KanbanVisual />
        </BentoCard>
        <BentoCard
          icon={Tags}
          title="Catálogo y variantes"
          text="Productos con fotos, categorías, tallas, colores, precios y stock por variante."
          delay={160}
        >
          <CatalogVisual />
        </BentoCard>
        <BentoCard
          icon={CreditCard}
          title="Pagos y entregas"
          text="Tus métodos de pago y zonas de entrega con su costo. El vendedor los comparte al cerrar."
          delay={80}
        >
          <PaymentsVisual />
        </BentoCard>
        <BentoCard
          icon={BellRing}
          title="Recordatorios por segmento"
          text="Seguimiento automático a clientes fríos, tibios y calientes, con mensajes que tú editas."
          delay={120}
        >
          <RemindersVisual />
        </BentoCard>
        <BentoCard
          icon={ChartColumn}
          title="Métricas"
          text="Ventas, conversiones, rendimiento por canal y productos más pedidos."
          delay={160}
        >
          <MetricsVisual />
        </BentoCard>
        <BentoCard
          icon={Headset}
          title="Derivación a humano"
          text="Define cuándo debe pasarte la conversación. Se pausa solo y tú sigues con todo el contexto."
          delay={200}
        >
          <HandoffVisual />
        </BentoCard>
      </ul>
    </Section>
  )
}
