import {
  keepPreviousData,
  queryOptions,
  useMutation,
  useQueryClient,
  type QueryClient,
  type QueryKey,
} from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/lib/api/client'

// ── Tipos del contrato ───────────────────────────────────────────────────────

export type ProductListItem = Schemas['ProductListItemDto']
export type ProductDetail = Schemas['ProductDetailDto']
export type ProductImage = Schemas['ProductImageDto']
export type ProductUpsertRequest = Schemas['ProductUpsertRequest']
export type ProductCategory = Schemas['ProductCategoryDto']
export type ProductBulkAction = Schemas['ProductBulkAction']
export type ProductStatusFilter = NonNullable<Schemas['ProductStatusFilter']>
export type ProductSort = NonNullable<Schemas['ProductSort']>
export type ProductPage = Schemas['PagedResultOfProductListItemDto']

export interface ProductListParams {
  search?: string
  categoryId?: number
  status?: ProductStatusFilter
  sort?: ProductSort
  page: number
  pageSize: number
}

// ── Claves de caché ──────────────────────────────────────────────────────────

export const productKeys = {
  all: ['products'] as const,
  lists: () => [...productKeys.all, 'list'] as const,
  list: (params: ProductListParams) => [...productKeys.lists(), params] as const,
  details: () => [...productKeys.all, 'detail'] as const,
  detail: (id: number) => [...productKeys.details(), id] as const,
}

export const categoryKeys = {
  all: ['product-categories'] as const,
}

// ── Consultas ────────────────────────────────────────────────────────────────

export const productListQuery = (params: ProductListParams) =>
  queryOptions({
    queryKey: productKeys.list(params),
    queryFn: ({ signal }) => unwrap(api.GET('/api/products', { params: { query: params }, signal })),
    placeholderData: keepPreviousData,
  })

export const productDetailQuery = (id: number) =>
  queryOptions({
    queryKey: productKeys.detail(id),
    queryFn: ({ signal }) => unwrap(api.GET('/api/products/{id}', { params: { path: { id } }, signal })),
  })

export const productCategoriesQuery = queryOptions({
  queryKey: categoryKeys.all,
  queryFn: ({ signal }) => unwrap(api.GET('/api/product-categories', { signal })),
  staleTime: 60_000,
})

// ── Mutaciones de productos ──────────────────────────────────────────────────

/** Invalida listados y categorías (el contador de productos por categoría cambia con casi todo). */
function invalidateCatalog(qc: QueryClient) {
  void qc.invalidateQueries({ queryKey: productKeys.lists() })
  void qc.invalidateQueries({ queryKey: categoryKeys.all })
}

export function useCreateProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ProductUpsertRequest) => unwrap(api.POST('/api/products', { body })),
    meta: { silent: true }, // el formulario muestra los errores en línea
    onSuccess: (product) => {
      qc.setQueryData(productKeys.detail(product.id), product)
      invalidateCatalog(qc)
    },
  })
}

export function useUpdateProduct(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ProductUpsertRequest) =>
      unwrap(api.PUT('/api/products/{id}', { params: { path: { id } }, body })),
    meta: { silent: true },
    onSuccess: (product) => {
      qc.setQueryData(productKeys.detail(id), product)
      invalidateCatalog(qc)
    },
  })
}

export function useDuplicateProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.POST('/api/products/{id}/duplicate', { params: { path: { id } } })),
    meta: { successMessage: 'Producto duplicado. La copia queda como no disponible hasta que la actives.' },
    onSuccess: (product) => {
      qc.setQueryData(productKeys.detail(product.id), product)
      invalidateCatalog(qc)
    },
  })
}

type ListSnapshot = [QueryKey, ProductPage | undefined][]

/** Quita filas de los listados en caché (actualización optimista) y devuelve la foto previa. */
async function removeFromLists(qc: QueryClient, ids: Set<number>): Promise<ListSnapshot> {
  await qc.cancelQueries({ queryKey: productKeys.lists() })
  const snapshot = qc.getQueriesData<ProductPage>({ queryKey: productKeys.lists() })
  qc.setQueriesData<ProductPage>({ queryKey: productKeys.lists() }, (page) =>
    page
      ? {
          ...page,
          items: page.items.filter((p) => !ids.has(p.id)),
          totalCount: Math.max(0, page.totalCount - page.items.filter((p) => ids.has(p.id)).length),
        }
      : page,
  )
  return snapshot
}

function restoreLists(qc: QueryClient, snapshot: ListSnapshot | undefined) {
  snapshot?.forEach(([key, data]) => qc.setQueryData(key, data))
}

export function useDeleteProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/products/{id}', { params: { path: { id } } })),
    onMutate: async (id) => ({ snapshot: await removeFromLists(qc, new Set([id])) }),
    onError: (_error, _id, context) => restoreLists(qc, context?.snapshot),
    onSuccess: (result, id) => {
      if (result.deactivated) toast.info(result.message)
      else toast.success(result.message)
      // Borrado real: solo se marca obsoleto (la página de edición abierta navega al listado sin parpadeo 404)
      void qc.invalidateQueries({
        queryKey: productKeys.detail(id),
        refetchType: result.deleted ? 'none' : 'active',
      })
    },
    onSettled: () => invalidateCatalog(qc),
  })
}

