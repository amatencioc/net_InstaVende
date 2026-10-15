/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** URL de la API si vive en otro origen del mismo sitio (vacío = mismo origen). */
  readonly VITE_API_BASE_URL?: string
  /** Solo desarrollo: credenciales precargadas en el login. */
  readonly VITE_DEV_LOGIN_EMAIL?: string
  readonly VITE_DEV_LOGIN_PASSWORD?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
