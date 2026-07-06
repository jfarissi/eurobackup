const { env } = require('process');

// Cible fixe du backend Backup (évite un mauvais port si variables ASP.NET absentes au npm start seul).
const target = env.BACKUP_API_URL || 'https://127.0.0.1:7157';

console.log('[proxy] /api ->', target);

const PROXY_CONFIG = [
  {
    context: ['/api'],
    target,
    secure: false,
    changeOrigin: true,
  },
];

module.exports = PROXY_CONFIG;
