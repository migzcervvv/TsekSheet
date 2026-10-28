import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import App from './App';
import type { Transport } from './bridge/client';

afterEach(() => { cleanup(); delete (window as unknown as { chrome?: unknown }).chrome; });

test('browser preview honestly disables native operations', () => {
  render(<App />);
  expect(screen.getByRole('status').textContent).toBe('Desktop host unavailable');
  expect((screen.getByRole('button', { name: 'Run diagnostic' }) as HTMLButtonElement).disabled).toBe(true);
});

test('handshake failure shows a safe error and retry without enabling native actions', async () => {
  const listeners = new Set<(event: { data: unknown }) => void>();
  const host: Transport = {
    addEventListener: (_, listener) => { listeners.add(listener); },
    removeEventListener: (_, listener) => { listeners.delete(listener); },
    postMessage: message => {
      const request = message as { requestId: string };
      queueMicrotask(() => listeners.forEach(listener => listener({ data: { protocolVersion: 1, type: 'response', requestId: request.requestId, ok: false, error: { code: 'UNSUPPORTED_VERSION', message: 'Protocol unavailable.' } } })));
    },
  };
  (window as unknown as { chrome: { webview: Transport } }).chrome = { webview: host };
  render(<App />);
  await screen.findByText('Connection failed');
  expect(screen.getByRole('alert').textContent).toBe('Protocol unavailable.');
  expect((screen.getByRole('button', { name: 'Run diagnostic' }) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByRole('button', { name: 'Retry connection' })).toBeDefined();
});

test('connects to the host, shows progress and cancellation, and renders file names as text', async () => {
  const listeners = new Set<(event: { data: unknown }) => void>();
  let runId = '';
  const emit = (m: unknown) => listeners.forEach(l => l({ data: m }));
  const reply = (requestId: string, result: unknown) => emit({ protocolVersion: 1, type: 'response', requestId, ok: true, result });
  const host: Transport = {
    addEventListener: (_, l) => { listeners.add(l); }, removeEventListener: (_, l) => { listeners.delete(l); },
    postMessage: message => {
      const m = message as { command: string; requestId: string };
      queueMicrotask(() => {
        if (m.command === 'foundation.handshake') reply(m.requestId, { selectedVersion: 1, applicationName: 'TsekSheet', version: '0.1.0', capabilities: { diagnostics: true, fileSelection: true, workbooks: false, templates: false, ocr: false } });
        if (m.command === 'foundation.getInfo') reply(m.requestId, { applicationName: 'TsekSheet', version: '0.1.0', platform: 'Windows x64', hasUnsavedWork: false });
        if (m.command === 'diagnostics.run') { runId = m.requestId; emit({ protocolVersion: 1, type: 'event', name: 'diagnostics.progress', operationId: runId, progress: { completed: 1, total: 2 } }); }
        if (m.command === 'diagnostics.cancel') { reply(m.requestId, { status: 'requested' }); reply(runId, { status: 'cancelled' }); }
        if (m.command === 'foundation.selectFile') reply(m.requestId, { status: 'selected', selectionId: 'opaque', displayName: '<b>receipt.txt' });
      });
    },
  };
  (window as unknown as { chrome: { webview: Transport } }).chrome = { webview: host };
  render(<App />);
  await waitFor(() => expect(screen.getByRole('status').textContent).toBe('Connected'));
  fireEvent.click(screen.getByRole('button', { name: 'Run diagnostic' }));
  await screen.findByText('50%');
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
  await screen.findByText('Cancelled');
  fireEvent.click(screen.getByRole('button', { name: 'Choose a file' }));
  expect((await screen.findByText('<b>receipt.txt')).tagName).toBe('DIV');
});
