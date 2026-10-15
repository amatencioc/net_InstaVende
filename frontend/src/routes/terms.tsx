import { createFileRoute, Link } from '@tanstack/react-router'
import { LegalPage } from '@/features/landing/components/legal-page'

export const Route = createFileRoute('/terms')({
  head: () => ({
    meta: [
      { title: 'Términos y condiciones — InstaVende' },
      {
        name: 'description',
        content:
          'Condiciones de uso del servicio InstaVende, vendedor con IA para WhatsApp, Instagram y Messenger.',
      },
    ],
  }),
  component: TermsPage,
})

function TermsPage() {
  return (
    <LegalPage title="Términos y condiciones" updatedAt="14 de octubre de 2026">
      <section>
        <h2>1. Aceptación</h2>
        <p>
          Estos términos regulan el uso de InstaVende, servicio operado por <strong>[Razón social]</strong>,
          con RUC <strong>[número de RUC]</strong> y domicilio en <strong>[dirección], Lima, Perú</strong>. Al
          crear una cuenta o usar el servicio aceptas estos términos y nuestra{' '}
          <Link to="/privacy" className="text-primary font-medium underline-offset-4 hover:underline">
            Política de privacidad
          </Link>
          . Si los aceptas en nombre de un negocio, declaras tener facultades para obligarlo.
        </p>
      </section>

      <section>
        <h2>2. El servicio</h2>
        <p>
          InstaVende ofrece un vendedor con inteligencia artificial que responde mensajes en los canales que
          conectes (WhatsApp, Instagram y Messenger), junto con herramientas para gestionar catálogo,
          conversaciones, pedidos, recordatorios y métricas. Las funciones disponibles dependen del plan
          contratado.
        </p>
      </section>

      <section>
        <h2>3. Tu cuenta</h2>
        <ul>
          <li>Debes proporcionar información veraz y mantener la confidencialidad de tu contraseña.</li>
          <li>Eres responsable de la actividad realizada desde tu cuenta y por los usuarios que invites.</li>
          <li>Debes tener al menos 18 años y capacidad legal para contratar.</li>
        </ul>
      </section>

      <section>
        <h2>4. Uso aceptable</h2>
        <p>Te comprometes a no usar InstaVende para:</p>
        <ul>
          <li>Enviar spam, mensajes masivos no solicitados o contenido engañoso.</li>
          <li>Vender productos o servicios ilegales, falsificados o que infrinjan derechos de terceros.</li>
          <li>
            Incumplir las políticas de las plataformas de mensajería (por ejemplo, las políticas comerciales
            de Meta y WhatsApp).
          </li>
          <li>Intentar vulnerar la seguridad del servicio o acceder a datos de otros negocios.</li>
        </ul>
      </section>

      <section>
        <h2>5. Respuestas generadas por IA</h2>
        <p>
          El vendedor IA responde con base en la información y las reglas que configures (catálogo, precios,
          descuentos, políticas). Aunque aplicamos controles, las respuestas generadas por IA pueden contener
          errores. Eres responsable de revisar la configuración, la información de tu catálogo y los pedidos
          generados, así como de cumplir la normativa de protección al consumidor frente a tus clientes.
        </p>
      </section>

      <section>
        <h2>6. Canales de terceros</h2>
        <p>
          La conexión con WhatsApp, Instagram y Messenger depende de servicios de terceros. No somos
          responsables por interrupciones, bloqueos de números o cuentas, ni por cambios en las condiciones de
          esas plataformas.
        </p>
      </section>

      <section>
        <h2>7. Planes, pagos y cancelación</h2>
        <ul>
          <li>Los precios se expresan en soles (S/) y no incluyen IGV, salvo que se indique lo contrario.</li>
          <li>
            Los planes de pago se renuevan automáticamente por el mismo periodo (mensual o anual) hasta que
            los canceles.
          </li>
          <li>
            Puedes cancelar en cualquier momento; el plan seguirá activo hasta el final del periodo pagado.
          </li>
          <li>[Completar: política de reembolsos, medios de pago y emisión de comprobantes electrónicos.]</li>
        </ul>
      </section>

      <section>
        <h2>8. Tus datos y contenido</h2>
        <p>
          El contenido que cargas (catálogo, imágenes, conversaciones) sigue siendo tuyo. Nos otorgas una
          licencia limitada para usarlo únicamente con el fin de prestar el servicio. Puedes exportar o
          eliminar tu información según las funciones disponibles en el panel.
        </p>
      </section>

      <section>
        <h2>9. Disponibilidad y limitación de responsabilidad</h2>
        <p>
          Trabajamos para mantener el servicio disponible, pero puede haber interrupciones por mantenimiento o
          causas ajenas a nosotros. En la medida permitida por la ley, nuestra responsabilidad total se limita
          al monto pagado por el servicio en los <strong>[3]</strong> meses anteriores al hecho que la
          origine, y no respondemos por lucro cesante ni daños indirectos.
        </p>
      </section>

      <section>
        <h2>10. Suspensión</h2>
        <p>
          Podemos suspender o cerrar cuentas que incumplan estos términos o pongan en riesgo la seguridad del
          servicio, avisándote cuando sea razonablemente posible.
        </p>
      </section>

      <section>
        <h2>11. Cambios</h2>
        <p>
          Podemos modificar estos términos. Te avisaremos con anticipación razonable cuando los cambios sean
          relevantes; si sigues usando el servicio después de su entrada en vigor, se entenderán aceptados.
        </p>
      </section>

      <section>
        <h2>12. Ley aplicable y jurisdicción</h2>
        <p>
          Estos términos se rigen por las leyes de la República del Perú. Cualquier controversia se someterá a
          los jueces y tribunales de <strong>[Lima Cercado]</strong>, sin perjuicio de los derechos que la
          normativa de protección al consumidor reconozca, incluido el Libro de Reclamaciones.
        </p>
      </section>
    </LegalPage>
  )
}
