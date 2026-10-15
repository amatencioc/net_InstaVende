/** Nombre visible de un contacto: nombre, teléfono o identificador. */
export function contactLabel(contact: { name?: string | null; phone?: string | null; id?: number }): string {
  return (
    contact.name?.trim() || contact.phone?.trim() || (contact.id ? `Contacto #${contact.id}` : 'Contacto')
  )
}
