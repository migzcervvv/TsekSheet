# Phase 4 bounded source-linked mapping preview

Status: implemented in autonomous cycle 13, 2026-11-02. Exact executed checks
and remaining MVP gates are recorded in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 12's next task in PLANS.md and AUTONOMOUS_PROGRESS.md;
GOALS.md verified source-to-worksheet mapping and ADR-0002/0003. Existing
architecture, dependencies and approved profile schema remain in force.

## Implementation plan

1. Add a headless single-record mapping command on SourceLinkedFieldReview,
   keeping its wrapped FieldReviewSession private. Accept only a live workbook
   session, exact destination/session/sheet/revision/source/profile binding and
   expected live review version. Never accept cached projections, supplied
   SourceEvidence or an unwrapped substitute review as live authority.
2. Reuse VerifiedMapping's approved profile, destination, mapping and conservative
   complete worksheet analysis rules. Serialize the entire command with other
   source-linked review operations. Fresh source checks bracket worksheet
   analysis and typed projection. Check the destination again after source I/O;
   check registration/lifetime/cancellation after that final destination read.
   Reentrant corrections cannot substitute a changed review during capture.
3. Return the existing immutable VerifiedMappingPreview with full typed/literal/
   source/review/profile/destination provenance, independent column and section
   bounds, no target row and unconditional CanApply=false. Retain terminal source
   invalidation, cancellation rollback and exact correction/verification policy.
4. Test both synthetic profiles and artificial native PDF/image bytes. Cover
   independent worksheets/copies, dirty copy preservation, drafts/corrections,
   invalid types, foreign/stale bindings, incompatible analysis, source changes/
   replacement/missing/locked/released/disposed state, cancellation and lifetime
   changes during worksheet/source/final destination reads. Compare original
   hashes, projected cells/formulas/formatting, revisions and change records.
5. Run focused application and workbook tests, full solution regression, then
   frontend tests after the desktop build's dependency installation completes.
   Compare protected baseline hashes. Record actual results, failed/unexecuted
   checks, exact changed files and a next task before writing agent-status.txt.

## Boundaries and limits

No placement batch, structural preflight, candidate approval/adoption, OCR tool
choice, rendering, UI/bridge, persistence or workbook mutation. Existing
synthetic/proposal APIs and every mutation/save gate remain unchanged. Source
reads are observations at checked instants, without permanent file locks; edits
restored entirely between observations cannot be detected. PDF ordinals remain
associations, not parsed/rendered page proof. Artificial Verify commands test
policy enforcement, not actual human receipt inspection or OCR acceptance.

Future placement/candidate operations must accept live source-linked authorities
and revalidate them, never promote this preview into verification authority.

## Implemented contract

`SourceLinkedFieldReview.PreviewMapping` accepts the exact expected review and
destination bindings and keeps the wrapped review private. Approved catalog
profile checks precede capture. The existing serialized staged-command boundary
checks all registered document sources, checks the destination again, reads the
complete bounded selected worksheet, and projects typed verified fields. It
reuses VerifiedMapping's deterministic field/column/section projection, without
calling the public synthetic-review mapping API or accepting its output.

After projection, fresh source reads run again. A final destination identity/
source/sheet/revision check runs after those reads. A final registration-only
check catches source release/disposal during the destination read; review
lifetime and cancellation are checked before returning. Source authority
failure terminally invalidates the review. Cancellation, incompatible analysis
and invalid commands do not change verified values or workbook state. The review
lock spans capture; reentrant mapping/correction rejects and concurrent commands
serialize. Cached results remain historical immutable observations.

The result retains literal formula-looking text, normalized/decimal/date values,
source/page/value revisions and exact profile/review/destination identities.
Each selected sheet resolves its own columns and section bounds, including
independent dirty working copies. Every CanApply remains false. This read-only
analysis certifies neither writable targets nor structural preservation.

Next bounded task: plan and connect one live source-linked review to the existing
read-only single-record placement planner. Preserve the same private ownership,
live source/destination checks, typed provenance, explicit section/entity choices
and duplicate warnings. Reuse the deterministic planner and retain CanApply=false.
No batches, structural preflight, candidate lifecycle, adoption/integration/export,
OCR/rendering/UI or workbook writes in that next slice.
