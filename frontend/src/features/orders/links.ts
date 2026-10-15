/**
 * Enlace a la conversación en la bandeja de mensajes.
 * Se usa un href simple (no un Link tipado) porque la ruta /inbox pertenece a otro módulo
 * y aún no declara sus parámetros de búsqueda.
 */
export function conversationHref(conversationId: number): string {
  return `/inbox?conversationId=${conversationId}`
}
