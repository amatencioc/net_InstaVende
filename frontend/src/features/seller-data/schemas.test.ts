import {
  deliveryZoneDefaults,
  deliveryZoneFormSchema,
  paymentMethodDefaults,
  paymentMethodFormSchema,
  toDeliveryZoneRequest,
  toPaymentMethodRequest,
} from './schemas'

describe('zonas de entrega', () => {
  const zone = { id: 1, name: 'Lima', cost: null, description: null, isActive: true, sortOrder: 0 }

  it('distingue costo a coordinar, gratis y fijo', () => {
    expect(deliveryZoneDefaults(zone).costMode).toBe('coordinate')
    expect(deliveryZoneDefaults({ ...zone, cost: 0 }).costMode).toBe('free')
    expect(deliveryZoneDefaults({ ...zone, cost: 12.5 })).toMatchObject({ costMode: 'fixed', cost: '12.5' })
  })

  it('convierte el formulario en la petición', () => {
    const base = { name: ' Callao ', cost: '10,456', description: '  ', isActive: false }
    expect(toDeliveryZoneRequest({ ...base, costMode: 'fixed' })).toEqual({
      name: 'Callao',
      cost: 10.46,
      description: null,
      isActive: false,
    })
    expect(toDeliveryZoneRequest({ ...base, costMode: 'free' }).cost).toBe(0)
    expect(toDeliveryZoneRequest({ ...base, costMode: 'coordinate' }).cost).toBeNull()
  })

  it('exige un monto válido solo con precio fijo', () => {
    const values = { name: 'Lima', costMode: 'fixed' as const, cost: '', description: '', isActive: true }
    expect(deliveryZoneFormSchema.safeParse(values).success).toBe(false)
    expect(deliveryZoneFormSchema.safeParse({ ...values, cost: '-1' }).success).toBe(false)
    expect(deliveryZoneFormSchema.safeParse({ ...values, costMode: 'coordinate' }).success).toBe(true)
  })
})

describe('métodos de pago', () => {
  it('solo acepta enlaces http o https', () => {
    const values = { ...paymentMethodDefaults(), name: 'Yape' }
    expect(paymentMethodFormSchema.safeParse({ ...values, paymentLink: 'https://pago.pe/x' }).success).toBe(true)
    expect(paymentMethodFormSchema.safeParse({ ...values, paymentLink: 'javascript:alert(1)' }).success).toBe(false)
    expect(paymentMethodFormSchema.safeParse({ ...values, paymentLink: 'pago.pe' }).success).toBe(false)
    expect(paymentMethodFormSchema.safeParse({ ...values, paymentLink: '' }).success).toBe(true)
  })

  it('envía null en los campos opcionales vacíos', () => {
    const request = toPaymentMethodRequest({ ...paymentMethodDefaults(), name: ' Yape ', type: 'Other' })
    expect(request).toEqual({
      type: 'Other',
      name: 'Yape',
      instructions: null,
      accountNumber: null,
      accountAlias: null,
      paymentLink: null,
      isActive: true,
    })
  })
})
