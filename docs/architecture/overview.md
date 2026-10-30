# TsekSheet Architecture Overview

Step 46 implements a separate package-inspection boundary alongside the editable engine. It retains a read-only source handle and bounded viewport cache, and cannot authorize mutation. The existing editable-session architecture remains in force. See [analyzer architecture and limits](../development/workbook-compatibility-analyzer.md).

## Status and Requirements

This document proposes an architecture; it does not describe an implemented system. [AGENTS.md](../../AGENTS.md) and [GOALS.md](../../GOALS.md) are authoritative. Implementation must follow their workbook safety, local processing, human verification, and testing requirements.

TsekSheet is an offline-first Windows desktop application. Its first workflow is accounting reimbursement, but document extraction, template mapping, and workbook operations must support other spreadsheet workflows without accounting-specific dependencies in the core.

## Implementation Plan

Before application implementation:

1. Establish workbook fixtures and evaluate a .NET workbook library against preservation, cloning, reference handling, and save requirements. Define supported formats and features from evidence.
2. Implement and test the in-memory session, isolated worksheet copies, integration previews, and explicit-save path independently of the interface.
3. Add generic template mappings and verified-data validation.
4. Connect the WPF host and React interface through a small typed message contract.
5. Add the local Python OCR process and source-linked verification interface.
6. Validate the complete reimbursement workflow with representative workbooks and scanned documents.

Each phase must preserve the original-file boundary. A library that cannot preserve required workbook content is a compatibility constraint, not permission to discard that content.

## Structure and Dependencies

Use a single desktop process for WPF, WebView2, and C# application services, plus a child Python process for OCR. Bundle the frontend, OCR runtime, models, and other runtime assets locally. No HTTP server, database, cloud service, or paid AI API is required.

| Component | Responsibilities | Dependencies |
| --- | --- | --- |
| Desktop host | WPF lifecycle, WebView2 initialization, local asset loading, native dialogs, message dispatch, application composition, shutdown | Application entry points, concrete engines at composition time, WPF/WebView2 |
| React frontend | Workbook navigation, viewport rendering, editing interactions, source-document display, OCR verification, operation previews, save status | Typed bridge messages and presentation models; no workbook-library access |
| Application layer | Coordinate sessions, edits, verification, templates, integration, cancellation, and saving; enforce authorization and revision checks | Domain and narrow engine contracts |
| Domain layer | Workbook and working-copy identities, revision values, change descriptions, verification state, integration rules, validation results | Plain .NET types only |
| Workbook engine | Read and retain workbook content, expose cell windows, clone, apply edits, inspect dependencies, integrate changes, serialize and validate candidates | Selected .NET workbook library and domain types |
| Generic template engine | Parse and validate configuration; map verified fields to typed worksheet edits | Domain types; application supplies worksheet context |
| OCR integration | Start and supervise Python, stage local inputs, parse structured extraction results, report progress and failures | Local process/filesystem APIs and domain result types; Python owns OCR/PDF tooling |

Dependencies point toward the domain. Engine implementations satisfy small contracts owned by the application layer. Domain types never depend on WPF, WebView2, Python, or workbook-library objects. The template engine returns proposed edits; it does not mutate workbooks or save files.

Use ordinary classes and explicit composition. Introduce interfaces where substituting an external boundary helps testing: workbook access, OCR execution, and file commit. Avoid a plugin framework, generic repository layer, distributed messaging, or a universal workflow engine. Logical boundaries can begin as folders and a few .NET projects; they do not require a project per service.

## React-to-C# Communication

Serve bundled React assets through a fixed local WebView2 origin. React sends JSON messages through the WebView2 web-message channel; the host returns JSON responses and progress events through the corresponding host API. Do not expose unrestricted .NET objects to JavaScript.

Each request has a protocol version, request ID, command name, and typed payload. Mutations include a session ID and expected revision. Responses carry the matching request ID, result or structured error, and current revision. Progress events identify the operation they belong to. Define a small command set such as open workbook, read viewport, create working copy, edit cells, run OCR, verify fields, preview integration, apply integration, and save.

The host validates origin, message size, command allowlist, and payload shape before dispatch. The application validates domain rules independently of the frontend. Native file dialogs resolve file choices into session or document IDs; subsequent frontend commands use those IDs rather than arbitrary paths. OCR content is untrusted data and must be rendered as text.

