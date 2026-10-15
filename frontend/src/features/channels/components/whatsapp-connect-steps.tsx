const steps = [
  {
    title: 'Abre WhatsApp en tu celular',
    text: 'Usa WhatsApp o WhatsApp Business del número con el que atiendes a tus clientes.',
  },
  {
    title: 'Entra a Dispositivos vinculados',
    text: 'Android: menú ⋮ › Dispositivos vinculados. iPhone: Configuración › Dispositivos vinculados.',
  },
  {
    title: 'Escanea el código QR',
    text: 'Toca «Vincular un dispositivo» y apunta la cámara al código de esta página.',
  },
]

/** Los 3 pasos numerados para vincular WhatsApp por QR. */
export function WhatsAppConnectSteps() {
  return (
    <section aria-labelledby="wa-steps-title" className="space-y-3">
      <h2 id="wa-steps-title" className="font-semibold">
        Pasos para conectar
      </h2>
      <ol className="grid gap-3 sm:grid-cols-3 lg:grid-cols-1">
        {steps.map((step, i) => (
          <li key={step.title} className="bg-card flex gap-3 rounded-xl border p-4">
            <span
              className="bg-primary text-primary-foreground flex size-7 shrink-0 items-center justify-center rounded-full text-sm font-semibold"
              aria-hidden
            >
              {i + 1}
            </span>
            <div className="space-y-0.5">
              <p className="text-sm font-medium">{step.title}</p>
              <p className="text-muted-foreground text-xs">{step.text}</p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  )
}
