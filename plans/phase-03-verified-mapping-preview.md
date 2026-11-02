# Phase 3 bounded verified mapping preview

Status: bounded preview implemented and tested, autonomous cycle 5, 2026-11-02.
Final solution: 587 tests passed; frontend: 70 tests passed. Phase 3 acceptance
is not claimed.

Authority: Cycle 4's handoff in `PLANS.md`, Phase 3 deterministic field mappings
and independent worksheet bindings, and ADR-0002/0003. This is a read-only
application-layer prerequisite, not permission to enable template application.

## Implementation plan

1. Add a single-record headless preview accepting a live `FieldReviewSession`,
   its explicit expected `ReviewVersion`, a live `IWorkbookSession` and an
   expected destination binding (session, worksheet, revision, source SHA-256,
   approved profile ID/hash). Never accept a snapshot or typed projection as
   verification authority. Use the existing approved catalog and schema.
2. Analyze the complete current selected worksheet using the existing bounded
   reader and its conservative header/section rejection rules. Bind each typed
   logical field to its configured header and independently resolved column.
   Retain exact literal text, normalized text, decimal/date values, field value
   revisions, source evidence and review/profile/destination identity. Capture
   resolved section bounds for inspection; produce no row insertion proposal.
3. Project the live review after worksheet analysis, then recheck the destination
   identity/revision/source. This brackets projection with stable destination
   reads and rejects a change during either analysis or projection. Review
   correction during analysis must fail the expected live version check. The
   result remains an observation at the captured revisions; subsequent edits
   can invalidate it and future operations must recheck both live authorities.
4. Return defensive read-only collections and an unconditional `CanApply=false`.
   Reject missing/foreign/stale bindings, mismatched profiles, incompatible
   worksheets, incomplete/invalid reviews and cancellation without mutations.
   Read-only compatibility does not establish editing or formula-cell safety.
5. Test both artificial profiles, independently permuted columns and worksheet
   names/lengths/bounds, literal formula-like text, optional absence, provenance,
   immutable captures, stale/foreign/corrected reviews and destination changes
   during capture. Real synthetic XLSX tests cover main sessions and independent
   copies, formulas/formatting/cell preservation and unchanged source hashes.
6. Run focused application/workbook tests, full solution regressions, then
   frontend tests sequentially after the desktop build's normal `npm ci`.
   Compare all 141 protected reference/fixture/branding/lock hashes and record
   failures and unexecuted checks in the progress tracker.

## Boundaries

No workbook edits, source registration, OCR, UI/bridge commands, persistence,
insertion, candidate generation/approval, export or production Apply. Existing
raw-input planning APIs remain proposal-only. Source hashes are supplied local
identities; document authentication and actual human acceptance remain separate.
Phase 2/3, complex-workbook preservation and offline/manual gates remain open.

## Implemented contract

`VerifiedMapping.Preview` accepts only live review/workbook sessions and explicit
expected bindings. The approved catalog profile must match both destination and
review. Complete worksheet analysis rejects ambiguous headers/sections and
unsupported captures. `Project` validates current verification/types under the
review lock after analysis; a final destination identity/source/revision/sheet
check rejects changes during capture. No shared workbook lock or mutation
capability was added. Consistency relies on the existing session contract's
monotonically advancing revision for every in-memory edit.

`VerifiedMappingPreview` defensively owns field and section lists. Each field
retains its configured header/column and `TypedReviewField` (literal and
normalized text, kind, nullable decimal/date, source, value revision). The result
retains full review/profile and destination bindings and the resolved worksheet
name/section bounds. It carries no target data row and always has `CanApply=false`.
Optional absent fields keep empty text and null typed/source values. Formula-like
text stays text. Compatibility for this read-only preview never certifies a
future write, protection policy, candidate preservation or integration.

Errors retain existing profile, worksheet-analysis, review and value-validation
codes. New destination guards return `INVALID_MAPPING_REQUEST` for missing or
invalid bindings and `INVALID_MAPPING_DESTINATION` for foreign session/source
identity. Unknown sheets use `UNKNOWN_WORKSHEET`, stale edits `STALE_REVISION`,
and mismatched profile hashes `STALE_PROFILE`. Rejection/cancellation modifies
neither session; subsequent correction requires fresh verification and preview.

Tests cover both generic profiles on independently shifted/permuted worksheets,
draft/stale/foreign/forged reviews, wrong profiles/sources/revisions, changes
during capture, final destination rechecking, literal and typed provenance,
optional absence and immutable observations. Synthetic XLSX controls compare
every projected cell (including formulas/formatting and hidden context sheets),
session/copy state and source hashes on success and incompatible-sheet failure.
One initial test-fixture error targeted an empty cell for a deliberate copy edit;
the engine correctly rejected it. Seeding that synthetic note cell repaired the
fixture; the product admission policy was preserved. Exact runs and remaining
acceptance gates are recorded in the Cycle 5 progress entry.
