# Phase 3 bounded multi-record verified placement

Status: implemented in autonomous cycle 7, 2026-11-02. Exact commands,
results and remaining gates are in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 6's next task in PLANS.md and AUTONOMOUS_PROGRESS.md,
Phase 3 verified mapping/preview and independent worksheet bindings, ADR-0002/0003.

## Implementation plan

1. Extend the headless verified placement API with a bounded 1-20-record request.
   Each item supplies a live review, explicit expected review/profile version and
   optional placement choices; all share one explicit destination binding.
   Copy the caller's input list, reject missing/repeated/foreign/stale/unverified
   or invalid typed input as a whole, and never accept cached projections.
2. Share existing binding validation, capture one complete worksheet analysis,
   and pass only live typed verified values into the existing pure planner.
   Retain request order, per-record mapping/source/value-version provenance,
   cross-record duplicates, unresolved choices and virtual row ordering.
3. Recheck the destination and every review after planning, then cancellation.
   Results remain captured observations without a shared multi-session lock or
   durable authority. Monotonic revisions bound consistency; future operations
   must independently revalidate every live authority.
4. Test both approved synthetic profiles, 1/20 bounds and invalid lists, repeated
   and foreign identities, stale/draft/retracted/invalid typed records at later
   positions, independent destinations, choices, duplicate links, deterministic
   order, defensive lists and changes/cancellation during capture. Workbook tests
   compare every projected cell, copy changes/dirty/revision state and artificial
   source hashes on independently shifted/permuted worksheets and working copies.
5. Run focused tests, full solution regressions, frontend tests serialized after
   desktop builds, protected hashes and diff checks. Record actual results and
   unexecuted checks in the progress tracker before writing the cycle status.

## Boundaries

Use the existing planner's bounded input, not larger document-set processing.
No profile/schema, dependency or foundational architecture changes. No candidate
generation, preflight, mutation, insertion, export, integration, OCR, source
registration, persistence or UI/bridge commands. Every CanApply remains false;
single-record and raw-input APIs retain their existing behavior and gates.
Phase 2/3 remain incomplete; synthetic verification does not establish human
receipt review, complex-workbook preservation or complete MVP acceptance.

## Related consistency repair

During review, the older single-record mapping path was found to lack the final
live-review/cancellation checks already required by placement. Add focused
regressions for correction/retraction/cancellation during its last destination
read, demonstrate rejection failures, then align that path with placement before
running final regressions. This strengthens the existing capture contract only.

## Implemented contract and evidence

`VerifiedPlacement.PreviewMany` accepts a live destination and 1-20
`VerifiedPlacementInput` items containing live reviews, expected versions and
optional choices. It owns a copied request list, projects every live review,
rejects repeated validated review IDs, analyzes one selected worksheet, and
calls only the existing pure planner. Invalid input rejects the entire request;
there are no partial results. Equal business values in distinct reviews remain
separate records with bidirectional duplicate warnings, never silent deduplication.

`VerifiedPlacementBatchPreview.Mappings` preserves request order and links each
review ID to its proposal record ID (GUID in N format). Every mapping retains
typed/literal/normalized/source/value-version data and independently resolved
columns/bounds. The proposal retains unresolved choices, existing and cross-record
duplicates and the planner's stable boundary/input ordering. If any choice is
unresolved, every virtual proposed row stays unavailable. Every CanApply is false.

After planning, destination identity/source/sheet/revision and all live expected
reviews are rechecked, followed by cancellation. A review's projection is atomic
under its own lock; this API adds no global lock. Captured versions are observations
and must never authorize later writes. Source evidence remains supplied identity,
not registered/authenticated document bytes or actual human review.

Application tests cover both synthetic profiles, bounds, ordering, defensive
results, source and typed provenance, duplicates, choices, invalid later inputs,
repeated/foreign/forged reviews, caller-list changes, cancellation and corrections
at first/middle/last positions during worksheet and final destination capture.
Real synthetic XLSX tests exercise independent shifted/reversed worksheets and
clean/dirty copies at 3 and 20 records, stale/foreign/invalid inputs, duplicates,
forged Apply rejection and unchanged projected cells/source hashes. Four existing
incompatible-sheet cases now also reject bounded previews.

The related mapping repair reproduced three expected failures (final-read
correction, retraction and cancellation), then added the same final review/token
checks used by placement. Existing single-record and raw-input APIs retain their
proposal-only contracts. No preflight, candidate or mutation API was added.
