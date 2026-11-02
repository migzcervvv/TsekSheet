# Phase 4 bounded source-linked field review

Status: implemented in autonomous cycle 12, 2026-11-02. Exact executed checks
and remaining MVP gates are recorded in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 11 next task in PLANS.md and AUTONOMOUS_PROGRESS.md;
GOALS.md local source-linked human verification and Phase 4 verification gates.
No architecture, dependency or OCR tooling decision changes.

## Implementation plan

1. Add a headless in-memory source-linked review that privately owns its field
   review and uses one live LocalDocumentSources registry. Drafts/corrections
   accept exact registered versions/page associations, never caller-created
   SourceEvidence or an existing unwrapped review. Snapshots remain observations.
2. Obtain evidence from fresh source reads. Check all supplied field sources
   before and after review creation, correction, explicit verification,
   retraction, validation and typed projection. Reject stale/foreign commands,
   changed/replaced/missing/locked/released/disposed sources and cancellation.
3. Stage changes on a private field-review fork preserving exact identity and
   revisions. Commit only after final live checks. Failure/cancellation cannot
   leave a new correction/verification committed. Source authority failures on
   retained associations terminally invalidate this review; restoring files or
   re-registering never revives it. Disposal forgets review state without owning
   or disposing the shared registry or deleting originals. Serialize commands.
4. Preserve existing synthetic/proposal-only review and mapping contracts. Never
   expose the wrapped FieldReviewSession or treat cached typed records as live
   verification authority. No source-linked mapping/adoption, workbook writes,
   renderer/OCR selection, UI/bridge, persistence or temporary copies here.
5. Test both generic synthetic profiles and artificial local PDF/image bytes.
   Cover literal/typed provenance, absent optional fields, explicit verification,
   correction invalidation, exact versions, independent sources, final-check
   cancellation/release/disposal, staged rollback and serialized commands. Use
   deterministic read seams and actual native reads, unchanged originals and
   exclusive handle checks. Run focused and related solution/frontend regressions.
6. Record exact files/commands/results, unfinished work and the next independent
   task in the progress tracker before writing the supervisor status.

## Limits

Source reads establish observations at checked instants, not continuous custody.
Changes restored entirely between observations remain undetectable. PDF page
ordinals are requested associations (1-10000), not parsed page counts or rendered
proof; images allow page one only. No actual receipt, human inspection, OCR or
recognition acceptance is claimed by artificial explicit verification tests.
Typed output is an observation and is not accepted as mutation authorization.
Existing supplied-evidence APIs remain synthetic/proposal-only and unchanged.
Source-aware downstream mapping must recheck the private live review and source
registry in a later documented slice. All workbook mutation/save gates remain.

## Implemented contract

`SourceLinkedFieldReview` accepts registered versions/page associations and owns
the wrapped `FieldReviewSession` privately. There is no constructor from a raw
review and no accessor for it. Each command checks exact review/profile versions
and all supplied field associations through live reads; no availability check
changes a field's verification state. Corrections, including identical text or
a new source, invalidate that field's verification. Optional absent fields
remain absent; supplied blank fields require explicit verification.

Changes are staged on an internal independent `FieldReviewSession.Fork`, keeping
the review ID, profile and exact revisions. Only successful final source checks
commit the staged review. Cancellation retains the previous review and permits
retry. Source failures invalidate the source-linked review terminally; cached
field statuses remain historical observations under the separate Invalidated
status. Bad commands/new foreign associations alone do not invalidate unrelated
current associations. Metadata checks after the read loop catch an earlier
registration's release during a later read. Per-review serialization rejects
reentrant commands; disposal during reads cannot publish results. Disposal
forgets field state and leaves the shared source registry to its owner.

Validate and Project also bracket their results with source checks. Typed
projection retains literal text, normalized/decimal/date values and exact
source/review/profile provenance, with the existing field constraints. It
remains an observation: existing synthetic mapping APIs cannot consume this
private review, and cached results supply no mutation authority.

Tests use native bounded reads of generated signature-bearing bytes plus an
internal read callback for deterministic cancellation, edits, release/disposal
and command interleaving. No actual image decoding or parsed PDF page counts
are claimed. The next bounded task connects this live review to the existing
read-only single-record mapping preview, with fresh checks around destination
analysis and projection; it must retain private review ownership and CanApply
false without extending candidate/mutation permissions.
