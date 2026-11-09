const runtimeApiBaseUrl = window.__APP_CONFIG__?.apiBaseUrl?.trim();
const buildApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

export const API_BASE_URL = (
  runtimeApiBaseUrl ||
  buildApiBaseUrl ||
  'http://localhost:8080'
).replace(/\/+$/, '');
