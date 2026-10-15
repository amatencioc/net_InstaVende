import { describe, expect, it } from 'vitest'
import { formatMoney, fromDateTimeLocalInput, initials, toDateTimeLocalInput } from '@/lib/format'

describe('format', () => {
  it('formatea importes con el símbolo del negocio', () => {
    expect(formatMoney(1250.5, 'S/')).toBe('S/ 1,250.50')
    expect(formatMoney(null)).toBe('S/ 0.00')
  })

  it('calcula iniciales', () => {
    expect(initials('María José Pérez')).toBe('MP')
    expect(initials('ana')).toBe('A')
    expect(initials('')).toBe('?')
  })

  it('datetime-local ida y vuelta conserva el instante', () => {
    const iso = '2026-10-14T15:30:00.000Z'
    expect(fromDateTimeLocalInput(toDateTimeLocalInput(iso))).toBe(iso)
  })
})
