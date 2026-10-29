export interface ProfileField { key: string; header: string; kind: 'text' | 'decimal' | 'date'; required: boolean; maxLength: number }
export interface WorkflowProfile { id: string; revision: number; displayName: string; profileHash: string; fields: ProfileField[] }
export interface WorkflowRecord { id: string; values: Record<string, string>; selectedSectionId: string | null; selectedVendor: string | null; confirmNewVendor: boolean }
export interface WorkflowRequest { sessionId: string; worksheetId: string; expectedRevision: number; profileId: string; profileHash: string; records: WorkflowRecord[] }
export interface RecordProposal {
  recordId: string; vendor: string; matchReason: string;
  matches: { sectionId: string; label: string; lastRow: number; entries: number }[];
  suggestedVendors: string[]; requiresVendorChoice: boolean; requiresSectionChoice: boolean;
  sectionId: string | null; sectionLabel: string | null; baseInsertionRow: number | null; proposedRow: number | null; affectedThroughRow: number;
  fields: { key: string; header: string; column: number; kind: string; value: string }[];
  duplicates: { existingRow: number | null; sectionId: string | null; batchRecordId: string | null; reason: string }[];
}
export interface WorkflowPlan { sessionId: string; worksheetId: string; revision: number; sourceSha256: string; profileId: string; profileHash: string;
  readyForReview: boolean; canApply: false; structuralStatus: string; review: { planner: string; preflight: string; generation: 'not-generated'; oracle: 'not-run'; approval: 'not-approved'; workingCopyAdoption: 'not-adopted'; integration: 'production-disabled'; export: 'unmerged-changes-excluded' }; insertionBlockedReason: string; requiredConfirmations: string[]; integrityConcerns: string[]; records: RecordProposal[] }

type Obj = Record<string, unknown>;
export interface CandidateReview {
  candidateId: string; reviewSha256: string; candidateSha256: string; manifestSha256: string; workbookName: string; worksheetName: string;
  proposal: WorkflowPlan; destinationSessionId: string; expectedDestinationRevision: number; reviewOnly: true;
  intendedWorkingCopyRevision: number; intendedDestinationRevision: number;
  lifecycle: { planner: string; preflight: string; generation: 'generated'; oracle: 'accepted'; approval: 'not-approved'|'approved'; workingCopyAdoption: 'not-adopted'; integration: 'not-integrated'; export: 'not-exported' };
  formulaImpacts: string[]; styleAssumptions: string[]; rejectionReasons: string[];
  operation: { insertionBoundary: number; rowCount: number; templateRow: number; firstDataRow: number; subtotalRow: number; subtotalPolicy: string };
  oracleReport: { accepted: true; allowedChanges: string[]; findings: [] };
}
export interface CandidateApproval {
  sessionId: string; destinationSessionId: string; worksheetId: string; candidateId: string; reviewSha256: string; candidateSha256: string;
  manifestSha256: string; sourceSha256: string; expectedCopyRevision: number; expectedDestinationRevision: number; profileId: string; profileHash: string; duplicatesReviewed: boolean;
}
const obj = (v: unknown): v is Obj => v !== null && typeof v === 'object' && !Array.isArray(v);
const text = (v: unknown, max = 1024): v is string => typeof v === 'string' && v.length <= max;
const num = (v: unknown, max = 2147483647): v is number => typeof v === 'number' && Number.isInteger(v) && v >= 0 && v <= max;
const guid = (v: unknown) => typeof v === 'string' && /^[a-f\d]{8}-(?:[a-f\d]{4}-){3}[a-f\d]{12}$/i.test(v);
const hash = (v: unknown) => typeof v === 'string' && /^[a-f\d]{64}$/i.test(v);
const nullableText = (v: unknown) => v === null || text(v, 128);
const row = (v: unknown): boolean => v === null || (num(v, 2020) && v > 0);
const list = (v: unknown, max: number, check: (x: unknown) => boolean): boolean => Array.isArray(v) && v.length <= max && v.every(check);
export function validProfiles(v: unknown): boolean {
  return obj(v) && Array.isArray(v.profiles) && v.profiles.length >= 1 && v.profiles.length <= 16
    && new Set(v.profiles.map(p => obj(p) ? p.id : null)).size === v.profiles.length
    && v.profiles.every(p => obj(p) && text(p.id,48) && num(p.revision) && p.revision > 0 && text(p.displayName,128) && hash(p.profileHash)
      && Array.isArray(p.fields) && p.fields.length >= 3 && p.fields.length <= 16
      && new Set(p.fields.map(f => obj(f) ? f.key : null)).size === p.fields.length
      && p.fields.every(f => obj(f) && text(f.key,48) && text(f.header,128) && ['text','decimal','date'].includes(String(f.kind)) && typeof f.required === 'boolean' && num(f.maxLength,256) && f.maxLength > 0));
}
export function validWorkflowPlan(v: unknown): boolean {
  if (!obj(v) || !guid(v.sessionId) || !guid(v.worksheetId) || !num(v.revision) || !hash(v.sourceSha256) || !text(v.profileId,48) || !hash(v.profileHash)
    || typeof v.readyForReview !== 'boolean' || v.canApply !== false || !text(v.insertionBlockedReason)
    || !['production-disabled','ambiguous-destination','duplicate-review-required','stale-proposal','unsupported-structure','experimental-envelope-only'].includes(String(v.structuralStatus))
    || !obj(v.review) || v.review.planner !== (v.readyForReview ? 'resolved' : 'choices-required') || v.review.preflight !== v.structuralStatus
    || v.review.generation !== 'not-generated' || v.review.oracle !== 'not-run' || v.review.approval !== 'not-approved'
    || v.review.workingCopyAdoption !== 'not-adopted' || v.review.integration !== 'production-disabled' || v.review.export !== 'unmerged-changes-excluded'
    || !list(v.requiredConfirmations,8,x=>text(x)) || !list(v.integrityConcerns,16,x=>text(x)) || !Array.isArray(v.records) || v.records.length < 1 || v.records.length > 20) return false;
  const valid = v.records.every(r => obj(r) && text(r.recordId,48) && text(r.vendor,128) && text(r.matchReason,128)
    && list(r.matches,16,m=>obj(m) && text(m.sectionId,48) && text(m.label,128) && num(m.lastRow,2000) && m.lastRow > 0 && num(m.entries,2000) && m.entries > 0)
    && list(r.suggestedVendors,8,x=>text(x,128)) && typeof r.requiresVendorChoice === 'boolean' && typeof r.requiresSectionChoice === 'boolean'
    && nullableText(r.sectionId) && nullableText(r.sectionLabel) && row(r.baseInsertionRow) && row(r.proposedRow) && num(r.affectedThroughRow,2000)
    && list(r.fields,16,f=>obj(f) && text(f.key,48) && text(f.header,128) && num(f.column,32) && f.column > 0 && ['text','decimal','date'].includes(String(f.kind)) && text(f.value,256))
    && list(r.duplicates,50,d=>obj(d) && row(d.existingRow) && nullableText(d.sectionId) && nullableText(d.batchRecordId) && text(d.reason)));
  return valid && v.readyForReview === v.records.every(r => obj(r) && r.baseInsertionRow !== null && r.proposedRow !== null && !r.requiresVendorChoice && !r.requiresSectionChoice);
}

