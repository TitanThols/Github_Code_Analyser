/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Absolute API origin when the SPA is hosted separately from the API. Empty means same origin. */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
