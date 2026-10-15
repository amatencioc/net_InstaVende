import { steps } from '../data'
import { Reveal, Section, SectionHeading } from './primitives'

export function HowItWorks() {
  return (
    <Section id="como-funciona" labelledBy="how-title">
      <SectionHeading
        id="how-title"
        eyebrow="Cómo funciona"
        title="De cero a vender en automático en tres pasos"
        description="Sin programar, sin flujos rígidos de botones. Configuras a tu vendedor como le explicarías el negocio a una persona."
      />
      <div className="relative">
        {/* Línea que conecta los pasos en escritorio */}
        <div
          aria-hidden
          className="via-primary/30 absolute top-[3.75rem] right-[16%] left-[16%] hidden h-px bg-gradient-to-r from-transparent to-transparent md:block"
        />
        <ol className="relative grid gap-6 md:grid-cols-3">
          {steps.map((step, i) => (
            <li key={step.title}>
              <Reveal delay={i * 120} className="relative h-full">
                <div className="bg-card hover:border-primary/40 flex h-full flex-col items-center rounded-2xl border p-7 text-center shadow-xs transition-colors">
                  <span className="relative">
                    <span className="bg-primary/10 text-primary ring-card relative z-10 flex size-16 items-center justify-center rounded-2xl ring-8">
                      <step.icon className="size-7" aria-hidden />
                    </span>
                    <span className="bg-brand-accent text-primary-foreground absolute -top-2 -right-2 z-20 flex size-6 items-center justify-center rounded-full text-xs font-bold">
                      {i + 1}
                    </span>
                  </span>
                  <h3 className="mt-6 text-xl font-semibold">{step.title}</h3>
                  <p className="text-muted-foreground mt-3 text-sm leading-relaxed">{step.text}</p>
                  <p className="text-primary mt-auto pt-5 text-xs font-semibold">{step.detail}</p>
                </div>
              </Reveal>
            </li>
          ))}
        </ol>
      </div>
    </Section>
  )
}
