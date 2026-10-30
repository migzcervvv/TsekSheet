import {act, cleanup, fireEvent, render, screen} from '@testing-library/react';
import {afterEach, expect, test, vi} from 'vitest';
import SectionReview from './SectionReview';
import {BridgeClient} from './bridge/client';
import type {Transport} from './bridge/client';
import {validSectionReview} from './bridge/sectionReview';
import {reviewBook, reviewFixture} from './reviewTestFixture';

let client: BridgeClient;
afterEach(() => { cleanup(); client?.dispose(); vi.useRealTimers(); });

function fakeTransport() {
  const listeners = new Set<(event: {data: unknown}) => void>();
  const sent: {requestId: string; command: string; payload: unknown}[] = [];
  const transport: Transport = {
    postMessage: message => {sent.push(message as typeof sent[number]);},
    addEventListener: (_, listener) => {listeners.add(listener);},
    removeEventListener: (_, listener) => {listeners.delete(listener);},
  };
  return {transport, sent, emit: (data: unknown) => listeners.forEach(listener => listener({data}))};
}

async function setup() {
  vi.useFakeTimers();
  const host = fakeTransport();
  client = new BridgeClient(host.transport, 100);
  const status = vi.fn();
  const ui = render(<SectionReview bridge={client} book={reviewBook} sheetId={reviewBook.worksheets[0].id} disabled={false} onNavigate={() => {}} onStatus={status}/>);
  const reply = async (result: unknown, ok = true) => {
    const request = host.sent.at(-1)!;
    await act(async () => host.emit({protocolVersion:1, type:'response', requestId:request.requestId, ok, ...(ok ? {result} : {error:result})}));
  };
  fireEvent.click(screen.getByRole('button', {name:'Select profile and start review'}));
  await reply({status:'started', review:reviewFixture()});
  const decide = () => {
    fireEvent.click(screen.getByRole('button', {name:'Unassigned Rows'}));
    fireEvent.change(screen.getByRole('textbox', {name:'Review reason'}), {target:{value:'Synthetic evidence checked'}});
    fireEvent.click(screen.getByRole('button', {name:'Confirm read-only assertion'}));
  };
  return {...host, reply, decide, ui, status};
}

async function confirmDecision(s: Awaited<ReturnType<typeof setup>>) {
  s.decide();
  const snapshot = reviewFixture();
  snapshot.binding.reviewRevision = 1;
  snapshot.decisions = [(s.sent.at(-1)!.payload as {decision: typeof snapshot.decisions[number]}).decision];
  await s.reply(snapshot);
  return snapshot;
}

test('idle unmount with confirmed decisions never automatically cancels the review', async () => {
  const s = await setup();
  await confirmDecision(s);
  expect(s.status).toHaveBeenLastCalledWith(1, false, false);
  expect(s.sent.filter(request => request.command === 'sectionReview.cancel')).toHaveLength(0);
  s.ui.unmount();
  expect(s.sent.filter(request => request.command === 'sectionReview.cancel')).toHaveLength(0);
});

test('cleanup uses the latest confirmed count after a successful retraction', async () => {
  const s = await setup();
  const snapshot = await confirmDecision(s);
  fireEvent.click(screen.getByRole('button', {name:'Review History'}));
  fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  const retracted = structuredClone(snapshot);
  retracted.binding.reviewRevision++;
  retracted.decisions = [];
  await s.reply(retracted);
  expect(s.sent.filter(request => request.command === 'sectionReview.cancel')).toHaveLength(0);
  s.ui.unmount();
  expect(s.sent.filter(request => request.command === 'sectionReview.cancel')).toHaveLength(1);
});

test('cleanup never uses a previous review snapshot to cancel a different worksheet identity', async () => {
  const s = await setup();
  const otherSheet = '88888888-8888-4888-8888-888888888888';
  s.ui.rerender(<SectionReview bridge={client} book={reviewBook} sheetId={otherSheet} disabled={false} onNavigate={() => {}} onStatus={s.status}/>);
  const cancellations = () => s.sent.filter(request => request.command === 'sectionReview.cancel');
  expect(cancellations()).toHaveLength(1);
  expect(cancellations()[0].payload).toEqual({sessionId:reviewBook.sessionId, worksheetId:reviewBook.worksheets[0].id});
  s.ui.unmount();
  expect(cancellations()).toHaveLength(1);
});

