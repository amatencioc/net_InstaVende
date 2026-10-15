import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { Link, useBlocker, useNavigate } from '@tanstack/react-router'
import {
  CircleAlert,
  Copy,
  ImageIcon,
  Info,
  Layers,
  Loader2,
  MoreHorizontal,
  Package,
  Save,
  Tags,
  Trash2,
  Wallet,
} from 'lucide-react'
import { useEffect, useRef, useState, type BaseSyntheticEvent, type ReactNode } from 'react'
import { useForm, useFormContext, useWatch, type FieldPath } from 'react-hook-form'
import { toast } from 'sonner'
import { Alert } from '@/components/shared/alert'
import { PageContainer } from '@/components/shared/page-header'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { useMoney, useSession } from '@/lib/auth/session'
import { formatDateTime } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  productKeys,
  uploadProductImages,
  useCreateProduct,
  useUpdateProduct,
  type ProductDetail,
} from '../api'
import { useProductActions } from '../hooks'
import { revokePendingImages, type PendingImage } from '../lib'
import {
  DESCRIPTION_MAX,
  detailToForm,
  emptyProductForm,
  formToRequest,
  MAX_IMAGES,
  NAME_MAX,
  productFormSchema,
  type ProductFormValues,
} from '../schemas'
import { CategoriesDialog } from './categories-dialog'
import { CategoryCombobox } from './category-combobox'
import { NumberInput } from './number-input'
import { AvailabilityBadge } from './product-bits'
import { LocalProductImages, RemoteProductImages } from './product-images-field'
import { VariantsField } from './variants-field'

/** Convierte una clave de error de la API ("Variants[0].AttributeName") en la ruta del formulario. */
function toFieldPath(key: string): string {
  return key
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
    .join('.')
}

const knownFields = new Set(Object.keys(emptyProductForm))

