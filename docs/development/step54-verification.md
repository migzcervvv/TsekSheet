# Step 54 — read-only fidelity and hierarchical section diagnostics

Date: 2026-10-30 (Asia/Manila).

## Outcome

Implemented conservative per-format isolation, richer inspector details, a bounded one-pass role capture and a separate configurable read-only section/identity analyzer. Requalified the actual authorized duplicate; **its starting and final SHA-256 match**, recorded privately only. No editing limit, mutation permission, production Apply, structural lifecycle or export gate was widened.

The real workbook has **21 qualified numeric interpretations** instead of Step 53's zero: 18 normalized date displays and three plain numeric displays. **3,703 General values remain raw**, and **19,728 numeric cells remain explicitly unsupported**, including 5,862 precision rejections. No currency, percentage or grouped accounting amounts were newly certified in this private qualification. This is a small qualified subset and a precise rejection report, not broad financial display fidelity.

The new analyzer captures the complete configured role range and recognizes both header variants, but **rejects the real section partition and identity coverage**. It retains 25 blank-vendor cases, marker/record conflicts and unassigned rows rather than creating an insertion proposal. A fully verified real hierarchy is still unavailable, which is an explicit supported rejection outcome for this milestone.

## Baseline and privacy

Repository `C:\Users\migzc\Desktop\TsekSheet`; branch `main`, HEAD `de641d53eb87bb6553f2132a42463d04b1ca9796`; origin `https://github.com/migzcervvv/TsekSheet.git`. All prior dirty/untracked Step 51–53 work and Step 52 checkout remediation were preserved. No reset, clean, source-index staging, commit, push or dependency upgrade occurred. Starting status/fingerprints and verification logs are under ignored `artifacts/step54/`.

Verified SDK **10.0.403**, runtime **10.0.14**, Node **24.16.0**, npm **11.13.0**, existing net10.0/net10.0-windows/WPF/WebView2 configuration and locked dependency graphs. Read AGENTS.md, goals/roadmap/readme, accepted ADRs and all requested safety/qualification/profile/toolchain documents. The implementation plan was recorded in PLANS.md before architectural work.

Only `local-test-workbooks/copy.xlsx` was considered. Before opening, validated canonical containment, regular local file status, every source/output ancestor through the drive root, and absence of links/reparse points. Verified directory/source/private artifacts are ignored and untracked. Starting size/SHA-256 are private. All reads use the existing inspection path and its writer/deletion-excluding source handle; sessions were closed before final hashing. No Excel automation, cloud service, remote link resolution, embedded execution, repair, normalization, resave or source write was performed on the private file.

Full sensitive reports/profile/runner remain only under ignored `artifacts/step54-private/`. Tracked materials contain technical counts and generic engineering facts, not actual vendors, identifiers, amounts, sheet names, external targets or private fingerprints. Desktop screenshots/tests use synthetic fixtures only.

## Number-format explanation and implementation

The previous parser treated every explicit `numFmt` with ID below 164 as a stylesheet-wide failure. That was too broad. The schema represents `numFmtId` as an unsigned integer; a standalone ID-43 definition passes the existing Open XML SDK schema check. Microsoft distinguishes implied built-ins from explicitly supplied codes, including language-dependent meanings. A low ID is not sufficient evidence that the stylesheet is malformed. [Number-format specification remarks](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.numberingformat?view=openxml-3.0.1), [Office implementation notes](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oi29500/17d11129-219b-4e2c-88db-45844d21e528).

`xfId` references a base style, while `applyNumberFormat` concerns whether the cell format applies. These attributes require more care than assuming every nonzero parent is unusable or choosing a parent whenever a flag is absent. [Cell-format attributes](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.cellformat?view=openxml-3.0.1).

`InspectionStyles` now separates bounded parse/index validation from local effective-format classification:

| Condition | Current conservative behavior |
| --- | --- |
| Supported built-in without explicit conflict | Existing qualified built-in/date interpretation; no locale/appearance claim. |
| Explicit fixed built-in 0/1/2/3/4/9/10, exact canonical code agreement | Qualified agreement; source ID/code preserved. No alternate low-ID code is silently substituted. |
| Conflicting explicit built-in code | Local ambiguous-format rejection. |
| Explicit ID 43 or other unqualified low ID | Local explicit rejection with original ID/code; never relabeled as a custom ID or replaced by an implied code. |
| Custom ID >=164 | Requires an explicit unique bounded definition and the existing display grammar. Missing, malformed or duplicate definitions reject only dependent styles when assignment is safe. |
| Cell/base number-format agreement | Can resolve the number-format ID independent of which side of an apply flag wins; no font/fill/protection inheritance is inferred. |
| Explicit cell-format override | Accepted only in the qualified scope with a valid base reference. |
| Divergent/missing/recursive inheritance | Raw value and unresolved-inheritance explanation. |
| No explicit cell style, non-default row/column style present | Raw, unresolved inheritance; no implicit default-General financial display. Late column-format declarations reject the view. |
| Global parse/index ambiguity, unknown numeric-affecting extension or exceeded stylesheet budgets | Whole-stylesheet raw fallback remains. Known empty slicer/timeline selectors and differential formats cannot override ordinary cell formats. |

