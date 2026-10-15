import { Pencil, Star, Trash2 } from 'lucide-react'
import type { KnowledgeEntry } from '../../api'
import { CategoryBadge } from './category-badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { formatDateTime, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

interface KnowledgeCardProps {
  entry: KnowledgeEntry
  onToggleFavorite: () => void
  onEdit: () => void
  onDelete: () => void
}

/** Tarjeta de un tema: título, categoría, contenido resumido y acciones. */
export function KnowledgeCard({ entry, onToggleFavorite, onEdit, onDelete }: KnowledgeCardProps) {
  const favoriteLabel = entry.isFavorite ? 'Quitar de favoritos' : 'Marcar como favorito'
  return (
    <Card
      className={cn(
        'group/card hover:border-primary/40 h-full gap-3 py-4 transition-colors',
        entry.isFavorite && 'border-warning/40',
      )}
    >
      <div className="flex items-start justify-between gap-2 px-4">
        <CategoryBadge category={entry.category} />
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={onToggleFavorite}
              aria-pressed={entry.isFavorite}
              aria-label={`${favoriteLabel}: ${entry.title}`}
              className="-mt-1 -mr-1"
            >
              <Star
                className={cn(
                  'size-4 transition-colors',
                  entry.isFavorite ? 'fill-warning text-warning' : 'text-muted-foreground',
                )}
                aria-hidden
              />
            </Button>
          </TooltipTrigger>
          <TooltipContent>{favoriteLabel}</TooltipContent>
        </Tooltip>
      </div>

      <div className="min-w-0 flex-1 space-y-1.5 px-4">
        <h3 className="line-clamp-2 leading-snug font-medium break-words">{entry.title}</h3>
        <p className="text-muted-foreground line-clamp-4 text-sm break-words whitespace-pre-line">{entry.content}</p>
      </div>

      <div className="flex items-center justify-between gap-2 px-4 pt-1">
        <time dateTime={entry.updatedAt} title={formatDateTime(entry.updatedAt)} className="text-muted-foreground text-xs">
          Actualizado {formatRelative(entry.updatedAt)}
        </time>
        <div className="flex gap-1">
          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Editar: ${entry.title}`}>
                <Pencil aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent>Editar</TooltipContent>
          </Tooltip>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={onDelete}
                aria-label={`Eliminar: ${entry.title}`}
                className="hover:text-destructive"
              >
                <Trash2 aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent>Eliminar</TooltipContent>
          </Tooltip>
        </div>
      </div>
    </Card>
  )
}
