import { KeyRound, Loader2, RefreshCw, Webhook } from 'lucide-react'
import { useRegenerateVerifyToken, type ChannelSummary } from '../api'
import { isPublicHttpsUrl, metaChannelInfo, metaLinks } from '../channel-info'
import { CopyField } from './copy-field'
import { Alert } from '@/components/shared/alert'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/** Datos del webhook que el comercio debe pegar en Meta for Developers. */
export function MetaWebhookCard({ summary, isAdmin }: { summary: ChannelSummary; isAdmin: boolean }) {
  const info = metaChannelInfo[summary.channel]
  const regenerate = useRegenerateVerifyToken(summary.channel)
  const confirm = useConfirm()
  const publicUrl = isPublicHttpsUrl(summary.webhookUrl)
  const sharedUrl = summary.channel !== 'WhatsApp'

  const onRegenerate = async () => {
    if (summary.webhookVerifyToken) {
      const ok = await confirm({
        title: '¿Generar un token de verificación nuevo?',
        description:
          'Meta solo lo usa al verificar el webhook: los mensajes seguirán llegando, pero si vuelves a verificarlo en Meta tendrás que pegar el token nuevo.',
        confirmLabel: 'Generar nuevo',
      })
      if (!ok) return
    }
    regenerate.mutate()
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <Webhook className="text-primary size-5" aria-hidden /> Webhook
        </CardTitle>
        <CardDescription>
          Pega estos datos en la configuración de webhooks de tu app en Meta for Developers para que los
          mensajes lleguen a tu vendedor.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <CopyField
          label="URL de devolución de llamada (Callback URL)"
          value={summary.webhookUrl}
          copyLabel="Copiar URL del webhook"
          description={
            sharedUrl
              ? 'Messenger e Instagram usan la misma URL; cada uno tiene su propio token de verificación.'
              : undefined
          }
        />
        {!publicUrl && (
          <Alert tone="warning" title="Esta dirección no es pública">
            Meta solo envía mensajes a direcciones públicas con HTTPS. Funcionará cuando InstaVende esté
            publicado con tu dominio seguro.
          </Alert>
        )}

        {summary.webhookVerifyToken ? (
          <CopyField
            label="Token de verificación (Verify token)"
            value={summary.webhookVerifyToken}
            copyLabel="Copiar token de verificación"
            description="Generado de forma segura por InstaVende. No lo compartas fuera de Meta."
            action={
              isAdmin && (
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  onClick={() => void onRegenerate()}
                  disabled={regenerate.isPending}
                  aria-label="Generar un token de verificación nuevo"
                  title="Generar un token de verificación nuevo"
                >
                  {regenerate.isPending ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                </Button>
              )
            }
          />
        ) : isAdmin ? (
          <div className="flex flex-col gap-3 rounded-lg border border-dashed p-4 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-muted-foreground text-sm">
              Aún no hay token de verificación. Se genera al guardar la configuración o puedes crearlo ahora.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void onRegenerate()}
              disabled={regenerate.isPending}
            >
              {regenerate.isPending ? <Loader2 className="animate-spin" /> : <KeyRound />}
              Generar token
            </Button>
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">
            El token de verificación solo es visible para administradores.
          </p>
        )}

        <div className="text-sm">
          <p className="font-medium">Campos a suscribir</p>
          <p className="text-muted-foreground mt-1">
            {info.webhookFields.split(', ').map((f) => (
              <code key={f} className="bg-muted mr-1.5 rounded px-1.5 py-0.5 font-mono text-xs">
                {f}
              </code>
            ))}
            <a
              href={metaLinks.webhooksDocs}
              target="_blank"
              rel="noopener noreferrer"
              className="text-primary ml-1 text-xs font-medium hover:underline"
            >
              ¿Cómo configurar webhooks?<span className="sr-only"> (se abre en una pestaña nueva)</span>
            </a>
          </p>
        </div>
      </CardContent>
    </Card>
  )
}
