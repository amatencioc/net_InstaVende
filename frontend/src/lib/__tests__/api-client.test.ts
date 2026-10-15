import { describe, expect, it } from 'vitest'
import { ApiError, unwrap } from '@/lib/api/client'

const response = (status: number) => new Response(null, { status })

describe('unwrap', () => {
  it('devuelve los datos en respuestas 2xx', async () => {
    await expect(unwrap(Promise.resolve({ data: { id: 1 }, response: response(200) }))).resolves.toEqual({ id: 1 })
  })

  it('lanza ApiError con el detalle del ProblemDetails', async () => {
    const error = await unwrap(
      Promise.resolve({ error: { detail: 'El SKU ya existe.' }, response: response(409) }),
    ).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).message).toBe('El SKU ya existe.')
  })

  it('convierte errores de red en ApiError con status 0', async () => {
    const error = await unwrap(Promise.reject(new TypeError('Failed to fetch'))).catch((e: unknown) => e)
    expect((error as ApiError).status).toBe(0)
    expect((error as ApiError).message).toMatch(/conectar/)
  })
})

describe('ApiError', () => {
  it('expone errores de validación por campo en camelCase', () => {
    const error = new ApiError(400, { errors: { Email: ['Ingresa un email válido.'], Password: ['Muy corta', 'x'] } })
    expect(error.fieldErrors).toEqual({ email: 'Ingresa un email válido.', password: 'Muy corta' })
    expect(error.message).toBe('Ingresa un email válido.')
  })

  it('usa mensajes por defecto según el código', () => {
    expect(new ApiError(403, {}).message).toMatch(/permiso/)
    expect(new ApiError(429, {}).message).toMatch(/Demasiadas/)
  })
})