export function useBulkProducts() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['ProductBulkRequest']) => unwrap(api.POST('/api/products/bulk', { body })),
    onMutate: async (body) =>
      body.action === 'delete' ? { snapshot: await removeFromLists(qc, new Set(body.ids)) } : { snapshot: undefined },
    onError: (_error, _body, context) => restoreLists(qc, context?.snapshot),
    onSuccess: (result) => {
      if (result.deactivated > 0) toast.info(result.message)
      else toast.success(result.message)
    },
    onSettled: () => {
      invalidateCatalog(qc)
      void qc.invalidateQueries({ queryKey: productKeys.details() })
    },
  })
}

// ── Imágenes ─────────────────────────────────────────────────────────────────

/** Actualiza la galería en la caché del detalle (y la imagen principal derivada). */
function setDetailImages(qc: QueryClient, id: number, images: ProductImage[]) {
  qc.setQueryData<ProductDetail>(productKeys.detail(id), (detail) =>
    detail ? { ...detail, images, imageUrl: images[0]?.url ?? null } : detail,
  )
}

export function uploadProductImages(id: number, files: File[]) {
  const form = new FormData()
  files.forEach((file) => form.append('files', file, file.name))
  return unwrap(
    api.POST('/api/products/{id}/images', {
      params: { path: { id } },
      body: {},
      // FormData: el navegador fija Content-Type multipart con su boundary
      bodySerializer: () => form,
    }),
  )
}

export function useUploadProductImages(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (files: File[]) => uploadProductImages(id, files),
    onSuccess: (images) => {
      setDetailImages(qc, id, images)
      void qc.invalidateQueries({ queryKey: productKeys.lists() })
    },
  })
}

export function useDeleteProductImage(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (imageId: number) =>
      unwrap(api.DELETE('/api/products/{id}/images/{imageId}', { params: { path: { id, imageId } } })),
    onSuccess: (images) => {
      setDetailImages(qc, id, images)
      void qc.invalidateQueries({ queryKey: productKeys.lists() })
    },
  })
}

/** Reordena (optimista): la primera imagen queda como principal. */
export function useReorderProductImages(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (imageIds: number[]) =>
      unwrap(api.PUT('/api/products/{id}/images/order', { params: { path: { id } }, body: { imageIds } })),
    onMutate: async (imageIds) => {
      await qc.cancelQueries({ queryKey: productKeys.detail(id) })
      const previous = qc.getQueryData<ProductDetail>(productKeys.detail(id))
      if (previous) {
        const byId = new Map(previous.images.map((img) => [img.id, img]))
        const next = imageIds
          .map((imageId) => byId.get(imageId))
          .filter((img): img is ProductImage => !!img)
          .map((img, index) => ({ ...img, sortOrder: index, isMain: index === 0 }))
        setDetailImages(qc, id, next)
      }
      return { previous }
    },
    onError: (_error, _ids, context) => {
      if (context?.previous) qc.setQueryData(productKeys.detail(id), context.previous)
    },
    onSuccess: (images) => {
      setDetailImages(qc, id, images)
      void qc.invalidateQueries({ queryKey: productKeys.lists() })
    },
  })
}

// ── Categorías ───────────────────────────────────────────────────────────────

export function useCreateCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['ProductCategoryUpsertRequest']) =>
      unwrap(api.POST('/api/product-categories', { body })),
    onSuccess: (category) => {
      qc.setQueryData<ProductCategory[]>(categoryKeys.all, (list) =>
        list ? [...list, category].sort((a, b) => a.name.localeCompare(b.name, 'es')) : list,
      )
      void qc.invalidateQueries({ queryKey: categoryKeys.all })
    },
  })
}

export function useUpdateCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: Schemas['ProductCategoryUpsertRequest'] & { id: number }) =>
      unwrap(api.PUT('/api/product-categories/{id}', { params: { path: { id } }, body })),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: categoryKeys.all })
      void qc.invalidateQueries({ queryKey: productKeys.all })
    },
  })
}

export function useDeleteCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/product-categories/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Categoría eliminada. Sus productos quedaron sin categoría.' },
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: categoryKeys.all })
      const previous = qc.getQueryData<ProductCategory[]>(categoryKeys.all)
      qc.setQueryData<ProductCategory[]>(categoryKeys.all, (list) => list?.filter((c) => c.id !== id))
      return { previous }
    },
    onError: (_error, _id, context) => {
      if (context?.previous) qc.setQueryData(categoryKeys.all, context.previous)
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: categoryKeys.all })
      void qc.invalidateQueries({ queryKey: productKeys.all })
    },
  })
}
