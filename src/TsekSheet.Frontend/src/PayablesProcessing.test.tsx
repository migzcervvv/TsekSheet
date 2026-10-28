import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import PayablesProcessing from './PayablesProcessing';
import { BridgeClient, type Transport } from './bridge/client';
import { validProfiles, validWorkflowPlan, type WorkflowPlan, type WorkflowProfile } from './bridge/workflows';

const sessionId = '11111111-1111-1111-1111-111111111111';
const worksheetId = '22222222-2222-2222-2222-222222222222';
const profile: WorkflowProfile = { id: 'synthetic-payables', revision: 1, displayName: 'Synthetic Payables', profileHash: 'B'.repeat(64), fields: [
  { key: 'vendor', header: 'Vendor name', kind: 'text', required: true, maxLength: 128 },
  { key: 'rfp', header: 'RFP number', kind: 'text', required: false, maxLength: 64 },
  { key: 'amount', header: 'Amount', kind: 'decimal', required: true, maxLength: 32 },
] };
function proposal(): WorkflowPlan {
  return { sessionId, worksheetId, revision: 0, sourceSha256: 'A'.repeat(64), profileId: profile.id, profileHash: profile.profileHash,
    readyForReview: false, canApply: false, insertionBlockedReason: 'Structural preservation is not proven.', requiredConfirmations: ['Review duplicates'], integrityConcerns: ['Formula ranges require validation'],
    records: [{ recordId: 'manual-1', vendor: 'Acme', matchReason: 'Exact match', matches: [
      { sectionId: 'internal', label: 'Internal', lastRow: 8, entries: 2 }, { sectionId: 'external', label: 'External', lastRow: 17, entries: 1 },
    ], suggestedVendors: [], requiresVendorChoice: false, requiresSectionChoice: true, sectionId: null, sectionLabel: null,
      baseInsertionRow: null, proposedRow: null, affectedThroughRow: 28,
      fields: [{ key: 'vendor', header: 'Vendor name', column: 1, kind: 'text', value: 'Acme' }],
      duplicates: [{ existingRow: 8, sectionId: 'internal', batchRecordId: null, reason: 'Same vendor and RFP number R3' }],
    }] };
}
const clients: BridgeClient[] = [];
afterEach(() => { cleanup(); clients.forEach(c => c.dispose()); clients.length = 0; });
function setup() {
  const listeners = new Set<(e: { data: unknown }) => void>();
  const sent: { command: string; payload: any; requestId: string }[] = [];
  let fail = false; let defer = false; let similar = false; let mismatch = false; let pending: (() => void) | undefined;
  const transport: Transport = {
    addEventListener: (_, f) => { listeners.add(f); }, removeEventListener: (_, f) => { listeners.delete(f); },
    postMessage: message => {
      const m = message as typeof sent[number]; sent.push(m);
      const respond = () => {
        const result = proposal(); const section = m.payload.records?.[0]?.selectedSectionId;
        if (similar && !m.payload.records?.[0]?.selectedVendor && !m.payload.records?.[0]?.confirmNewVendor)
          Object.assign(result.records[0], { vendor: 'Acme Limited', matches: [], suggestedVendors: ['Acme'], requiresVendorChoice: true, requiresSectionChoice: false });
        if (mismatch) result.sourceSha256 = 'C'.repeat(64);
        if (section) Object.assign(result.records[0], { requiresSectionChoice: false, sectionId: section, sectionLabel: 'External', baseInsertionRow: 18, proposedRow: 18 });
        if (section) result.readyForReview = true;
        listeners.forEach(f => f({ data: { protocolVersion: 1, type: 'response', requestId: m.requestId, ok: !fail,
          result: m.command === 'workflow.profiles' ? { profiles: [profile] } : result,
          error: fail ? { code: 'UNSAFE_SECTION', message: 'Ambiguous subtotal' } : undefined } }));
      };
      if (defer && m.command === 'workbook.planRecords') pending = respond; else queueMicrotask(respond);
    },
  };
  const bridge = new BridgeClient(transport); clients.push(bridge);
  const props = { bridge, sessionId, worksheetId, revision: 0, sourceSha256: 'A'.repeat(64), workbookName: 'synthetic.xlsx', worksheetName: 'Ledger', disabled: false };
  return { props, sent, fail: () => { fail = true; }, similar: () => { similar = true; }, mismatch: () => { mismatch = true; }, defer: () => { defer = true; }, finish: () => pending?.() };
}
async function enter() {
  await screen.findByRole('option', { name: /Synthetic Payables/ });
  fireEvent.change(screen.getByLabelText('Workflow profile'), { target: { value: profile.id } });
  fireEvent.change(screen.getByLabelText('Payable Vendor name'), { target: { value: 'Acme' } });
  fireEvent.change(screen.getByLabelText('Payable RFP number'), { target: { value: 'R3' } });
  fireEvent.change(screen.getByLabelText('Payable Amount'), { target: { value: '123.45' } });
}
test('manual planning requires explicit section choice and a second validated preview; Apply stays disabled', async () => {
  const s = setup(); render(<PayablesProcessing {...s.props} />); await enter();
  fireEvent.click(screen.getByText('Preview payable destination'));
  await screen.findByText(/Potential duplicates/);
  expect((screen.getByLabelText('Destination section') as HTMLSelectElement).value).toBe('');
  expect(screen.getByText(/Resolve the entity/)).toBeDefined();
  fireEvent.change(screen.getByLabelText('Destination section'), { target: { value: 'external' } });
  expect(screen.getByText(/Choices changed/)).toBeDefined();
  fireEvent.click(screen.getByText('Preview payable destination'));
  await screen.findByText(/insert at base row 18/);
  expect(s.sent.at(-1)?.payload.records[0].selectedSectionId).toBe('external');
  expect((screen.getByText('Apply row insertion — unavailable') as HTMLButtonElement).disabled).toBe(true);
  expect(s.sent.every(m => !m.command.includes('apply') && !JSON.stringify(m.payload).includes('path'))).toBe(true);
});
test('unsafe analysis retains manual input for correction', async () => {
  const s = setup(); render(<PayablesProcessing {...s.props} />); await enter(); s.fail();
  fireEvent.click(screen.getByText('Preview payable destination')); await screen.findByText('Ambiguous subtotal');
  expect((screen.getByLabelText('Payable Vendor name') as HTMLInputElement).value).toBe('Acme');
  expect(screen.queryByLabelText('Payable insertion proposal')).toBeNull();
});
test('revision changes discard late proposals', async () => {
  const s = setup(); const view = render(<PayablesProcessing {...s.props} />); await enter(); s.defer();
  fireEvent.click(screen.getByText('Preview payable destination'));
  view.rerender(<PayablesProcessing {...s.props} revision={1} />); s.finish();
  await waitFor(() => expect((screen.getByText('Preview payable destination') as HTMLButtonElement).disabled).toBe(false));
  expect(screen.queryByLabelText('Payable insertion proposal')).toBeNull();
});
test('similar names require explicit identity selection in a subsequent request', async () => {
  const s = setup(); s.similar(); render(<PayablesProcessing {...s.props} />); await enter();
  fireEvent.click(screen.getByText('Preview payable destination'));
  const identity = await screen.findByLabelText('Vendor identity');
  expect((identity as HTMLSelectElement).value).toBe('');
  expect(screen.queryByLabelText('Destination section')).toBeNull();
  fireEvent.change(identity, { target: { value: 'Acme' } });
  fireEvent.click(screen.getByText('Preview payable destination'));
  await screen.findByLabelText('Destination section');
  expect(s.sent.at(-1)?.payload.records[0].selectedVendor).toBe('Acme');
  expect(s.sent.at(-1)?.payload.records[0].confirmNewVendor).toBe(false);
});
test('a proposal for another source fingerprint is rejected', async () => {
  const s = setup(); s.mismatch(); render(<PayablesProcessing {...s.props} />); await enter();
  fireEvent.click(screen.getByText('Preview payable destination'));
  await screen.findByText(/mismatched proposal/);
  expect(screen.queryByLabelText('Payable insertion proposal')).toBeNull();
});
test('bridge rejects executable or inconsistent workflow results and invalid profile bounds', () => {
  const plan = proposal(); expect(validWorkflowPlan(plan)).toBe(true);
  expect(validWorkflowPlan({ ...plan, canApply: true })).toBe(false);
  expect(validWorkflowPlan({ ...plan, readyForReview: true })).toBe(false);
  expect(validWorkflowPlan({ ...plan, revision: -1 })).toBe(false);
  expect(validProfiles({ profiles: [profile] })).toBe(true);
  expect(validProfiles({ profiles: [profile, profile] })).toBe(false);
  expect(validProfiles({ profiles: [{ ...profile, fields: [{ ...profile.fields[0], maxLength: 10000 }] }] })).toBe(false);
});
