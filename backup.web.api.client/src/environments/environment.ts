export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7157/api',
  /** Appel direct au backend (évite le proxy Vite qui casse les uploads multipart). */
  pythonServiceUrl: 'https://127.0.0.1:7157/api/python',
  enablePythonTest: true,
  /** Base HTTP des images produits ERP (PicName), alignée sur ErpSync:ImageBaseUrl. */
  erpImageBaseUrl: 'http://eurobrico.ddns.net:15022',
};


