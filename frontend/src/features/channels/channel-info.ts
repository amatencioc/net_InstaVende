import type { ChannelSummary, ChannelType } from './api'

/** Enlaces oficiales de Meta usados en las guías. */
export const metaLinks = {
  apps: 'https://developers.facebook.com/apps',
  businessSuite: 'https://business.facebook.com',
  systemUsers: 'https://business.facebook.com/settings/system-users',
  whatsappDocs: 'https://developers.facebook.com/docs/whatsapp/cloud-api/get-started',
  whatsappPricing: 'https://developers.facebook.com/docs/whatsapp/pricing',
  messengerDocs: 'https://developers.facebook.com/docs/messenger-platform/getting-started',
  instagramDocs: 'https://developers.facebook.com/docs/messenger-platform/instagram',
  webhooksDocs: 'https://developers.facebook.com/docs/graph-api/webhooks/getting-started',
} as const

export interface GuideStep {
  title: string
  description: string
  link?: { href: string; label: string }
}

/** Campo del formulario de Meta que identifica la cuenta de cada canal. */
export type AccountField = 'phoneNumberId' | 'pageId' | 'instagramAccountId'

export interface MetaChannelInfo {
  /** Nombre de la integración en la pestaña y el título del formulario. */
  title: string
  description: string
  accountField: AccountField
  accountLabel: string
  accountPlaceholder: string
  accountHelp: string
  tokenLabel: string
  tokenHelp: string
  appSecretHelp: string
  /** Campos a los que hay que suscribir el webhook en Meta. */
  webhookFields: string
  docsUrl: string
  guide: GuideStep[]
}

const appSecretHelp =
  'Meta for Developers › tu app › Configuración de la app › Básica › «Clave secreta de la app». La usamos para comprobar que cada mensaje viene realmente de Meta.'

const createAppStep: GuideStep = {
  title: 'Crea una app en Meta for Developers',
  description: 'Elige el tipo «Empresa» y vincúlala al portfolio comercial de tu negocio.',
  link: { href: metaLinks.apps, label: 'Abrir Meta for Developers' },
}

const appSecretStep: GuideStep = {
  title: 'Copia la clave secreta de la app',
  description: 'En Configuración de la app › Básica pulsa «Mostrar» junto a la clave secreta y pégala aquí.',
}

const finishStep: GuideStep = {
  title: 'Guarda, prueba y activa',
  description: 'Guarda los datos en esta página, pulsa «Probar conexión» y activa el canal.',
}

