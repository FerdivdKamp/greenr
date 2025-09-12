/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API origin for the backend (e.g. https://api.example.com). */
  readonly VITE_API_BASE_URL?: string; // optional: absent in dev when proxying
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
