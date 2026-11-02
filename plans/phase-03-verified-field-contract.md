# Phase 3 bounded verified-field contract

Status: bounded contract implemented and tested, 2026-11-02 (autonomous cycle 3).

Authority: the Phase 3 verification criterion in `PLANS.md`, its Cycle 2 next
task, and ADR-0002/0003. This is a prerequisite contract, not Phase 3 acceptance.

## Scope and implementation plan

1. Add a headless, in-memory application service owning one record's field
   review. Use the existing validated `WorkflowProfile` field definitions and
   snapshot their identity/hash and immutable field definitions. No new template
   schema or architectural decision is needed.
2. Accept only bounded draft values with local source identities (document ID,
   SHA-256, document revision and one-based page). The service performs no file
   access; source registration/authentication and page lifetime remain future
   adapter responsibilities. Missing fields remain visible in the snapshot.
3. Own draft/corrected/explicitly verified state in the service. Commands bind to
   the review identity/revision, profile identity/hash, field key/value revision
   and exact source. Any correction, including same-text correction or a source
   change, advances the value revision and clears verification. Explicit
   verification/retraction advances the review revision. Reject stale or foreign
   commands atomically. Returned snapshots are never accepted as authority.
4. Validate against current authoritative state at an expected review version:
   reject missing/blank required fields and every supplied unverified field;
   optional absent fields may remain absent. Verification preserves exact text,
   including formula-looking text, without parsing/evaluating it. Typed mapping
   and workflow constraints remain the next mapping layer's responsibility.
5. Test reimbursement and non-reimbursement artificial records, correction and
   source invalidation, stale/foreign/replayed commands, snapshot isolation,
   missing fields, malformed/oversized inputs and unchanged state on rejection.
   Run application tests and related workbook/desktop/frontend regressions, and
   compare protected reference/fixture/branding/lock fingerprints.

## Boundaries

No OCR, extraction, confidence policy, source-file registry, filesystem writes,
UI/bridge commands, persistence, template mapping, insertion, export or workbook
mutation is enabled. This service is callable directly by trusted application
composition; future commands must route explicit human intent through it. Its
synthetic tests do not prove that a real human reviewed a source document.
Existing proposal-only planning remains proposal-only, with all Apply/adoption
and inspection mutation gates unchanged. Future mapping must validate the live
review version; a serialized snapshot or a cached validation result is not an
authorization token. Existing Phase 2/3 and later manual/offline gates stay open.

## Implemented contract and evidence

`src/TsekSheet.Application/Verification/FieldReviewSession.cs` owns the state.
Its `Snapshot` contains all configured fields in profile order, with absent
fields represented by empty text and no source. `Correct`, `Verify` and
`Retract` require a current `FieldReviewBinding`; `Validate` requires the current
`ReviewVersion`. Each successful command advances the record revision. Only
corrections advance the field value revision. Commands and captures are
serialized under one in-memory lock. Retraction retains the exact value/source
and requires fresh explicit verification. Rejected commands change no state.

The source SHA-256 is a bounded identity supplied by trusted composition, not a
file fingerprint calculated or authenticated by this service. Source replacement
must enter through correction; tracking a source's current availability/content
and invalidating all affected records remains the future document registry's
responsibility. A profile change requires a new review with fresh verification;
old profile bindings cannot transfer verification to it.

`FieldReviewSessionTests.cs` adds 37 synthetic cases. They cover both approved
generic profiles, verification/retraction, same-text and source corrections,
returning to old values, stale/foreign/replayed/altered bindings, concurrent
commands, missing required fields, optional absence/blank values, immutable
snapshots, defensive collection copies and invalid input. Formula-looking text
is retained literally; successful review validation does not establish decimal,
date, identifier or mapping validity. See the Cycle 3 entry in
`docs/development/AUTONOMOUS_PROGRESS.md` for executed checks and open gates.

Next bounded task: extract the existing proposal planner's pure field-value
validation without changing its accepted semantics, then add a headless typed
record projection from the live review at an expected version/profile. Reject
unverified, stale, wrongly typed and workflow-invalid data before producing a
projection; retain literal text and source/version provenance. Consume values
and verification atomically so a correction cannot race a validate-then-read
sequence. Do not accept caller-created snapshots as proof. This is a Phase 3
field/mapping prerequisite, not permission to add workbook edits, UI/bridge,
insertion, export or OCR adapters. Record its focused plan before implementation.

Cycle 4 follow-through: the [typed projection](phase-03-typed-review-projection.md)
now implements that bounded task. `Validate` remains verification/completeness
only; `Project` validates the live version and types atomically. Neither a
snapshot nor a projection is authorization for later workbook mutation. The
current next task is the read-only mapping preview in `PLANS.md`; all gates above
remain in force.
