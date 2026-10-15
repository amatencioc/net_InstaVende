import { CheckCircle2, Download, FileSpreadsheet, FileUp, Loader2, TriangleAlert, X } from 'lucide-react'
import { useState } from 'react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { useImportKnowledge, type KnowledgeImportResult } from '../../api'
import { KNOWLEDGE_IMPORT_MAX_ROWS } from '../../meta'
import { downloadTextFile, formatFileSize } from '../../utils'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { cn } from '@/lib/utils'

const MAX_BYTES = 1024 * 1024

const TEMPLATE = [
  'titulo,contenido,categoria',
  '"¿Cuál es su horario de atención?","Atendemos de lunes a sábado de 9:00 a. m. a 7:00 p. m.",Horarios',
  '"¿Hacen envíos a provincia?","Sí, enviamos a todo el país por agencia. El envío tarda de 2 a 4 días hábiles.",Envios',
  '"¿Aceptan pago con tarjeta?","Sí, aceptamos Visa y Mastercard. El motorizado lleva POS.",Pagos',
  '"¿Puedo cambiar un producto?","Sí, tienes 7 días para cambios con el producto sin usar y su boleta.",Devoluciones',
].join('\r\n')

/** Importación masiva de temas desde un CSV, con plantilla descargable y resultado por fila. */
export function KnowledgeImportDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">{open && <ImportFlow onDone={() => onOpenChange(false)} />}</DialogContent>
    </Dialog>
  )
}

function rejectionMessage(rejections: FileRejection[]): string {
  const code = rejections[0]?.errors[0]?.code
  if (code === 'file-too-large') return 'El archivo supera el tamaño máximo de 1 MB.'
  if (code === 'file-invalid-type') return 'El archivo debe tener formato CSV.'
  if (code === 'too-many-files') return 'Selecciona un solo archivo.'
  return 'No se pudo leer el archivo seleccionado.'
}

function ImportFlow({ onDone }: { onDone: () => void }) {
  const importMutation = useImportKnowledge()
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<KnowledgeImportResult | null>(null)

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    accept: { 'text/csv': ['.csv'], 'application/vnd.ms-excel': ['.csv'], 'text/plain': ['.csv', '.txt'] },
    maxFiles: 1,
    multiple: false,
    maxSize: MAX_BYTES,
    disabled: importMutation.isPending,
    onDropAccepted: ([accepted]) => {
      setError(null)
      setFile(accepted ?? null)
    },
    onDropRejected: (rejections) => setError(rejectionMessage(rejections)),
  })

  const runImport = () => {
    if (!file) return
    setError(null)
    importMutation.mutate(file, {
      onSuccess: setResult,
      onError: (e) => setError(e.message),
    })
  }

  const reset = () => {
    setFile(null)
    setResult(null)
    setError(null)
    importMutation.reset()
  }

  if (result) return <ImportResult result={result} onAgain={reset} onDone={onDone} />

  return (
    <div className="grid gap-5">
      <DialogHeader>
        <DialogTitle>Importar temas desde CSV</DialogTitle>
        <DialogDescription>
          Carga varios temas a la vez. El archivo debe tener las columnas <strong>titulo</strong>,{' '}
          <strong>contenido</strong> y <strong>categoria</strong> (máximo {KNOWLEDGE_IMPORT_MAX_ROWS} filas).
        </DialogDescription>
      </DialogHeader>

      {error && <Alert tone="destructive">{error}</Alert>}

      {file ? (
        <div className="bg-muted/40 flex items-center gap-3 rounded-xl border p-4">
          <div className="bg-primary/10 text-primary flex size-10 shrink-0 items-center justify-center rounded-lg">
            <FileSpreadsheet className="size-5" aria-hidden />
          </div>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{file.name}</p>
            <p className="text-muted-foreground text-xs">{formatFileSize(file.size)}</p>
          </div>
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={() => setFile(null)}
            disabled={importMutation.isPending}
            aria-label="Quitar archivo"
          >
            <X aria-hidden />
          </Button>
        </div>
      ) : (
        <div
          {...getRootProps({
            className: cn(
              'border-border hover:border-primary/60 hover:bg-primary/5 focus-visible:ring-ring/50 flex cursor-pointer flex-col items-center gap-2 rounded-xl border-2 border-dashed px-6 py-10 text-center transition-colors outline-none focus-visible:ring-[3px]',
              isDragActive && 'border-primary bg-primary/5',
            ),
            'aria-label': 'Seleccionar archivo CSV',
          })}
        >
          <input {...getInputProps()} />
          <div className="bg-primary/10 text-primary flex size-11 items-center justify-center rounded-full">
            <FileUp className="size-5" aria-hidden />
          </div>
          <p className="text-sm font-medium">
            {isDragActive ? 'Suelta el archivo aquí' : 'Arrastra tu archivo CSV o haz clic para elegirlo'}
          </p>
          <p className="text-muted-foreground text-xs">CSV separado por comas o punto y coma · máx. 1 MB</p>
        </div>
      )}

      <div className="bg-muted/40 flex flex-col gap-3 rounded-xl border p-4 text-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-0.5">
          <p className="font-medium">¿No tienes un archivo?</p>
          <p className="text-muted-foreground text-xs">
            Descarga la plantilla, complétala en Excel o Google Sheets y guárdala como CSV. Categorías válidas: Productos,
            Horarios, Envíos, Pagos, Devoluciones y Otros (vacío = Otros).
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="shrink-0"
          onClick={() => downloadTextFile('plantilla-base-conocimiento.csv', TEMPLATE)}
        >
          <Download aria-hidden /> Descargar plantilla
        </Button>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={onDone} disabled={importMutation.isPending}>
          Cancelar
        </Button>
        <Button onClick={runImport} disabled={!file || importMutation.isPending}>
          {importMutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
          Importar temas
        </Button>
      </DialogFooter>
    </div>
  )
}

