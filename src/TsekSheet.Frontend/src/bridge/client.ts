export interface Transport {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', handler: (event: { data: unknown }) => void): void;
  removeEventListener(type: 'message', handler: (event: { data: unknown }) => void): void;
}
export interface Info { applicationName: string; version: string; platform: string; hasUnsavedWork: boolean }
export interface Handshake { selectedVersion: number; applicationName: string; version: string; capabilities: { diagnostics: boolean; fileSelection: boolean; workbooks: boolean; templates: boolean; ocr: boolean } }
export interface Selection { status: 'selected' | 'cancelled'; selectionId?: string; displayName?: string }
export interface Progress { operationId: string; completed: number; total: number }
export class BridgeError extends Error { constructor(public readonly code: string, message: string) { super(message); } }
export interface Worksheet { id: string; name: string; order: number; visibility: 'Visible' | 'Hidden' | 'VeryHidden'; lastRow: number; lastColumn: number }
export interface Workbook { sessionId: string; parentSessionId: string | null; displayName: string; sourceSha256: string; revision: number; isDirty: boolean; editingAllowed: boolean; creator: string; title: string; worksheets: Worksheet[]; findings: { code: string; message: string }[] }
export interface Cell { row: number; column: number; value: string; kind: string; formula: string | null; truncated: boolean; format: { numberFormat: string; numberFormatId: number; bold: boolean; italic: boolean; fillColor: string } }
export interface Viewport { sessionId: string; worksheetId: string; startRow: number; startColumn: number; rows: number; columns: number; cells: Cell[] }
export interface WorkingCopy { sessionId: string; sourceWorksheetId: string; baseRevision: number; revision: number; isDirty: boolean; changeCount: number }
export interface SaveReceipt { status: 'saved'; sessionId: string; savedRevision: number; sourceStillDirty: boolean; rebound: false; cancellationAfterCommit: boolean; destinationSha256: string; displayName: string }
export interface MergePayload { sessionId: string; workingCopyId: string; worksheetId: string; expectedCopyRevision: number; expectedRevision: number; expectedBaseRevision: number }
export interface MergePreview { destinationName: string; pendingCells: number; canApply: boolean; conflicts: { code: string; message: string }[] }
interface Results {
  'workflow.profiles': { profiles: WorkflowProfile[] };
  'workbook.planRecords': WorkflowPlan;
  'workbook.generateCandidate': CandidateReview;
  'workbook.approveCandidate': CandidateReview;
  'workbook.cancelCandidate': {status:'cancelled'};
  'foundation.handshake': Handshake;
  'foundation.getInfo': Info;
  'foundation.selectFile': Selection;
  'diagnostics.run': { status: 'completed' | 'cancelled' };
  'diagnostics.cancel': { status: 'requested' | 'notRunning' };
  'workbook.open': { status: 'cancelled' | 'opened'; workbook?: Workbook | null };
  'workbook.readViewport': Viewport;
  'workbook.close': { status: 'closed' };
  'workbook.discard': { status: 'closed' };
  'workbook.createCopy': WorkingCopy;
  'workbook.editCopy': WorkingCopy;
  'workbook.saveAsNew': { status: 'saved' | 'cancelled'; receipt?: SaveReceipt | null };
  'workbook.previewMerge': MergePreview;
  'workbook.merge': { workbook: Workbook; appliedCells: number };
}
interface Payloads {
  'workflow.profiles': Record<string, never>;
  'workbook.planRecords': WorkflowRequest;
  'workbook.generateCandidate': WorkflowRequest & {destinationSessionId:string; expectedDestinationRevision:number};
  'workbook.approveCandidate': CandidateApproval;
  'workbook.cancelCandidate': {sessionId:string; candidateId:string; expectedRevision:number};
  'foundation.handshake': { supportedVersions: number[] };
  'foundation.getInfo': Record<string, never>;
  'foundation.selectFile': Record<string, never>;
  'diagnostics.run': { steps: number; delayMs: number };
  'diagnostics.cancel': { operationId: string };
  'workbook.open': Record<string, never>;
  'workbook.readViewport': { sessionId: string; worksheetId: string; startRow: number; startColumn: number; rows: number; columns: number };
  'workbook.close': { sessionId: string };
  'workbook.discard': { sessionId: string };
  'workbook.createCopy': { sessionId: string; worksheetId: string; expectedRevision: number };
  'workbook.editCopy': { sessionId: string; worksheetId: string; row: number; column: number; kind: 'text' | 'number' | 'date' | 'formula'; value: string; expectedRevision: number };
  'workbook.saveAsNew': { sessionId: string; expectedRevision: number };
  'workbook.previewMerge': MergePayload;
  'workbook.merge': MergePayload;
}
type Command = keyof Results;
type ObjectValue = Record<string, unknown>;
const object = (value: unknown): value is ObjectValue => typeof value === 'object' && value !== null && !Array.isArray(value);
const text = (v: unknown): v is string => typeof v === 'string' && v.length <= 512;
const content = (v: unknown): v is string => typeof v === 'string' && v.length <= 1024;
const guid = (v: unknown): v is string => typeof v === 'string' && /^[a-f\d]{8}-(?:[a-f\d]{4}-){3}[a-f\d]{12}$/i.test(v);
const integer = (v: unknown, min: number, max: number): v is number => typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;
function validWorkbook(v: unknown): v is Workbook {
  return object(v) && guid(v.sessionId) && (v.parentSessionId === null || guid(v.parentSessionId)) && text(v.displayName) && !/[\\/]/.test(v.displayName)
    && typeof v.sourceSha256 === 'string' && /^[a-f\d]{64}$/i.test(v.sourceSha256) && integer(v.revision, 0, 2147483647) && typeof v.isDirty === 'boolean' && typeof v.editingAllowed === 'boolean'
    && content(v.creator) && content(v.title) && Array.isArray(v.worksheets) && v.worksheets.length > 0 && v.worksheets.length <= 32
    && new Set(v.worksheets.map((s: unknown) => object(s) ? s.id : null)).size === v.worksheets.length
    && v.worksheets.every((s: unknown) => object(s) && guid(s.id) && text(s.name) && integer(s.order, 1, 32) && ['Visible', 'Hidden', 'VeryHidden'].includes(String(s.visibility)) && integer(s.lastRow, 0, 1048576) && integer(s.lastColumn, 0, 16384))
    && Array.isArray(v.findings) && v.findings.length <= 32 && v.findings.every((f: unknown) => object(f) && text(f.code) && content(f.message));
}
function validViewport(v: ObjectValue): boolean {
  if (!guid(v.sessionId) || !guid(v.worksheetId) || !integer(v.startRow, 1, 1048576) || !integer(v.startColumn, 1, 16384) || !integer(v.rows, 1, 20) || !integer(v.columns, 1, 10) || v.startRow + v.rows - 1 > 1048576 || v.startColumn + v.columns - 1 > 16384 || !Array.isArray(v.cells) || v.cells.length !== v.rows * v.columns) return false;
  const keys = new Set<string>();
  return v.cells.every((c: unknown) => {
    if (!object(c) || !integer(c.row, v.startRow as number, (v.startRow as number) + (v.rows as number) - 1) || !integer(c.column, v.startColumn as number, (v.startColumn as number) + (v.columns as number) - 1) || !content(c.value) || !text(c.kind) || !(c.formula === null || content(c.formula)) || typeof c.truncated !== 'boolean' || !object(c.format) || !content(c.format.numberFormat) || !integer(c.format.numberFormatId, -1, 2147483647) || typeof c.format.bold !== 'boolean' || typeof c.format.italic !== 'boolean' || typeof c.format.fillColor !== 'string' || !/^(?:[a-f\d]{8})?$/i.test(c.format.fillColor)) return false;
    const key = `${c.row}:${c.column}`; if (keys.has(key) || c.row !== (v.startRow as number) + Math.floor(keys.size / (v.columns as number)) || c.column !== (v.startColumn as number) + keys.size % (v.columns as number)) return false; keys.add(key); return true;
  });
}
function validResult(command: Command, r: unknown): boolean {
  if (!object(r)) return false;
  switch (command) {
    case 'workflow.profiles': return validProfiles(r);
    case 'workbook.planRecords': return validWorkflowPlan(r);
    case 'workbook.generateCandidate': case 'workbook.approveCandidate': return validCandidateReview(r);
    case 'workbook.cancelCandidate': return r.status==='cancelled';
    case 'foundation.handshake': return r.selectedVersion === 1 && text(r.applicationName) && text(r.version) && object(r.capabilities)
      && ['diagnostics', 'fileSelection', 'workbooks', 'templates', 'ocr'].every(k => typeof (r.capabilities as ObjectValue)[k] === 'boolean');
    case 'foundation.getInfo': return text(r.applicationName) && text(r.version) && text(r.platform) && typeof r.hasUnsavedWork === 'boolean';
    case 'foundation.selectFile': return r.status === 'cancelled' || (r.status === 'selected' && text(r.selectionId) && text(r.displayName) && !/[\\/]/.test(r.displayName));
    case 'diagnostics.run': return r.status === 'completed' || r.status === 'cancelled';
    case 'diagnostics.cancel': return r.status === 'requested' || r.status === 'notRunning';
    case 'workbook.open': return r.status === 'cancelled' || (r.status === 'opened' && validWorkbook(r.workbook) && r.workbook.revision === 0 && !r.workbook.isDirty);
    case 'workbook.previewMerge': return text(r.destinationName) && integer(r.pendingCells, 0, 1000) && typeof r.canApply === 'boolean'
      && Array.isArray(r.conflicts) && r.conflicts.length <= 16 && r.conflicts.every((c: unknown) => object(c) && text(c.code) && content(c.message)) && r.canApply === (r.conflicts.length === 0);
    case 'workbook.merge': return validWorkbook(r.workbook) && integer(r.appliedCells, 0, 1000);
    case 'workbook.readViewport': return validViewport(r);
    case 'workbook.close': return r.status === 'closed';
    case 'workbook.discard': return r.status === 'closed';
    case 'workbook.saveAsNew': return r.status === 'cancelled' || (r.status === 'saved' && object(r.receipt) && r.receipt.status === 'saved' && guid(r.receipt.sessionId) && integer(r.receipt.savedRevision, 0, 2147483647) && typeof r.receipt.sourceStillDirty === 'boolean' && r.receipt.rebound === false && typeof r.receipt.cancellationAfterCommit === 'boolean' && typeof r.receipt.destinationSha256 === 'string' && /^[a-f\d]{64}$/i.test(r.receipt.destinationSha256) && text(r.receipt.displayName) && !/[\\/]/.test(r.receipt.displayName));
    case 'workbook.createCopy': case 'workbook.editCopy': return guid(r.sessionId) && guid(r.sourceWorksheetId) && integer(r.baseRevision, 0, 2147483647) && integer(r.revision, 0, 2147483647) && typeof r.isDirty === 'boolean' && integer(r.changeCount, 0, 1000);
  }
}

