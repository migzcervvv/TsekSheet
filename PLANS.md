# TsekSheet Implementation Roadmap

## Basis and Execution Rules

This roadmap follows [AGENTS.md](AGENTS.md), [GOALS.md](GOALS.md), the [architecture overview](docs/architecture/overview.md), and accepted decisions [ADR-0001](docs/architecture/decisions/ADR-0001-desktop-framework.md), [ADR-0002](docs/architecture/decisions/ADR-0002-local-first.md), and [ADR-0003](docs/architecture/decisions/ADR-0003-generic-template-engine.md).

Phase 0 is in progress. Phases 1 through 5 are planned. Existing architecture documents record decisions, not completed implementation or passed tests. Phase completion requires evidence against its acceptance criteria; no calendar estimates are assumed before dependency evaluation.

- Prioritize workbook preservation and automated regression tests before expanding UI or OCR capabilities. Never trade integrity for feature coverage.
- Operate offline after installation. Introduce no database, cloud services, paid AI APIs, hosted assets, or remote OCR fallback.
- Keep business logic independent of WPF and WebView2. Use plain domain types, application services, and narrow adapters for workbook access, OCR, and file commit.
- Keep originals unchanged until an explicit authorized Save or Save As. In-memory integration does not authorize disk writes.
- Build small vertical slices and use fixture-backed compatibility limits. Reject unsupported operations instead of silently losing content.
- Add tests alongside each implementation phase. Record failed and unexecuted checks with their impact; passing mocks alone cannot establish workbook compatibility.

The numbered phases describe delivery milestones. Phase 1 establishes only the shell and transport; production workbook commands remain unavailable until Phase 2 integrity gates pass. Workbook-library evaluation begins in Phase 0, and core workbook and template tests run independently of the desktop interface, preserving the overview's requirement to prove core behavior before connecting it to UI commands.

## Phase 0: Repository Foundation and Architecture

Status: In progress

### Objectives

- Establish a maintainable repository and test strategy around the accepted architecture.
- Resolve the highest-risk workbook and offline-distribution assumptions before broad implementation.
- Define a realistic MVP compatibility contract rather than claiming full Excel support.

### Deliverables

- Product goals, architecture overview, accepted ADRs, and this roadmap, reviewed together for consistency.
- Minimal proposed source/test layout and documented dependency directions for host, frontend, application/domain, workbook/template components, and OCR adapter.
- Reproducible build/test instructions, dependency version policy, and local development prerequisites.
- Synthetic or sanitized workbook and scanned-document fixture inventory with expected preservation invariants.
- A bounded workbook-library evaluation covering offline use, license/redistribution, round trips, deep copies, formulas, reference updates, row insertion, and sheet import/replacement.
- Initial supported-format/feature matrix, calculation policy, and risk register. Evaluate `.xlsx` first; explicitly decide how unsupported formats and advanced content are rejected.
- Initial offline runtime/installer feasibility assessment and a minimal test harness for later phases.

### Acceptance Criteria

- Architecture boundaries and save invariants are documented; unresolved choices have explicit evaluation criteria.
- Workbook evaluation has reproducible fixture results, including unchanged formulas, formatting, hidden sheets, names, and cross-sheet references for the proposed supported scope.
- A viable library and license path is identified before Phase 2 implementation depends on it. Preservation failures narrow support or trigger another evaluation; they are not accepted as silent data loss.
- Test commands and environment requirements are reproducible. Unavailable tooling and unexecuted checks are reported.
- No architecture document is treated as implementation completion. Phase 0 remains open until its repository, evaluation, and validation deliverables are satisfied.

### Dependencies

- Authoritative requirements and accepted ADRs.
- Access to development tooling, candidate dependencies and their redistribution terms, and representative safe test fixtures.

### Major Technical Risks

- A workbook library may round-trip simple cells while damaging advanced or unrelated workbook content.
- Replacement, cross-sheet copy, or formula adjustment may exceed candidate-library capabilities.
- Offline runtime packaging or dependency licensing may constrain distribution.
- Overly broad format support or excessive project splitting may expand scope before core safety is proven.