export const metaChannelInfo: Record<ChannelType, MetaChannelInfo> = {
  WhatsApp: {
    title: 'WhatsApp Cloud API',
    description: 'API oficial de WhatsApp Business: la opción más estable para volumen alto de mensajes.',
    accountField: 'phoneNumberId',
    accountLabel: 'Identificador del número de teléfono',
    accountPlaceholder: 'Ej.: 106540352242922',
    accountHelp:
      'Está en Meta for Developers › tu app › WhatsApp › Configuración de la API. Es un número largo, distinto de tu número de teléfono.',
    tokenLabel: 'Token de acceso',
    tokenHelp:
      'Usa un token permanente de un usuario del sistema con los permisos whatsapp_business_messaging y whatsapp_business_management. El token temporal de prueba caduca en 24 horas.',
    appSecretHelp,
    webhookFields: 'messages',
    docsUrl: metaLinks.whatsappDocs,
    guide: [
      { ...createAppStep, description: `${createAppStep.description} Luego añade el producto WhatsApp.` },
      {
        title: 'Copia los identificadores',
        description:
          'En WhatsApp › Configuración de la API copia el identificador del número de teléfono y el de la cuenta de WhatsApp Business.',
      },
      {
        title: 'Genera un token permanente',
        description:
          'En Meta Business Suite crea un usuario del sistema, asígnale la app y la cuenta de WhatsApp y genera un token con los permisos whatsapp_business_messaging y whatsapp_business_management.',
        link: { href: metaLinks.systemUsers, label: 'Abrir usuarios del sistema' },
      },
      appSecretStep,
      {
        title: 'Configura el webhook',
        description:
          'En WhatsApp › Configuración pega la URL y el token de verificación de esta página, pulsa «Verificar y guardar» y suscríbete al campo messages.',
      },
      finishStep,
    ],
  },
  FacebookMessenger: {
    title: 'Messenger',
    description: 'Responde automáticamente los mensajes que llegan a tu página de Facebook.',
    accountField: 'pageId',
    accountLabel: 'Identificador de la página de Facebook',
    accountPlaceholder: 'Ej.: 102345678901234',
    accountHelp:
      'En tu página de Facebook › Información › Transparencia de la página, o en Meta for Developers › Messenger › Configuración de la API.',
    tokenLabel: 'Token de acceso de la página',
    tokenHelp:
      'Genéralo en Messenger › Configuración de la API › «Generar token» para tu página (permiso pages_messaging). Para que no caduque, genéralo con un usuario del sistema.',
    appSecretHelp,
    webhookFields: 'messages, messaging_postbacks',
    docsUrl: metaLinks.messengerDocs,
    guide: [
      { ...createAppStep, description: `${createAppStep.description} Luego añade el producto Messenger.` },
      {
        title: 'Conecta tu página y genera su token',
        description:
          'En Messenger › Configuración de la API agrega tu página de Facebook y genera su token de acceso con el permiso pages_messaging.',
      },
      {
        title: 'Copia el identificador de la página',
        description: 'Aparece junto al nombre de la página en la misma pantalla.',
      },
      appSecretStep,
      {
        title: 'Configura el webhook',
        description:
          'En Messenger › Webhooks pega la URL y el token de verificación, verifica y suscribe tu página a messages y messaging_postbacks.',
      },
      finishStep,
    ],
  },
  Instagram: {
    title: 'Instagram',
    description: 'Atiende los mensajes directos de tu cuenta profesional de Instagram.',
    accountField: 'instagramAccountId',
    accountLabel: 'Identificador de la cuenta de Instagram',
    accountPlaceholder: 'Ej.: 17841400000000000',
    accountHelp:
      'Tu cuenta debe ser profesional (empresa o creador) y estar vinculada a una página de Facebook. Lo encuentras en Meta Business Suite › Configuración › Cuentas de Instagram.',
    tokenLabel: 'Token de acceso de la página vinculada',
    tokenHelp:
      'Token de la página de Facebook conectada a tu Instagram, con los permisos instagram_basic, instagram_manage_messages y pages_manage_metadata.',
    appSecretHelp,
    webhookFields: 'messages',
    docsUrl: metaLinks.instagramDocs,
    guide: [
      {
        title: 'Prepara tu cuenta de Instagram',
        description:
          'Conviértela en cuenta profesional (empresa o creador) y vincúlala a tu página de Facebook.',
      },
      {
        ...createAppStep,
        description: `${createAppStep.description} Luego añade el producto Messenger (incluye Instagram).`,
      },
      {
        title: 'Genera el token de la página',
        description:
          'Genera el token de la página vinculada con los permisos instagram_basic, instagram_manage_messages y pages_manage_metadata, y copia el identificador de la cuenta de Instagram.',
      },
      appSecretStep,
      {
        title: 'Configura el webhook',
        description:
          'En la configuración de Instagram de tu app pega la URL y el token de verificación, verifica y suscríbete al campo messages.',
      },
      {
        title: 'Permite el acceso a los mensajes',
        description:
          'En la app de Instagram ve a Configuración › Privacidad › Mensajes › Herramientas conectadas y activa «Permitir acceso a los mensajes».',
      },
      finishStep,
    ],
  },
}

/** Texto con el número o la cuenta visible del canal. */
export function channelAccountLabel(summary: ChannelSummary): string | null {
  if (summary.phoneNumber) return `+${summary.phoneNumber}`
  if (summary.channel === 'FacebookMessenger' && summary.pageId) return `Página ${summary.pageId}`
  if (summary.channel === 'Instagram' && summary.instagramAccountId)
    return `Cuenta ${summary.instagramAccountId}`
  if (summary.channel === 'WhatsApp' && summary.whatsAppMode === 'CloudApi' && summary.phoneNumberId)
    return `ID ${summary.phoneNumberId}`
  return null
}

/** Meta solo entrega webhooks a URLs públicas con HTTPS. */
export function isPublicHttpsUrl(url: string): boolean {
  try {
    const { protocol, hostname } = new URL(url)
    const local =
      hostname === 'localhost' ||
      hostname.endsWith('.localhost') ||
      /^(127\.|10\.|192\.168\.|0\.0\.0\.0)/.test(hostname)
    return protocol === 'https:' && !local
  } catch {
    return false
  }
}
