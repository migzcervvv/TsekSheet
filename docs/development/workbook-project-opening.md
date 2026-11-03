# Workbook project opening and accounting compatibility — 2026-11-03

## Cause and evidence

The project picker called `WorkbookWorkspace.OpenAsync(false)` →
`WorkbookEngine.Open` → `PackageGuard.Inspect` → Open XML validation → ClosedXML.
Any rejection ended opening. `Inspect workbook read-only` called
`OpenAsync(true)` → `WorkbookEngine.Inspect` → `WorkbookInspection`, a separate
bounded streaming reader. There was no fallback between them.

The restrictive editable admission is intentional preservation policy. Coupling
that admission to whether a workbook can become a project was the functional
defect. A workbook can be safe to inspect while unsafe to rewrite.

The retained [Step 53 qualification](step53-verification.md) records the actual
authorized duplicate: editable validation stopped at **50,001 stored cells**,
code **LIMIT_EXCEEDED**, against a 50,000-cell limit. Independent inspection had
counted 439,321 stored cells. It also observed external-link parts, extensive
defined names (including pre-existing broken references), VML/comments, printer
settings and complex shared formulas. Raising the cell limit alone cannot qualify
that workbook for editing. No private workbook was opened, changed or copied into
fixtures during this fix. This historical first rejection is not attributed to
every newly reported workbook; current Details gives each selected file's result.

## Trace of the two paths

| Concern | Editable-model admission | Independent inspection |
| --- | --- | --- |
| Selection/location | Native selected local `.xlsx`, no frontend paths; reject network/reparse paths | Same native selection and local-path restrictions, independently checked |
| Resource admission | 16 MiB file, 64 MiB expanded, 1,024 entries, 50,000 stored cells, 32 sheets, 4,096 styles, 500,000 XML nodes; session/copy memory reservations | 128 MiB file, 512 MiB declared expanded, 4,096 entries, 512 tabs, bounded metadata; each selected-sheet scan separately limited to 64 MiB XML/2M cells/10 seconds |
| Structure | Scans all package XML, relationships, SDK schema, then constructs full library model | ZIP allocation/path checks, relationship graph and transitional workbook metadata; unrequested worksheet XML and opaque content are not certified |
| Source access | Read-only writer-excluding read; private original-byte/hash baseline; source handle released after load | Read-only writer/deletion-excluding handle and SHA-256 retained until session close; never executes objects or follows external targets |
| Qualification | Model load can still return EditingAllowed=false for advanced/unknown/truncated content; individual cell/formula, snapshot, integration and output gates remain | EditingAllowed=false, revision 0, clean, no mutable library model; every mutation/copy/export denied |
| Session/project | One serialized active session; successful candidate replaces old session; pending dirty changes block replacement | Same project ownership/switching; activation cannot convert the session or confer privileges |
| Saving | Explicit native selection of a separate new file; revision, candidate, source/destination and native commit checks | Denied before any output picker; no original or separate output writes |

## Changed behavior

`Open workbook` first attempts the unchanged editable admission. Only known
`LIMIT_EXCEEDED`, `UNSUPPORTED_FEATURE`, `EXTERNAL_RESOURCE`,
`PROTECTED_WORKBOOK` and `INVALID_PACKAGE` rejections trigger an **independent**
inspection attempt. Format/location/access/busy failures and cancellation never
trigger fallback. A default adapter with no inspector retains the original error.

Successful fallback adopts the actual inspection session. Its returned project
findings retain the exact first error under `EDITABLE_ADMISSION_<original code>`.
No editable flags or engine limits change. Application copy creation now also
explicitly denies noneditable/inspection projects; existing session, save, receipt
mapping, workflow planning, integration and structural/output gates remain.

If independent inspection fails, both attempts' code/message pairs cross the
bridge in bounded structured error details. The previous active project remains
usable. Metadata XML, compression and file-access errors have specific inspection
explanations. Metadata success may coexist with an unavailable worksheet: the
individual view reports its own blocker and other safe sheets remain navigable.

