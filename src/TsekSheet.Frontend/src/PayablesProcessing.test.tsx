import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import PayablesProcessing from './PayablesProcessing';
import { BridgeClient, type Transport } from './bridge/client';
import { validProfiles, validWorkflowPlan, validCandidateReview, type CandidateReview, type WorkflowPlan, type WorkflowProfile } from './bridge/workflows';

const sessionId = '11111111-1111-1111-1111-111111111111';
const worksheetId = '22222222-2222-2222-2222-222222222222';
const profile: WorkflowProfile = { id: 'synthetic-payables', revision: 1, displayName: 'Synthetic Payables', profileHash: 'B'.repeat(64), fields: [
  { key: 'vendor', header: 'Vendor name', kind: 'text', required: true, maxLength: 128 },
  { key: 'rfp', header: 'RFP number', kind: 'text', required: false, maxLength: 64 },
  { key: 'amount', header: 'Amount', kind: 'decimal', required: true, maxLength: 32 },
] };
function proposal(): WorkflowPlan {
  return { sessionId, worksheetId, revision: 0, sourceSha256: 'A'.repeat(64), profileId: profile.id, profileHash: profile.profileHash,
    readyForReview: false, canApply: false, structuralStatus: 'ambiguous-destination', insertionBlockedReason: 'Structural preservation is not proven.', requiredConfirmations: ['Review duplicates'], integrityConcerns: ['Formula ranges require validation'],
    review: { planner: 'choices-required', preflight: 'ambiguous-destination', generation: 'not-generated', oracle: 'not-run', approval: 'not-approved', workingCopyAdoption: 'not-adopted', integration: 'production-disabled', export: 'unmerged-changes-excluded' },
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
  let blocked=false;let deferCandidate=false;let pendingCandidate:(()=>void)|undefined;let wrongApproval=false;
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
        result.review.planner = result.readyForReview ? 'resolved' : 'choices-required'; result.review.preflight = result.structuralStatus;
        let response:unknown=result;
        if(m.command==='workbook.generateCandidate'||m.command==='workbook.approveCandidate')response=candidateResult(blocked,m.command==='workbook.approveCandidate',wrongApproval);
        if(m.command==='workbook.cancelCandidate')response={status:'cancelled'};
        listeners.forEach(f => f({ data: { protocolVersion: 1, type: 'response', requestId: m.requestId, ok: !fail,
          result: m.command === 'workflow.profiles' ? { profiles: [profile] } : response,
          error: fail ? { code: 'UNSAFE_SECTION', message: 'Ambiguous subtotal' } : undefined } }));
      };
      if(deferCandidate&&m.command==='workbook.generateCandidate')pendingCandidate=respond;
      else if (defer && m.command === 'workbook.planRecords') pending = respond; else queueMicrotask(respond);
    },
  };
  const bridge = new BridgeClient(transport); clients.push(bridge);
  const props = { bridge, sessionId, worksheetId, revision: 0, sourceSha256: 'A'.repeat(64), workbookName: 'synthetic.xlsx', worksheetName: 'Ledger', disabled: false,
    isWorkingCopy:true,destinationSessionId:'33333333-3333-3333-3333-333333333333',destinationRevision:0 };
  return { props, sent, fail: () => { fail = true; }, similar: () => { similar = true; }, mismatch: () => { mismatch = true; }, defer: () => { defer = true; }, finish: () => pending?.(),
    block:()=>{blocked=true;},deferGeneration:()=>{deferCandidate=true;},finishGeneration:()=>pendingCandidate?.(),wrongApproval:()=>{wrongApproval=true;} };
}
function candidateResult(blocked=false,approved=false,wrong=false):CandidateReview {
  const plan=proposal();plan.readyForReview=true;Object.assign(plan.records[0],{requiresSectionChoice:false,sectionId:'external',sectionLabel:'External',baseInsertionRow:18,proposedRow:18});plan.review.planner='resolved';
  return { candidateId:wrong&&approved?'44444444-4444-4444-4444-444444444444':'55555555-5555-5555-5555-555555555555',reviewSha256:'C'.repeat(64),candidateSha256:'D'.repeat(64),manifestSha256:'E'.repeat(64),
    workbookName:'synthetic.xlsx',worksheetName:'Ledger',proposal:plan,destinationSessionId:'33333333-3333-3333-3333-333333333333',expectedDestinationRevision:0,reviewOnly:true,intendedWorkingCopyRevision:1,intendedDestinationRevision:1,
    lifecycle:{planner:'resolved',preflight:'eligible-experimental',generation:'generated',oracle:'accepted',approval:approved?'approved':'not-approved',workingCopyAdoption:'not-adopted',integration:'not-integrated',export:'not-exported'},
    formulaImpacts:['E22 -> E23: SUM(E16:E21) -> SUM(E16:E22)'],styleAssumptions:['Template row 16; unmapped constants cleared'],rejectionReasons:blocked?['Height 22 is not qualified; no tolerance.']:[],
    operation:{insertionBoundary:18,rowCount:1,templateRow:16,firstDataRow:16,subtotalRow:22,subtotalPolicy:'Complete section SUM'},oracleReport:{accepted:true,allowedChanges:['One row shift'],findings:[]} };
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
  expect(screen.getByLabelText('Structural insertion status').textContent).toContain('Ambiguous destination');
  expect(screen.getByText(/Resolve the entity/)).toBeDefined();
  fireEvent.change(screen.getByLabelText('Destination section'), { target: { value: 'external' } });
  expect(screen.getByText(/Choices changed/)).toBeDefined();
  fireEvent.click(screen.getByText('Preview payable destination'));
  await screen.findByText(/insert at base row 18/);
  expect(s.sent.at(-1)?.payload.records[0].selectedSectionId).toBe('external');
  expect((screen.getByText('Apply row insertion — unavailable') as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByLabelText('Candidate lifecycle review').textContent).toContain('Candidate: not generated');
  expect(screen.getByLabelText('Candidate lifecycle review').textContent).toContain('Export excludes unmerged records');
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
  expect(validWorkflowPlan({ ...plan, structuralStatus: 'safe-to-apply' })).toBe(false);
  expect(validWorkflowPlan({ ...plan, structuralStatus: 'unsupported-structure', review: { ...plan.review, preflight: 'unsupported-structure' } })).toBe(true);
  expect(validWorkflowPlan({ ...plan, review: { ...plan.review, oracle: 'accepted' } })).toBe(false);
  expect(validWorkflowPlan({ ...plan, review: { ...plan.review, generation: 'generated' } })).toBe(false);
  expect(validProfiles({ profiles: [profile] })).toBe(true);
  expect(validProfiles({ profiles: [profile, profile] })).toBe(false);
  expect(validProfiles({ profiles: [{ ...profile, fields: [{ ...profile.fields[0], maxLength: 10000 }] }] })).toBe(false);
});

