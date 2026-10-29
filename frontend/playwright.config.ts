import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', testMatch: 'workspace.spec.ts', fullyParallel: false, workers: 1, timeout: 45_000,
  reporter: [['list'], ['json', { outputFile: '../test-results/browser.json' }]],
  use: { browserName: 'chromium', headless: true, launchOptions: process.env.NORDLINK_CHROMIUM ? { executablePath: process.env.NORDLINK_CHROMIUM } : {} },
});