Malformed but assignable style/format records are localized; unassignable definition IDs, duplicate containers, invalid XML and global resource failures remain global. Overlong codes show a bounded prefix with an explicit truncation warning. Original raw value, format ID/code, effective ID where proven, resolution and rejection classification remain available. Step 50 precision/rounding policy and the accounting grammar were not broadened. Conditional formatting is explicitly not evaluated; displays concern qualified base number formats.

## Independent native evidence

Created 19 artificial format-resolution inputs using independent literal expectations and new-file-only generation. They reproduce low IDs, fixed-code agreement/conflicts, IDs >=164, nonzero `xfId`, matching/divergent inheritance, missing references, identical/conflicting duplicates, accounting/currency/percent/date/time, locale syntax and over-precision. No source records or source format code were copied from the private workbook.

`Capture-FormatResolution.ps1` accepts only owned artificial directories under `artifacts/step54-style-reference/`, rejects redirected/linked/active inputs, checks input/procedure hashes, refuses existing Excel processes/capture/output files, disables macros/events/link updates, opens synthetic files read-only and saves new copies only. All original synthetic input hashes are rechecked. It quits/releases its own Excel instance and never receives a private input path.

Actual tool: **Microsoft Excel 16.0 build 20527**. All **19 cases were observed**. **Ten qualified displays exactly match their independent literals and the inspector. Nine remain explicit inspector rejections**, despite Excel displaying them. Those include low-ID conflict/43, ambiguous or missing inheritance, missing custom formats, duplicates, locale and over-precision. Native tolerance/repair behavior is not a preservation certificate. In particular, Excel used an explicit conflicting low-ID code and accepted differing apply-flag cases; the inspector retains conservative rejection rather than generalizing from those observations.

Retained bundle: `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/FormatResolution/`, with 19 artificial input XLSX, 19 Excel-produced XLSX, index and capture manifests. Capture records original IDs/codes/raw input, actual Text/Value2/NumberFormat, Excel version/build and fingerprints. Inputs/outputs, generator, procedure and index bindings are verified. New immutable JSON uses `-text` Git attributes to preserve exact captured bytes; generator/procedure source uses LF. Original Step 44–50 evidence is untouched.

Existing native comparisons retain **42 exact supported / 24 rejected** precision observations and **26 exact supported / 4 rejected** earlier accounting observations, with zero supported divergence. No accepted hash, mismatch or negative expectation was weakened.

## Real numeric requalification

Two disjoint explicit column captures per sheet, including the far observed column, accounted for **all 23,452 nonempty numeric-tagged cells**. The complete streaming scan supplies an independent numeric count; retained numeric counts match it on every tab. A missing captured numeric value would prevent a complete coverage claim. All five worksheets remain browsable; the original package counts/restrictions remain as recorded in Step 53.

| Sheet ordinal | Numeric considered | Qualified | General/raw | Unsupported | Explicit precision rejection |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1, target | 4,649 | 1 | 750 | 3,898 | 1,077 |
| 2 | 4,629 | 1 | 737 | 3,891 | 1,110 |
| 3 | 4,698 | 19 | 747 | 3,932 | 1,181 |
| 4 | 4,709 | 0 | 733 | 3,976 | 1,208 |
| 5 | 4,767 | 0 | 736 | 4,031 | 1,286 |
| Total | **23,452** | **21** | **3,703** | **19,728** | **5,862** |

Qualified cases are **18 normalized dates and 3 plain numeric displays**, with **0 qualified currency/grouped-accounting/percentage displays** in this run. The unsupported total splits into **13,866 unsupported-format cases** and **5,862 precision cases**. General/raw values are not counted as successfully formatted. No numeric cell now uses the stylesheet-wide fallback; localized grammar/precision rejections remain substantial. Text dates/identifiers are separate stored text and were not inferred from numeric values.

All **16,990 numeric formula caches** retain unverified/unsupported formula status; no calculation or cache-currentness claim is made. Ordinary stored/shared formulas remain separately distinguishable. No new shared-formula grammar was introduced.

## Hierarchical analysis and actual coverage

