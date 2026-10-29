# Structural preservation oracle — Step 43

## Step 45 update

[Native formatting investigation](step45-verification.md) proves that the known 22/21.95 difference also occurs on no-op Excel saves; an additional 20/20.100000000000001 difference is retained. Exact height/custom-height/default/style preservation in the oracle is unchanged. New independent defects cover height flags/defaults, effective format definitions/IDs, borders/fills, duplicated vendor/RFP/invoice values and reordered batches; the formula grammar is unchanged.

The public review path can build/reopen/run this oracle and bind its actual acceptance report into the review fingerprint. Native-format review qualification is separate: only the narrow fixed-21/default-15 policy permits metadata approval, and every public ticket is permanently review-only. Acceptance or approval cannot enable adoption/integration through internal flags. Source/editing/saving restrictions remain mandatory. Older milestone descriptions below are historical.

## Step 44 update

[Independent Excel reference evidence](excel-reference-corpus.md) now includes four actually generated native Excel cases, literal expectations and seven unsupported controls. New defective-candidate tests independently catch missed absolute-reference movement, missed row shift, incomplete SUM coverage and duplicated identifiers. The oracle algorithm/feature envelope was not broadened or weakened. Compared native content/formulas/styles agree; measured Excel row-height and package serialization differences remain explicit, so no full Excel equivalence is claimed.

`StructuralLifecycle.cs` adds internally gated generated-candidate review/approval/adoption, separate structural integration and structural export proof. Approval binds exact candidate/review/manifest hashes and revisions. Structural Merge requires a clean single-operation copy, identical destination/base package and current revisions; export reopens and reruns the oracle before the existing safe commit. Production Apply and public structural Merge remain disabled. [Current lifecycle evidence](step44-verification.md) supersedes the Step 43-only disabled-experiment description below.

Status: a standalone read-only oracle and an internal bounded candidate editor are implemented and tested on synthetic workbooks. Desktop Apply, structural Merge/export and existing-file saving are disabled. Phase 2 remains incomplete. This is not native Excel compatibility certification.

## Architecture and actual independence

`StructuralPreservationOracle.Evaluate(beforeBytes, manifest, candidateBytes, token)` reads ZIP/XML directly without ClosedXML mutation. Both inputs must pass the existing bounded package/security/schema guard. Its report includes acceptance/rejection, worksheet, insertion boundary/count, permitted changes and categorized location/reason differences. Unverifiable packages reject; cancellation propagates.

The manifest identifies worksheet, original boundary, template row, first data row, original subtotal and ordered mapped records. Record count specifies the exact row shift. It describes permitted changes, not user authorization. Execution separately validates the original request and reviewed plan, provenance, session, revision, profile, approval, choices and duplicate review.

Editor and oracle share package reading, namespace/address utilities, the envelope classifier and normal engine guard. They do **not** share expected-row construction, formula adjustment or field serialization. The oracle derives expected original-cell identities and rewrites reference numbers by a reverse positional scan. The editor mutates a separate tree and translates references through a forward replacement. A hand-specified golden package with literal row/formula dictionaries is accepted without invoking the editor. Independent literal cell assertions and tampered-candidate tests supplement this evidence. Shared envelope assumptions and absence of an external Excel reference engine remain limitations.

## Permitted changes and semantic comparison

1. Insert 1–20 records at one original boundary in one section, preserving input order.
2. Move every existing selected-sheet row at/after the boundary by exactly that count. Preserve row attributes, constants, style indexes and stored cell identities/order. Move the optional used dimension's ending row accordingly.
3. Translate supported same-sheet references by original referenced row identity. Preserve relative/absolute/mixed dollar markers; dollar markers affect copying, not movement of referenced cells.
4. Extend the selected section's complete SUM at the subtotal edge. At interior insertions, its end reference shifts normally. Every mapped decimal column requires an exact `SUM(firstDataRow:subtotalRow-1)` in the original subtotal. Only ordered single-column ranges are supported.
5. Populate approved literal fields using template styles. Copy only approved row-height attributes and remove the copied span hint. Clear unrelated constants, RFP/invoice values, payment status and vouchers. Empty approved fields stay blank. Formula-bearing record templates reject.
6. Remove cached `v` values from all selected-sheet formula cells and set workbook `fullCalcOnLoad=1` and `forceFullCalc=1`; preserve other workbook XML. No recalculation or reliable cached total is claimed.

The entry set must match; all other package parts must retain identical uncompressed bytes. ZIP compression/timestamps/container bytes may differ. Target-sheet/workbook XML comparison uses expanded names, sorted attributes and length-prefixed values; namespace prefixes, inter-element indentation and comments are ignored. Leaf text/whitespace remains significant. Cell duplicates/order, attributes, row heights, style indexes, formulas and non-value XML are checked. The unaffected-part byte rule intentionally rejects harmless broad serializer rewrites, including shared-string-table normalization.

Difference categories include cells/rows, content-or-formatting, formatting, formulas, defined-names, references, worksheet-structure, relationships, package and unsupported-or-invalid. Names/tables/merges are rejection categories, not supported transformations. Removed parts and changed relationships fail even if the normal engine can open the output.

## Supported structural operation matrix