Run expensive workbook and OCR operations away from the WPF UI thread. Serialize workbook mutations per session because library objects may be mutable and unsafe for concurrent access. Support cancellation at safe boundaries and return errors without leaving partial edits. Read viewport-sized data windows instead of sending entire workbooks across the bridge. React caches display data, but C# owns authoritative state.

## In-Memory Workbook Sessions

Opening a workbook reads it without writing or retaining a write lock on the source. A session retains:

- The source path, content fingerprint, and immutable original package bytes, subject to an explicit size limit.
- An editable in-memory workbook representing accepted session changes.
- Stable application worksheet IDs, a session revision, dirty state, and compatibility findings.
- Independent working copies and their base revisions.

Preserving original package bytes provides an unchanged baseline; it does not by itself guarantee library round-trip preservation. All original worksheets remain in the workbook even when the user focuses on selected sheets. Navigation and selection must never remove hidden or unselected content.

Direct edits to session sheets change only the in-memory workbook. Working-copy edits change only their isolated copies. Integration changes the session workbook. None of these actions saves to disk. Distinguish pending working-copy edits from integrated but unsaved session changes in the interface.

For supported mutations, validate a proposed change set before applying it. For multi-step or structural operations, apply changes to a candidate clone and adopt that candidate only after validation succeeds. A failed operation leaves the prior session usable. Memory limits and disposal rules must prevent unlimited retained clones; general undo history is a separate decision.

## Isolated Worksheet Working Copies

A working copy contains its own workbook backing objects, source worksheet identity, base snapshot, base revision, and recorded changes. Never share mutable cells, styles, ranges, or worksheet objects with the session workbook.

For the MVP, prefer cloning the full supported workbook context and exposing only the selected worksheet for editing. This consumes more memory but retains the surrounding context needed by cross-sheet formulas and workbook-scoped names. Other worksheets in that clone are read-only context. A working copy represents the selected sheet, not permission to import every sheet from its backing clone.

Cloning must use the workbook engine's verified deep-copy or serialization mechanism, with tests proving independence. Do not reconstruct a copy from displayed cell values: that loses formulas and worksheet metadata. Cross-sheet references and shared resources must be evaluated again when a copy is integrated into a changed session.

## Merge, Replace, and Create-New Operations

All integration operations have two stages: preview and apply. A preview identifies the source copy, destination, changed cells or structures, reference effects, compatibility issues, and conflicts. Apply requires the same source and destination revisions as the preview; stale previews must be regenerated. Applying integrates in memory and marks the session dirty. Saving remains a separate action.

### Merge

Compare the working copy to its base snapshot to produce a change set. Apply only changed supported properties to the selected destination sheet; leave unrelated destination content untouched. Compare the destination against the same base for those properties. If both destination and copy changed the same property differently, stop and surface a conflict instead of silently choosing a value.

MVP merge should cover explicit cell values, formulas, and supported formatting edits. Do not merge whole rows merely because one cell changed. Structural edits, table resizing, and ambiguous coordinate shifts require dedicated support and tests; reject them when their effects cannot be determined safely. Detailed interactive conflict resolution can follow later.

### Replace

Replace the destination worksheet's supported contents and structure with the working copy while preserving the destination identity wherever the library allows. Before applying, inspect inbound formulas, defined names, tables, and other relationships affected by replacement. Do not implement replacement as an unconditional delete-and-add: that can invalidate references and relationships.

If a safe replacement requires unsupported reference rewrites or would invalidate dependent content, block it and report the dependency. Apply the replacement to a candidate workbook and validate it before adopting the result.

### Create New

Import the selected working-copy worksheet under a user-visible unique sheet name. Resolve name collisions for tables, defined names, and other workbook-scoped resources. Preserve references to existing context sheets where valid; remap references intended to target the copied sheet using workbook-aware APIs, never text substitution. Preview the resulting reference behavior. Block unsupported or ambiguous dependencies instead of silently redirecting them.

## Formula and Workbook Integrity

Store formulas as formulas, separately from displayed or cached results. Generic data edits must not overwrite formula cells unless the user explicitly edits the formula or an approved mapping permits that target. Template mappings must validate destination types and protected targets before producing changes.

Use workbook-aware operations for references, styles, merged ranges, names, tables, validations, and relationships. Structural changes require dependency analysis. Preserve unaffected content, including hidden sheets and workbook metadata, within the supported compatibility contract.

