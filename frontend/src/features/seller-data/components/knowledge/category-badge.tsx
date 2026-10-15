import type { KnowledgeCategory } from '../../api'
import { categoryMeta } from '../../meta'
import { cn } from '@/lib/utils'

/** Insignia de categoría con icono y color propios. */
export function CategoryBadge({ category, className }: { category: KnowledgeCategory; className?: string }) {
  const meta = categoryMeta[category]
  const Icon = meta.icon
  return (
    <span
      className={cn(
        'inline-flex w-fit items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        meta.tone,
        className,
      )}
    >
      <Icon className="size-3" aria-hidden />
      {meta.label}
    </span>
  )
}
