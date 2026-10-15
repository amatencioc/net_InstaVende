import { CircleCheck, CircleX, Quote } from 'lucide-react'
import { comparison, stats, testimonials } from '../data'
import { GradientText, Reveal, Section, SectionHeading } from './primitives'

export function Benefits() {
  return (
    <Section id="beneficios" labelledBy="benefits-title">
      <SectionHeading
        id="benefits-title"
        eyebrow="Beneficios"
        title={
          <>
            Vende más <GradientText>sin vivir pegado al celular</GradientText>
          </>
        }
        description="Tu vendedor IA se encarga de lo repetitivo para que tú te enfoques en comprar, despachar y hacer crecer la marca."
      />

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((s, i) => (
          <li key={s.label}>
            <Reveal delay={i * 80} className="h-full">
              <div className="bg-card h-full rounded-2xl border p-6 shadow-xs">
                <p className="from-primary to-brand-accent font-display bg-gradient-to-r bg-clip-text text-5xl font-bold tracking-tight text-transparent">
                  {s.value}
                </p>
                <p className="mt-3 text-sm font-semibold">{s.label}</p>
                <p className="text-muted-foreground mt-1 text-sm">{s.text}</p>
              </div>
            </Reveal>
          </li>
        ))}
      </ul>

      {/* Comparativa */}
      <div className="mt-20">
        <Reveal className="mx-auto mb-10 max-w-2xl text-center">
          <h3 className="text-2xl font-semibold sm:text-3xl">Así cambia tu día a día</h3>
        </Reveal>
        <div className="grid gap-4 md:grid-cols-2">
          <Reveal>
            <div className="bg-muted/40 h-full rounded-2xl border p-6 sm:p-8">
              <h4 className="text-muted-foreground flex items-center gap-2 text-lg font-semibold">
                <CircleX className="text-destructive size-5" aria-hidden /> Sin InstaVende
              </h4>
              <ul className="mt-6 space-y-4">
                {comparison.without.map((item) => (
                  <li key={item} className="text-muted-foreground flex gap-3 text-sm">
                    <CircleX className="text-destructive/70 mt-0.5 size-4 shrink-0" aria-hidden />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          </Reveal>
          <Reveal delay={120}>
            <div className="lp-gradient-border relative h-full overflow-hidden rounded-2xl p-6 shadow-lg sm:p-8">
              <div
                aria-hidden
                className="bg-primary/10 absolute -top-24 -right-24 size-56 rounded-full blur-3xl"
              />
              <h4 className="relative flex items-center gap-2 text-lg font-semibold">
                <CircleCheck className="text-primary size-5" aria-hidden /> Con InstaVende
              </h4>
              <ul className="relative mt-6 space-y-4">
                {comparison.with.map((item) => (
                  <li key={item} className="flex gap-3 text-sm">
                    <CircleCheck className="text-primary mt-0.5 size-4 shrink-0" aria-hidden />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          </Reveal>
        </div>
      </div>
    </Section>
  )
}

export function Testimonials() {
  return (
    <Section labelledBy="testimonials-title" className="bg-muted/30">
      <SectionHeading
        id="testimonials-title"
        eyebrow="Casos de uso"
        title="Hecho para quienes venden por chat todos los días"
        description="Situaciones típicas de un negocio que vende por WhatsApp e Instagram, y cómo las resuelve InstaVende."
      />
      <ul className="grid gap-4 md:grid-cols-3">
        {testimonials.map((t, i) => (
          <li key={t.role}>
            <Reveal delay={i * 100} className="h-full">
              <figure className="bg-card flex h-full flex-col rounded-2xl border p-6 shadow-xs">
                <Quote className="text-primary/40 size-8" aria-hidden />
                <blockquote className="mt-4 flex-1 text-[15px] leading-relaxed">“{t.quote}”</blockquote>
                <figcaption className="mt-6 flex items-center gap-3 border-t pt-5">
                  <span
                    aria-hidden
                    className="from-primary/20 to-brand-accent/20 text-foreground flex size-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br text-sm font-semibold"
                  >
                    {t.initials}
                  </span>
                  <span className="text-sm">
                    <span className="block font-semibold">{t.role}</span>
                    <span className="text-muted-foreground">{t.place}</span>
                  </span>
                </figcaption>
              </figure>
            </Reveal>
          </li>
        ))}
      </ul>
      <p className="text-muted-foreground mt-6 text-center text-xs">
        Testimonios ilustrativos: representan casos de uso típicos, no clientes identificables.
      </p>
    </Section>
  )
}
