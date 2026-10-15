import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, Loader2, UserRound } from 'lucide-react'
import { useState } from 'react'
import { channelMeta } from '@/components/shared/channel'
import { Button } from '@/components/ui/button'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'
import { remindersQueries } from '../api'
import { useDebouncedValue } from '../hooks'
import type { ReminderContactValue } from '../schemas'
import { contactLabel } from '../utils'
import { ContactAvatar } from './contact-avatar'

interface ContactComboboxProps {
  id?: string
  value: ReminderContactValue | null
  onChange: (contact: ReminderContactValue) => void
  disabled?: boolean
  /** Atributos que inyecta FormControl para asociar label, descripción y error. */
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

/** Selector de contacto con búsqueda remota (nombre o teléfono). */
export function ContactCombobox({
  id,
  value,
  onChange,
  disabled,
  'aria-invalid': invalid,
  'aria-describedby': describedBy,
}: ContactComboboxProps) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const debounced = useDebouncedValue(search.trim(), 300)
  const contacts = useQuery({ ...remindersQueries.contacts(debounced), enabled: open })
  const searching = contacts.isFetching || debounced !== search.trim()

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-invalid={invalid}
          aria-describedby={describedBy}
          disabled={disabled}
          className="h-auto min-h-10 w-full justify-between px-3 py-1.5 font-normal"
        >
          {value ? (
            <span className="flex min-w-0 items-center gap-2.5">
              <ContactAvatar name={contactLabel(value)} avatarUrl={value.avatarUrl} channel={value.channel} />
              <span className="min-w-0 text-left">
                <span className="block truncate text-sm font-medium">{contactLabel(value)}</span>
                <span className="text-muted-foreground block truncate text-xs">
                  {[value.phone, channelMeta[value.channel].label].filter(Boolean).join(' · ')}
                </span>
              </span>
            </span>
          ) : (
            <span className="text-muted-foreground flex items-center gap-2">
              <UserRound aria-hidden /> Busca un contacto…
            </span>
          )}
          <ChevronsUpDown className="opacity-50" aria-hidden />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-72 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput placeholder="Nombre o teléfono…" value={search} onValueChange={setSearch} />
          <CommandList>
            {contacts.isPending ? (
              <div className="text-muted-foreground flex items-center justify-center gap-2 py-6 text-sm">
                <Loader2 className="size-4 animate-spin" aria-hidden /> Buscando contactos…
              </div>
            ) : contacts.isError ? (
              <p role="alert" className="text-destructive py-6 text-center text-sm">
                No se pudieron cargar los contactos.
              </p>
            ) : (
              <>
                <CommandEmpty>
                  {debounced ? `Sin resultados para “${debounced}”.` : 'Aún no tienes contactos.'}
                </CommandEmpty>
                <CommandGroup
                  heading={
                    <span className="flex items-center justify-between">
                      {debounced ? 'Resultados' : 'Contactos recientes'}
                      {searching && <Loader2 className="size-3 animate-spin" aria-label="Buscando" />}
                    </span>
                  }
                >
                  {contacts.data.map((c) => {
                    const label = contactLabel(c)
                    return (
                      <CommandItem
                        key={c.id}
                        value={String(c.id)}
                        onSelect={() => {
                          onChange({
                            id: c.id,
                            name: c.name,
                            phone: c.phone,
                            avatarUrl: c.avatarUrl,
                            channel: c.channel,
                          })
                          setOpen(false)
                        }}
                        className="gap-2.5"
                      >
                        <ContactAvatar name={label} avatarUrl={c.avatarUrl} channel={c.channel} />
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-medium">{label}</span>
                          <span className="text-muted-foreground block truncate text-xs">
                            {[c.phone, channelMeta[c.channel].label].filter(Boolean).join(' · ')}
                          </span>
                        </span>
                        <Check
                          className={cn('size-4', value?.id === c.id ? 'opacity-100' : 'opacity-0')}
                          aria-hidden
                        />
                      </CommandItem>
                    )
                  })}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
