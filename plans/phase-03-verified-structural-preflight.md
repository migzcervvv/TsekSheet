# Phase 3 read-only structural preflight for live verified records

Status: implemented in autonomous cycle 8, 2026-11-02. Exact commands,
results and remaining gates are in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 7 next task in PLANS.md and AUTONOMOUS_PROGRESS.md;
Phase 3 verification/mapping/capability diagnostics and ADR-0002/0003.

## Implementation plan

1. Add a headless preflight entry accepting the existing bounded 1-20 live
   review/version/choice inputs and one explicit destination. Reuse live typed
   projection, independent worksheet analysis and pure placement; never accept
   cached observations as authority. Preserve ordered per-record provenance.
2. Share the existing WorkflowPlanning.Prepare preflight precedence: unresolved
   choices first, duplicate review second, then the engine's read-only structural
   inspection. Keep planner/preflight/generation/oracle/approval/adoption/
   integration/export facts distinct. Every CanApply remains false.
3. Recheck destination identity/source/sheet/revision and every expected live
   review after preflight, followed by cancellation, including early diagnostic
   branches. No global lock or durable authority is implied by a capture.
4. Test both synthetic profiles, bounded inputs, precedence, provenance, invalid
   reviews/destinations, changes/cancellation during preflight and final reads,
   and defensive input ownership. Mutation/candidate APIs must never be called.
5. Test real artificial plain and rejected multi-boundary/merged/hidden/unsupported
   fixtures on independent shifted/permuted worksheets and clean/dirty copies.
   Respect the existing envelope's rejection of formulas on unselected sheets;
   a preflight pass is not complex-workbook compatibility. Compare all projected
   cells, revisions, copy changes, review snapshots and generated source hashes.
6. Run focused tests, full solution regressions, frontend tests after desktop
   builds, protected hashes and diff checks. Record failures and unexecuted checks
   in the tracker before writing the supervisor status as the cycle's last step.

## Boundaries

No candidate generation, approval, adoption, insertion, integration, export,
production Apply, OCR/source registration, persistence, UI/bridge, dependencies,
profile schema, editable admission or architectural policy changes. Existing
preview-only APIs remain preview-only. Phase 2/3 and MVP acceptance remain open;
synthetic verification does not establish actual receipt/accounting-owner review.

## Implemented contract

`VerifiedPlacement.PreflightMany` consumes the same live, bounded inputs and
defensive request capture as `PreviewMany`. It adds only the read-only engine
diagnostic. `WorkflowPlanning.Prepare` and verified preflight share one internal
precedence helper: choices produce `ambiguous-destination`, duplicates produce
`duplicate-review-required`, and resolved, nonduplicate plans reach inspection.
Existing Preview/PreviewMany remain planning-only and do not inspect structure.

All branches then recheck the destination and each live review, followed by
cancellation. Captured versions and source identities are observations without a
cross-session lock, authenticated document registration or future write authority.
Typed/literal/source/value-version mappings and stable proposal ordering remain
linked by review ID. The proposal's existing lifecycle keeps generation, oracle,
approval, adoption, integration and export separate from planning/preflight.

Application tests cover 1/20 records, both profiles, defensive request/results,
choices/duplicates precedence, every review's correction/retraction during
inspection, destination changes, final-read changes/cancellation on all branches,
and invalid input before inspection. The fake rejects every mutation/candidate
operation. Synthetic XLSX tests cover separate shifted/permuted selected sheets,
clean/dirty copies, plain envelope passes, multi-boundary/merged/hidden/unsupported
formula/unselected-sheet formula rejection, forged Apply rejection, unchanged
projected cells/revisions/copy changes/review snapshots and generated source hashes.

The existing envelope permits formulas only on the selected sheet, so independent
plain positive bindings use separate artificial workbooks with constant-only
context. A separate negative fixture retains formulas on an unselected sheet and
proves rejection. No package is stripped or compatibility restriction relaxed.