## Phase 1: WPF + WebView2 + React Desktop Foundation

Status: Planned

### Objectives

- Establish a Windows shell with locally hosted React + TypeScript and a narrow C# command boundary.
- Keep UI plumbing separate from workbook and workflow logic.

### Deliverables

- WPF lifecycle, WebView2 initialization, local frontend assets, and native dialog adapters.
- Versioned JSON requests, responses, errors, and progress events with request IDs and session/revision fields where applicable.
- Origin and command allowlists, payload validation, controlled navigation, and no unrestricted .NET object or filesystem exposure.
- Minimal interface states for loading, errors, cancellation, and unsaved-work status, initially backed by test services.
- Bridge contract tests and a small desktop startup/shutdown smoke test.

### Acceptance Criteria

- The installed development build launches its bundled interface without network access or a local HTTP server.
- Valid test requests reach application entry points; malformed, oversized, unknown, and unauthorized-origin messages are rejected predictably.
- A long-running test operation does not block the UI, reports progress, and supports cancellation and clean shutdown.
- Application/domain tests run without WPF or WebView2. Production workbook editing/saving is not enabled before Phase 2 acceptance.

### Dependencies

- Phase 0 component boundaries, toolchain baseline, and runtime feasibility findings.
- A supported WebView2 runtime installation path; no first-run download assumption.

### Major Technical Risks

- Runtime availability, UI-thread dispatch, and host shutdown can cause startup or lifecycle failures.
- Contract drift or excessive bridge payloads can create fragile UI behavior.
- Business rules can leak into event handlers unless application entry points remain authoritative.

## Phase 2: Excel Workbook Engine and Safe Editing

Status: Planned

### Objectives

- Prove supported workbook preservation before exposing real editing through the bridge.
- Establish in-memory sessions, isolated working copies, and an explicit, safe persistence boundary.

### Deliverables

- Workbook compatibility inspection, read-only source loading, original-byte baseline/fingerprint, stable worksheet IDs, session revisions, and dirty state.
- Worksheet navigation and viewport reads that retain all unselected and hidden content.
- Typed cell-value, formula, and supported-format edits with validation and formula/protected-cell safeguards.
- Independent selected-sheet working copies backed by verified deep clones of supported workbook context, with base snapshots and change records.
- Candidate validation/adoption primitives for multi-step mutations and engine-level probes for sheet integration and row insertion needed by Phase 3.
- Explicit Save and Save As with destination authorization, candidate serialization/reopening, content-based external-change detection, safe commit/backup behavior, and no direct serialization into originals.
- Defined Save As rebinding, calculation/stale-result behavior, memory/copy limits, cancellation boundaries, and failure reporting.
- Automated workbook fixtures and file-commit failure tests; minimal real-workbook UI integration after core checks pass.

### Acceptance Criteria

- Opening, navigating, editing, copying, cancelling, and closing without saving leave original bytes unchanged.
- Working-copy mutations share no mutable cell/style objects with session sheets or other copies; supported cross-sheet context remains intact.
- Reopened saved fixtures preserve formulas, formatting, names, merged ranges, references, and unaffected workbook content within the declared support contract. Unknown preservation behavior blocks the affected operation or save.
- Save writes only to the authorized destination; Save As to a separate path preserves the source. Pending copies are never silently integrated.
- External changes, including same-timestamp content changes and destinations created during Save As, prevent silent overwrites. Test coordination near commit, not only the initial fingerprint check.
- Locked files, permission/disk errors, failed serialization/validation, and pre-commit cancellation retain unsaved state and original integrity. Commit-boundary cancellation reports the actual outcome and preserves required recovery artifacts.
- Core workbook regression tests pass before real editing commands are enabled in the desktop UI. Tests distinguish formula preservation from recalculation.

### Dependencies

- Phase 0 library evaluation, supported scope, and fixtures.
- Phase 1 bridge for UI delivery; core workbook development and testing do not depend on the host.

### Major Technical Risks

- Serialization may drop unsupported workbook parts or alter cached calculations.
- Full-context clones can exhaust memory on large workbooks.
- External writers and filesystem replacement behavior can undermine overwrite checks; unsupported safe-commit environments must fail safely.
- Excessive editing scope can turn the MVP into an attempted Excel replacement.

