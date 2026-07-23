// @ts-check
const { defineConfig } = require('@playwright/test');

/** URL complète de l'assistant, ex. http://euro13/assistant */
const assistantUrl = (process.env.ASSISTANT_BASE_URL || 'http://euro13/assistant').replace(/\/$/, '');

module.exports = defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 180_000,
  expect: { timeout: 30_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    headless: process.env.HEADED !== '1',
    launchOptions: {
      slowMo: process.env.SLOW_MO ? Number(process.env.SLOW_MO) : 0,
    },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'fr-BE',
  },
  metadata: { assistantUrl },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
