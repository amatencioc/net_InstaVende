import { Outlet, useParams } from '@tanstack/react-router'
import { useInboxRealtime, useMediaQuery } from '../hooks'
import { ConversationListPanel } from './conversation-list'
import { InboxSidebar } from './inbox-sidebar'
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable'
import { useIsMobile } from '@/hooks/use-mobile'

/**
 * Bandeja de mensajes.
 * - Escritorio ancho: categorías | lista | chat (+ ficha del contacto dentro del chat).
 * - Tablet: lista | chat (categorías en la cabecera de la lista).
 * - Móvil: lista o chat a pantalla completa, con botón para volver.
 */
export function InboxLayout() {
  const { conversationId } = useParams({ strict: false })
  const isMobile = useIsMobile()
  const isWide = useMediaQuery('(min-width: 1280px)')
  useInboxRealtime(conversationId)

  return (
    // Altura disponible bajo la barra superior (h-16) para que cada panel tenga su propio scroll
    <div className="flex h-[calc(100svh-4rem)] min-h-0 overflow-hidden">
      {isMobile ? (
        <div className="min-w-0 flex-1">
          {conversationId ? <Outlet /> : <ConversationListPanel activeId={undefined} compact />}
        </div>
      ) : (
        <>
          {isWide && <InboxSidebar className="bg-muted/30 w-56 shrink-0 border-r" />}
          <ResizablePanelGroup id="inbox-panels" className="min-w-0 flex-1">
            <ResizablePanel id="inbox-list" defaultSize="34" minSize={280} maxSize="50">
              <ConversationListPanel activeId={conversationId} compact={!isWide} />
            </ResizablePanel>
            <ResizableHandle />
            <ResizablePanel id="inbox-chat" minSize={360}>
              <Outlet />
            </ResizablePanel>
          </ResizablePanelGroup>
        </>
      )}
    </div>
  )
}