/** Formulario de alta/edición de producto (secciones + aviso de cambios sin guardar). */
export function ProductFormPage({ product }: { product?: ProductDetail }) {
  const isEdit = !!product
  const navigate = useNavigate()
  const qc = useQueryClient()
  const create = useCreateProduct()
  const update = useUpdateProduct(product?.id ?? 0)
  const actions = useProductActions()
  const [pendingImages, setPendingImages] = useState<PendingImage[]>([])
  const [formError, setFormError] = useState<string | null>(null)
  const [uploadingAfterCreate, setUploadingAfterCreate] = useState(false)
  const allowLeave = useRef(false)

  const form = useForm<ProductFormValues>({
    resolver: zodResolver(productFormSchema),
    defaultValues: product ? detailToForm(product) : emptyProductForm,
    mode: 'onTouched',
  })

  const isSaving = create.isPending || update.isPending || uploadingAfterCreate
  const isDirty = form.formState.isDirty || pendingImages.length > 0

  // Libera las previsualizaciones locales al salir
  const pendingRef = useRef(pendingImages)
  useEffect(() => {
    pendingRef.current = pendingImages
  }, [pendingImages])
  useEffect(() => () => revokePendingImages(pendingRef.current), [])

  const blocker = useBlocker({
    shouldBlockFn: () => isDirty && !allowLeave.current,
    enableBeforeUnload: () => isDirty && !allowLeave.current,
    withResolver: true,
  })

  const applyServerError = (error: unknown) => {
    if (!(error instanceof ApiError)) {
      toast.error('No se pudo guardar el producto. Inténtalo de nuevo.')
      return
    }
    if (error.status === 409) {
      form.setError('sku', { message: error.message }, { shouldFocus: true })
      return
    }
    let mapped = 0
    for (const [key, messages] of Object.entries(error.problem.errors ?? {})) {
      const path = toFieldPath(key)
      if (knownFields.has(path.split('.')[0] ?? '') && messages[0]) {
        form.setError(path as FieldPath<ProductFormValues>, { message: messages[0] }, { shouldFocus: mapped === 0 })
        mapped++
      }
    }
    if (mapped === 0) setFormError(error.message)
  }

  const save = async (values: ProductFormValues) => {
    setFormError(null)
    const body = formToRequest(values)
    try {
      if (product) {
        const saved = await update.mutateAsync(body)
        form.reset(detailToForm(saved))
        toast.success('Cambios guardados.')
        return
      }

      const created = await create.mutateAsync(body)
      allowLeave.current = true
      let uploadFailed = false
      if (pendingImages.length > 0) {
        setUploadingAfterCreate(true)
        try {
          await uploadProductImages(
            created.id,
            pendingImages.map((p) => p.file),
          )
        } catch (e) {
          uploadFailed = true
          toast.error(`El producto se creó, pero las imágenes no se pudieron subir: ${e instanceof Error ? e.message : ''}`)
        } finally {
          setUploadingAfterCreate(false)
          void qc.invalidateQueries({ queryKey: productKeys.detail(created.id) })
          void qc.invalidateQueries({ queryKey: productKeys.lists() })
        }
      }
      toast.success('Producto creado.')
      if (uploadFailed) {
        await navigate({ to: '/products/$productId', params: { productId: String(created.id) } })
      } else {
        await navigate({ to: '/products' })
      }
    } catch (error) {
      allowLeave.current = false
      applyServerError(error)
    }
  }
  // handleSubmit se invoca solo en eventos (envío del formulario o atajo de teclado)
  const onSubmit = (e?: BaseSyntheticEvent) => form.handleSubmit(save)(e)

  // Ctrl/Cmd + S guarda
  const submitRef = useRef(onSubmit)
  useEffect(() => {
    submitRef.current = onSubmit
  })
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        void submitRef.current()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const title = isEdit ? 'Editar producto' : 'Nuevo producto'

  return (
    <PageContainer className="max-w-6xl">
      <div className="space-y-3">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink asChild>
                <Link to="/products">Productos</Link>
              </BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage className="max-w-60 truncate">{product?.name ?? 'Nuevo producto'}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{title}</h1>
            {isDirty && (
              <Badge variant="outline" className="border-warning/40 bg-warning/10 text-warning" role="status">
                <CircleAlert aria-hidden /> Cambios sin guardar
              </Badge>
            )}
            {product && !product.isActive && <Badge variant="secondary">No disponible</Badge>}
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Button variant="outline" asChild>
              <Link to="/products">Cancelar</Link>
            </Button>
            {product && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="icon" aria-label="Más acciones">
                    <MoreHorizontal />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem disabled={actions.isDuplicating} onSelect={() => actions.duplicate(product.id)}>
                    <Copy /> Duplicar
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    variant="destructive"
                    onSelect={() =>
                      void actions.remove(product, {
                        onDone: (result) => {
                          if (!result.deleted) {
                            // Desactivado por tener pedidos: refleja el nuevo estado sin perder otros cambios
                            form.resetField('isActive', { defaultValue: false })
                            return
                          }
                          allowLeave.current = true
                          void navigate({ to: '/products' })
                        },
                      })
                    }
                  >
                    <Trash2 /> Eliminar
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
            <Button type="submit" form="product-form" disabled={isSaving || (isEdit && !isDirty)}>
              {isSaving ? <Loader2 className="animate-spin" /> : <Save />}
              {uploadingAfterCreate ? 'Subiendo imágenes…' : 'Guardar'}
            </Button>
          </div>
        </div>
      </div>

      {formError && <Alert tone="destructive">{formError}</Alert>}
      {product?.hasOrders && (
        <Alert tone="info" title="Este producto figura en pedidos">
          Si lo eliminas, se marcará como no disponible para conservar el historial de ventas.
        </Alert>
      )}

      <Form {...form}>
        <form id="product-form" onSubmit={onSubmit} noValidate className="grid items-start gap-6 lg:grid-cols-3">
          <div className="space-y-6 lg:col-span-2">
            <Section icon={Info} title="Información básica" description="Lo que tu vendedor IA contará a tus clientes.">
              <BasicInfoFields />
            </Section>

            <Section
              icon={ImageIcon}
              title="Imágenes"
              description={
                isEdit
                  ? `Hasta ${MAX_IMAGES} imágenes. Los cambios en la galería se guardan al instante.`
                  : `Hasta ${MAX_IMAGES} imágenes. Se subirán al guardar el producto.`
              }
            >
              {product ? (
                <RemoteProductImages productId={product.id} images={product.images} legacyImageUrl={product.imageUrl} />
              ) : (
                <LocalProductImages value={pendingImages} onChange={setPendingImages} disabled={isSaving} />
              )}
            </Section>

            <Section
              icon={Layers}
              title="Variantes"
              description="Ofrece opciones como talla, color o material. El ajuste se suma al precio base."
            >
              <VariantsField />
            </Section>
          </div>

          <div className="space-y-6 lg:sticky lg:top-20">
            <Section icon={Wallet} title="Precio">
              <PriceFields />
            </Section>
            <Section icon={Package} title="Inventario">
              <InventoryFields />
            </Section>
            {product && (
              <Card className="gap-0 py-0">
                <CardContent className="text-muted-foreground space-y-1.5 p-5 text-xs">
                  <p>
                    Creado el <span className="text-foreground">{formatDateTime(product.createdAt)}</span>
                  </p>
                  {product.updatedAt && (
                    <p>
                      Última modificación: <span className="text-foreground">{formatDateTime(product.updatedAt)}</span>
                    </p>
                  )}
                </CardContent>
              </Card>
            )}
          </div>
        </form>
      </Form>

      <AlertDialog open={blocker.status === 'blocked'}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>¿Salir sin guardar?</AlertDialogTitle>
            <AlertDialogDescription>
              Tienes cambios sin guardar en este producto. Si sales ahora, se perderán.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => blocker.reset?.()}>Seguir editando</AlertDialogCancel>
            <AlertDialogAction className={buttonVariants({ variant: 'destructive' })} onClick={() => blocker.proceed?.()}>
              Salir sin guardar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </PageContainer>
  )
}