async function readyPreview() {
  await enter();fireEvent.click(screen.getByText('Preview payable destination'));await screen.findByLabelText('Destination section');
  fireEvent.change(screen.getByLabelText('Destination section'),{target:{value:'external'}});fireEvent.click(screen.getByText('Preview payable destination'));await screen.findByText(/insert at base row 18/);
}
test('generated review requires explicit general and duplicate acknowledgment and never applies',async()=>{
  const s=setup();render(<PayablesProcessing {...s.props}/>);await readyPreview();fireEvent.click(screen.getByText('Generate candidate for review'));await screen.findByLabelText('Generated candidate review');
  expect((screen.getByText('Approve reviewed candidate') as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('I reviewed these exact values, destination, formulas and styling assumptions.'));
  expect((screen.getByText('Approve reviewed candidate') as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('I explicitly reviewed every duplicate RFP/invoice warning for this candidate.'));
  fireEvent.click(screen.getByText('Approve reviewed candidate'));await screen.findByText(/user approved review/);
  expect(s.sent.find(m=>m.command==='workbook.approveCandidate')?.payload.duplicatesReviewed).toBe(true);
  expect(s.sent.every(m=>!['workbook.merge','workbook.saveAsNew','workbook.adoptCandidate'].includes(m.command))).toBe(true);
  expect((screen.getByText('Apply row insertion — unavailable') as HTMLButtonElement).disabled).toBe(true);
});
test('native height finding blocks approval even though the OOXML oracle accepted',async()=>{
  const s=setup();s.block();render(<PayablesProcessing {...s.props}/>);await readyPreview();fireEvent.click(screen.getByText('Generate candidate for review'));
  await screen.findByText('Height 22 is not qualified; no tolerance.');expect((screen.getByText('Approve reviewed candidate') as HTMLButtonElement).disabled).toBe(true);
});
test('material input change discards a generated review and retracts its server ticket',async()=>{
  const s=setup();render(<PayablesProcessing {...s.props}/>);await readyPreview();fireEvent.click(screen.getByText('Generate candidate for review'));await screen.findByLabelText('Generated candidate review');
  fireEvent.change(screen.getByLabelText('Payable Amount'),{target:{value:'45'}});await waitFor(()=>expect(screen.queryByLabelText('Generated candidate review')).toBeNull());
  await waitFor(()=>expect(s.sent.some(m=>m.command==='workbook.cancelCandidate')).toBe(true));
});
test('cancelled generation ignores and retracts a late candidate response',async()=>{
  const s=setup();s.deferGeneration();render(<PayablesProcessing {...s.props}/>);await readyPreview();fireEvent.click(screen.getByText('Generate candidate for review'));await waitFor(()=>expect(s.sent.some(m=>m.command==='workbook.generateCandidate')).toBe(true));fireEvent.click(screen.getByText('Cancel candidate review'));s.finishGeneration();
  await waitFor(()=>expect(s.sent.some(m=>m.command==='workbook.cancelCandidate')).toBe(true));expect(screen.queryByLabelText('Generated candidate review')).toBeNull();
});
test('approval response for a different candidate invalidates the review',async()=>{
  const s=setup();s.wrongApproval();render(<PayablesProcessing {...s.props}/>);await readyPreview();fireEvent.click(screen.getByText('Generate candidate for review'));await screen.findByLabelText('Generated candidate review');
  fireEvent.click(screen.getByLabelText('I reviewed these exact values, destination, formulas and styling assumptions.'));fireEvent.click(screen.getByLabelText('I explicitly reviewed every duplicate RFP/invoice warning for this candidate.'));
  fireEvent.click(screen.getByText('Approve reviewed candidate'));await screen.findByText(/Approval returned a different candidate/);expect(screen.queryByLabelText('Generated candidate review')).toBeNull();
});
test('candidate response guard rejects false acceptance, adoption and manifest mismatch',()=>{
  const candidate=candidateResult();expect(validCandidateReview(candidate)).toBe(true);
  expect(validCandidateReview({...candidate,reviewOnly:false})).toBe(false);
  expect(validCandidateReview({...candidate,oracleReport:{...candidate.oracleReport,accepted:false}})).toBe(false);
  expect(validCandidateReview({...candidate,lifecycle:{...candidate.lifecycle,workingCopyAdoption:'adopted'}})).toBe(false);
  expect(validCandidateReview({...candidate,operation:{...candidate.operation,rowCount:2}})).toBe(false);
});
