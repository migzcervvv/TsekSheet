# Phase 4 bounded local document source registration

Status: implemented in autonomous cycle 11, 2026-11-02. Exact executed checks
and remaining MVP gates are recorded in AUTONOMOUS_PROGRESS.md.

Authority: Cycle 10 next task in PLANS.md and AUTONOMOUS_PROGRESS.md;
GOALS.md local import and Phase 4 source-linked verification. No foundational
architecture or tool choice changes.

## Implementation plan

1. Add headless application-owned in-memory registration of explicitly selected
   local PDF/PNG/JPEG files. Accept regular absolute Windows fixed NTFS paths
   only, rejecting network/device/stream/alias/reparse locations. Separate the
   source lifecycle from the narrow Windows read adapter; no WPF/WebView2.
2. Bound active registrations (20), each read (32 MiB, with configurable lower
   limits), and retained state. Stream SHA-256 using fixed-size buffers; retain
   identity, fingerprint, length, kind and version, never document bytes. Check
   signatures against extensions as input triage, not parser validation.
3. Reopen read-only with write/delete sharing denied during observation; bind
   NTFS identity and canonical handle path and protect ancestor directories while
   reading. Issue path-free SourceEvidence only after a fresh bounded identity/
   content check at the exact registered version. Missing, replaced or changed
   files invalidate the registration; cancellation preserves retryable state.
   Release/dispose forget paths and close all handles without deleting files.
4. Keep supplied SourceEvidence and the existing review APIs unchanged: this
   slice does not connect source registration to review or authenticate arbitrary
   caller evidence. PDF page ordinals are requested associations, not parsed page
   count or rendered-page proof. No rendering, OCR, temporary copies, persistence,
   UI/bridge, candidate adoption or workbook writes.
5. Test artificial local image/PDF bytes, identity replacement with identical
   content/timestamps, timestamp-preserving edits, missing/locked/unsupported
   sources, bounds, cancellation, ownership/version/release/disposal and sanitized
   errors. Use real local filesystem checks plus internal deterministic seams
   for changes/cancellation during reads; compare original bytes and handle release.
6. Run focused tests and related solution/frontend regressions. Update the
   progress tracker with exact results/remaining gates, then write final status.

## Limits

This is bounded source registration, not receipt/PDF parsing, source rendering,
human verification or OCR acceptance. Snapshots/evidence describe the checked
instant; future consumers must check live authorities again. Changes followed by
restoration entirely between observations cannot be detected. Local software
with equivalent privileges is outside this boundary's threat model. No permanent
file lock or durable evidence capability is granted.

## Implemented contract

`LocalDocumentSources` owns registrations and their privately retained selected
paths. `Register` creates a fresh application ID at revision zero; duplicate file
selections are independent registrations. `IssueEvidence` requires the exact
live version and reopens/rechecks file identity, creation/write metadata, length,
signature and SHA-256. Single images permit page one. PDF associations permit
requested ordinals 1-10000 but establish no parsed page count or rendering proof.
Caller-created `SourceEvidence` remains unauthenticated in existing review APIs;
connecting live source authority is explicitly unfinished.

Changes, replacement, missing/locked files or other read failures during evidence
issuance invalidate the registration, increment its revision and retain the old
observation for reporting. Restoring old bytes never revives it: release its
current version and explicitly register again. Invalidated entries still count
toward capacity. Invalid commands and cancellation leave registrations intact.
Snapshots are immutable and path-free. Release/dispose forget associations;
neither operation deletes or writes any file. No handles survive any observation.

`WindowsDocumentReader` is a separate narrow internal adapter. It denies write/
delete sharing on the file and ancestors only during a bounded streaming read;
rejects redirects, reparse points, network/device/stream paths and aliases; and
uses native volume/file-index/creation identity plus canonical handle paths on
local fixed NTFS. Reads use a 64 KiB buffer; no document content is retained.
Limits may be lowered, never raised beyond 20 registrations and 32 MiB per file.
The adapter sanitizes platform failures without retaining path-bearing inner
exceptions. Signature triage deliberately does not claim parser validation,
malware detection, valid page structure or image-decoding support. Synchronous
local I/O has cooperative cancellation between reads, not a hard I/O deadline.

Tests use generated artificial signature-bearing bytes, not private receipts or
accounting workbooks. They cover both native filesystem observations and internal
deterministic cancellation/lifecycle seams. No dependency, OCR tool, UI/bridge,
workbook contract or save policy changed.
