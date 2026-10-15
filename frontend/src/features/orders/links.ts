/** Enlace a la conversación del pedido en la bandeja de mensajes. */
export function conversationHref(conversationId: number): string {
  return `/inbox/${conversationId}`
}
