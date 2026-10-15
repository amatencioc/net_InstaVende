import { Link } from '@tanstack/react-router'
import { ArrowRight, Check, Sparkles } from 'lucide-react'
import { InstagramIcon, MessengerIcon, WhatsAppIcon } from '@/components/shared/brand-icons'
import { Button } from '@/components/ui/button'
import { ChatDemo } from './chat-demo'
import { AnchorLink, GradientText } from './primitives'

const verticals = [
  { initials: 'M', label: 'Moda', className: 'bg-primary/15 text-primary' },
  { initials: 'B', label: 'Belleza', className: 'bg-brand-accent/15 text-brand-accent' },
  { initials: 'T', label: 'Tecnología', className: 'bg-info/15 text-info' },
  { initials: 'H', label: 'Hogar', className: 'bg-warning/20 text-foreground' },
]

const assurances = ['Sin tarjeta de crédito', 'Listo en minutos', 'Cancela cuando quieras']

export function Hero() {
  return (
    <section
      id="inicio"
      tabIndex={-1}
      aria-labelledby="hero-title"
      className="relative isolate overflow-hidden px-4 pt-12 pb-20 outline-none sm:px-6 md:pt-20 lg:pb-28"
    >
      {/* Fondo decorativo: cuadrícula + halos de marca */}
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="lp-grid absolute inset-0" />
        <div className="bg-primary/20 absolute -top-40 left-1/2 h-[36rem] w-[36rem] -translate-x-[70%] rounded-full blur-3xl" />
        <div className="bg-brand-accent/15 absolute -top-24 left-1/2 h-[30rem] w-[30rem] translate-x-[5%] rounded-full blur-3xl" />
      </div>

      <div className="mx-auto grid max-w-6xl items-center gap-14 lg:grid-cols-[1.05fr_1fr] lg:gap-10">
        <div className="text-center lg:text-left">
          <p className="bg-card/70 text-muted-foreground mx-auto inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-medium shadow-sm backdrop-blur lg:mx-0">
            <Sparkles className="text-brand-accent size-3.5" aria-hidden />
            Vendedor con IA para WhatsApp, Instagram y Messenger
          </p>

          <h1
            id="hero-title"
            className="mt-6 text-[2.5rem] leading-[1.05] font-bold text-balance sm:text-5xl lg:text-6xl xl:text-[4rem]"
          >
            Tu vendedor con IA que <GradientText>atiende, negocia y cierra</GradientText> ventas 24/7
          </h1>

          <p className="text-muted-foreground mx-auto mt-6 max-w-xl text-lg text-pretty lg:mx-0">
            InstaVende responde al instante a tus clientes, recomienda productos de tu catálogo, negocia
            dentro de tus reglas y registra cada pedido. Tú solo despachas.
          </p>

          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row lg:justify-start">
            <Button asChild size="lg" className="shadow-primary/30 h-12 px-7 text-base shadow-lg">
              <Link to="/register">
                Empieza gratis <ArrowRight aria-hidden />
              </Link>
            </Button>
            <Button asChild size="lg" variant="outline" className="h-12 px-7 text-base">
              <AnchorLink to="como-funciona">Ver cómo funciona</AnchorLink>
            </Button>
          </div>

          <ul className="text-muted-foreground mt-5 flex flex-wrap justify-center gap-x-5 gap-y-2 text-sm lg:justify-start">
            {assurances.map((a) => (
              <li key={a} className="flex items-center gap-1.5">
                <Check className="text-primary size-4" aria-hidden /> {a}
              </li>
            ))}
          </ul>

          {/* Prueba social: rubros para los que está pensado y canales soportados */}
          <div className="mt-10 flex flex-col items-center gap-4 sm:flex-row sm:justify-center lg:justify-start">
            <div className="flex -space-x-2" aria-hidden>
              {verticals.map((v) => (
                <span
                  key={v.label}
                  title={v.label}
                  className={`ring-background flex size-9 items-center justify-center rounded-full text-xs font-bold ring-2 ${v.className}`}
                >
                  {v.initials}
                </span>
              ))}
            </div>
            <p className="text-muted-foreground max-w-xs text-sm text-pretty">
              Pensado para emprendedores de{' '}
              <strong className="text-foreground font-semibold">moda, belleza, tecnología y hogar</strong> que
              venden por chat en Latinoamérica.
            </p>
          </div>
        </div>

        <div className="relative sm:px-12 lg:px-0">
          <ChatDemo />
        </div>
      </div>
    </section>
  )
}

const channels = [
  {
    name: 'WhatsApp',
    detail: 'Código QR o API oficial',
    icon: WhatsAppIcon,
    className: 'text-whatsapp bg-whatsapp/10',
  },
  {
    name: 'Instagram',
    detail: 'Mensajes directos',
    icon: InstagramIcon,
    className: 'text-instagram bg-instagram/10',
  },
  {
    name: 'Messenger',
    detail: 'Tu página de Facebook',
    icon: MessengerIcon,
    className: 'text-messenger bg-messenger/10',
  },
]

export function ChannelsStrip() {
  return (
    <section aria-labelledby="channels-title" className="bg-muted/40 border-y px-4 py-10 sm:px-6">
      <div className="mx-auto flex max-w-6xl flex-col items-center gap-6 md:flex-row md:justify-between">
        <h2
          id="channels-title"
          className="text-muted-foreground text-center font-sans text-sm font-medium tracking-normal md:text-left"
        >
          Atiende y vende en los canales donde ya te escriben
        </h2>
        <ul className="grid w-full grid-cols-1 gap-3 sm:grid-cols-3 md:w-auto">
          {channels.map((c) => (
            <li
              key={c.name}
              className="bg-card flex items-center gap-3 rounded-xl border px-4 py-3 shadow-xs"
            >
              <span className={`flex size-10 items-center justify-center rounded-lg ${c.className}`}>
                <c.icon className="size-5" />
              </span>
              <span className="leading-tight">
                <span className="block text-sm font-semibold">{c.name}</span>
                <span className="text-muted-foreground block text-xs">{c.detail}</span>
              </span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