See [architecture, profile example and rejection rules](read-only-payables-section-analysis.md). The new Application analyzer is independent of WPF/WebView2 and the editable planner. The workbook adapter uses the existing bounded streaming reader, including hidden rows, requested shared strings and qualified shared-formula reconstruction. It performs no full-workbook model reconstruction, unsafe ZIP seeking or disk indexing.

The actual target capture covers **rows 1–1608**, **13 configured role columns**, **20,904 coordinates** and **1,519 hidden row elements**, with 10,650,648 accounted bytes. All observed cell/row elements and configured roles are covered; inflated far-right stored extents were neither allocated nor trimmed. Header aliases are explicit private configuration derived from prior inspected headers of the same source fingerprint. The generic engine contains no private years/headings/coordinates.

The result recognizes headers at **3 and 907**, separate heading/entity columns and nine configured heading candidates. **Seven syntactic boundary rules match; two remain ambiguous/unresolved.** One matched rule still finds the intermediate total that caused the old shortcut to fail. Section status remains provisional, `HierarchyVerified=false`, `RecordCoverageComplete=false`, and every report/section has `CanInsert=false`.

Actual identity/coverage diagnostics:

- **807 record-shaped rows:** 782 literal candidates and **25 identifiers with blank vendors**, with no fill-down or inferred identity.
- **73 normalized literal identities**, **46 repeated identities**, **21 potential identifier-duplicate groups**, one entity-only row and zero suggestions from the deliberately narrow similarity heuristic.
- **Seven marker/record conflicts** are retained inside the unresolved cases rather than hidden as subtotal rows.
- **480 record-shaped rows have no verified owner**. Including unclassified formula context, **484 distinct rows remain unassigned**. There are 155 unclassified-summary diagnostics, 151 lexical subtotal candidates and 48 summary-dependency entries; dependency entries explicitly remain incomplete.
- Known candidate-boundary memberships show no certified multi-section vendor result. This does **not** contradict Step 53's one identity across provisional heading groups: hundreds of rows have unverified ownership, so every vendor's `SectionCoverageComplete` remains false. Whole-worksheet vendor destination/absence conclusions are unavailable.

The private profile is intentionally a diagnostic hypothesis using explicit contiguous-range intent. No nearest/first SUM or predecessor vendor is selected. Generic synthetic tests prove nested parents/children and separate marker/entity columns work when rules are unambiguous; the real hierarchy is correctly rejected until its larger aggregation intent and identities are reviewed. No production request-specific vendor placement was run.

## Performance

Final measured run on this development host:

| Workload | Observed time |
| --- | --- |
| Metadata | 127.7619 ms |
| Initial per-sheet viewport | 118.2387–226.6508 ms |
| Repeated cached viewport | 0.0150–0.6573 ms |
| Distant viewport | 97.6505–164.9970 ms |
| Per-sheet numeric capture plus interpretation | 341.4506–504.9542 ms |
| Full target role capture | 384.4909 ms |
| Pure section/identity analysis | 192.3336 ms |
| End-to-end private diagnostics, five tabs | 4,716.7595 ms |

Process-lifetime peak working set was **125,837,312 bytes**; cumulative managed allocations **1,448,368,232 bytes**. The target capture's accounted payload is **10,650,648 bytes**. Cumulative allocations are churn, not simultaneously retained memory or a hard heap bound.

Step 53's one-off repeated-viewport diagnostic took about 43.9 seconds and allocated about 29.1 GB cumulatively. The new target capture is one worksheet pass plus bounded shared-string/style reads, rather than 96 full-sheet viewport rescans. These are different diagnostic workloads/measurement runs and not a controlled universal speedup or maximum-capacity claim. Existing recent-window caching remains unchanged in purpose; larger analysis captures are not cached on disk or in the UI cache.

## Desktop and proposed reviewed output

The existing inspector details now show source/effective number-format IDs, format resolution, display classification, raw/display values and unverified formula caches. Client validation checks optional metadata without granting mutation capabilities. Existing styling/logos/navigation remain in place. Actual WPF/WebView2 smoke covers these details on artificial fixtures and retains disabled copy/Save As/Apply behavior.

The section feature is exposed as structured backend diagnostics, **not a complete new desktop section/profile editor** or a production Payables planner. No speculative placement UI or analysis-to-editable session adapter was introduced. The private runner demonstrates the API and supplies a redacted bounded summary.

[Reviewed separate-output design](reviewed-payables-output-design.md) specifies a future revision/source/profile-bound posting schedule with explicit identity/financial/duplicate review and a separate new-file commit. It is design-only: no output/export implementation, source integration or new approval capability exists. A reviewed schedule must never be called an applied ledger row.