export function nativeTransport(): Transport | undefined {
  const host = window as unknown as { chrome?: { webview?: Transport } };
  const transport = host.chrome?.webview;
  return transport && typeof transport.postMessage === 'function' && typeof transport.addEventListener === 'function'
    && typeof transport.removeEventListener === 'function' ? transport : undefined;
}

export class BridgeClient {
  private pending = new Map<string, { command: Command; resolve: (value: unknown) => void; reject: (error: Error) => void; timer: ReturnType<typeof setTimeout> }>();
  private disposed = false;
  onProgress?: (progress: Progress) => void;
  constructor(private transport: Transport, private timeoutMs = 30000) { transport.addEventListener('message', this.receive); }

  request<C extends Command>(command: C, payload: Payloads[C]) {
    const id = crypto.randomUUID();
    const promise = new Promise<Results[C]>((resolve, reject) => {
      if (this.disposed) { reject(new Error('Desktop connection closed.')); return; }
      if (this.pending.size >= 16) { reject(new Error('Too many pending requests.')); return; }
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error('Desktop request timed out. Its outcome is unknown.')); }, command === 'foundation.selectFile' || command === 'workbook.open' || command === 'workbook.saveAsNew' ? 120000 : this.timeoutMs);
      this.pending.set(id, { command, resolve: value => resolve(value as Results[C]), reject, timer });
      try { this.transport.postMessage({ protocolVersion: 1, requestId: id, command, payload }); }
      catch { this.finish(id); reject(new Error('Desktop connection is unavailable.')); }
    });
    return { id, promise };
  }

  private finish(id: string) { const p = this.pending.get(id); if (p) clearTimeout(p.timer); this.pending.delete(id); return p; }
  private receive = (event: { data: unknown }) => {
    const m = event.data;
    if (!object(m) || m.protocolVersion !== 1) return;
    if (m.type === 'event') {
      if (m.name !== 'diagnostics.progress' || typeof m.operationId !== 'string' || this.pending.get(m.operationId)?.command !== 'diagnostics.run' || !object(m.progress)) return;
      const { completed, total } = m.progress;
      if (typeof completed !== 'number' || typeof total !== 'number' || !Number.isInteger(completed) || !Number.isInteger(total) || total < 1 || total > 100 || completed < 0 || completed > total) return;
      this.onProgress?.({ operationId: m.operationId, completed, total });
      return;
    }
    if (m.type !== 'response' || typeof m.requestId !== 'string') return;
    const p = this.pending.get(m.requestId);
    if (!p) return;
    if (m.ok === true && validResult(p.command, m.result)) { this.finish(m.requestId); p.resolve(m.result); }
    else if (m.ok === false && object(m.error) && text(m.error.code) && text(m.error.message)) { this.finish(m.requestId); p.reject(new BridgeError(m.error.code, m.error.message)); }
  };

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    this.transport.removeEventListener('message', this.receive);
    for (const [id, p] of this.pending) { this.finish(id); p.reject(new Error('Desktop connection closed.')); }
  }
}
import { validProfiles, validWorkflowPlan, validCandidateReview } from './workflows';
import type { WorkflowProfile, WorkflowRequest, WorkflowPlan, CandidateReview, CandidateApproval } from './workflows';
