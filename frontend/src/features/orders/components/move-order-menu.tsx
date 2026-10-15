import { Fragment, type ReactNode } from 'react'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { OrderSubStatus, OrderWorkflow } from '../api'
import { StatusDot } from '@/components/shared/order-status'

interface MoveOrderMenuProps {
  workflow: OrderWorkflow | undefined
  current: OrderSubStatus
  onMove: (target: OrderSubStatus) => void
  /** Botón que abre el menú (se usa como `asChild`). */
  trigger: ReactNode
  /** Elementos adicionales al inicio del menú (p.ej. "Ver detalle"). */
  header?: ReactNode
  align?: 'start' | 'end'
}

/**
 * Menú "Mover a…" con todos los sub-estados agrupados por pestaña.
 * Es la alternativa accesible (teclado/lector de pantalla) al arrastrar y soltar.
 */
export function MoveOrderMenu({ workflow, current, onMove, trigger, header, align = 'end' }: MoveOrderMenuProps) {
  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>{trigger}</DropdownMenuTrigger>
      <DropdownMenuContent align={align} className="w-60">
        {header && (
          <>
            {header}
            <DropdownMenuSeparator />
          </>
        )}
        <DropdownMenuLabel>Mover a…</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={current} onValueChange={(value) => onMove(value as OrderSubStatus)}>
          {workflow?.tabs.map((tab, index) => (
            <Fragment key={tab.tab}>
              {index > 0 && <DropdownMenuSeparator />}
              <DropdownMenuLabel className="text-muted-foreground py-1 text-xs font-normal">{tab.label}</DropdownMenuLabel>
              {tab.columns.map((col) => (
                <DropdownMenuRadioItem key={col.subStatus} value={col.subStatus} disabled={col.subStatus === current}>
                  <StatusDot css={col.css} />
                  {col.label}
                </DropdownMenuRadioItem>
              ))}
            </Fragment>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
