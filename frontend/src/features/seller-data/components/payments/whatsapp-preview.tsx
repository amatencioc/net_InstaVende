import { Bot, CheckCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import type { PaymentMethodType } from '../../api'
import { paymentFieldLabels } from '../../meta'

interface PreviewData {
  type: PaymentMethodType
  name: string
  instructions: string
  accountNumber: string
  accountAlias: string
  paymentLink: string
}

/** Texto del mensaje tal como lo enviaría el vendedor (formato de WhatsApp: *negrita*, _cursiva_). */
function buildMessage(data: PreviewData): string {
  const labels = paymentFieldLabels(data.type)
  const lines = [`Puedes pagar con *${data.name.trim() || 'tu método de pago'}* 👇`]
  if (data.instructions.trim()) lines.push('', data.instructions.trim())
  const details: string[] = []
  if (data.accountNumber.trim()) details.push(`${labels.account}: *${data.accountNumber.trim()}*`)
  if (data.accountAlias.trim()) details.push(`${labels.alias}: ${data.accountAlias.trim()}`)
  if (data.paymentLink.trim()) details.push(`🔗 ${data.paymentLink.trim()}`)
  if (details.length) lines.push('', ...details)
  return lines.join('\n')
}

/** Aplica *negrita*, _cursiva_ y ~tachado~ como en WhatsApp sin usar HTML crudo. */
function renderWhatsAppText(text: string): ReactNode[] {
  const pattern = /(\*[^*\n]+\*|_[^_\n]+_|~[^~\n]+~)/g
  return text.split(pattern).map((part, i) => {
    if (part.length > 2 && part.startsWith('*') && part.endsWith('*')) return <strong key={i}>{part.slice(1, -1)}</strong>
    if (part.length > 2 && part.startsWith('_') && part.endsWith('_')) return <em key={i}>{part.slice(1, -1)}</em>
    if (part.length > 2 && part.startsWith('~') && part.endsWith('~')) return <s key={i}>{part.slice(1, -1)}</s>
    return part
  })
}

/** Vista previa en vivo de cómo verá el cliente las instrucciones de pago en el chat. */
export function WhatsAppPreview({ data, businessName }: { data: PreviewData; businessName: string }) {
  const message = buildMessage(data)
  return (
    <figure className="flex h-full flex-col overflow-hidden rounded-xl border" aria-label="Vista previa del mensaje">
      <div className="bg-whatsapp/90 flex items-center gap-2 px-3 py-2 text-white">
        <div className="flex size-8 items-center justify-center rounded-full bg-white/20">
          <Bot className="size-4" aria-hidden />
        </div>
        <div className="min-w-0 leading-tight">
          <p className="truncate text-sm font-semibold">{businessName}</p>
          <p className="text-xs text-white/80">en línea</p>
        </div>
      </div>
      <div className="bg-muted/60 flex flex-1 flex-col gap-2 p-3 text-sm">
        <div className="bg-whatsapp/20 ml-auto max-w-[85%] rounded-lg rounded-tr-none px-3 py-1.5 shadow-xs">
          ¿Cómo puedo pagar?
          <span className="text-muted-foreground ml-2 inline-flex items-center gap-0.5 align-bottom text-[10px]">
            10:24 <CheckCheck className="text-info size-3" aria-hidden />
          </span>
        </div>
        <div className="bg-card max-w-[85%] rounded-lg rounded-tl-none px-3 py-1.5 break-words whitespace-pre-wrap shadow-xs">
          {renderWhatsAppText(message)}
          <span className="text-muted-foreground ml-2 align-bottom text-[10px]">10:24</span>
        </div>
      </div>
      <figcaption className="text-muted-foreground border-t px-3 py-2 text-xs">
        Así verá el cliente estos datos cuando pregunte cómo pagar.
      </figcaption>
    </figure>
  )
}
