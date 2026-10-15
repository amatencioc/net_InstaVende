/** Hora actual "HH:mm" para las burbujas de chat. */
export function nowTime(): string {
  return new Date().toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' })
}
