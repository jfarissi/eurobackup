export const environment = {
  production: false,
  /** Nouveau chatbot Python (FastAPI + Angular) — port 81 en Docker prod. */
  chatbotPublicUrl: 'http://localhost:81',
  /** Same-origin via Angular proxy / SpaProxy (HTTP en local). */
  apiBaseUrl: '/api',
  /** Hub SignalR (direct API — le proxy WS Angular est fragile en local). */
  signalRHubUrl: 'http://127.0.0.1:5243/hubs/permissions',
  signalRSupplierQuotesHubUrl: 'http://127.0.0.1:5243/hubs/supplier-quotes',
  /** Appel direct au backend (évite le proxy Angular qui casse les uploads multipart). */
  pythonServiceUrl: 'http://127.0.0.1:5243/api/python',
  enablePythonTest: true,
  /** @deprecated Images passent par /api/erp-products/image (proxy HTTP → 15022). */
  erpImageBaseUrl: '/api/erp-products/image',
};
