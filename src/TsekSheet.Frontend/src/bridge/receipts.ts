import type { WorkflowPlan } from './workflows';
import { validWorkflowPlan } from './workflows';
export interface ReviewVersion { reviewId: string; revision: number; profileId: string; profileHash: string }
export interface ReceiptField { key: string; value: string; valueRevision: number; status: 'Draft' | 'Corrected' | 'Verified' }
export interface ReceiptState {
  source: { displayName: string; document: { version: { documentId: string; revision: number; sha256: string }; kind: number; byteLength: number; status: number } } | null;
  version: ReviewVersion | null; pageNumber: number; status: 'NotStarted' | 'Active' | 'Invalidated'; fields: ReceiptField[];
}
export interface ReceiptDestination { sessionId: string; worksheetId: string; revision: number; sourceSha256: string; profileId: string; profileHash: string }
export interface ReceiptPreview { canApply: false; proposal: WorkflowPlan; mapping: { canApply: false; worksheetName: string; destination: ReceiptDestination; review: ReviewVersion;
  sections: { id: string; label: string; headingRow: number; firstDataRow: number; subtotalRow: number }[];
  fields: { header: string; column: number; value: { key: string; kind: string; literalText: string; normalizedText: string } }[] } }
type Obj = Record<string, unknown>;
const obj = (v: unknown): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v);
const str = (v: unknown): v is string => typeof v === 'string' && v.length <= 256;
const num = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) >= 0;
const guid = (v: unknown) => typeof v === 'string' && /^[a-f\d]{8}-(?:[a-f\d]{4}-){3}[a-f\d]{12}$/i.test(v);
const hash = (v: unknown) => typeof v === 'string' && /^[a-f\d]{64}$/i.test(v);
const version = (v: unknown) => obj(v) && guid(v.reviewId) && num(v.revision) && str(v.profileId) && hash(v.profileHash);
export function validReceiptState(v: unknown): boolean {
  if (!obj(v) || !num(v.pageNumber) || v.pageNumber < 1 || v.pageNumber > 10000 || !['NotStarted', 'Active', 'Invalidated'].includes(String(v.status)) || !Array.isArray(v.fields) || v.fields.length > 16) return false;
  if (v.source !== null) {
    if (!obj(v.source) || !str(v.source.displayName) || /[\\/]/.test(v.source.displayName) || !obj(v.source.document)) return false;
    const d = v.source.document;
    if (!obj(d.version) || !guid(d.version.documentId) || !num(d.version.revision) || !hash(d.version.sha256) || ![0, 1, 2].includes(Number(d.kind)) || !num(d.byteLength) || d.byteLength > 33554432 || d.status !== 0) return false;
  }
  return (v.version === null ? v.fields.length === 0 && v.status === 'NotStarted' : v.source !== null && version(v.version) && v.status !== 'NotStarted')
    && new Set(v.fields.map(f => obj(f) ? f.key : null)).size === v.fields.length
    && v.fields.every(f => obj(f) && str(f.key) && str(f.value) && num(f.valueRevision) && ['Draft', 'Corrected', 'Verified'].includes(String(f.status)));
}
export function validReceiptPreview(v: unknown): boolean {
  if (!obj(v) || v.canApply !== false || !validWorkflowPlan(v.proposal) || !obj(v.mapping)) return false;
  const m = v.mapping;
  return m.canApply === false && str(m.worksheetName) && version(m.review) && obj(m.destination)
    && guid(m.destination.sessionId) && guid(m.destination.worksheetId) && num(m.destination.revision) && hash(m.destination.sourceSha256) && str(m.destination.profileId) && hash(m.destination.profileHash)
    && Array.isArray(m.sections) && m.sections.length <= 16 && m.sections.every(s => obj(s) && str(s.id) && str(s.label) && num(s.headingRow) && num(s.firstDataRow) && num(s.subtotalRow))
    && Array.isArray(m.fields) && m.fields.length <= 16 && m.fields.every(f => obj(f) && str(f.header) && num(f.column) && obj(f.value) && str(f.value.key) && str(f.value.kind) && str(f.value.literalText) && str(f.value.normalizedText));
}

export interface SourceRender {source:NonNullable<ReceiptState['source']>['document']['version'];page:{pageNumber:number;pageCount:number;width:number;height:number;pngBase64:string}}
export function validSourceRender(v:unknown):boolean {
 if(!obj(v)||!obj(v.source)||!guid(v.source.documentId)||!num(v.source.revision)||!hash(v.source.sha256)||!obj(v.page))return false;
 const p=v.page;
 return num(p.pageNumber)&&p.pageNumber>=1&&num(p.pageCount)&&p.pageCount<=1000&&p.pageNumber<=p.pageCount
 &&num(p.width)&&p.width>=1&&p.width<=1600&&num(p.height)&&p.height>=1&&p.height<=1600
 &&typeof p.pngBase64==='string'&&p.pngBase64.length<=16777216&&p.pngBase64.startsWith('iVBORw0KGgo')&&/^[A-Za-z0-9+/]+={0,2}$/.test(p.pngBase64);
}