The workbook engine must inspect compatibility before editing and before committing structural changes. Content it cannot reliably preserve must block the affected operation or saving, with an explicit reason. Treat unknown preservation behavior as unresolved, not as supported.

Formula preservation and formula calculation are separate capabilities. Do not promise Excel-equivalent recalculation. Decide and test whether the chosen engine recalculates supported formulas or preserves formulas and requests recalculation on a later Excel open. Surface stale cached results; do not present them as verified calculations.

Validate serialized candidates by reopening them, checking workbook structure and targeted invariants, and comparing representative unaffected content. Reopening alone does not prove fidelity. Automated fixtures must cover formulas, cross-sheet references, formatting, names, hidden sheets, merged cells, and supported workbook features.

## Generic Template Engine

Represent templates as local versioned configuration files, initially JSON. A template describes fields, types, worksheet selectors, target addresses or supported table mappings, and simple required-field or value constraints. The reimbursement template is configuration using this mechanism.

The engine accepts verified field values and a read-only destination description, then returns proposed cell edits and validation errors. It must not execute arbitrary scripts, access source files, launch OCR, or save workbooks. Keep initial rules declarative and small. Complex layout detection and workflow-specific automation are future extensions.

## Template-Driven Row Insertion

Templates must identify repeatable data regions, their start and end boundaries, the summary boundary, and the template row or row pattern used for new entries. Resolve these through configured named ranges, table metadata, or explicit marker rules against the current worksheet. Reject missing, ambiguous, overlapping, or inconsistent boundaries. Do not hardcode row 30, worksheet names such as `MARIA`, or other workbook-specific coordinates in application logic.

Bind the same template independently to multiple selected worksheets using stable worksheet IDs. Resolve each worksheet's boundaries and template rows separately; worksheets using one template may have different data lengths and summary positions. After every insertion, resolve or update boundaries against the resulting revision so repeated insertions continue before the current summary section.

The template engine produces a structural insertion plan describing the region, insertion point, row count, template-row sources, field destinations, and required formula-range expansion. The application coordinates the operation; the workbook engine performs it. Insert complete worksheet rows immediately before the summary boundary when new entries require them. Shift the summary rows and all content below downward by the inserted row count, across every column, without overwriting existing content or exceeding worksheet limits.

Copy formatting, row heights, and applicable formulas from the configured template rows into the inserted rows. Translate copied formulas using workbook-aware relative, absolute, and mixed-reference rules. Populate only configured input fields with new values; do not duplicate prior entry values. Preserve and correctly adjust affected existing formulas, formatting, merged ranges, defined names, table ranges, and same-sheet and cross-sheet references. Summary formulas must include the expanded data region according to the template's declared aggregation rules; do not assume insertion at a range boundary automatically expands every formula correctly.

Inspect merges and references that intersect or span the insertion boundary. Shift ranges below the boundary and expand or replicate intersecting ranges only where template intent and verified engine support make the result unambiguous. Reject unsupported structural changes, reference patterns, merge layouts, or workbook features rather than risking workbook corruption. Never adjust formulas through string replacement.

Apply the entire structural plan to an isolated candidate workbook at the expected revision. Validate resolved boundaries, inserted row count, shifted summary and lower content, copied styles and formulas, affected references, and unaffected workbook content. Serialize and reopen the candidate for integrity checks before adopting it into the active session or working copy. On failure, discard the candidate and retain the prior state. Integration previews must include structural effects; replay an insertion plan only against a compatible base revision, rather than treating shifted coordinates as ordinary cell differences. Adoption remains in memory and does not save the original workbook.

Automated regression tests are required when implementing row insertion. Use real workbook fixtures and perform successive insertions into the same region, including after serialization and reopening. Assert that the summary moves by the cumulative inserted row count, all lower content remains intact, totals cover every new entry, copied formulas retain correct reference semantics, formatting and merged ranges remain valid, and cross-sheet references stay correct. Test the same template on multiple worksheets with different names and boundary positions, including positions other than row 30. Include ambiguous boundaries, unsupported intersecting merges, stale plans, and failed validation; assert rejection leaves the active workbook and original file unchanged. These tests are an implementation requirement; this documentation change does not add executable tests.

## Offline OCR and Human Verification

The application asks the OCR integration to process a locally selected receipt or PDF. The adapter starts the bundled Python runtime as a child process without a shell, using fixed arguments and a controlled per-job directory. Python handles PDF page rendering, image preprocessing, and local OCR. OCR has no workbook access.

