import { cleanup, configure, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import WorkbookViewer from './WorkbookViewer';
import { BridgeClient } from './bridge/client';
import type { Transport, Workbook, Viewport } from './bridge/client';

const sessionId = '11111111-1111-1111-1111-111111111111';
const sheetId = '22222222-2222-2222-2222-222222222222';
const hiddenId = '33333333-3333-3333-3333-333333333333';
const copyId = '44444444-4444-4444-4444-444444444444';
const book: Workbook = { sessionId, parentSessionId: null, displayName: '<b>synthetic.xlsx', sourceSha256: 'A'.repeat(64), revision: 0, isDirty: false, editingAllowed: true, creator: 'Synthetic', title: 'Artificial',
  worksheets: [{ id: sheetId, name: 'Data', order: 1, visibility: 'Visible', lastRow: 50, lastColumn: 8 }, { id: hiddenId, name: 'Lookup', order: 2, visibility: 'Hidden', lastRow: 2, lastColumn: 2 }], findings: [{ code: 'READ_ONLY', message: 'Inspection only.' }] };
function viewport(payload: Record<string, unknown>): Viewport {
  const startRow = payload.startRow as number; const startColumn = payload.startColumn as number; const rows = payload.rows as number; const columns = payload.columns as number;
  return { sessionId: payload.sessionId as string, worksheetId: payload.worksheetId as string, startRow, startColumn, rows, columns,
    cells: Array.from({ length: rows * columns }, (_, i) => ({ row: startRow + Math.floor(i / columns), column: startColumn + i % columns, value: i === 0 ? '<img src=x>' : startRow + Math.floor(i / columns) === 5 && startColumn + i % columns === 1 ? 'Sample-1' : '', kind: 'Text', formula: i === 1 ? 'Lookup!B2' : null, truncated: false, format: { numberFormat: '', numberFormatId: 0, bold: i === 0, italic: false, fillColor: '' } })) };
}
const clients: BridgeClient[] = [];
configure({ asyncUtilTimeout: 5000 });
afterEach(() => { cleanup(); clients.forEach(c => c.dispose()); clients.length = 0; });
function setup() {
  const listeners = new Set<(event: { data: unknown }) => void>();
  let mode = 'opened';
  let changed = false;
  const host: Transport = {
    addEventListener: (_, listener) => { listeners.add(listener); }, removeEventListener: (_, listener) => { listeners.delete(listener); },
    postMessage: request => {
      const m = request as { command: string; requestId: string; payload: Record<string, unknown> };
      queueMicrotask(() => {
        const error = (m.command === 'workbook.open' && mode === 'error') || (m.command === 'workbook.editCopy' && (mode === 'edit-error' || mode === 'edit-timeout')) || (m.command === 'workbook.saveAsNew' && (mode === 'save-error' || mode === 'save-uncertain')) || (m.command === 'workbook.merge' && mode === 'merge-error');
        let result: unknown;
        if (m.command === 'workbook.open') result = mode === 'opened' ? { status: 'opened', workbook: book } : { status: 'cancelled' };
        else if (m.command === 'workbook.close' || m.command === 'workbook.discard') result = { status: 'closed' };
        else if (m.command === 'workbook.createCopy') result = { sessionId: copyId, sourceWorksheetId: sheetId, baseRevision: 0, revision: 0, isDirty: false, changeCount: 0 };
        else if (m.command === 'workbook.previewMerge') result = { destinationName: 'Data', pendingCells: changed ? 1 : 0, canApply: mode !== 'merge-conflict', conflicts: mode === 'merge-conflict' ? [{ code: 'DESTINATION_CONFLICT', message: 'Create a fresh copy.' }] : [] };
        else if (m.command === 'workbook.merge') result = { workbook: { ...book, revision: changed ? 1 : 0, isDirty: changed }, appliedCells: changed ? 1 : 0 };
        else if (m.command === 'workbook.editCopy') { if (!error) changed = true; result = { sessionId: copyId, sourceWorksheetId: sheetId, baseRevision: 0, revision: 1, isDirty: true, changeCount: 1 }; }
        else if (m.command === 'workbook.saveAsNew') result = mode === 'save-cancel' ? { status: 'cancelled' } : { status: 'saved', receipt: { status: 'saved', sessionId, savedRevision: 0, sourceStillDirty: false, rebound: false, cancellationAfterCommit: false, destinationSha256: 'B'.repeat(64), displayName: 'export.xlsx' } };
        else { const v = viewport(m.payload); if (changed && m.payload.sessionId === copyId) { const cell = v.cells.find(c => c.row === 5 && c.column === 1); if (cell) cell.value = 'Copy-only value'; } result = v; }
        listeners.forEach(l => l({ data: { protocolVersion: 1, type: 'response', requestId: m.requestId, ok: !error, result, error: error ? { code: 'REJECTED', message: mode === 'save-error' ? 'Choose a new filename. Existing files cannot be replaced.' : mode === 'save-uncertain' ? 'The commit occurred but final verification failed. Inspect the selected file before retrying.' : mode === 'edit-error' ? 'This cell contains a formula. Choose an explicit formula edit instead.' : mode === 'edit-timeout' ? 'Desktop request timed out. Its outcome is unknown.' : 'Unsupported workbook.' } : undefined } }));
      });
    },
  };
  const client = new BridgeClient(host); clients.push(client);
  return { client, mode: (value: string) => { mode = value; } };
}

test('Merge previews the destination, requires confirmation and refreshes the original view', async () => {
  const { client } = setup(); render(<WorkbookViewer bridge={client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Select A5' }));
  fireEvent.change(screen.getByRole('textbox', { name: 'Cell value' }), { target: { value: 'Copy-only value' } });
  fireEvent.click(screen.getByRole('button', { name: 'Apply to working copy' })); await screen.findByText('Copy-only value');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Review Merge' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Review Merge' }));
  await screen.findByRole('alertdialog', { name: 'Review worksheet merge' });
  expect(screen.getByText('Merge into Data')).toBeDefined(); expect(screen.getByText(/1 effective pending cell edits/)).toBeDefined();
  fireEvent.click(screen.getByRole('button', { name: 'Cancel merge' }));
  expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).value).toBe(copyId);
  fireEvent.click(screen.getByRole('button', { name: 'Review Merge' })); await screen.findByRole('alertdialog', { name: 'Review worksheet merge' });
  fireEvent.click(screen.getByRole('button', { name: 'Confirm merge in memory' }));
  await screen.findByText(/Merged 1 cells in memory/);
  expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).value).toBe('original');
  expect(screen.getByRole('option', { name: /Working copy 1/ })).toBeDefined();
}, 15000);

