import type { ApiError } from './contracts';
export async function api<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`/api/${path}`, {
    method: body === undefined ? 'GET' : 'POST', credentials: 'same-origin', cache: 'no-store',
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const error = await response.json() as ApiError;
    throw new Error([error.reason, ...(error.missingPrerequisites ?? [])].join(' · '));
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