## Phase 3: Generic Template Engine and Worksheet Integration

Status: Planned

### Objectives

- Map verified records through reusable JSON definitions without accounting-specific engine branches.
- Deliver merge, replace, create-new, and template-driven row insertion with fixture-backed preservation guarantees.

### Deliverables

- A minimal versioned JSON format, parser, validation, field typing, deterministic mappings, and actionable diagnostics, evolving from ADR-0003 rather than an upfront universal schema.
- Separate structural selectors/bindings and simple workflow constraints; template identity/revision distinct from format version.
- Independent bindings for multiple worksheets using one template, with region boundaries, summary markers, and template row patterns resolved per worksheet.
- Reimbursement example configuration and at least one non-reimbursement fixture.
- Preview/apply flows tied to template, working-copy, and destination revisions, with per-worksheet operation results.
- Three-way merge of supported changed properties against the base snapshot, conflict rejection, safe replacement with dependency inspection, and create-new import with unique names and resource/reference resolution.
- Structural insertion plans that insert full rows before summary sections, copy formatting and applicable formulas, expand declared aggregates, and shift summary/lower content without hardcoded row numbers or worksheet names.
- Automated integration and repeated-insertion regression suites plus focused UI controls after engine tests pass.

### Acceptance Criteria

- Unknown template versions, ambiguous/missing/overlapping boundaries, invalid mappings, unsupported directives, and incompatible worksheets are rejected without mutations.
- The same template works on worksheets with different names, lengths, and summary positions; bindings and mutable workbook state remain independent.
- Merge touches only intended supported properties and rejects competing changes. Replacement preserves valid inbound dependencies or is blocked. Create-new resolves supported names/references without importing unintended context sheets.
- Repeated insertion, including after serialization/reopening, moves summaries by the cumulative row count and preserves all lower content, formatting, merges, names, tables, and affected references within the supported scope.
- Copied formulas retain relative, absolute, and mixed-reference semantics; summaries include new entries. No formula string replacement or assumption of automatic range expansion is used.
- Structural edits run on isolated candidates that are validated, serialized, and reopened before adoption. Failed validation, unsupported merges/references, stale plans, and cancellation leave prior state and original files unchanged.
- Shifted coordinates are not treated as ordinary independent merge edits. Structural plans integrate only against compatible revisions.
- Templates cannot bypass verification, formula-cell protection, or explicit saving. Use synthetic explicitly verified records before Phase 4; do not introduce an OCR-verification bypass.

### Dependencies

- Phase 2 sessions, working-copy isolation, workbook capability checks, candidate validation, and safe-save tests.
- Phase 1 transport and interface for user-facing delivery; template and integration tests remain headless.

### Major Technical Risks

- Boundary resolution and insertion across merged ranges or complex formulas can be ambiguous.
- Replacement/import can invalidate names, tables, or cross-sheet dependencies.
- Structural merges against divergent revisions can corrupt intent if treated as cell diffs.
- Template scripting, broad migration support, or multi-sheet transactions can add unnecessary complexity; retain a bounded declarative vocabulary and per-sheet scope.

## Phase 4: Offline OCR and Human Verification

Status: Planned

### Objectives

- Extract draft information from scanned receipts and PDFs locally and require human review before worksheet mapping.
- Contain OCR failures and sensitive temporary data without affecting workbook integrity.

### Deliverables

- Selected local PDF renderer/OCR tooling with redistribution review, supported languages/layouts, and packaged Python/runtime models.
- Supervised child-process adapter using controlled arguments without a shell and a versioned structured request/result contract.
- Source-linked extraction candidates with page/region information and confidence where available.
- Review UI with source pages, corrections, explicit field verification, and verification invalidation after changes.
- Job progress, limits, timeout/cancellation, malformed-result handling, and temporary-file ownership/cleanup.
- Contract tests, process-failure tests, representative recognition evaluation, and verification-gate application tests.

### Acceptance Criteria

