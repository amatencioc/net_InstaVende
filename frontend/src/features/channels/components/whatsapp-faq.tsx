import { Link } from '@tanstack/react-router'
import { ArrowRight, ExternalLink } from 'lucide-react'
import type { ReactNode } from 'react'
import { metaLinks } from '../channel-info'
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion'

function ExtLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className="text-primary inline-flex items-center gap-1 font-medium hover:underline"
    >
      {children}
      <ExternalLink className="size-3" aria-hidden />
      <span className="sr-only">(se abre en una pestaña nueva)</span>
    </a>
  )
}

const faqs: { q: string; a: ReactNode }[] = [
  {
    q: '¿Qué tipo de conexión elijo?',
    a: (
      <>
        <p>Tienes dos opciones:</p>
        <ul className="list-disc space-y-2 pl-5">
          <li>
            <strong>Código QR (WhatsApp Web).</strong> Es la forma más rápida: usas tu número de siempre, sin
            trámites con Meta, y sigues usando WhatsApp en tu celular. Ideal para empezar o si recibes un
            volumen moderado de mensajes. Funciona igual que WhatsApp Web, por lo que tu celular debe mantener
            WhatsApp instalado y con internet.
          </li>
          <li>
            <strong>API oficial de WhatsApp Business (Cloud API).</strong> Recomendada si recibes muchos
            mensajes al día, trabajas con varios agentes o necesitas la máxima estabilidad: no depende de tu
            celular y cumple las políticas de Meta. Requiere un portfolio comercial de Meta y configurar una
            app en Meta for Developers.
          </li>
        </ul>
        <p>
          Nuestra recomendación: empieza con el código QR y, cuando tu volumen crezca, pasa a la{' '}
          <Link
            to="/channels/meta"
            search={{ channel: 'WhatsApp' }}
            className="text-primary font-medium hover:underline"
          >
            API oficial
          </Link>
          .
        </p>
      </>
    ),
  },
  {
    q: '¿Necesito tener una cuenta en Meta Business?',
    a: (
      <>
        <p>
          <strong>Con código QR, no.</strong> Solo necesitas tu celular con WhatsApp.
        </p>
        <p>
          <strong>Con la API oficial, sí.</strong> Necesitas un portfolio comercial en Meta Business Suite y
          una app en Meta for Developers. Crear ambos es gratis.
        </p>
      </>
    ),
  },
  {
    q: '¿Qué es el Portfolio Meta y cómo dejarlo listo?',
    a: (
      <>
        <p>
          El portfolio comercial (antes «Business Manager») agrupa los activos de tu empresa en Meta: páginas
          de Facebook, cuentas de Instagram, cuentas de WhatsApp Business, personas con acceso y métodos de
          pago.
        </p>
        <ol className="list-decimal space-y-1 pl-5">
          <li>
            Créalo en <ExtLink href={metaLinks.businessSuite}>Meta Business Suite</ExtLink> con el nombre de
            tu negocio.
          </li>
          <li>Completa los datos de la empresa: dirección, sitio web y teléfono.</li>
          <li>Verifica tu empresa en el Centro de seguridad para ampliar los límites de mensajes.</li>
          <li>Agrega un método de pago si vas a enviar mensajes de plantilla (los que inicias tú).</li>
        </ol>
        <p>
          Ten en cuenta que en un portfolio nuevo algunas funciones pueden tardar entre 24 y 72 horas en
          habilitarse.
        </p>
      </>
    ),
  },
  {
    q: '¿Qué pasa si no conecto mi WhatsApp?',
    a: (
      <p>
        Tu vendedor IA no podrá recibir ni responder mensajes de WhatsApp. Puedes configurar productos, pagos
        y probarlo en{' '}
        <Link to="/seller/test" className="text-primary font-medium hover:underline">
          Probar vendedor
        </Link>
        , pero tus clientes no lo verán hasta que conectes al menos un canal. Si lo prefieres, también puedes
        empezar con Messenger o Instagram.
      </p>
    ),
  },
  {
    q: "¿Debo seleccionar 'Compartir chats'?",
    a: (
      <>
        <p>
          Esa opción aparece al registrar en la API oficial un número que ya usa la app WhatsApp Business:
          Meta pregunta si quieres compartir el historial de chats con la plataforma conectada.
        </p>
        <p>
          No es obligatorio. InstaVende trabaja con los mensajes nuevos que lleguen después de conectar, así
          que tu vendedor funcionará igual elijas lo que elijas. Con el código QR esta pregunta no aparece.
        </p>
      </>
    ),
  },
  {
    q: '¿Cuánto cuesta usar la API de WhatsApp Business?',
    a: (
      <>
        <p>
          <strong>Con código QR</strong> no hay costos de Meta.
        </p>
        <p>
          <strong>Con la API oficial</strong>, responder a un cliente dentro de las 24 horas siguientes a su
          último mensaje no tiene costo. Los mensajes de plantilla que tú inicias (marketing, utilidad o
          autenticación) se cobran por mensaje y la tarifa depende del país del cliente. Consulta los{' '}
          <ExtLink href={metaLinks.whatsappPricing}>precios vigentes de Meta</ExtLink>.
        </p>
      </>
    ),
  },
  {
    q: '¿Puedo usar un WhatsApp normal?',
    a: (
      <>
        <p>
          Sí, con el código QR funciona tanto WhatsApp como WhatsApp Business. Te recomendamos WhatsApp
          Business (es gratis) porque muestra un perfil de empresa con horario, dirección y catálogo.
        </p>
        <p className="border-warning/40 bg-warning/10 rounded-lg border p-3">
          <strong>Importante:</strong> el modo QR usa WhatsApp Web, que no es la API oficial de Meta. Mantén
          tu celular con batería e internet, no cierres la sesión desde «Dispositivos vinculados» y evita los
          envíos masivos: WhatsApp puede restringir números que se comportan como spam. Si tu negocio maneja
          mucho volumen, usa la API oficial.
        </p>
      </>
    ),
  },
  {
    q: '¿Qué pasa si cometo un error al conectar?',
    a: (
      <ul className="list-disc space-y-2 pl-5">
        <li>
          <strong>Código QR:</strong> pulsa «Generar QR nuevo» y vuelve a escanear. Si vinculaste el número
          equivocado, desconéctalo y escanea con el correcto. Si el servicio no responde, usa «Reiniciar
          servicio».
        </li>
        <li>
          <strong>API oficial:</strong> corrige los datos y guarda de nuevo. Si dejas vacíos el token o la
          clave secreta se conservan los actuales. Usa «Probar conexión» para confirmar que todo está bien.
        </li>
        <li>
          Nada se pierde: tus productos, configuración y conversaciones se conservan aunque desconectes el
          canal.
        </li>
      </ul>
    ),
  },
]

/** Preguntas frecuentes sobre la conexión de WhatsApp. */
export function WhatsAppFaq() {
  return (
    <section aria-labelledby="wa-faq-title" className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <h2 id="wa-faq-title" className="text-lg font-semibold">
          Dudas frecuentes
        </h2>
        <Link
          to="/channels/meta"
          search={{ channel: 'WhatsApp' }}
          className="text-primary inline-flex items-center gap-1 text-sm font-medium hover:underline"
        >
          Explorar la guía detallada <ArrowRight className="size-3.5" aria-hidden />
        </Link>
      </div>
      <Accordion type="single" collapsible className="bg-card rounded-xl border px-4 sm:px-6">
        {faqs.map((item, i) => (
          <AccordionItem key={item.q} value={`faq-${i}`}>
            <AccordionTrigger className="text-base">{item.q}</AccordionTrigger>
            <AccordionContent className="text-muted-foreground space-y-3 leading-relaxed">
              {item.a}
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>
    </section>
  )
}
