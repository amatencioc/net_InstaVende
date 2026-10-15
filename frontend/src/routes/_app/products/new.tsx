import { createFileRoute } from '@tanstack/react-router'
import { productCategoriesQuery } from '@/features/products/api'
import { ProductFormPage } from '@/features/products/components/product-form'

export const Route = createFileRoute('/_app/products/new')({
  loader: ({ context }) => {
    void context.queryClient.prefetchQuery(productCategoriesQuery)
  },
  head: () => ({ meta: [{ title: 'Nuevo producto — InstaVende' }] }),
  component: () => <ProductFormPage />,
})
