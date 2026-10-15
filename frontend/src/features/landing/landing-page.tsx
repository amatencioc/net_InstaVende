import { Benefits, Testimonials } from './components/benefits'
import { Faq, FinalCta, SiteFooter } from './components/closing'
import { FeaturesBento } from './components/features-bento'
import { ChannelsStrip, Hero } from './components/hero'
import { HowItWorks } from './components/how-it-works'
import { Pricing } from './components/pricing'
import { SiteHeader } from './components/site-header'
import './landing.css'

/** Landing pública de InstaVende. */
export function LandingPage() {
  return (
    <div className="bg-background text-foreground min-h-svh overflow-x-clip">
      <a
        href="#contenido"
        onClick={(e) => {
          e.preventDefault()
          document.getElementById('contenido')?.focus()
        }}
        className="bg-primary text-primary-foreground sr-only z-50 rounded-md px-4 py-2 text-sm font-medium focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Saltar al contenido
      </a>
      <SiteHeader />
      <main id="contenido" tabIndex={-1} className="outline-none">
        <Hero />
        <ChannelsStrip />
        <HowItWorks />
        <FeaturesBento />
        <Benefits />
        <Testimonials />
        <Pricing />
        <Faq />
        <FinalCta />
      </main>
      <SiteFooter />
    </div>
  )
}