function Section({
  icon: Icon,
  title,
  description,
  children,
}: {
  icon: typeof Info
  title: string
  description?: string
  children: ReactNode
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Icon className="text-primary size-4" aria-hidden />
          {title}
        </CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent className="space-y-5">{children}</CardContent>
    </Card>
  )
}

function CharCounter({ value, max }: { value: string; max: number }) {
  const length = value.length
  return (
    <span
      className={cn('text-muted-foreground text-xs tabular-nums', length > max && 'text-destructive font-medium')}
      aria-live="polite"
    >
      {length}/{max}
    </span>
  )
}

function BasicInfoFields() {
  const form = useFormContextTyped()
  const name = useWatch({ control: form.control, name: 'name' })
  const description = useWatch({ control: form.control, name: 'description' })

  return (
    <>
      <FormField
        control={form.control}
        name="name"
        render={({ field }) => (
          <FormItem>
            <div className="flex items-center justify-between gap-2">
              <FormLabel>Nombre del producto *</FormLabel>
              <CharCounter value={name} max={NAME_MAX} />
            </div>
            <FormControl>
              <Input placeholder="Ej. Camiseta de algodón" autoComplete="off" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="description"
        render={({ field }) => (
          <FormItem>
            <div className="flex items-center justify-between gap-2">
              <FormLabel>Descripción</FormLabel>
              <CharCounter value={description} max={DESCRIPTION_MAX} />
            </div>
            <FormControl>
              <Textarea
                rows={5}
                placeholder="Ej. Camiseta 100 % algodón peinado, suave y transpirable. Ideal para uso diario. Tallas S a XL."
                className="min-h-28 resize-y"
                {...field}
              />
            </FormControl>
            <FormDescription>Incluye materiales, medidas y usos: el vendedor IA lo usa para responder dudas.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      <div className="grid gap-5 sm:grid-cols-2">
        <FormField
          control={form.control}
          name="sku"
          render={({ field }) => (
            <FormItem>
              <FormLabel>SKU</FormLabel>
              <FormControl>
                <Input placeholder="Ej. CAM-ALG-001" autoComplete="off" maxLength={64} {...field} />
              </FormControl>
              <FormDescription>Código interno opcional. Debe ser único.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="categoryId"
          render={({ field }) => (
            <FormItem>
              <div className="flex items-center justify-between gap-2">
                <FormLabel>Categoría</FormLabel>
                <CategoriesDialog
                  trigger={
                    <button type="button" className="text-primary inline-flex items-center gap-1 text-xs font-medium hover:underline">
                      <Tags className="size-3" aria-hidden /> Gestionar
                    </button>
                  }
                />
              </div>
              <FormControl>
                <CategoryCombobox ref={field.ref} value={field.value} onChange={field.onChange} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField
          control={form.control}
          name="isActive"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between gap-3 rounded-lg border p-3">
              <div className="space-y-0.5">
                <FormLabel>Disponible</FormLabel>
                <FormDescription className="text-xs">El vendedor IA puede ofrecerlo.</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} ref={field.ref} />
              </FormControl>
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="isFeatured"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between gap-3 rounded-lg border p-3">
              <div className="space-y-0.5">
                <FormLabel>Destacado</FormLabel>
                <FormDescription className="text-xs">Se recomienda primero.</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} ref={field.ref} />
              </FormControl>
            </FormItem>
          )}
        />
      </div>
    </>
  )
}

function PriceFields() {
  const form = useFormContextTyped()
  const currency = useSession().business.currencySymbol
  const money = useMoney()
  const price = useWatch({ control: form.control, name: 'price' })
  const original = useWatch({ control: form.control, name: 'priceOriginal' })
  const discount =
    original != null && Number.isFinite(original) && Number.isFinite(price) && original > price && original > 0
      ? Math.round(((original - price) / original) * 100)
      : null

  return (
    <>
      <FormField
        control={form.control}
        name="price"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Precio *</FormLabel>
            <FormControl>
              <NumberInput
                ref={field.ref}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                prefix={currency}
                placeholder="0.00"
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="priceOriginal"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Precio anterior</FormLabel>
            <FormControl>
              <NumberInput
                ref={field.ref}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                allowEmpty
                prefix={currency}
                placeholder="Opcional"
              />
            </FormControl>
            <FormDescription>Se muestra tachado para comunicar una oferta.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      {discount != null && (
        <div className="bg-success/10 text-success flex items-center justify-between rounded-lg px-3 py-2 text-sm">
          <span>
            Antes <span className="line-through">{money(original)}</span>
          </span>
          <span className="font-semibold">-{discount}%</span>
        </div>
      )}
    </>
  )
}

function InventoryFields() {
  const form = useFormContextTyped()
  const stock = useWatch({ control: form.control, name: 'stock' })
  const isActive = useWatch({ control: form.control, name: 'isActive' })
  const variantCount = useWatch({ control: form.control, name: 'variants' }).length

  return (
    <>
      <FormField
        control={form.control}
        name="stock"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Stock disponible *</FormLabel>
            <FormControl>
              <NumberInput
                ref={field.ref}
                name={field.name}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                integer
                placeholder="0"
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <div className="flex items-center justify-between gap-2 text-sm">
        <span className="text-muted-foreground">Estado en el catálogo</span>
        <AvailabilityBadge isActive={isActive} stock={Number.isFinite(stock) ? stock : 0} />
      </div>
      {variantCount > 0 && (
        <p className="text-muted-foreground text-xs">Cada variante tiene además su propio stock en la sección Variantes.</p>
      )}
    </>
  )
}

/** useFormContext tipado para las secciones del formulario. */
function useFormContextTyped() {
  return useFormContext<ProductFormValues>()
}