Use a versioned JSON request/result contract with job IDs. Return extracted text and candidate fields with page identifiers, bounding regions, and confidence where available. Keep diagnostics separate from machine-readable results. Support cancellation, timeouts, malformed-output handling, and child-process cleanup. A failed OCR job must not mutate workbook data.

The application retains source associations and verification state in memory. React displays candidates beside the relevant source page. Users correct and explicitly verify values before the application permits template mapping into worksheet data. Confidence never substitutes for verification. Editing a verified value invalidates its verification until it is reviewed again.

Job files and rendered pages remain local and are removed when no longer needed. Define bounded cleanup on cancellation and startup for abandoned job directories. No network-dependent OCR, remote fallback, or document upload is permitted.

## Explicit Saving

Save and Save As are explicit user actions with a visible destination. Integration approval does not authorize a file write. Native dialogs and application save commands establish the destination; opening a workbook does not grant indefinite background overwrite permission.

The application freezes a session revision for serialization, writes a candidate to a temporary file in the destination directory, and validates it before committing. No serializer writes directly into the original file. For an existing destination, compare its current fingerprint with the opened baseline or the destination state approved for that save. External changes require a fresh user decision; never overwrite them silently.

Commit with a platform-supported atomic replacement where available, with a recoverable backup during replacement. For a new destination, use a same-directory rename after validation. If the filesystem cannot provide the required safe commit behavior, fail safely rather than falling back to truncating the destination. Locks, validation errors, cancellation before commit, and disk failures must retain the in-memory session and leave the original intact.

After a successful commit, update the saved baseline and fingerprint. Clear dirty state only for the revision actually saved. Serialize mutations during saving for the MVP to avoid ambiguous revision behavior. Pending working copies remain pending and are never silently integrated by Save. Session recovery and persistent edit history are future decisions; there is no automatic write to original workbooks.

## Testing and Acceptance

Unit-test domain rules, verification gates, template validation, and application sequencing without WPF. Use fake OCR results and a replaceable file-commit boundary for deterministic failure tests.

Workbook tests must use real fixture files to verify round-trip preservation, copy isolation, conflict detection, merge scope, replacement references, new-sheet resource collisions, and formula handling. Save tests must cover source-byte stability before saving, failed commits, external changes, locked destinations, and successful Save As without changing the source. Include local OCR contract and cancellation tests separately from recognition-quality evaluation.

Run a small desktop integration suite for bridge validation, revision handling, source review, and explicit saving. Report failed and unexecuted checks. No workbook compatibility claim should depend solely on mocked tests.

## Architectural Risks and Unresolved Decisions

| Risk or decision | Required resolution |
| --- | --- |
| .NET workbook library and license | Evaluate offline capability, redistribution terms, preservation, cloning, reference behavior, and testability before selection. |
| Supported formats and advanced content | Proposed MVP scope is `.xlsx`; explicitly decide handling of `.xls`, macros, encryption, signatures, charts, pivots, external links, and embedded objects. Unsupported content must not be silently lost. |
| Working-copy memory cost | Measure representative workbooks, set size/copy limits, and retain the full-context clone until a smaller representation proves equivalent. |
| Replace and import fidelity | Prove library behavior for identities, references, names, tables, and relationships; block operations outside demonstrated support. |
| Calculation semantics | Define supported recalculation and stale-result presentation independently of formula preservation. |
| OCR packaging and quality | Select local OCR/PDF tools, licenses, models, supported languages, distribution size, and performance targets using representative scans. |
| Save guarantees | Test atomic replacement, backup cleanup, file locks, and external-change detection on supported Windows filesystems. A final race with external writers remains a risk to address in the commit implementation. |
| UI editing scope | Define supported formatting and structural edits without committing to full Excel parity. |
| Template schema | Establish initial mapping forms, version validation, and migration behavior before distributing templates. |
| Crash recovery and temporary data | MVP sessions are memory-resident; decide whether later opt-in local recovery files are needed and define retention without introducing a database. |

## Future Boundaries

Additional templates, larger batches, multi-workbook coordination, richer conflict resolution, and broader OCR layouts can extend these components later. They do not justify a workflow framework or background service in the MVP. Future work must preserve local execution, human verification, workbook integrity, and explicit saving.
