import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { ArrowLeft, PackageX } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'
import { PageContainer } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { productCategoriesQuery, productDetailQuery } from '@/features/products/api'
import { ProductFormPage } from '@/features/products/components/product-form'
import { ApiError } from '@/lib/api/client'

const parseId = (value: string): number | null => {
  const id = Number(value)
  return Number.isInteger(id) && id > 0 ? id : null
}

export const Route = createFileRoute('/_app/products/$productId')({
  loader: ({ context, params }) => {
    const id = parseId(params.productId)
    if (id) void context.queryClient.prefetchQuery(productDetailQuery(id))
    void context.queryClient.prefetchQuery(productCategoriesQuery)
  },
  head: () => ({ meta: [{ title: 'Editar producto — InstaVende' }] }),
  component: EditProductRoute,
})

function EditProductRoute() {
  const { productId } = Route.useParams()
  const id = parseId(productId)
  const product = useQuery({ ...productDetailQuery(id ?? 0), enabled: id !== null })

  if (id === null || (product.error instanceof ApiError && product.error.status === 404)) return <NotFound />
  if (product.isError) {
    return (
      <PageContainer className="max-w-6xl">
        <QueryError error={product.error} onRetry={() => void product.refetch()} />
      </PageContainer>
    )
  }
  if (!product.data) return <FormSkeleton />
  // key: si cambia el producto (p.ej. tras duplicar) el formulario se reinicia con sus datos
  return <ProductFormPage key={product.data.id} product={product.data} />
}

function NotFound() {
  return (
    <PageContainer className="max-w-6xl">
      <EmptyState
        icon={PackageX}
        title="Producto no encontrado"
        description="Puede que se haya eliminado o que el enlace no sea correcto."
        action={
          <Button asChild variant="outline">
            <Link to="/products">
              <ArrowLeft /> Volver a productos
            </Link>
          </Button>
        }
      />
    </PageContainer>
  )
}

function FormSkeleton() {
  return (
    <PageContainer className="max-w-6xl">
      <span role="status" className="sr-only">
        Cargando producto…
      </span>
      <div className="space-y-3">
        <Skeleton className="h-4 w-40" />
        <div className="flex items-center justify-between">
          <Skeleton className="h-9 w-56" />
          <Skeleton className="h-9 w-40" />
        </div>
      </div>
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {[0, 1, 2].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-5 w-40" />
              </CardHeader>
              <CardContent className="space-y-4">
                <Skeleton className="h-9 w-full" />
                <Skeleton className="h-24 w-full" />
              </CardContent>
            </Card>
          ))}
        </div>
        <div className="space-y-6">
          {[0, 1].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-5 w-24" />
              </CardHeader>
              <CardContent className="space-y-4">
                <Skeleton className="h-9 w-full" />
                <Skeleton className="h-9 w-full" />
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </PageContainer>
  )
}
