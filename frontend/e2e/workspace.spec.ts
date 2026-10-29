import { test, expect } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

let process: ChildProcess; let data: string; let launch: { url: string; origin: string };
test.beforeAll(async () => {
  data = await mkdtemp(join(tmpdir(), 'nordlink-browser-'));
  process = spawn('dotnet', [resolve('../artifacts/gateway/Nordlink.Gateway.dll'), '--data-root', data], { stdio: 'ignore' });
  for (let i = 0; i < 200; i++) {
    try { launch = JSON.parse(await readFile(join(data, 'launch.json'), 'utf8')); return; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
  }
  throw new Error('Published gateway did not start.');
});
test.afterAll(async () => {
  if (process && process.exitCode === null) { process.kill(); await new Promise(resolve => process.once('exit', resolve)); }
  if (data) await rm(data, { recursive: true, force: true });
});
test('guided simulation, approved operation, SignalR, recordings, isolation and responsive navigation', async ({ page }) => {
  const failures: string[] = []; page.on('pageerror', error => failures.push(error.message));
  await page.goto(launch.url);
  await expect(page.getByRole('heading', { name: 'Start with a trusted connection.' })).toBeVisible();
  await expect.poll(() => new URL(page.url()).hash).toBe('');
  await expect(page.getByText('SignalR: Connected')).toBeVisible();
  await expect(page.getByText('Not identified', { exact: true })).toHaveCount(2);
  await page.getByRole('button', { name: 'Simulation Isolated demo data' }).click();
  await expect(page.getByText('SIMULATION ENVIRONMENT', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Connect simulation' }).click();
  await expect(page.getByRole('button', { name: 'Disconnect', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Review exercise', exact: true }).click();
  await expect(page.getByText('Reviewed summary', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Execute reviewed exercise' }).click();
  await expect(page.getByText('completed', { exact: true })).toBeVisible();
  await expect(page.getByText('Simulation exercise completed. No vehicle write.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Live Data', exact: true }).click();
  await page.getByRole('button', { name: 'Start fixture stream' }).click();
  await expect(page.getByText(/Latest simulation stream sample:/)).toBeVisible();
  await page.getByRole('button', { name: 'Stop fixture stream' }).click();
  await page.getByRole('button', { name: 'Create fixture recording' }).click();
  await expect(page.getByRole('img', { name: /Recorded fixture voltage graph/ })).toBeVisible();
  await expect(page.locator('tbody tr')).toHaveCount(25);
  await page.getByRole('checkbox', { name: 'Favourite parameter' }).check();
  await page.getByRole('button', { name: 'Replay recording' }).click();
  await expect(page.getByRole('button', { name: 'Stop replay' })).toBeVisible();
  await page.getByRole('button', { name: 'Stop replay' }).click();
  const download = page.waitForEvent('download'); await page.getByRole('link', { name: 'Download CSV' }).click();
  const file = await download; expect(file.suggestedFilename()).toBe('nordlink-recording.csv');
  const csv = await readFile((await file.path())!, 'utf8'); expect(csv.split(',,true').length - 1).toBe(2);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('button', { name: /Guided/ }).click();
  await expect(page.getByRole('heading', { name: 'Start with a trusted connection.' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Disconnect', exact: true }).click();
  await page.getByRole('button', { name: 'Hardware Windows + TOPDON' }).click();
  await expect(page.getByText('SIMULATION ENVIRONMENT', { exact: true })).toHaveCount(0);
  await expect(page.getByText('No operations recorded in this environment.')).toBeVisible();
  await expect(page.getByText('Simulation exercise completed. No vehicle write.', { exact: true })).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: '../test-results/guided-mobile.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: '../test-results/guided-desktop.png', fullPage: true });
  expect(failures).toEqual([]);
});