function ImportResult({
  result,
  onAgain,
  onDone,
}: {
  result: KnowledgeImportResult
  onAgain: () => void
  onDone: () => void
}) {
  const failed = result.errors.length
  return (
    <div className="grid gap-5">
      <DialogHeader>
        <DialogTitle>Resultado de la importación</DialogTitle>
        <DialogDescription>
          Se procesaron {result.totalRows} {result.totalRows === 1 ? 'fila' : 'filas'} del archivo.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3 sm:grid-cols-2" role="status">
        <div className="border-success/30 bg-success/5 flex items-center gap-3 rounded-xl border p-4">
          <CheckCircle2 className="text-success size-6 shrink-0" aria-hidden />
          <div>
            <p className="font-display text-2xl font-semibold tabular-nums">{result.created}</p>
            <p className="text-muted-foreground text-sm">{result.created === 1 ? 'tema creado' : 'temas creados'}</p>
          </div>
        </div>
        <div
          className={cn(
            'flex items-center gap-3 rounded-xl border p-4',
            failed > 0 ? 'border-destructive/30 bg-destructive/5' : 'bg-muted/40',
          )}
        >
          <TriangleAlert
            className={cn('size-6 shrink-0', failed > 0 ? 'text-destructive' : 'text-muted-foreground')}
            aria-hidden
          />
          <div>
            <p className="font-display text-2xl font-semibold tabular-nums">{failed}</p>
            <p className="text-muted-foreground text-sm">{failed === 1 ? 'fila con error' : 'filas con error'}</p>
          </div>
        </div>
      </div>

      {failed > 0 && (
        <div className="overflow-hidden rounded-xl border">
          <div className="max-h-64 overflow-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">Filas que no se importaron</caption>
              <thead className="bg-muted/60 sticky top-0 text-left">
                <tr>
                  <th scope="col" className="w-16 px-3 py-2 font-medium">
                    Fila
                  </th>
                  <th scope="col" className="px-3 py-2 font-medium">
                    Tema
                  </th>
                  <th scope="col" className="px-3 py-2 font-medium">
                    Motivo
                  </th>
                </tr>
              </thead>
              <tbody>
                {result.errors.map((e) => (
                  <tr key={e.row} className="border-t align-top">
                    <td className="px-3 py-2 tabular-nums">{e.row}</td>
                    <td className="text-muted-foreground max-w-48 truncate px-3 py-2" title={e.title ?? undefined}>
                      {e.title ?? '—'}
                    </td>
                    <td className="px-3 py-2">{e.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={onAgain}>
          Importar otro archivo
        </Button>
        <Button onClick={onDone}>Listo</Button>
      </DialogFooter>
    </div>
  )
}
