import { useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { useDeleteProduct, useDuplicateProduct } from './api'

/** Valor con retardo (búsqueda mientras se escribe). */
export function useDebouncedValue<T>(value: T, delay = 350): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])
  return debounced
}

/** Acciones de fila reutilizables: duplicar (y abrir la copia) y eliminar con confirmación. */
export function useProductActions() {
  const confirm = useConfirm()
  const navigate = useNavigate()
  const duplicate = useDuplicateProduct()
  const remove = useDeleteProduct()

  return {
    duplicate: (id: number) =>
      duplicate.mutate(id, {
        onSuccess: (copy) => void navigate({ to: '/products/$productId', params: { productId: String(copy.id) } }),
      }),
    isDuplicating: duplicate.isPending,
    /** Devuelve true si el producto se eliminó o desactivó. */
    remove: async (product: { id: number; name: string }, options?: { onDone?: (result: { deleted: boolean; deactivated: boolean }) => void }) => {
      const ok = await confirm({
        title: '¿Eliminar producto?',
        description: `Estás a punto de eliminar «${product.name}». Si tiene pedidos, se marcará como no disponible para conservar el historial; si no, se eliminará definitivamente junto con sus imágenes.`,
        confirmLabel: 'Eliminar producto',
        tone: 'destructive',
      })
      if (!ok) return false
      remove.mutate(product.id, {
        // El aviso se muestra en la mutación: la fila desaparece (optimista) antes de que responda la API
        onSuccess: (result) => options?.onDone?.(result),
      })
      return true
    },
    isRemoving: remove.isPending,
  }
}
