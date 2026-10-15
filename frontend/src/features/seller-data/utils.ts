import { useEffect, useState } from 'react'
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form'
import { ApiError } from '@/lib/api/client'

/** Devuelve `value` tras `delay` ms sin cambios (búsquedas mientras se escribe). */
export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])
  return debounced
}

/**
 * Lleva los errores de validación del servidor a los campos del formulario.
 * Devuelve el mensaje general que debe mostrarse en línea, o null si todo quedó asignado a un campo.
 */
export function applyServerErrors<T extends FieldValues>(
  form: UseFormReturn<T>,
  error: unknown,
  fields: readonly Path<T>[],
): string | null {
  if (!(error instanceof ApiError)) return 'Ocurrió un error inesperado. Inténtalo de nuevo.'
  const fieldErrors = error.fieldErrors
  let assigned = false
  for (const field of fields) {
    const message = fieldErrors[field]
    if (message) {
      form.setError(field, { type: 'server', message }, { shouldFocus: !assigned })
      assigned = true
    }
  }
  return assigned ? null : error.message
}

/** Descarga un texto como archivo (plantillas CSV). */
export function downloadTextFile(fileName: string, content: string, mime = 'text/csv;charset=utf-8') {
  // BOM para que Excel abra el CSV con tildes correctamente
  const blob = new Blob(['﻿', content], { type: mime })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
