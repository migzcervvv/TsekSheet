# ADR-0003: Generic, Versioned JSON Template Engine

Status: Accepted

Date: 2026-10-28

## Context

[AGENTS.md](../../../AGENTS.md) and [GOALS.md](../../../GOALS.md) require modular, testable workbook processing that preserves formulas, formatting, and worksheet integrity. The reimbursement workbook is the first example, but TsekSheet must support other spreadsheet workflows through generic configuration.

[ADR-0001](ADR-0001-desktop-framework.md) places application and workbook processing in C#/.NET, independently of WPF and WebView2. [ADR-0002](ADR-0002-local-first.md) requires offline operation, in-memory sessions, local processing without a database or cloud services, human verification, and explicit saving. The [architecture overview](../overview.md) defines a generic template engine and candidate-validated structural changes, including repeatable row insertion before summary sections.

Workbook layouts vary between workflows and between worksheets in one workbook. Hardcoded accounting fields, worksheet names, row numbers, or summary positions would couple workbook processing to the first example and make reuse unsafe.

## Decision

Use generic, declarative, versioned JSON template definitions stored locally. Templates describe how supported workbook structures are recognized and how verified workflow data maps into them. Implement interpretation and validation in C#/.NET without dependencies on WPF, WebView2, or React.

Keep the engine small: parse a supported definition, validate it, bind it to a read-only worksheet description, and produce proposed typed edits or a structural insertion plan. The application coordinates preview and application. The workbook engine performs supported mutations and preservation checks. Templates do not directly mutate workbook objects, run OCR, or save files.

Distinguish the template format version from a particular template definition's identity and revision. Reject unsupported format versions rather than guessing their meaning. Associate previews with the resolved template revision, worksheet binding, and workbook revision; changing any of them invalidates the preview. Compatibility and migration behavior must be explicit and tested before new versions are supported. Never silently rewrite user template files.

This ADR establishes responsibilities and invariants, not a complete JSON schema. Exact property names, detailed selector syntax, serialization types, and migration mechanisms remain implementation decisions. Do not introduce a general scripting language, plugin framework, or universal workflow engine.

## Template Responsibilities

Templates describe the following concepts within a supported, bounded vocabulary:

- Identity, format version, definition revision, and descriptive metadata.
- Expected worksheet structure and selectors based on configured names, table metadata, or explicit markers.
- Repeatable data regions with start and end boundaries, summary boundaries, and template rows or row patterns.
- Workflow field identifiers, types, required values, and simple declarative validation constraints.
- Mappings from verified fields into supported cell targets, columns, or table fields, including which destinations accept data and which contain formulas.
- Formatting and applicable formula sources for new rows, together with declared aggregation-range expansion intent.

The reimbursement example supplies its labels, field mappings, and constraints through configuration. Other workflows can define different fields and layouts without changing the generic workbook engine. Templates describe intended operations; engine capability checks determine whether an operation can be performed safely.

## Separation Between Workbook Structure and Workflow Rules

Keep structural binding and workflow rules distinct within the template model, even if initially stored in one JSON file.

| Concern | Ownership and behavior |
| --- | --- |
| Workbook structure | Template configuration identifies regions, boundaries, targets, and row patterns. Binding resolves these against the current worksheet description without assigning accounting meaning to them. |
| Workflow rules | Declarative field types, required values, and simple constraints validate the supplied record before mapping it into structural targets. |
| Workbook mechanics | The workbook engine owns row insertion, formula translation, merged-range handling, dependency inspection, copying, and serialization. |
| Application policy | The application enforces human verification, revision checks, preview/apply sequencing, working-copy isolation, integration choices, and explicit Save or Save As. |
| Presentation and extraction | React presents fields, sources, errors, and previews; Python returns local OCR candidates. Neither owns template or workbook correctness. |

Workflow rules refer to logical field and target identifiers rather than embedding workbook-library calls. Structural rules do not assume reimbursement categories or accounting approval behavior. Keep future complex workflow automation outside the MVP until a concrete requirement justifies it.

Human verification and save authorization are mandatory application policies. A template cannot disable them, authorize overwriting an original, bypass protection checks, or permit an operation outside the engine's supported compatibility contract.

## Template-Driven Row Insertion and Formula Preservation

Resolve each repeatable region and its boundaries against the current worksheet revision. Reject missing, ambiguous, overlapping, or inconsistent boundaries. Do not hardcode row 30, worksheet names such as `MARIA`, or other example-specific positions in application logic.

When new entries require additional rows, generate an insertion plan containing the bound region, insertion point before the summary section, row count, template-row sources, field destinations, and aggregation adjustments. Insert complete worksheet rows so summary rows and all content below shift downward across every column. Check worksheet limits and affected content before attempting the operation.

Copy formatting, row heights, and applicable formulas from configured template rows or patterns. Populate only mapped input fields; do not copy previous entry values into new records. Use workbook-aware formula translation that respects relative, absolute, and mixed references. Preserve and correctly adjust affected existing formulas, formatting, merged ranges, names, table ranges, and same-sheet and cross-sheet references. Never rewrite formulas with string replacement.

Apply declared summary-range expansion so totals include newly inserted records. Do not assume a library automatically extends a range when insertion occurs at its boundary. Shift ranges below the boundary and expand or replicate intersecting ranges only where the configured intent and verified engine behavior are unambiguous. Reject unsupported reference patterns, merge layouts, or affected workbook features.

