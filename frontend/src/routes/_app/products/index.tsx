import { createFileRoute } from '@tanstack/react-router'
import { productCategoriesQuery, productListQuery } from '@/features/products/api'
import { ProductsPage } from '@/features/products/components/products-page'
import { productSearchSchema, toListParams } from '@/features/products/schemas'

export const Route = createFileRoute('/_app/products/')({
  validateSearch: productSearchSchema,
  loaderDeps: ({ search }) => toListParams(search),
  // Precarga sin bloquear: la página muestra su propio Skeleton
  loader: ({ context, deps }) => {
    void context.queryClient.prefetchQuery(productListQuery(deps))
    void context.queryClient.prefetchQuery(productCategoriesQuery)
  },
  head: () => ({ meta: [{ title: 'Productos — InstaVende' }] }),
  component: ProductsRoute,
})

function ProductsRoute() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <ProductsPage
      search={search}
      onSearchChange={(patch, options) =>
        void navigate({ search: (prev) => ({ ...prev, ...patch }), replace: options?.replace })
      }
    />
  )
}