test('Merge conflicts disable confirmation and retain the selected working copy', async () => {
  const fake = setup(); render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' })); await screen.findByRole('button', { name: 'Select A5' });
  await waitFor(() => expect((screen.getByRole('button', { name: 'Review Merge' }) as HTMLButtonElement).disabled).toBe(false));
  fake.mode('merge-conflict'); fireEvent.click(screen.getByRole('button', { name: 'Review Merge' }));
  await screen.findByText(/DESTINATION_CONFLICT/);
  expect((screen.getByRole('button', { name: 'Confirm merge in memory' }) as HTMLButtonElement).disabled).toBe(true);
  expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).value).toBe(copyId);
}, 15000);

test('A rejected Merge keeps its working copy available', async () => {
  const fake = setup(); render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' })); await screen.findByRole('button', { name: 'Select A5' });
  await waitFor(() => expect((screen.getByRole('button', { name: 'Review Merge' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Review Merge' })); await screen.findByRole('alertdialog', { name: 'Review worksheet merge' });
  fake.mode('merge-error'); fireEvent.click(screen.getByRole('button', { name: 'Confirm merge in memory' }));
  await screen.findByRole('alert');
  expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).value).toBe(copyId);
}, 15000);

test('opens a read-only workbook, renders literal text and formulas, pages, switches hidden sheets and closes', async () => {
  const { client } = setup(); render(<WorkbookViewer bridge={client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' }));
  await screen.findByRole('table');
  expect(screen.getByText('<b>synthetic.xlsx').tagName).toBe('STRONG');
  expect(screen.getByText('<img src=x>').tagName).toBe('TD');
  expect(screen.getByText('=Lookup!B2')).toBeDefined();
  expect(screen.getByText('Original workbook view. Edit an isolated working copy.')).toBeDefined();
  fireEvent.click(screen.getByRole('button', { name: 'Next rows' }));
  await screen.findByText(/Rows 21–40/);
  await waitFor(() => expect((screen.getByRole('combobox', { name: 'Worksheet' }) as HTMLSelectElement).disabled).toBe(false));
  fireEvent.change(screen.getByRole('combobox', { name: 'Worksheet' }), { target: { value: hiddenId } });
  await screen.findByText(/Lookup · Rows 1–20/);
  await waitFor(() => expect((screen.getByRole('button', { name: 'Close workbook' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Close workbook' }));
  await screen.findByText('Workbook closed. Original file unchanged.');
  expect(screen.queryByRole('table')).toBeNull();
}, 15000);

test('formula protection errors remain visible and do not mark the working copy dirty', async () => {
  const fake = setup(); render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Select B1' }));
  expect(screen.getByText(/This cell contains a formula/)).toBeDefined();
  fireEvent.change(screen.getByRole('combobox', { name: 'Value type' }), { target: { value: 'text' } });
  fake.mode('edit-error'); fireEvent.click(screen.getByRole('button', { name: 'Apply to working copy' }));
  expect((await screen.findByRole('alert')).textContent).toContain('explicit formula edit');
  expect(screen.queryByText('Pending changes in working copies')).toBeNull();
}, 15000);

test('Save As exports only original session and retains pending copy changes', async () => {
  const { client } = setup(); render(<WorkbookViewer bridge={client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Select A5' }));
  expect((screen.getByRole('button', { name: 'Save As' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.change(screen.getByRole('textbox', { name: 'Cell value' }), { target: { value: 'Copy-only value' } });
  fireEvent.click(screen.getByRole('button', { name: 'Apply to working copy' })); await screen.findByText('Copy-only value');
  await waitFor(() => expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).disabled).toBe(false));
  fireEvent.change(screen.getByRole('combobox', { name: 'Workbook view' }), { target: { value: 'original' } });
  await screen.findByText('Sample-1');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Save As' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Save As' }));
  await screen.findByText(/Created export.xlsx from original session revision 0/);
  expect(screen.getByText('Pending changes in working copies')).toBeDefined();
  expect(screen.getByText(/Save As creates a new .xlsx file/)).toBeDefined();
}, 15000);

test('Save As distinguishes cancellation, rejection and uncertain result', async () => {
  const fake = setup(); render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Save As' }) as HTMLButtonElement).disabled).toBe(false));
  fake.mode('save-cancel'); fireEvent.click(screen.getByRole('button', { name: 'Save As' })); await screen.findByText('Save As cancelled. No file was created.');
  fake.mode('save-error'); fireEvent.click(screen.getByRole('button', { name: 'Save As' })); await screen.findByText(/Choose a new filename/);
  fake.mode('save-uncertain'); fireEvent.click(screen.getByRole('button', { name: 'Save As' })); await screen.findByText(/commit occurred but final verification failed/);
  expect(screen.queryByText(/Created export.xlsx/)).toBeNull(); expect(screen.getByRole('table')).toBeDefined();
}, 15000);

test('an unknown edit outcome requires explicit discard and blocks further edits', async () => {
  const fake = setup(); render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Select A5' }));
  fake.mode('edit-timeout'); fireEvent.click(screen.getByRole('button', { name: 'Apply to working copy' }));
  await screen.findByText('Working-copy outcome unknown');
  expect((screen.getByRole('button', { name: 'Apply to working copy' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Close workbook' }));
  expect(screen.getByRole('alertdialog')).toBeDefined();
  fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
  await screen.findByText('Workbook closed. Original file unchanged.');
}, 15000);

test('edits an isolated copy, shows pending changes, retains the original and confirms discard', async () => {
  const { client } = setup(); render(<WorkbookViewer bridge={client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Create working copy' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Create working copy' }));
  const cell = await screen.findByRole('button', { name: 'Select A5' }); fireEvent.click(cell);
  fireEvent.change(screen.getByRole('textbox', { name: 'Cell value' }), { target: { value: 'Copy-only value' } });
  fireEvent.click(screen.getByRole('button', { name: 'Apply to working copy' }));
  await screen.findByText('Copy-only value'); await screen.findByText(/Pending changes in working copies/);
  await waitFor(() => expect((screen.getByRole('combobox', { name: 'Workbook view' }) as HTMLSelectElement).disabled).toBe(false));
  fireEvent.change(screen.getByRole('combobox', { name: 'Workbook view' }), { target: { value: 'original' } });
  await screen.findByText('Sample-1'); expect(screen.queryByText('Copy-only value')).toBeNull();
  await waitFor(() => expect((screen.getByRole('button', { name: 'Close workbook' }) as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(screen.getByRole('button', { name: 'Close workbook' }));
  expect(screen.getByRole('alertdialog').textContent).toContain('Discard pending');
  fireEvent.click(screen.getByRole('button', { name: 'Keep editing' })); expect(screen.getByRole('table')).toBeDefined();
  fireEvent.click(screen.getByRole('button', { name: 'Close workbook' })); fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }));
  await screen.findByText('Workbook closed. Original file unchanged.');
}, 15000);

test('selection cancellation and rejected opening retain the current view; unavailable host disables opening', async () => {
  const fake = setup(); const view = render(<WorkbookViewer bridge={fake.client} available />);
  fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('table');
  await waitFor(() => expect((screen.getByRole('button', { name: 'Open workbook' }) as HTMLButtonElement).disabled).toBe(false));
  fake.mode('cancelled'); fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByText('Workbook selection cancelled.');
  expect(screen.getByRole('table')).toBeDefined();
  fake.mode('error'); fireEvent.click(screen.getByRole('button', { name: 'Open workbook' })); await screen.findByRole('alert');
  expect(screen.getByRole('alert').textContent).toBe('Unsupported workbook.'); expect(screen.getByRole('table')).toBeDefined();
  view.rerender(<WorkbookViewer bridge={null} available={false} />);
  expect((screen.getByRole('button', { name: 'Open workbook' }) as HTMLButtonElement).disabled).toBe(true);
}, 15000);

test('malformed and duplicate-cell viewport responses cannot resolve a pending request', async () => {
  let listener: ((event: { data: unknown }) => void) | undefined; let requestId = '';
  const host: Transport = { addEventListener: (_, l) => { listener = l; }, removeEventListener: () => {}, postMessage: m => { requestId = (m as { requestId: string }).requestId; } };
  const client = new BridgeClient(host); clients.push(client);
  const payload = { sessionId, worksheetId: sheetId, startRow: 1, startColumn: 1, rows: 1, columns: 2 };
  const pending = client.request('workbook.readViewport', payload); let resolved = false; void pending.promise.then(() => { resolved = true; });
  const send = (result: unknown) => listener!({ data: { protocolVersion: 1, type: 'response', requestId, ok: true, result } });
  const valid = viewport(payload); send({ ...valid, rows: 999 }); send({ ...valid, cells: [valid.cells[0], valid.cells[0]] });
  await Promise.resolve(); expect(resolved).toBe(false);
  send(valid); expect(await pending.promise).toEqual(valid);
});
