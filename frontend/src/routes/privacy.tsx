import { createFileRoute } from '@tanstack/react-router'
import { LegalPage } from '@/features/landing/components/legal-page'
import { CONTACT_EMAIL } from '@/features/landing/data'

export const Route = createFileRoute('/privacy')({
  head: () => ({
    meta: [
      { title: 'Política de privacidad — InstaVende' },
      {
        name: 'description',
        content:
          'Cómo InstaVende recopila, usa y protege los datos personales de sus usuarios y de los clientes de sus usuarios.',
      },
    ],
  }),
  component: PrivacyPage,
})

function PrivacyPage() {
  return (
    <LegalPage title="Política de privacidad" updatedAt="14 de octubre de 2026">
      <section>
        <h2>1. Quiénes somos</h2>
        <p>
          InstaVende es un servicio de software (SaaS) operado por <strong>[Razón social]</strong>, con RUC{' '}
          <strong>[número de RUC]</strong> y domicilio en <strong>[dirección], Lima, Perú</strong> (en
          adelante, “InstaVende”, “nosotros”). Esta política explica cómo tratamos los datos personales
          conforme a la Ley N.° 29733, Ley de Protección de Datos Personales, y su Reglamento.
        </p>
      </section>

      <section>
        <h2>2. Roles en el tratamiento de datos</h2>
        <ul>
          <li>
            <strong>Datos de nuestros usuarios</strong> (personas que crean una cuenta para su negocio):
            InstaVende actúa como titular del banco de datos y responsable del tratamiento.
          </li>
          <li>
            <strong>Datos de los clientes finales</strong> (personas que escriben a un negocio por WhatsApp,
            Instagram o Messenger): el negocio usuario es el responsable del tratamiento y InstaVende actúa
            como encargado, tratando esos datos solo para prestar el servicio y según sus instrucciones.
          </li>
        </ul>
      </section>

      <section>
        <h2>3. Datos que recopilamos</h2>
        <ul>
          <li>
            Datos de cuenta: nombre, apellidos, correo electrónico, contraseña (guardada como hash, nunca en
            texto plano) y rol.
          </li>
          <li>
            Datos del negocio: nombre comercial, país, moneda, zona horaria, catálogo, métodos de pago y zonas
            de entrega.
          </li>
          <li>
            Datos de conversaciones: mensajes, nombre visible, número o identificador del canal y pedidos
            generados a través de los canales que el negocio conecte.
          </li>
          <li>
            Datos técnicos: dirección IP, tipo de navegador, registros de acceso y cookies estrictamente
            necesarias.
          </li>
        </ul>
      </section>

      <section>
        <h2>4. Finalidades</h2>
        <ul>
          <li>
            Prestar el servicio: atender conversaciones con el vendedor IA, gestionar pedidos, recordatorios y
            métricas.
          </li>
          <li>Gestionar tu cuenta, la seguridad de la plataforma y la prevención de fraudes.</li>
          <li>Brindar soporte y enviarte comunicaciones operativas sobre el servicio.</li>
          <li>
            Enviarte novedades comerciales, solo si das tu consentimiento; puedes retirarlo en cualquier
            momento.
          </li>
        </ul>
        <p>No vendemos ni alquilamos datos personales a terceros.</p>
      </section>

      <section>
        <h2>5. Inteligencia artificial y proveedores</h2>
        <p>
          Para generar las respuestas del vendedor IA, el contenido de las conversaciones y la información del
          negocio se envía a proveedores de modelos de lenguaje que actúan como encargados del tratamiento.
          También usamos proveedores de infraestructura en la nube y las plataformas de mensajería que el
          negocio conecte (WhatsApp, Instagram y Messenger de Meta). Algunos de estos proveedores pueden estar
          fuera del Perú, por lo que se realiza flujo transfronterizo de datos con las garantías exigidas por
          la normativa.
        </p>
        <p>
          Lista de proveedores principales: <strong>[completar: proveedor de IA, hosting, correo]</strong>.
        </p>
      </section>

      <section>
        <h2>6. Conservación</h2>
        <p>
          Conservamos los datos mientras la cuenta esté activa y, después, durante el plazo necesario para
          cumplir obligaciones legales o atender reclamaciones (<strong>[plazo]</strong>). Al cerrar la
          cuenta, el negocio puede solicitar la eliminación de sus datos y los de sus clientes.
        </p>
      </section>

      <section>
        <h2>7. Seguridad</h2>
        <p>
          Aplicamos medidas técnicas y organizativas razonables: aislamiento de datos por negocio, cifrado de
          las credenciales de los canales, conexiones cifradas (HTTPS), cookies de sesión protegidas y control
          de acceso por roles. Ningún sistema es infalible; si detectamos un incidente que afecte tus datos,
          te lo comunicaremos conforme a la ley.
        </p>
      </section>

      <section>
        <h2>8. Tus derechos</h2>
        <p>
          Puedes ejercer tus derechos de información, acceso, rectificación, cancelación, oposición y
          tratamiento objetivo (derechos ARCO) escribiendo a <strong>{CONTACT_EMAIL}</strong> con una copia de
          tu documento de identidad. Si eres cliente final de un negocio que usa InstaVende, te recomendamos
          dirigirte primero a ese negocio. Si consideras que no atendimos tu solicitud, puedes acudir a la
          Autoridad Nacional de Protección de Datos Personales del Ministerio de Justicia y Derechos Humanos.
        </p>
      </section>

      <section>
        <h2>9. Cookies</h2>
        <p>
          Usamos cookies técnicas necesarias para iniciar sesión y proteger los formularios, y almacenamiento
          local para recordar preferencias como el tema claro u oscuro. No usamos cookies publicitarias de
          terceros.
        </p>
      </section>

      <section>
        <h2>10. Menores de edad</h2>
        <p>El servicio está dirigido a negocios y personas mayores de 18 años.</p>
      </section>

      <section>
        <h2>11. Cambios en esta política</h2>
        <p>
          Podemos actualizar esta política. Si los cambios son relevantes, te avisaremos por correo o dentro
          del panel antes de que entren en vigor.
        </p>
      </section>
    </LegalPage>
  )
}
