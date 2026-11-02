# Phase 3 live verified candidate preparation

Status: implemented in autonomous cycle 9, 2026-11-02. Exact commands/results
and outstanding MVP gates are in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 8 next task in PLANS.md and AUTONOMOUS_PROGRESS.md;
Phase 3 verified inputs and isolated candidate/reopen/validation requirements.

## Implementation plan

1. Add a headless application entry consuming 1-20 live reviews/expected versions,
   explicit copy and parent MappingDestination bindings, and expected base revision.
   Freeze caller inputs, reuse live typed placement, and construct the existing
   engine request from those captured records. Never consume cached preflight.
2. Require the selected copy's parent/source/sheet/base to match the explicit
   original destination. Leave clean-copy, structure, template and oracle checks
   to the existing public review-only PrepareCandidateReview contract.
3. Recheck copy, parent, every review and cancellation after generation. Discard
   only the newly returned ticket on stale state or cancellation, using its ID
   and the current copy revision. Report every cleanup failure alongside the
   original error, including supersession: the public contract cannot distinguish
   a replaced ticket from a concurrent revision without a cross-session lock.
4. Return ordered typed/literal/source provenance and the actual engine review,
   candidate/manifest/review fingerprints, oracle findings and lifecycle facts.
   CanApply remains false. Never call approval/adoption/integration/export/save.
5. Add deterministic application tests for bounded inputs, both profiles, binding
   failures, input ownership, changes at generation/final reads and exact cleanup.
   Add artificial workbook tests for both profiles, shifted/permuted independent
   bindings, 1/20 records, formatting policy, unsupported structures, cancellation,
   unchanged cells/revisions/copy changes/reviews and source hashes.
6. Run focused tests, solution and frontend regressions, protected hashes and
   diff checks. Update progress with actual results, failures, limitations and
   next work before writing the supervisor status as the final cycle action.

## Boundaries

No approval is synthesized. No cross-session lock, future write authority or
authenticated document registration is implied. This is a review-only candidate
observation; live reviews are required again by any future consuming operation.
Existing compatibility and fixed-height formatting restrictions remain intact.
No architecture/schema/dependency, OCR, persistence, UI/bridge, production Apply,
admission, structural integration/export or save-policy changes. Phase 2/3 and
the MVP remain incomplete.

## Implemented contract and limits

`VerifiedCandidatePreparation.Prepare` freezes the bounded input list, validates
explicit copy/parent/profile/source/sheet/base bindings, captures fresh live typed
placement and builds a request with the original verified values and explicit
choices. It rechecks all authorities before calling the existing public engine
review-only generation path and again after that path returns. It returns ordered
mappings and the actual generated review, hashes, oracle and lifecycle facts.

Final revalidation failure discards the exact generated ticket at the current
copy revision without using the cancelled token. Cleanup failures are reported
as an AggregateException containing the original and cleanup errors. In
particular, STALE_REVIEW is not silently interpreted as successful cleanup: the
contract cannot distinguish another ticket from a concurrently changed revision.
A newer ticket is never cancelled by ID substitution. The engine retains its
existing replacement semantics for previously pending reviews; this adapter
does not restore an older ticket after successful generation replaces it.

Application tests cover both profiles, 1/20 bounds, defensive input ownership,
typed/literal/source provenance, invalid input/bindings, every review's correction
or retraction during generation, destination/base/revision changes, final-read
review/cancellation, exact cleanup and explicit cleanup failure reporting. Real
artificial XLSX tests cover shifted/permuted bindings, constant context sheets,
the fixed-21/default-15 formatting policy, duplicate warnings, dirty-copy and
unsupported/multi-boundary rejection, oracle tampering, cancellation, retry and
post-generation changes on the public contract. Cells, revisions, copy changes,
review state and generated-source hashes remain unchanged except for deliberately
injected external changes. No approval or adoption method is called.

These observations grant no durable approval capability. Later consumption must
revalidate the live reviews and copy/parent authorities, bind the exact candidate
and hashes, and require an explicit review action. SourceEvidence remains a
supplied local identity, not authenticated document bytes. No actual receipt
review, general Excel compatibility or maximum-bound performance is certified.
