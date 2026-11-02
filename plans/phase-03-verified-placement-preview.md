# Phase 3 bounded verified placement preview

Status: implemented in autonomous cycle 6, 2026-11-02. Exact final regression
results and remaining acceptance gates are in the progress tracker.

Authority: Cycle 5's explicit next task in PLANS.md and the progress tracker,
Phase 3 verified mapping/preview and independent bindings criteria, ADR-0002/0003.

## Implementation plan

1. Share live binding validation, complete worksheet capture and typed review
   projection with the existing mapping preview without changing its public API.
   Accept live sessions and explicit expected review/profile/destination versions;
   never accept a cached mapping, projection or caller verification flag.
2. Feed the single verified record into the existing pure section/entity/duplicate
   planner. Expose only section choice and explicit retention of a new entity.
   Similar-name substitution requires correcting the live field and verifying it
   again. Configured aliases retain the verified literal and normalized values.
3. Return the read-only mapping/provenance alongside the existing proposal,
   warnings, unresolved choices and affected rows. CanApply stays false. Do not
   invoke structural inspection, candidate generation, mutation or saving.
4. Recheck the live review and destination after pure planning and honor
   cancellation. Captures remain observations, not future write authority; the
   existing monotonic session revisions bound destination consistency.
5. Test both approved synthetic profiles: exact/alias/fallback/similar matches,
   ambiguous/invalid section choices, duplicate warnings, typing and literal
   provenance, stale/foreign/unverified inputs, cancellation and changes during
   capture. Synthetic XLSX tests cover independently configured worksheets and
   clean/dirty working copies, every projected cell and unchanged source hashes.
6. Run focused checks, full solution regressions, then frontend tests after the
   desktop dependency build. Compare all 141 protected hashes and record actual
   results, failures and checks not run in AUTONOMOUS_PROGRESS.md before status.

## Boundaries

No profile/schema changes, OCR, source registration, UI/bridge, persistence,
insertion, candidate lifecycle, export, integration or production Apply. Existing
raw-input planning and all mutation/save gates retain their current behavior.
Phase 2/3 and the MVP remain incomplete. Actual accounting-owner review and
complex-workbook preservation/manual/offline acceptance remain separate gates.

## Implemented contract and limits

`VerifiedPlacement.Preview` accepts live sessions, `MappingDestination`, expected
`ReviewVersion` and optional `PlacementChoices`. The shared internal capture in
`VerifiedMapping` retains its existing public mapping API and guards. It resolves
the complete bounded worksheet and projects live verified types. Placement uses
only `WorkflowPlanning.Plan`, never structural preflight/candidate generation.
The deterministic record ID comes from the live review identity. Section choice
and explicit retention of a new entity never supply replacement field values.
Configured aliases affect matching while preserving verified field text.

The result owns a read-only mapping/provenance observation and the existing pure
proposal, including all warnings, suggestions, matches, affected rows and base/
virtual row positions. All three `CanApply` properties are false. Readiness means
only that placement choices are resolved. The engine still rejects even a forged
executable proposal. It does not certify target-cell safety, compatibility for
editing, structural preservation or a future save.

After planning, the destination is rechecked, then the live review version and
cancellation are checked again. This also rejects review correction/cancellation
during the final destination read. The destination guard relies on the existing
monotonic session revision contract; there is no shared multi-session lock.
Results describe captured versions, never durable write authority. Later actions
must recheck live state. Source evidence remains supplied identity, not document
registration/authentication or actual human acceptance of an OCR result.

Tests cover both artificial profiles, independently permuted/shifted worksheets,
exact/alias/fallback/similar matches, ambiguous/invalid section choices, duplicate
retention, renewed verification, literal/typed/source provenance, immutable and
deterministic observations, invalid review/destination/type/boundary inputs,
cancellation and changes during capture. Synthetic XLSX tests compare all
projected cells, copy changes/dirty state and source hashes, including formulas,
formatting, a merged footer, hidden rows and hidden context. Rejection cases
preserve the same state. These are preview tests, not preservation proof for
insertion, candidate generation or export.
