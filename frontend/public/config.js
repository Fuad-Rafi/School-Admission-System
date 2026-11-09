// Local development default. The nginx entrypoint rewrites this file at container start
// using the API_BASE_URL environment variable.
window.__APP_CONFIG__ = window.__APP_CONFIG__ || {
  apiBaseUrl: "http://localhost:8080",
};