| Feature | Implemented and tested scope |
| --- | --- |
| Plain `.xlsx`, selected visible sheet | Existing safety policy plus structural bounds; surrounding constant-only context retained |
| Existing vendor / middle insertion | Planner's final matching row; explicit choice for multiple matching sections |
| Absent vendor / immediately before subtotal | Configured fallback section; no hardcoded generic coordinates or business labels |
| Same/different vendors in a batch | One boundary/section, 1–20 ordered records; other boundaries reject |
| Repeated insertion | Fresh requests per accepted revision; cumulative summaries/subtotals verified |
| Original cell types | Plain numbers, strings and booleans retained; new fields are literal text/decimal/date |
| Formulas | Uppercase bounded A1 reference, addition of A1 references, or one ordered single-column SUM |
| Relative/absolute/mixed references | Literal expected address translations and preserved markers |
| Downstream summaries | Same-sheet references to correctly shifted section subtotal cells |
| Dates | 1900 system, at/after 1900-03-01, explicit `yyyy-mm-dd` template format |
| Formatting | Existing currency/date styles, fill, borders, font, alignment and row height; style part unchanged |
| Clean rows | Unmapped constants cleared, approved fields alone populated; no formula copying |

Bounds: 8 MiB compressed, 16 MiB expanded, 128 ZIP entries; stored source rows at most 2,000, columns at most 32; formula length at most 128; 20 added rows. Existing engine/session memory reservations remain active. Cancellation checks cover package/row loops and transaction stages. These are conservative work limits, not a hard wall-clock/CLR memory quota; maximum-bound performance is unqualified.

## Detected and rejected features

- All workbook/local defined names, including print areas/titles or broken references; nothing is deleted to force acceptance.
- All tables/structured references and table parts. An empty `tableParts` marker emitted by ClosedXML is preserved.
- All merges, including heading-only and crossing-boundary merges.
- AutoFilters, validation, conditional formatting, extensions and advanced worksheet children.
- Shared/array/dynamic-spill formulas; cross-sheet/self-qualified/external formulas; names, unsupported functions/operators/literals, broken references. Formulas outside the selected sheet reject.
- A non-section SUM ending immediately above insertion: expansion intent is unverified. General vendor subtotal expansion is therefore unsupported.
- Hidden/grouped rows or columns. Nonselected hidden constant-only sheets may be retained by the package policy; structural fidelity of their layout is not certified.
- Inconsistent record cell/style/height patterns, formula-bearing records, missing/blank/non-record template, missing subtotals, ambiguous headings and unclassified records.
- 1904 date system and date mappings without the explicit supported template format/range.
- Drawings, comments/anchors, worksheet relationships, calculation chains, custom XML, unknown/opaque/unreferenced parts. Original macro/external-link/protection/binary/signature restrictions remain mandatory.
- Multiple boundaries/sections per batch, unresolved choices, unreviewed duplicates, stale/forged proposal/session/profile/revision/provenance.

Not analyzed: general Excel formula/reference semantics and recalculation, feature-specific dependency graphs, rich templates, pivots/charts/slicers, arbitrary named-range/table/merge transformations, native Excel repair/rendering, structural Merge/rebase/export, real user-workbook compatibility, maximum-size throughput and clean-machine offline deployment. Rejection applies where detection exists; unanalyzed features gain no support.

## Candidate transaction, rollback and capability gate

`WorkbookSession.ExperimentInsert` is internal; `WorkbookEngine.StructuralExperimentsEnabled` defaults false. No public engine/interface/bridge/frontend route enables the experiment. It operates only on the selected full-context working copy. Under the session lock it re-plans from the original request, compares the complete reviewed plan, revalidates profile/revision/provenance/section boundaries and requires approved choices/duplicates. It verifies uniform record templates and complete mapped-decimal subtotals.

The editor clones OOXML bytes/tree, shifts rows, clears template content, maps literals, adjusts only supported references, serializes, reopens through the normal workbook engine, runs the oracle and checks cancellation before adopting. Success increments the copy revision once and marks it dirty. Main-session and disk source remain unchanged. Old requests become stale.

Eight deterministic fault/cancellation seams cover before creation, after shift, mapping, formula adjustment, serialization, reopen, oracle and immediately before adoption. Tests assert unchanged revision/dirty state/content/source and successful later retry. Actual corrupt packages and reopenable semantic tampering are injected separately. Rejected candidate resources are disposed, and reservations follow existing session ownership rules.

Experimental structural copies are blocked from cell-only Merge and snapshots; direct Save As already rejects copies. Merge rejects before inspecting recorded coordinates that may have moved. This prevents partial structural export. Production `InsertWorkflowRows` still rejects forged `CanApply=true`; there is no automatic save.

## Desktop status

`WorkflowPlan.StructuralStatus` distinguishes ambiguous destination, duplicate review, unsupported structure, stale proposal, experimental package envelope only and production disabled. Resolved nonduplicate plans receive a read-only package preflight. Ambiguity/duplicates take precedence. Preflight passing is not candidate acceptance: template consistency, serialization, reopen and full oracle validation remain pending. Internal candidate-validation failures cannot appear as successful desktop candidates.

`CanApply` always remains false. The frontend rejects executable/unknown-status responses, displays structural reasons, invalidates stale previews and keeps Apply disabled. Approved profile `insertionPolicy` remains `disabled`; no experimental editor is wired to desktop. See [Step 43 verification](step43-verification.md) for evidence and limitations.