- Receipt and scanned-PDF extraction works with networking disabled and no model downloads or remote fallback.
- Draft or invalidated OCR values cannot reach template application through either UI or direct application commands. Confidence does not count as verification.
- Python has no workbook-editing responsibility; OCR failure/cancellation leaves active workbook state and originals unchanged.
- Source evidence remains available for review. Intermediates are removed after consumers finish; rendered pages are removed when review/session use ends.
- Success, failure, cancellation, deletion failure, and abandoned jobs exercise bounded cleanup without deleting originals, active jobs, unrelated files, or save recovery backups.
- Recognition quality is measured separately from contract correctness; limitations are documented rather than hidden by automatic posting.

### Dependencies

- Phase 3 field/mapping contract and application verification policy.
- Phase 1 review interface and progress transport; Phase 2 session safety.
- Local OCR/PDF tools, representative scans, and offline redistribution rights.

### Major Technical Risks

- Scan quality and layout variation may produce incorrect or incomplete candidates.
- OCR/model distribution can increase installation size and resource use.
- Child-process cleanup, retained source pages, and cancelled jobs can leak local temporary data.
- A visually complete review screen can mask missing application-level verification enforcement.

## Phase 5: End-to-End Integration, Testing, and Packaging

Status: Planned

### Objectives

- Deliver a supported Windows installation that completes the verified-document-to-workbook workflow offline.
- Establish release evidence for integrity, safe failure, usability, and operational limits.

### Deliverables

- Complete flow: open workbook, select worksheets, create working copies, extract/review documents, map verified data, insert rows, preview merge/replace/create-new, integrate, and explicitly save.
- Offline installer with required .NET, WebView2, Python, frontend assets, OCR models, and dependency/license notices supplied through supported distribution mechanisms.
- Automated core/fixture regression runs, focused desktop end-to-end tests, and real-filesystem save/recovery fault tests.
- Resource and responsiveness measurements on representative workloads, with documented workbook/job/copy limits.
- User guidance for supported features, stale formula results, pending copies, unsaved-session loss, overwrite conflicts, recovery artifacts, and temporary-file handling.
- Release checklist and results that distinguish passed, failed, and unexecuted checks; offline installation and upgrade verification on clean Windows environments.

### Acceptance Criteria

- A clean supported Windows environment installs through the offline path and completes first launch and the full workflow without network requests or downloads.
- Reimbursement and non-reimbursement examples use the same generic engine. One template binds correctly to multiple distinct worksheets without state leakage.
- End-to-end saved workbooks reopen and satisfy preservation invariants; source fingerprints remain unchanged until authorized saving. Unsupported features fail with actionable diagnostics.
- All mandatory workbook, repeated-insertion, integration, verification, and save-safety regressions pass. Any failure affecting integrity or original-file protection blocks release; unexecuted mandatory checks do not count as acceptance.
- Exercise external modification, near-commit races, locked destinations, disk exhaustion, interrupted OCR, shutdown, cancellation, and process interruption around safe-save boundaries. Validate actual outcomes and recovery artifacts.
- Logs, frontend storage, and temporary directories show no unintended document retention. Cleanup handles abandoned jobs without removing active or unrelated data.
- Measured performance fits documented limits, UI remains responsive, and errors preserve unsaved state wherever the process remains available.

### Dependencies

- Accepted Phase 1–4 deliverables and passing integrity suites.
- Supported Windows/filesystem test environments, offline runtime distributions, and representative complete fixtures.

### Major Technical Risks

- Dependency/runtime gaps may appear only on clean offline machines.
- End-to-end flows can expose stale revisions, verification loss, or save-state mismatches absent from isolated tests.
- Filesystem behavior and process interruption may reveal recovery gaps.
- Memory and OCR costs can make larger workflows impractical; retain measured limits rather than adding hidden persistence or services.

## Deferred Scope

Defer full Excel parity, advanced interactive conflict resolution, large multi-workbook batches, multi-sheet transactions, rich template editors or scripting, persistent crash recovery, extensive template migration, real-time collaboration, and non-Windows hosts until concrete requirements justify them. No deferred feature authorizes databases, cloud services, unverified OCR posting, or background overwrites of originals.
