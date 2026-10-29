import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', testMatch: 'cloud.spec.ts', workers: 1, timeout: 60_000,
  reporter: [['list'], ['json', { outputFile: '../test-results/cloud-browser.json' }]],
  use: { browserName: 'chromium', headless: true, launchOptions: process.env.NORDLINK_CHROMIUM ? { executablePath: process.env.NORDLINK_CHROMIUM } : {} },
});