export function validCandidateReview(v: unknown): boolean {
  if (!obj(v) || !guid(v.candidateId) || !hash(v.reviewSha256) || !hash(v.candidateSha256) || !hash(v.manifestSha256)
    || !text(v.workbookName,512) || !text(v.worksheetName,128) || !validWorkflowPlan(v.proposal) || !obj(v.proposal)
    || !guid(v.destinationSessionId) || !num(v.expectedDestinationRevision) || v.reviewOnly !== true
    || !num(v.intendedWorkingCopyRevision) || !num(v.intendedDestinationRevision) || v.intendedWorkingCopyRevision !== Number(v.proposal.revision)+1
    || v.intendedDestinationRevision !== Number(v.expectedDestinationRevision)+1 || !obj(v.lifecycle)
    || v.proposal.readyForReview !== true || v.lifecycle.planner !== 'resolved' || v.lifecycle.preflight !== 'eligible-experimental'
    || v.lifecycle.generation !== 'generated' || v.lifecycle.oracle !== 'accepted' || !['not-approved','approved'].includes(String(v.lifecycle.approval))
    || v.lifecycle.workingCopyAdoption !== 'not-adopted' || v.lifecycle.integration !== 'not-integrated' || v.lifecycle.export !== 'not-exported'
    || !list(v.formulaImpacts,64,x=>text(x)) || !list(v.styleAssumptions,8,x=>text(x)) || !list(v.rejectionReasons,8,x=>text(x))
    || !obj(v.operation) || !num(v.operation.rowCount,20) || Number(v.operation.rowCount)<1 || !text(v.operation.subtotalPolicy)
    || !['insertionBoundary','templateRow','firstDataRow','subtotalRow'].every(k=>num((v.operation as Obj)[k],2020) && Number((v.operation as Obj)[k])>0)
    || !obj(v.oracleReport) || v.oracleReport.accepted !== true || !list(v.oracleReport.allowedChanges,8,x=>text(x))
    || !Array.isArray(v.oracleReport.findings) || v.oracleReport.findings.length !== 0) return false;
  const operation=v.operation;
  return Array.isArray(v.proposal.records) && operation.rowCount === v.proposal.records.length
    && v.proposal.records.every(r=>obj(r) && r.baseInsertionRow===operation.insertionBoundary);
}
