import { createFileRoute } from '@tanstack/react-router'
import { LandingPage } from '@/features/landing/landing-page'

const title = 'InstaVende — Vendedor con IA para WhatsApp, Instagram y Messenger'
const description =
  'Tu vendedor con IA que atiende, negocia y cierra ventas 24/7 por WhatsApp, Instagram y Messenger. Catálogo, pedidos, pagos, recordatorios y métricas en un solo lugar. Empieza gratis.'

export const Route = createFileRoute('/')({
  head: () => ({
    meta: [
      { title },
      { name: 'description', content: description },
      { property: 'og:type', content: 'website' },
      { property: 'og:title', content: title },
      { property: 'og:description', content: description },
      { property: 'og:locale', content: 'es_PE' },
      { name: 'twitter:card', content: 'summary' },
    ],
  }),
  component: LandingPage,
})
