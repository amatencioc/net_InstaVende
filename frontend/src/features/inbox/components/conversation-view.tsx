import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { MessageSquareOff, MessagesSquare, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import {
  conversationDetailQuery,
  useChangeStatus,
  useMarkRead,
  useRetryMessage,
  useSendMessage,
  type InboxConversationDetail,
} from '../api'
import { inboxRouteApi } from '../filters'
import { useMediaQuery, useStoredFlag } from '../hooks'
import { ChatHeader, ChatHeaderSkeleton } from './chat-header'
import { Composer } from './composer'
import { ContactPanel, ContactPanelSkeleton } from './contact-panel'
import { MessageList } from './message-list'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { useIsMobile } from '@/hooks/use-mobile'
import { ApiError } from '@/lib/api/client'

function newClientId() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

/** Chat abierto: cabecera con acciones, historial, caja de respuesta y ficha del contacto. */
export function ConversationView({ conversationId }: { conversationId: number }) {
  const isMobile = useIsMobile()
  const isWide = useMediaQuery('(min-width: 1280px)')
  const detail = useQuery(conversationDetailQuery(conversationId))
  const [panelPref, setPanelPref] = useStoredFlag('inbox.contact-panel', true)
  const [sheetOpen, setSheetOpen] = useState(false)

  const { mutate: markRead } = useMarkRead()
  const send = useSendMessage(conversationId)
  const retry = useRetryMessage(conversationId)
  const changeStatus = useChangeStatus(conversationId)

  // Al abrir el chat (y al volver a la pestaña) los mensajes entrantes quedan leídos
  useEffect(() => {
    markRead(conversationId)
    const onVisible = () => {
      if (document.visibilityState === 'visible') markRead(conversationId)
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [conversationId, markRead])

  const panelOpen = isWide ? panelPref : sheetOpen
  const toggleContact = () => (isWide ? setPanelPref(!panelPref) : setSheetOpen((v) => !v))

  if (detail.isError && detail.error instanceof ApiError && detail.error.status === 404) {
    return <ConversationNotFound />
  }

  const chat = (
    <div className="flex h-full min-h-0 flex-col">
      {detail.isPending ? (
        <ChatHeaderSkeleton showBack={isMobile} />
      ) : detail.isError ? (
        <div className="border-b p-3">
          <QueryError error={detail.error} onRetry={() => void detail.refetch()} />
        </div>
      ) : (
        <ChatHeader
          detail={detail.data}
          showBack={isMobile}
          contactOpen={panelOpen}
          onToggleContact={toggleContact}
        />
      )}
      <MessageList conversationId={conversationId} onRetry={(message) => retry.mutate(message)} />
      {detail.data ? (
        <Composer
          conversationId={conversationId}
          status={detail.data.status}
          autoFocus={!isMobile}
          onSend={(text) => send.mutate({ text, clientId: newClientId() })}
          onReopen={() => changeStatus.mutate('BotActive')}
          reopening={changeStatus.isPending}
        />
      ) : (
        <div className="border-t p-3">
          <Skeleton className="h-11 w-full rounded-xl" />
        </div>
      )}
    </div>
  )

  if (isWide && panelPref) {
    return (
      <ResizablePanelGroup id="inbox-chat-panels">
        <ResizablePanel id="inbox-chat-main" minSize={360}>
          {chat}
        </ResizablePanel>
        <ResizableHandle />
        <ResizablePanel id="inbox-contact" defaultSize={320} minSize={260} maxSize={440}>
          <aside aria-label="Ficha del contacto" className="bg-card flex h-full min-h-0 flex-col">
            <div className="flex h-16 shrink-0 items-center justify-between border-b px-4">
              <h2 className="text-sm font-semibold">Contacto</h2>
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Cerrar ficha del contacto"
                onClick={toggleContact}
              >
                <X />
              </Button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto">
              <ContactBody detail={detail.data} pending={detail.isPending} />
            </div>
          </aside>
        </ResizablePanel>
      </ResizablePanelGroup>
    )
  }

  return (
    <>
      {chat}
      {!isWide && (
        <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
          <SheetContent side="right" className="w-full gap-0 overflow-y-auto p-0 sm:max-w-sm">
            <SheetHeader className="border-b">
              <SheetTitle>Contacto</SheetTitle>
              <SheetDescription className="sr-only">
                Datos del cliente y sus últimos pedidos.
              </SheetDescription>
            </SheetHeader>
            <ContactBody detail={detail.data} pending={detail.isPending} />
          </SheetContent>
        </Sheet>
      )}
    </>
  )
}

function ContactBody({ detail, pending }: { detail: InboxConversationDetail | undefined; pending: boolean }) {
  if (pending || !detail) return <ContactPanelSkeleton />
  return <ContactPanel detail={detail} />
}

function ConversationNotFound() {
  const search = inboxRouteApi.useSearch()
  return (
    <div className="flex h-full items-center justify-center p-6">
      <EmptyState
        className="border-0"
        icon={MessageSquareOff}
        title="Conversación no encontrada"
        description="Puede que se haya eliminado o que el enlace no sea correcto."
        action={
          <Button asChild variant="outline" size="sm">
            <Link to="/inbox" search={search}>
              Volver a la bandeja
            </Link>
          </Button>
        }
      />
    </div>
  )
}

/** Estado vacío del panel de chat cuando no hay conversación abierta. */
export function NoConversationSelected() {
  return (
    <div className="bg-muted/30 flex h-full flex-col items-center justify-center gap-4 p-8 text-center">
      <div className="relative">
        <div className="bg-primary/10 absolute inset-0 animate-ping rounded-full opacity-40 [animation-duration:3s] motion-reduce:hidden" />
        <div className="bg-primary/10 text-primary relative flex size-16 items-center justify-center rounded-full">
          <MessagesSquare className="size-7" aria-hidden />
        </div>
      </div>
      <div className="max-w-sm space-y-1.5">
        <h2 className="text-lg font-semibold">Selecciona una conversación</h2>
        <p className="text-muted-foreground text-sm">
          Elige un chat de la lista para leer los mensajes, responder a tus clientes o tomar el control del
          vendedor IA.
        </p>
      </div>
      <p className="text-muted-foreground/80 text-xs">
        Las conversaciones nuevas aparecen aquí en tiempo real.
      </p>
    </div>
  )
}
