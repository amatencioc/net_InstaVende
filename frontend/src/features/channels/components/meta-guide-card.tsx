import { BookOpen, ExternalLink } from 'lucide-react'
import type { ChannelType } from '../api'
import { metaChannelInfo } from '../channel-info'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/** Guía paso a paso para obtener las credenciales de Meta del canal. */
export function MetaGuideCard({ channel }: { channel: ChannelType }) {
  const info = metaChannelInfo[channel]
  return (
    <Card className="lg:sticky lg:top-20">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <BookOpen className="text-primary size-5" aria-hidden /> Guía paso a paso
        </CardTitle>
        <CardDescription>Sigue estos pasos en Meta. Te tomará unos 15 minutos.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <ol className="space-y-4">
          {info.guide.map((step, i) => (
            <li key={step.title} className="flex gap-3">
              <span
                className="bg-primary/10 text-primary flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
                aria-hidden
              >
                {i + 1}
              </span>
              <div className="min-w-0 space-y-1 text-sm">
                <p className="font-medium">{step.title}</p>
                <p className="text-muted-foreground">{step.description}</p>
                {step.link && <GuideLink href={step.link.href}>{step.link.label}</GuideLink>}
              </div>
            </li>
          ))}
        </ol>
        <div className="border-t pt-4 text-sm">
          <GuideLink href={info.docsUrl}>Documentación oficial de Meta</GuideLink>
        </div>
      </CardContent>
    </Card>
  )
}

function GuideLink({ href, children }: { href: string; children: string }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className="text-primary inline-flex items-center gap-1 text-sm font-medium hover:underline"
    >
      {children}
      <ExternalLink className="size-3" aria-hidden />
      <span className="sr-only">(se abre en una pestaña nueva)</span>
    </a>
  )
}