The UI shows inspection success only for a successfully opened noneditable
project, with collapsed Details containing rejection codes, findings and package
categories. Explicit Inspect says editing eligibility was not evaluated; automatic
fallback says editable admission was rejected. Advanced models list detected
features, including conditional formatting, shared/array formulas and calculation
chains. Failure Details identifies both blockers. Long codes wrap within notices.
The sidebar identifies noneditable projects as inspection only. No repair,
simplification, link removal or automatic editable retry is performed.

## Editing compatibility and remaining requirements

| Condition / current code | Safe improvement delivered | Missing proof before editing |
| --- | --- | --- |
| Cell/sheet/file/style/XML/memory budgets: LIMIT_EXCEEDED | Inspection project fallback; first measured threshold retained | Full-context loading/copy/validation/serialization resource measurements; a resource extension cannot waive feature gates |
| External relationships: EXTERNAL_RESOURCE; externalLink/connections parts: UNSUPPORTED_FEATURE | Distinguish exact detected feature and inspect without resolving targets | Preserve relationship IDs/types/targets, link parts, formulas and caches; independently prove affected dependencies and no refresh |
| Images, embedded objects, VML, printer binary: UNSUPPORTED_FEATURE | Specific category in rejection; bounded cells remain viewable | Retain part bytes/content types/relationships; prove anchors, comments and print context remain semantically correct after the proposed edit |
| Protection: PROTECTED_WORKBOOK | Inspect unencrypted XML without removing protection | Explicit policy and feature preservation tests; encrypted containers still cannot be inspected as ZIP |
| Shared/array formulas, conditional formatting, data validation, extensions, drawings, pivots, calcChain: ADVANCED_CONTENT | Detected feature list; existing noneditable load retained | Independent before/after expression, cache, range, style and dependency oracles for the exact operation |
| Unknown/unreferenced parts or relationships: UNKNOWN_PARTS | Existing rejection/restrictions retained and displayed | Complete dependency classification and unaffected-part/graph invariants; never delete unknown content |
| Defined names, existing broken names, merges, tables, hidden/grouped layout and cross-sheet references | Ordinary proven cases retain existing narrow support; inspection counts/navigation confer no structural authority | Scope/target/inbound-use analysis; distinguish existing defects from new damage; independent reference/anchor/range transforms for insertion/replacement/import |
| Malformed/unsafe package, prohibited metadata XML, unsupported format/location | Actual blocker retained; previous project preserved | Correct input within supported security/inspection bounds; schema bypass grants no editing eligibility |

Production receipt insertion/export, Replace/Create New, structural adoption and
source/existing-file overwrite remain disabled. The existing narrow cell-copy,
Merge and new-file-only Save As workflows retain their checks. Production editing
of these complex accounting templates requires operation-specific independent
package/semantic preservation evidence and representative accounting-owner
acceptance. Successful viewing, a higher test count or output reopening alone is
insufficient. Targeted OOXML transactions remain a potential future strategy,
not an implemented workaround.

## Verification

Evidence is under `artifacts/workbook-opening/`. Artificial fixtures generate the
50,001-cell, 33-tab, external relationship/link-part, VML, image, printer settings,
protection, advanced-format and malformed-metadata/worksheet cases. They contain
no private accounting records. Source hashes, denied copy/edit/save/planning,
native-output picker noninvocation, hidden-sheet navigation, switching, stale
identities and pending-copy guards are exercised. Existing complete regressions
cover narrow edits, integration, formula/format/context preservation and Save As
faults/races/no-overwrite rules. The hosted regression uses actual WPF/WebView2,
bundled React, native-selector injection, project switching and bridge rejection
probes; it does not automate the physical Windows file dialog.

Final executed totals and any failed/unexecuted checks are recorded in
[the progress tracker](AUTONOMOUS_PROGRESS.md). No gate/test was removed, skipped
or weakened. Private-workbook mutation, Excel rendering/calculation fidelity,
accounting-owner acceptance and clean-machine offline deployment remain unexecuted.
