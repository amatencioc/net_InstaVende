import { Link } from '@tanstack/react-router'
import { Copy, MoreHorizontal, Pencil, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useProductActions } from '../hooks'

/** Menú de acciones de un producto (editar, duplicar, eliminar). */
export function ProductRowActions({
  product,
  className,
}: {
  product: { id: number; name: string }
  className?: string
}) {
  const actions = useProductActions()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon-sm" className={className} aria-label={`Acciones de ${product.name}`}>
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuItem asChild>
          <Link to="/products/$productId" params={{ productId: String(product.id) }}>
            <Pencil /> Editar
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem disabled={actions.isDuplicating} onSelect={() => actions.duplicate(product.id)}>
          <Copy /> Duplicar
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onSelect={() => void actions.remove(product)}>
          <Trash2 /> Eliminar
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