## Verification and preservation

| Check | Final result |
| --- | --- |
| Locked solution/evaluation/native/managed NuGet restores | Passed, unchanged dependency graphs/locks |
| Normal .NET 10 Release build | Passed, zero warnings/errors |
| Complete solution tests | **401 passed / 0 failed / 0 skipped**: Application 62, Workbooks 326, Desktop 13 |
| New .NET coverage | **42 additional passing cases**: 15 Application and 27 Workbooks, including 19 native-format fixture controls inside their test method |
| Retained provenance/native accounting/precision/format comparisons | Passed, exact historical counts and source hashes retained |
| Checkout-portability/evidence safeguards | **5 passed**, both autocrlf conditions, including new immutable capture bytes/hashes |
| Frontend typecheck/build/tests | Passed; **35 tests**, including format-detail rendering and malformed-metadata rejection |
| Toolchain / evaluation safety | **3 / 13 passed**; evaluation synthetic only |
| Native / managed file-commit harnesses | **600 / 390 passed**, zero failed |
| Self-contained win-x64 publish | Passed to `artifacts/step54-win-x64`, runtime 10.0.14 |
| Actual WPF/WebView2 smoke | All **13 Desktop tests** passed, selector seams and artificial data only |
| Original tracked fixture/logo/lockfile fingerprints | **88 checked, zero changed** |
| Private source before/after fingerprint | Matches starting private baseline; all sessions closed |
| Private paths and logs | Input/reports remain ignored/untracked; only safe technical summaries emitted |

An additional local privacy audit checked 68 new/changed text artifacts and ordinary Step 54 logs against private source identity and bounded candidate terms, with zero matches. This scoped check supplements review and synthetic-only generation; it is not a universal detector for every possible private substring.

Negative coverage includes malformed/global versus localized styles, collisions, inheritance/default rows, strict profile parsing, aliases/repeated or shifted headers, nested/overlapping regions, intermediate/duplicate subtotals, summaries, marker conflicts, missing/formula/truncated identities, incomplete rows/cells, styled hidden tails, stale bindings, cancellation during scan, resource limits, source hashes and unchanged inspection mutation rejection. No native Excel execution occurred on private content.

Development failures were retained in ignored logs: the first stricter stylesheet parser also rejected existing empty slicer/timeline selectors, causing two historical exact-match tests to fail. Handling was narrowed to those non-cell selectors and differential formats; all original exact expectations then passed unchanged. A TypeScript callback lost object narrowing and was fixed. A frontend check was launched while the build's npm ci temporarily removed its tools; rerunning after installation completed passed. Initial test-only nullable warnings were corrected. None remains a final failed/skipped test, and no accepted hash was edited to hide a failure.

Five evaluation-only ImageSharp advisories remain visible/unresolved (three high/two moderate). The historical library research matrix stays **22 passes / 24 known failures / 26 unexecuted** and was not rerun. Manual native dialogs, clean-machine offline installation, full Excel appearance/calculation fidelity, complete real hierarchy and source structural preservation remain unexecuted/unproven.

## Changed files and Step 55

New implementation: `InspectionStyles.cs`, `InspectionAnalysisContracts.cs`, `ReadOnlySectionAnalysis.cs`. Updated `WorkbookInspection.cs`, `InspectionCellFormatting.cs`, workbook DTOs, frontend bridge/detail rendering and existing UI/smoke tests. New tests: `InspectionStyleIsolationTests.cs`, `ReadOnlySectionAnalysisTests.cs`, `FormatResolutionFixture.cs`, `NativeFormatResolutionTests.cs`. Added `Capture-FormatResolution.ps1`, the 40-file synthetic/native evidence bundle, scoped attributes and extended portability checks. Added this report, read-only analysis and separate-output design documents; updated interpretation/fidelity/analyzer/strategy/workflow/roadmap/readme links. No production dependency, SVG, original binary fixture or lockfile changed.

Recommended **Step 55**: explicitly review the real hierarchical aggregation boundaries and missing identities using a bounded section-diagnostics/profile interface, and prioritize narrowly evidenced date/accounting grammar work if useful. Keep the 484 unassigned rows and 25 unresolved identities visible until verified. A reviewed separate schedule remains the smallest future posting design; direct package editing still needs an independent reference/relationship/anchor preservation proof for the actual feature envelope. No cell-limit increase or broad reconstruction is justified by these read-only results.

Production Apply remains disabled, Step 45 tickets permanently review-only, inspection copies/editing/Merge/export disabled, and source/existing-file overwrite, Replace/Create New, autosave, PDF OCR, cloud services and databases remain unavailable. **Phase 2 is incomplete.**
