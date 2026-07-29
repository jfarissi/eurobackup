export const environment = {
  production: true,
  /** Remplacé au build Docker par CHATBOT_PUBLIC_URL (voir Dockerfile). */
  chatbotPublicUrl: 'CHATBOT_PUBLIC_URL_PLACEHOLDER',
  apiBaseUrl: '/api',
  signalRHubUrl: '/hubs/permissions',
  pythonServiceUrl: '/api/python',
  enablePythonTest: false,
  /** @deprecated Images passent par /api/erp-products/image (proxy HTTP → 15022). */
  erpImageBaseUrl: '/api/erp-products/image',
};

