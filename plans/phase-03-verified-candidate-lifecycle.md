# Phase 3 live verified candidate review lifecycle

Status: implemented in autonomous cycle 10, 2026-11-02. Exact executed commands
and results are recorded in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 9 next task in PLANS.md and AUTONOMOUS_PROGRESS.md; Phase 3
verified preview/revision lifecycle and explicit review requirements.

## Implementation plan

1. Add an application-owned in-memory review session constructed only by fresh
   VerifiedCandidatePreparation from 1-20 live reviews and explicit copy/parent
   bindings. Retain live authorities, ordered typed/literal/source provenance,
   and private copies of the exact generated ticket and hashes. Snapshots are
   observations and cannot construct a session or grant approval.
2. Accept a deliberate CandidateReviewApproval command matching every retained
   ticket/hash/source/profile/copy/parent/revision binding. Require explicit
   duplicate acknowledgment. Keep formatting/oracle gates and engine checks.
   Replayed/foreign commands cannot approve or cancel a different ticket.
3. Revalidate all live reviews and copy/parent/base/profile/source bindings before
   and after calling the existing public metadata-only approval operation.
   Discard this exact ticket at the current copy revision on stale authorities,
   cancellation or uncertain approval failure; report cleanup failures with the
   original error. Allow correction of command-only/duplicate/formatting failures
   without synthesizing approval. Serialize this session's commands, without
   implying a lock across workbook and field-review sessions.
4. Return defensive observations with distinct lifecycle facts and CanApply=false.
   Add explicit discard for the owned ticket. Never call adoption, integration,
   export, save, UI/bridge, OCR, persistence or production Apply. Public tickets
   stay permanently review-only; no foundational architecture/schema change.
5. Test exact/foreign/stale/replayed commands, defensive ownership, every review,
   before/during/after approval changes and cancellation, duplicate and formatting
   gates, replacement protection and cleanup reporting. Use both synthetic
   profiles and artificial workbook writes; compare cells/formulas/formats,
   source hashes, revisions and copy changes, and prove adoption stays disabled.
6. Run focused, solution and frontend regressions plus protected-hash/diff checks.
   Record actual results and unfinished MVP gates before the final status write.

## Limits

Metadata approval is an observation at checked versions, never durable write
authority or actual source-document acceptance. Separate sessions have no shared
lock; later operations must revalidate. Source identities remain supplied
evidence, not registered/authenticated document bytes. Phase 2/3 and MVP remain
incomplete. Existing formatting and compatibility restrictions stay in force.

## Implemented behavior

VerifiedCandidateReviewSession.Prepare freezes caller inputs and invokes the
existing fresh live preparation path. It retains the live reviews/versions and
copy/parent bindings privately, with a deep copy of the generated engine ticket.
Approve accepts the full explicit CandidateReviewApproval command; every field
must match the retained bindings. Foreign commands leave this ticket untouched.
Duplicate acknowledgment and qualified native formatting remain mandatory.
Replayed successful commands reject without calling the engine again.

Approval rechecks live authorities before and after the existing public engine
operation. Only the returned approval lifecycle fact may differ; a changed
ticket/hash/proposal/oracle/lifecycle rejects and discards this session's ticket.
Snapshots deep-copy mutable candidate collections. Mappings retain ordered
immutable typed/literal/source provenance. Commands are serialized per review
session; this does not lock field reviews and both workbooks together.

On stale authorities, cancellation or engine/response failure, cleanup uses only
the retained candidate ID at the current copy revision, without the cancelled
token. Cleanup success marks the session Discarded. Cleanup failure marks it
Invalidated, reports both errors, and does not claim the engine ticket was
discarded; explicit Discard follows the same distinction and reports its error.
Both states reject future approval. A newer pending ticket is never cancelled.
After a valid engine approval response, its metadata approval fact is retained
even if final live validation fails and the ticket is subsequently discarded.
The session status separately identifies whether approval was accepted by this
application operation. All snapshots remain observations, including Approved;
there is no durable write authority or active revalidation on snapshot reads.

59 application cases and 44 artificial-workbook cases cover the implemented
lifecycle, both generic profiles, 1/20 records, independent shifted/permuted
bindings, literal/type/source preservation, exact commands, replay/concurrency,
duplicates, fixed-height formatting restrictions, before/during/after approval
changes, cancellation, source/copy isolation, failure reporting and replacement
protection. Production insertion and adoption stay disabled even when tests
enable experimental engine flags. Full regression evidence is in the tracker.
