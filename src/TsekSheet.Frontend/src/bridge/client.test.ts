import { afterEach, expect, test, vi } from 'vitest';
import { BridgeClient, BridgeError, BridgeTransportError } from './client';
import type { Transport } from './client';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

export function fakeTransport() {
  const listeners = new Set<(e: { data: unknown }) => void>();
  const sent: Record<string, unknown>[] = [];
  const transport: Transport = {
    postMessage: m => sent.push(m as Record<string, unknown>),
    addEventListener: (_, listener) => { listeners.add(listener); },
    removeEventListener: (_, listener) => { listeners.delete(listener); },
  };
  const emit = (data: unknown) => listeners.forEach(listener => listener({ data }));
  return { transport, sent, emit };
}
afterEach(() => vi.useRealTimers());

test('accepts the same metadata wire fixture used by C# tests', async () => {
  const fixture = JSON.parse(readFileSync(resolve(process.cwd(), '../../tests/contracts/protocol-v1.json'), 'utf8'));
  const fake = fakeTransport(); const client = new BridgeClient(fake.transport);
  const request = client.request('foundation.getInfo', {});
  fake.emit({ ...fixture.metadataResponse, requestId: request.id });
  expect(await request.promise).toEqual(fixture.metadataResponse.result);
  client.dispose();
});

test('correlates concurrent requests and ignores unexpected or malformed messages', async () => {
  const fake = fakeTransport(); const client = new BridgeClient(fake.transport);
  const first = client.request('diagnostics.cancel', { operationId: 'x' });
  const second = client.request('diagnostics.cancel', { operationId: 'y' });
  fake.emit(null); fake.emit('bad'); fake.emit({ protocolVersion: 9, type: 'response' });
  fake.emit({ protocolVersion: 1, type: 'response', requestId: first.id, ok: true, result: { status: 'invalid' } });
  fake.emit({ protocolVersion: 1, type: 'response', requestId: second.id, ok: true, result: { status: 'notRunning' } });
  fake.emit({ protocolVersion: 1, type: 'response', requestId: first.id, ok: true, result: { status: 'requested' } });
  expect(await second.promise).toEqual({ status: 'notRunning' });
  expect(await first.promise).toEqual({ status: 'requested' });
  client.dispose();
});

test('only forwards bounded progress for an active diagnostic request', async () => {
  const fake = fakeTransport(); const client = new BridgeClient(fake.transport); const progress = vi.fn(); client.onProgress = progress;
  const run = client.request('diagnostics.run', { steps: 3, delayMs: 10 });
  for (const [id, completed] of [['unknown', 1], [run.id, 999], [run.id, 1]] as const)
    fake.emit({ protocolVersion: 1, type: 'event', name: 'diagnostics.progress', operationId: id, progress: { completed, total: 3 } });
  expect(progress).toHaveBeenCalledTimes(1);
  fake.emit({ protocolVersion: 1, type: 'response', requestId: run.id, ok: true, result: { status: 'completed' } });
  await run.promise; client.dispose();
});

test('times out pending requests and rejects outstanding work on disposal', async () => {
  vi.useFakeTimers(); const fake = fakeTransport(); const client = new BridgeClient(fake.transport, 100);
  const request = client.request('foundation.getInfo', {});
  const rejection = expect(request.promise).rejects.toThrow('outcome is unknown');
  await vi.advanceTimersByTimeAsync(101); await rejection;
  const pending = client.request('foundation.getInfo', {});
  const closed = expect(pending.promise).rejects.toThrow('closed'); client.dispose(); await closed;
});

test('transport failures carry outcome uncertainty separately from authoritative host errors', async () => {
  vi.useFakeTimers();
  const fake = fakeTransport(); const client = new BridgeClient(fake.transport, 100);
  const request = client.request('sectionReview.cancel', {sessionId:'s', worksheetId:'w'});
  const timedOut = request.promise.catch(error => error);
  await vi.advanceTimersByTimeAsync(101);
  expect(await timedOut).toBeInstanceOf(BridgeTransportError);
  expect(await timedOut).toMatchObject({reason:'timeout', outcomeUnknown:true});
  const rejected = client.request('sectionReview.cancel', {sessionId:'s', worksheetId:'w'});
  const hostError = rejected.promise.catch(error => error);
  fake.emit({protocolVersion:1, type:'response', requestId:rejected.id, ok:false, error:{code:'BUSY', message:'Busy.'}});
  expect(await hostError).toBeInstanceOf(BridgeError);
  expect(await hostError).not.toBeInstanceOf(BridgeTransportError);
  const pending = client.request('foundation.getInfo', {}).promise.catch(error => error);
  client.dispose();
  expect(await pending).toMatchObject({reason:'closed', outcomeUnknown:true});
  const unsent = await client.request('foundation.getInfo', {}).promise.catch(error => error);
  expect(unsent).toMatchObject({reason:'closed', outcomeUnknown:false});
});

test('a send exception is conservatively uncertain while capacity rejection is known unsent', async () => {
  const fake = fakeTransport();
  const throwing = new BridgeClient({...fake.transport, postMessage: () => {throw new Error('Transport failed.');}});
  const error = await throwing.request('foundation.getInfo', {}).promise.catch(error => error);
  expect(error).toBeInstanceOf(BridgeTransportError);
  expect(error).toMatchObject({reason:'unavailable', outcomeUnknown:true});
  throwing.dispose();
  const client = new BridgeClient(fake.transport);
  const pending = Array.from({length:16}, () => client.request('foundation.getInfo', {}).promise.catch(error => error));
  const rejected = await client.request('foundation.getInfo', {}).promise.catch(error => error);
  expect(rejected).toMatchObject({reason:'capacity', outcomeUnknown:false});
  expect(fake.sent).toHaveLength(16);
  client.dispose(); await Promise.all(pending);
});