Apply the complete plan to an isolated candidate workbook at the expected revision. Validate boundaries, row movement, formulas, copied formatting, references, and unaffected content, then serialize and reopen the candidate before adoption. Failure or cancellation before adoption discards the candidate and retains the prior active state. Adoption changes the in-memory session or working copy only.

Resolve boundaries again after insertion so repeated operations use the new summary position. Preserve structural intent in integration previews; do not merge shifted coordinates as ordinary independent cell edits or replay a plan against an incompatible revision.

Formula preservation remains distinct from recalculation. Templates do not supply a new calculation engine or guarantee Excel-equivalent evaluation. Preserve supported formulas and surface stale cached results according to the workbook engine's defined behavior.

## Supporting Multiple Worksheets with the Same Template

A template definition is reusable configuration, not a container for mutable worksheet state. Bind it separately to each selected worksheet using stable application worksheet IDs. Keep resolved boundaries, target locations, and revisions in per-worksheet bindings.

Worksheets using the same template may have different names, data lengths, and summary positions. Resolve and validate each independently. Reject an incompatible sheet with a specific reason; do not borrow another sheet's coordinates or silently fall back to the first match. Navigation or selection must not modify hidden or unselected sheets.

For the MVP, apply and report operations per worksheet rather than implying an unimplemented multi-sheet transaction. Any batch orchestration must explicitly define its validation and commit scope before implementation. Shared template use does not share mutable cells, styles, or workbook objects between working copies.

## Validation and Security

Validate definitions at three boundaries:

1. Definition loading: enforce supported versions, required concepts, valid types, unique identifiers, bounded sizes and nesting, and permitted rule kinds. Reject unknown operational directives rather than silently ignoring them.
2. Worksheet binding and plan creation: verify selector uniqueness, boundary ordering, target compatibility, formula/protected-cell rules, verified input state, supported workbook features, and expected revisions.
3. Candidate adoption: verify the complete resulting workbook, not only mapped cells. Reopen serialized candidates and check preservation invariants before replacing active state.

Treat JSON templates, workbook content, and OCR values as untrusted data. Allow no arbitrary scripts, executable expressions, shell commands, network references, external schema retrieval, or filesystem directives. Keep configuration and validation entirely local with no database, cloud service, or paid AI API. Rule evaluation must have bounded work and produce deterministic results for the same definition, input, binding, and workbook revision.

Write input values using their declared data types. Text resembling a spreadsheet formula must remain literal text unless the destination is explicitly approved for a supported formula operation. Formula propagation must not enable macro execution or external-link refresh. Templates cannot grant capabilities disallowed by the application.

Return actionable errors identifying the definition, field, region, or target involved without leaking document contents into logs. Any necessary temporary files and their cleanup follow ADR-0002. Original workbooks remain untouched until an explicit authorized save with validation and external-change detection.

## Alternatives Considered

| Alternative | Reason not selected |
| --- | --- |
| Hardcoded reimbursement fields and worksheet coordinates | Couples the engine to the first workbook and cannot safely handle other layouts or workflows. |
| Separate application code for every workbook | Duplicates mapping and validation behavior and makes each layout change an application change. |
| Unversioned JSON configuration | Gives no reliable basis for interpreting changed semantics or rejecting incompatible definitions. |
| Arbitrary scripts or workbook macros as templates | Expands execution privileges, undermines deterministic validation, and complicates offline security and testing. |
| A database-backed template catalog or cloud template service | Violates the local-first constraints and adds persistence or service dependencies unnecessary for local definitions. |
| Full workflow framework and complete schema designed upfront | Adds abstraction and speculative features beyond the initial mapping and structural requirements. Evolve a bounded format from tested examples. |

## Consequences and Testing Requirements

The first reimbursement template becomes an example of a reusable mechanism rather than the engine's domain model. New compatible layouts and workflows can be expressed through configuration, while unsupported mechanics still require explicit engine implementation and tests. Configuration does not make every workbook safe to edit.

Versioning, selector validation, diagnostics, and candidate checks add work. Flexible boundaries can be ambiguous, and row insertion remains dependent on proven workbook-library behavior. Keep the MVP vocabulary narrow and reject unsupported cases. A template editor, elaborate migrations, advanced layout inference, and complex workflow automation remain future decisions.

Implementation must include:

- Unit tests for version handling, definition validation, deterministic mappings, field types, required values, malformed configurations, unsupported directives, and bounded rule evaluation, without WPF or WebView2.
- Application tests proving that templates cannot bypass verification, formula-cell protections, revision checks, or explicit-save authorization; modified verified values require renewed verification.
- Real workbook fixtures for region binding, worksheet compatibility, missing and ambiguous markers, independent working copies, and formula/formatting preservation.
- Repeated row-insertion regression tests, including serialization and reopening between insertions. Assert cumulative summary movement, intact lower content, complete totals, correct relative/absolute/mixed formula references, formatting, merged ranges, names, and cross-sheet references.
- Tests applying one template to worksheets with different names and boundary positions, including positions other than row 30. Verify independent bindings, no mutable-state leakage, and rejection of incompatible sheets.
- Failure tests for unsupported structural changes, stale template or workbook revisions, conflicting integrations, cancellation, and failed candidate validation. Confirm that active state and original file bytes remain unchanged on rejection.
- Save-path integration tests confirming validated output, source preservation during Save As, external-change detection, and safe commit behavior under ADR-0002.
- At least one non-reimbursement fixture demonstrating that the same engine can map different logical fields and apply the same structural rules without accounting-specific branches.

Report failed and unexecuted checks. This ADR records an accepted architectural decision; it does not introduce a complete JSON schema, executable tests, or an implemented template engine.
