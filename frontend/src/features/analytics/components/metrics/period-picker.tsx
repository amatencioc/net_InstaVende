import { format, parseISO, subDays } from 'date-fns'
import { CalendarDays } from 'lucide-react'
import { useState } from 'react'
import type { DateRange } from 'react-day-picker'
import { es } from 'react-day-picker/locale'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { cn } from '@/lib/utils'
import type { MetricsParams } from '../../schemas'
import { formatRange } from '../../utils'

const presets = [
  { value: '7d', label: '7 días' },
  { value: '30d', label: '30 días' },
  { value: '90d', label: '90 días' },
] as const

const MAX_DAYS = 366

interface PeriodPickerProps {
  value: MetricsParams
  onChange: (value: MetricsParams) => void
}

/** Selector de periodo: atajos de 7/30/90 días o rango personalizado con calendario. */
export function PeriodPicker({ value, onChange }: PeriodPickerProps) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<DateRange | undefined>()
  const isCustom = value.period === 'custom' && value.from && value.to
  const today = new Date()

  const openChange = (next: boolean) => {
    if (next) {
      setDraft(
        isCustom
          ? { from: parseISO(value.from!), to: parseISO(value.to!) }
          : { from: subDays(today, 6), to: today },
      )
    }
    setOpen(next)
  }

  const apply = () => {
    if (!draft?.from || !draft.to) return
    onChange({ period: 'custom', from: format(draft.from, 'yyyy-MM-dd'), to: format(draft.to, 'yyyy-MM-dd') })
    setOpen(false)
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <ToggleGroup
        type="single"
        variant="outline"
        value={isCustom ? '' : value.period}
        onValueChange={(v) => v && onChange({ period: v as MetricsParams['period'] })}
        aria-label="Periodo"
      >
        {presets.map((p) => (
          <ToggleGroupItem key={p.value} value={p.value} className="px-3">
            {p.label}
          </ToggleGroupItem>
        ))}
      </ToggleGroup>
      <Popover open={open} onOpenChange={openChange}>
        <PopoverTrigger asChild>
          <Button
            variant="outline"
            className={cn(isCustom && 'border-primary/50 bg-primary/5 text-foreground')}
            aria-label={
              isCustom
                ? `Periodo personalizado: ${formatRange(value.from!, value.to!)}`
                : 'Elegir periodo personalizado'
            }
          >
            <CalendarDays aria-hidden />
            {isCustom ? formatRange(value.from!, value.to!) : 'Personalizado'}
          </Button>
        </PopoverTrigger>
        <PopoverContent align="end" className="w-auto p-0">
          <Calendar
            mode="range"
            locale={es}
            numberOfMonths={2}
            defaultMonth={draft?.from ? subDays(draft.to ?? draft.from, 31) : undefined}
            selected={draft}
            onSelect={setDraft}
            max={MAX_DAYS - 1}
            disabled={{ after: today }}
          />
          <div className="flex items-center justify-between gap-3 border-t p-3">
            <p className="text-muted-foreground text-xs" aria-live="polite">
              {draft?.from && draft.to
                ? formatRange(format(draft.from, 'yyyy-MM-dd'), format(draft.to, 'yyyy-MM-dd'))
                : 'Elige el día de inicio y el de fin'}
            </p>
            <Button size="sm" onClick={apply} disabled={!draft?.from || !draft.to}>
              Aplicar
            </Button>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  )
}
