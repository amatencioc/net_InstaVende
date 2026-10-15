import { Link } from '@tanstack/react-router'
import { Check } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { plans, YEARLY_PAID_MONTHS, type BillingCycle, type Plan } from '../data'
import { Reveal, Section, SectionHeading } from './primitives'

const soles = (n: number) => `S/ ${n.toLocaleString('es-PE')}`

function BillingToggle({ value, onChange }: { value: BillingCycle; onChange: (v: BillingCycle) => void }) {
  const options: { id: BillingCycle; label: string }[] = [
    { id: 'monthly', label: 'Mensual' },
    { id: 'yearly', label: 'Anual' },
  ]
  return (
    <div
      role="group"
      aria-label="Ciclo de facturación"
      className="bg-muted inline-flex items-center rounded-full border p-1"
    >
      {options.map((o) => (
        <button
          key={o.id}
          type="button"
          aria-pressed={value === o.id}
          onClick={() => onChange(o.id)}
          className={cn(
            'focus-visible:ring-ring/50 inline-flex h-9 items-center gap-2 rounded-full px-4 text-sm font-medium transition-colors outline-none focus-visible:ring-[3px]',
            value === o.id
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {o.label}
          {o.id === 'yearly' && (
            <span className="bg-primary/15 text-primary rounded-full px-2 py-0.5 text-[11px] font-semibold">
              2 meses gratis
            </span>
          )}
        </button>
      ))}
    </div>
  )
}

function PlanCard({ plan, cycle }: { plan: Plan; cycle: BillingCycle }) {
  const yearlyTotal = plan.monthlyPrice * YEARLY_PAID_MONTHS
  const perMonth = cycle === 'yearly' ? Math.round(yearlyTotal / 12) : plan.monthlyPrice
  const isFree = plan.monthlyPrice === 0

  return (
    <article
      aria-labelledby={`plan-${plan.id}`}
      className={cn(
        'relative flex h-full flex-col rounded-2xl p-7',
        plan.highlighted ? 'lp-gradient-border shadow-primary/10 shadow-xl' : 'bg-card border shadow-xs',
      )}
    >
      {plan.highlighted && (
        <span className="from-primary to-brand-accent text-primary-foreground absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-gradient-to-r px-3 py-1 text-xs font-semibold shadow-md">
          Más elegido
        </span>
      )}
      <h3 id={`plan-${plan.id}`} className="text-xl font-semibold">
        {plan.name}
      </h3>
      <p className="text-muted-foreground mt-2 min-h-10 text-sm">{plan.description}</p>

      <p className="mt-6 flex items-baseline gap-1">
        <span
          key={`${plan.id}-${cycle}`}
          className="font-display motion-safe:animate-in motion-safe:fade-in text-4xl font-bold tracking-tight tabular-nums"
        >
          {soles(perMonth)}
        </span>
        <span className="text-muted-foreground text-sm">/mes</span>
      </p>
      <p className="text-muted-foreground mt-1 h-5 text-xs">
        {isFree
          ? 'Gratis para siempre'
          : cycle === 'yearly'
            ? `${soles(yearlyTotal)} facturados al año`
            : 'Facturado mensualmente'}
      </p>

      <Button asChild size="lg" variant={plan.highlighted ? 'default' : 'outline'} className="mt-6 w-full">
        <Link to="/register">{plan.cta}</Link>
      </Button>

      <ul className="mt-7 space-y-3 text-sm">
        {plan.features.map((f) => (
          <li key={f} className="flex gap-2.5">
            <Check className="text-primary mt-0.5 size-4 shrink-0" aria-hidden />
            <span>{f}</span>
          </li>
        ))}
      </ul>
    </article>
  )
}

export function Pricing() {
  const [cycle, setCycle] = useState<BillingCycle>('monthly')
  return (
    <Section id="precios" labelledBy="pricing-title">
      <SectionHeading
        id="pricing-title"
        eyebrow="Planes y precios"
        title="Empieza gratis y crece a tu ritmo"
        description="Sin contratos ni permanencia. Cambia de plan o cancela cuando quieras."
        className="mb-8 md:mb-10"
      />
      <div className="mb-12 flex justify-center">
        <BillingToggle value={cycle} onChange={setCycle} />
      </div>
      <ul className="grid items-stretch gap-6 lg:grid-cols-3">
        {plans.map((plan, i) => (
          <li key={plan.id}>
            <Reveal delay={i * 100} className="h-full">
              <PlanCard plan={plan} cycle={cycle} />
            </Reveal>
          </li>
        ))}
      </ul>
      <p className="text-muted-foreground mt-10 text-center text-xs">
        Precios en soles peruanos (S/), no incluyen IGV. Los límites de conversaciones se renuevan cada mes.
      </p>
    </Section>
  )
}