test('a host-applied decision with an invalid runtime response stays pending then blocks on timeout', async () => {
  const s = await setup();
  const confirmed = await confirmDecision(s);
  s.decide();
  const applied = structuredClone(confirmed);
  applied.binding.reviewRevision++;
  applied.decisions.push((s.sent.at(-1)!.payload as {decision: typeof applied.decisions[number]}).decision);
  const invalid = {...applied, canInsert:true};
  expect(validSectionReview(invalid)).toBe(false);
  await s.reply(invalid);
  expect(s.status).toHaveBeenLastCalledWith(1, true, false);
  const count = s.sent.length;
  fireEvent.click(screen.getByRole('button', {name:'Confirm read-only assertion'}));
  expect(s.sent).toHaveLength(count);
  await act(async () => { await vi.advanceTimersByTimeAsync(101); });
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  expect(s.status).toHaveBeenLastCalledWith(2, false, true);
  fireEvent.click(screen.getByRole('button', {name:'Overview'}));
  expect(screen.getByText(/review revision 1$/)).toBeDefined();
  fireEvent.click(screen.getByRole('button', {name:'Review History'}));
  expect(screen.getAllByRole('button', {name:/Retract assertion/})).toHaveLength(1);
  expect((screen.getByRole('button', {name:/Retract assertion/}) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  fireEvent.click(screen.getByRole('button', {name:'Unassigned Rows'}));
  fireEvent.click(screen.getByRole('button', {name:'Confirm read-only assertion'}));
  expect(s.sent).toHaveLength(count);
});

test.each(['identity', 'revision'])('an invalid retraction %s preserves the confirmed snapshot and blocks both mutations', async mismatch => {
  const s = await setup();
  const confirmed = await confirmDecision(s);
  fireEvent.click(screen.getByRole('button', {name:'Review History'}));
  fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  const applied = structuredClone(confirmed);
  applied.binding.reviewRevision++;
  applied.decisions = [];
  if (mismatch === 'identity') applied.binding.reviewId = '77777777-7777-4777-8777-777777777777';
  else applied.binding.reviewRevision++;
  expect(validSectionReview(applied)).toBe(true); // Rejected by the component binding check, not the bridge validator.
  await s.reply(applied);
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  expect(s.status).toHaveBeenLastCalledWith(1, false, true);
  expect(screen.getAllByRole('button', {name:/Retract assertion/})).toHaveLength(1);
  expect((screen.getByRole('button', {name:/Retract assertion/}) as HTMLButtonElement).disabled).toBe(true);
  const count = s.sent.length;
  fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  fireEvent.click(screen.getByRole('button', {name:'Overview'}));
  expect(screen.getByText(/review revision 1$/)).toBeDefined();
  fireEvent.click(screen.getByRole('button', {name:'Unassigned Rows'}));
  fireEvent.click(screen.getByRole('button', {name:'Confirm read-only assertion'}));
  expect(s.sent).toHaveLength(count);
});

test.each(['decision', 'retraction'])('a dropped %s reply blocks mutations, preserves confirmed history and ignores late replies', async operation => {
  const s = await setup();
  s.decide();
  const snapshot = reviewFixture();
  snapshot.binding.reviewRevision = 1;
  // The host commits a decision before sending its response.
  snapshot.decisions = [(s.sent.at(-1)!.payload as {decision: typeof snapshot.decisions[number]}).decision];
  if (operation === 'retraction') {
    await s.reply(snapshot);
    fireEvent.click(screen.getByRole('button', {name:'Review History'}));
    fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  }
  const lost = s.sent.at(-1)!;
  await act(async () => { await vi.advanceTimersByTimeAsync(101); });
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  expect(s.status).toHaveBeenLastCalledWith(1, false, true);
  fireEvent.click(screen.getByRole('button', {name:'Review History'}));
  expect(screen.queryAllByRole('button', {name:/Retract assertion/})).toHaveLength(operation === 'retraction' ? 1 : 0);
  if (operation === 'retraction') expect((screen.getByRole('button', {name:/Retract assertion/}) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', {name:'Overview'}));
  expect(screen.getByText(new RegExp(`review revision ${operation === 'retraction' ? 1 : 0}$`))).toBeDefined();
  const count = s.sent.length;
  fireEvent.click(screen.getByRole('button', {name:'Unassigned Rows'}));
  fireEvent.click(screen.getByRole('button', {name:'Confirm read-only assertion'}));
  expect(s.sent).toHaveLength(count);
  const applied = structuredClone(snapshot);
  if (operation === 'retraction') {applied.binding.reviewRevision++; applied.decisions = [];}
  await act(async () => s.emit({protocolVersion:1, type:'response', requestId:lost.requestId, ok:true, result:applied}));
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  s.ui.unmount();
  expect(s.sent).toHaveLength(count); // Uncertain state must not trigger automatic discard.
});

test.each(['decision', 'retraction'])('authoritative %s validation rejection keeps the review usable', async operation => {
  const s = await setup(); s.decide();
  if (operation === 'retraction') {
    const snapshot = reviewFixture();
    snapshot.binding.reviewRevision = 1;
    snapshot.decisions = [(s.sent.at(-1)!.payload as {decision: typeof snapshot.decisions[number]}).decision];
    await s.reply(snapshot);
    fireEvent.click(screen.getByRole('button', {name:'Review History'}));
    fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  }
  await s.reply({code:'INVALID_REVIEW', message:'Documented reason rejected.'}, false);
  expect(screen.queryByText(/Review requires recovery/)).toBeNull();
  const count = s.sent.length;
  fireEvent.click(screen.getByRole('button', {name:operation === 'retraction' ? /Retract assertion/ : 'Confirm read-only assertion'}));
  expect(s.sent).toHaveLength(count + 1);
});

test('failed explicit cancellation retains the snapshot and never announces closure', async () => {
  const s = await setup(); s.decide();
  await act(async () => { await vi.advanceTimersByTimeAsync(101); });
  fireEvent.click(screen.getByRole('button', {name:'Overview'}));
  fireEvent.click(screen.getByRole('button', {name:'Close section review'}));
  fireEvent.click(screen.getByRole('button', {name:'Discard & Close'}));
  expect(screen.getByRole('table', {name:'Review coverage'})).toBeDefined();
  const lostCancel = s.sent.at(-1)!;
  await act(async () => { await vi.advanceTimersByTimeAsync(101); });
  expect(screen.getByRole('table', {name:'Review coverage'})).toBeDefined();
  expect(screen.queryByText(/Review closed\./)).toBeNull();
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  await act(async () => s.emit({protocolVersion:1, type:'response', requestId:lostCancel.requestId, ok:true, result:{status:'cancelled'}}));
  expect(screen.queryByText(/Review closed\./)).toBeNull();
  fireEvent.click(screen.getByRole('button', {name:'Close section review'}));
  fireEvent.click(screen.getByRole('button', {name:'Discard & Close'}));
  await s.reply({status:'cancelled'});
  expect(screen.queryByRole('table')).toBeNull();
  expect(screen.getByText(/Review closed\./)).toBeDefined();
});

test.each(['decision', 'retraction'])('bridge disposal during a pending %s blocks the mounted review', async operation => {
  const s = await setup(); s.decide();
  if (operation === 'retraction') {
    const snapshot = reviewFixture(); snapshot.binding.reviewRevision = 1;
    snapshot.decisions = [(s.sent.at(-1)!.payload as {decision: typeof snapshot.decisions[number]}).decision];
    await s.reply(snapshot);
    fireEvent.click(screen.getByRole('button', {name:'Review History'}));
    fireEvent.click(screen.getByRole('button', {name:/Retract assertion/}));
  }
  await act(async () => client.dispose());
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
});

test('unmount during a pending mutation does not send cancellation or publish a late result', async () => {
  const s = await setup(); s.decide();
  const count = s.sent.length, calls = s.status.mock.calls.length;
  const lost = s.sent.at(-1)!;
  s.ui.unmount();
  await act(async () => s.emit({protocolVersion:1, type:'response', requestId:lost.requestId, ok:true, result:reviewFixture()}));
  await act(async () => { await vi.advanceTimersByTimeAsync(101); });
  expect(s.sent).toHaveLength(count);
  expect(s.status.mock.calls).toHaveLength(calls);
});

test('authoritative cleanup rejection retains confirmed history without claiming closure or inventing uncertainty', async () => {
  const s = await setup(); s.decide();
  const snapshot = reviewFixture(); snapshot.binding.reviewRevision = 1;
  snapshot.decisions = [(s.sent.at(-1)!.payload as {decision: typeof snapshot.decisions[number]}).decision];
  await s.reply(snapshot);
  fireEvent.click(screen.getByRole('button', {name:'Close section review'}));
  fireEvent.click(screen.getByRole('button', {name:'Discard & Close'}));
  await s.reply({code:'BUSY', message:'Cleanup unavailable.'}, false);
  expect(screen.queryByText(/Review requires recovery/)).toBeNull();
  fireEvent.click(screen.getByRole('button', {name:'Review History'}));
  expect((screen.getByRole('button', {name:/Retract assertion/}) as HTMLButtonElement).disabled).toBe(false);
  expect(screen.queryByText(/Review closed\./)).toBeNull();
});

test('an uncertain start with zero decisions still requires explicit discard confirmation', async () => {
  const s = await setup();
  fireEvent.click(screen.getByRole('button', {name:'Select profile and start review'}));
  await act(async () => { await vi.advanceTimersByTimeAsync(120001); });
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
  expect(s.status).toHaveBeenLastCalledWith(0, false, true);
  fireEvent.click(screen.getByRole('button', {name:'Close section review'}));
  expect(screen.getByRole('alertdialog')).toBeDefined();
  expect(s.sent.at(-1)!.command).toBe('sectionReview.start');
  fireEvent.click(screen.getByRole('button', {name:'Continue Reviewing'}));
  expect(screen.getByText(/Review requires recovery/)).toBeDefined();
});
