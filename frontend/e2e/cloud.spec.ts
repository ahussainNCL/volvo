import { test, expect } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createServer } from 'node:net';

let cloud: ChildProcess; let helper: ChildProcess | undefined; let directory: string; let origin: string;
const password = randomBytes(32).toString('hex');
async function stop(process?: ChildProcess) {
  if (process && process.exitCode === null) { process.kill('SIGINT'); await new Promise(resolve => process.once('exit', resolve)); }
}
test.beforeAll(async () => {
  directory = await mkdtemp(join(tmpdir(), 'nordlink-cloud-'));
  const listener = createServer(); await new Promise<void>(resolve => listener.listen(0, '127.0.0.1', resolve));
  const address = listener.address(); if (!address || typeof address === 'string') throw new Error('No test port');
  origin = `http://127.0.0.1:${address.port}`; await new Promise<void>(resolve => listener.close(() => resolve()));
  cloud = spawn('dotnet', [resolve('../artifacts/cloud/Nordlink.Cloud.dll'), '--development-loopback'], {
    env: { ...process.env, NORDLINK_PUBLIC_ORIGIN: origin, NORDLINK_ADMIN_PASSWORD: password }, stdio: 'ignore',
  });
  for (let i = 0; i < 150; i++) {
    try { if ((await fetch(origin + '/health')).ok) return; } catch { /* wait for actual readiness */ }
    if (cloud.exitCode !== null) throw new Error('Cloud exited before readiness');
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Cloud not ready');
});
test.afterAll(async () => { await stop(helper); await stop(cloud); if (directory) await rm(directory, { recursive: true, force: true }); });
test('online sign-in, one-use pairing, isolated helper relay, progress/live/CSV and unpair', async ({ page, browser }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  expect((await page.request.get(origin + '/api/status')).status()).toBe(401);
  expect((await page.request.post(origin + '/api/cloud/login', { data: { password } })).status()).toBe(403);
  expect((await page.request.post(origin + '/api/cloud/login', { headers: { Origin: 'https://other.invalid' }, data: { password } })).status()).toBe(403);
  await page.goto(origin);
  await page.getByLabel('Workspace password').fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Pair this browser with your PC' })).toBeVisible();
  await page.getByRole('button', { name: 'Create pairing code' }).click();
  const code = await page.locator('.pairing-code').innerText();
  helper = spawn('dotnet', [resolve('../artifacts/helper/Nordlink.Helper.dll'), '--cloud', origin,
    '--gateway-root', resolve('../artifacts/gateway'), '--data-root', directory, '--development-loopback'], {
    env: { ...process.env, NORDLINK_PAIRING_CODE: code }, stdio: 'ignore',
  });
  await expect(page.getByText('NORDLINK ONLINE', { exact: true })).toBeVisible({ timeout: 25_000 });
  await expect(page.getByText('SignalR: Connected')).toBeVisible();
  expect((await page.request.post(origin + '/api/helper/pair', { headers: { Origin: origin }, data: { code } })).status()).toBe(401);
  expect((await page.request.post(origin + '/api/credentials/not-an-id/reveal', { headers: { Origin: origin }, data: {} })).status()).toBe(409);
  const other = await browser.newContext();
  expect((await other.request.post(origin + '/api/cloud/login', { headers: { Origin: origin }, data: { password } })).status()).toBe(204);
  expect((await other.request.get(origin + '/api/cloud/session')).status()).toBe(200);
  expect((await other.request.get(origin + '/api/cloud/session')).ok()).toBe(true);
  const otherStatus = await (await other.request.get(origin + '/api/cloud/session')).json(); expect(otherStatus.helperConnected).toBe(false);
  expect((await other.request.get(origin + '/api/status')).status()).toBe(503); await other.close();
  await page.getByRole('button', { name: 'Simulation Isolated demo data' }).click();
  await page.getByRole('button', { name: 'Connect simulation' }).click();
  await page.getByRole('button', { name: 'Review exercise', exact: true }).click();
  await page.getByRole('button', { name: 'Execute reviewed exercise' }).click();
  await expect(page.getByText('Simulation exercise completed. No vehicle write.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Live Data', exact: true }).click();
  await page.getByRole('button', { name: 'Start fixture stream' }).click();
  await expect(page.getByText(/Latest simulation stream sample:/)).toBeVisible();
  await page.getByRole('button', { name: 'Stop fixture stream' }).click();
  await page.getByRole('button', { name: 'Create fixture recording' }).click();
  await expect(page.locator('tbody tr')).toHaveCount(25);
  const download = page.waitForEvent('download'); await page.getByRole('link', { name: 'Download CSV' }).click();
  expect((await download).suggestedFilename()).toBe('nordlink-recording.csv');
  await page.screenshot({ path: '../test-results/cloud-connected.png', fullPage: true });
  await page.getByRole('button', { name: 'Unpair helper' }).click();
  await expect(page.getByRole('heading', { name: 'Pair this browser with your PC' })).toBeVisible();
  expect((await page.request.get(origin + '/api/status')).status()).toBe(503);
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  expect((await page.request.get(origin + '/api/status')).status()).toBe(401);
  expect(errors).toEqual([]);
});
