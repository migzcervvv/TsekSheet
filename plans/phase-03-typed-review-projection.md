# Phase 3 bounded typed review projection

Status: implemented and tested, autonomous cycle 4, 2026-11-02. Final solution:
545 tests passed; frontend: 70 tests passed. The progress tracker records the
initial frontend dependency-overlap failure and successful sequential rerun.

Authority: the Cycle 3 handoff, Phase 3 field typing and verification criteria
in `PLANS.md`, and ADR-0002/0003. This extends the headless verified-field
contract; it does not change foundational architecture or enable workbook writes.

## Implementation plan

1. Extract the proposal planner's pure value validation into one application
   helper. Preserve all accepted semantics: configured keys, raw length/XML
   checks before trimming, required values, invariant decimals bounded to zero
   through one trillion and four decimal places, exact ISO dates, and at least
   one identifier. Keep planner normalization, errors and proposal behavior.
2. Add `FieldReviewSession.Project(expectedVersion)`. Under the same review
   lock, require the live review/profile version, validate completeness and
   explicit verification, validate and type all values, then capture a defensive
   result. Snapshot flags and previous validation results are never inputs.
   Capture identifier configuration defensively alongside field definitions.
3. Retain exact literal text separately from normalized text and typed decimal/
   date values. Preserve review/profile identity, field value revision and exact
   source identity/page. Optional absent/blank typed values stay empty, never
   become zero or a default date; formula-looking text stays text.
4. Test both artificial profiles, accepted/rejected value boundaries and parity
   with the planner, optional values, culture independence, immutable captures,
   stale/foreign/profile versions, correction/retraction, forged observations,
   and concurrent projection/correction. Rejection must change no review state.
5. Run focused and full solution/frontend regressions; compare the protected
   141 reference/fixture/branding/lock files and report failures/unexecuted work.

## Boundaries

The projection is an immutable observation of a verified review at a specific
version, not a durable authorization token. A later mapping/apply command must
check live review and destination revisions again. No caller-created snapshot or
projection will be accepted by this API. Repeated projection is read-only and
does not advance the review revision or prevent subsequent correction.

No OCR adapter, source registry, persistence, UI/bridge command, workbook edit,
insertion, export or integration is added. Source hashes remain supplied local
identities, not authenticated files. Synthetic explicit verification exercises
the contract, not actual human acceptance. Phase 2/3 and later manual gates stay
open. Tests write no workbook files.

## Implemented behavior

`WorkflowValueValidation` owns the extracted rules and parsed values; the
proposal planner uses its normalized strings. Existing planner error messages
and allowed values are preserved. `FieldReviewSession.Project` checks the exact
expected version/profile, current completeness and verification, then validates
and captures all fields under the same lock used by correction/retraction.
`TypedReviewRecord` defensively owns its field collection and has no public
constructor. Its fields retain literal and normalized text, nullable decimal/
date values, source evidence and field revision; the record retains the complete
review/profile version. Optional blank/absent typed fields remain null.

The method rejects stale/null/foreign versions with `STALE_RECORD_REVIEW`,
incomplete verification with `REVIEW_INCOMPLETE`, and invalid types/workflow
values with the planner's established `INVALID_RECORD`. `Validate` still checks
only completeness/verification and supplies field-specific review issues; its
result is not projection authority. Projection changes no state, consumes no
verification and may be repeated while the version remains current.

`TypedReviewRecordTests.cs` adds 44 cases, including literal text and typed
provenance, both profiles, invariant numeric/date boundaries, planner parity,
optional blanks/absence, mutable caller collections, forged observations,
correction/retraction and concurrent correction. See the Cycle 4 progress entry
for exact regression commands/results and unexecuted acceptance gates.
