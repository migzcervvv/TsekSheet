# Step 53 — actual Payables workbook qualification

Date: 2026-10-30 (Asia/Manila).

## Result and scope

The authorized duplicate `local-test-workbooks/copy.xlsx` was supplied and qualified with the existing bounded read-only engine. **Its final SHA-256 matches the privately recorded starting fingerprint.** All five worksheets can be browsed. Safe editing remains blocked, and section/vendor feasibility is partial rather than production-ready.

Detailed reports, original fingerprint, defined-name text, external targets and local diagnostic runner remain only under ignored `artifacts/step53-private/`. This tracked report contains technical aggregates, generic field mappings and source coordinates, with no worksheet names, vendor identities, RFP/invoice values, amounts or private fingerprints. No Excel automation, remote service, link resolution, embedded execution, source repair or saving was used.

## Baseline and safety

Repository `C:\Users\migzc\Desktop\TsekSheet`, branch `main`, HEAD `de641d53eb87bb6553f2132a42463d04b1ca9796`; origin `https://github.com/migzcervvv/TsekSheet.git`. Existing Step 51/52 documentation, attributes, scripts and scoped byte remediation were preserved. No reset, clean, commit, push, dependency upgrade or production code change occurred. Starting Git status is in ignored `artifacts/step53/starting-status.txt`.

Verified SDK 10.0.403, Node 24.16.0 and npm 11.13.0; existing .NET 10/runtime 10.0.14, net10.0/net10.0-windows, WPF/WebView2 and locked package configuration remain in use. Reviewed the project instructions, goals/roadmap/readme, accepted ADRs and requested compatibility/display/structural/Payables reports.

Before opening, checked absolute containment within the designated input directory, regular-file status, and reparse/link attributes for the file and every ancestor through the drive root. Checked the private output ancestors similarly. `git check-ignore` confirms directory, individual source and private output exclusion; `git ls-files` confirms no private inputs/outputs are tracked. Source size/hash were recorded privately. The engine opens with `FileAccess.Read` and `FileShare.Read`, excluding writers/deletion during each session. All sessions were disposed before final hashing; the initial, complete diagnostic and final audit fingerprints all match the original private baseline.

## Existing engine observations

| Package fact | Actual measurement |
| --- | --- |
| Source length | 4,093,983 bytes |
| Compressed / declared expanded part bytes | 3,996,403 / 27,888,296 |
| Package entries / relationships | 611 / 618 |
| Worksheets | 5 visible, 0 hidden/very-hidden tabs; many rows within tabs are hidden |
| Defined names | 11,075; 9,399 contain literal `#REF!`; 1,447 contain brackets consistent with external-looking references; 5 have local-sheet scope |
| External-link XML parts | 290, plus their 290 relationship parts |
| External relationships | 305: 286 externalLinkPath, 8 externalLinkLongPath, 11 xlPathMissing; targets never followed |
| Additional package features | Calculation chain; 3 comment parts; 3 VML drawing parts; 5 binary printer-setting parts |
| Images / embedded OLE parts | 0 / 0 observed in package inventory |
| Styles | 657 total `xf`, 632 cell `xf`; 6 explicit number-format definitions; styles XML 113,964 bytes |

The inspector's **607 preservation-sensitive/unknown relationship classifications** include ordinary but unqualified advanced relationships, including the calculation chain. They are not 607 malformed relationships. Metadata validation accepted the internal graph under the existing limits. The literal broken-name count is detectable evidence, not complete formula/name resolution. Existing defects were neither repaired nor accepted as harmless to a future insertion.

All five selected-sheet scans completed. Sheet numbers below are stable order labels within this assessment, not names:

| Sheet | Stored cells | Serialized nonempty values | Explicit styled-only empty cells | Empty cells without explicit style | Formula cells | Declared and observed stored extent |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 1, target | 87,291 | 10,221 | 27,456 | 49,614 | 3,379 | A1:XEX1608 |
| 2 | 87,256 | 10,137 | 27,567 | 49,552 | 3,397 | A1:XEX1596 |
| 3 | 88,274 | 10,214 | 28,516 | 49,544 | 3,441 | A1:XFD1605 |
| 4 | 88,234 | 10,217 | 28,466 | 49,551 | 3,471 | A1:XEX1619 |
| 5 | 88,266 | 10,284 | 44,766 | 33,216 | 3,495 | A1:XEX1634 |
| Total | **439,321** | **51,073** | **156,771** | **231,477** | **17,183** | All stored extents measured, not allocated as rectangles |

“Serialized nonempty” includes shared-string indices and formula caches; it does not prove the referenced text is nonempty or that a cached result is current. Styled-only here means an explicit cell `s` attribute, no stored nonempty value and no formula; it excludes inherited row/column formatting. The four categories sum to the stored count because no formula cell in these scans lacked a nonempty cache. No cache was recalculated or trusted as current.

Four sheets' nonempty serialized extents end at column T (20); sheet 3 contains nonempty serialized content at XFD (16,384). The target's nonempty extent is T1608, despite stored blank cells extending to XEX (16,378). This is measured sparse structure, not permission to trim tails. The engine's inflation heuristic flags four tabs and does not flag sheet 3; the latter is not proof of sensible worksheet layout.

Supplemental bounded feature inventory found 11,098 shared-formula cells with 820 nonempty anchors; no array/data-table formula metadata in these scans. **117 stored formula texts contain `#REF!`**, and 117 contain `!`; shared followers without stored expressions are not counted as complete expanded dependency analysis. No stored formula text contained `[`, but external names/link parts are still present. Formatting includes 61 merges, five AutoFilters, 25 conditional-formatting elements and substantial hidden rows. Target sheet 1 has 1,519 hidden row elements, seven merges and 11 conditional-formatting elements. The inspector returns ordinary stored rows, including hidden rows; it does not reproduce Excel's filter/merge/layout appearance.

## Exact admission and display blockers

The local runner invoked **only `PackageGuard.Inspect` with default limits**, not `WorkbookEngine.Open`, `Load`, ClosedXML construction or an editable session. Its first actual rejection is:

`LIMIT_EXCEEDED`: validation stops at **50,001 stored cell elements**, against the unchanged 50,000-cell workbook limit. Each individual sheet exceeds that limit; the full measured total is 439,321.

Independent inspection also establishes further restrictions that would remain if a resource limit were raised:

| Restriction | Exact current rule and observed relevance |
| --- | --- |
| External data | `PackageGuard` rejects part names containing `externallinks`/`connections`; external relationship modes/URL/file targets separately reject. The package has 290 external-link XML parts and 305 external targets. |
| Unqualified package parts | The editable guard accepts only `.xml`, `.rels`, `.psmdcp` suffixes for non-directory parts. Three `.vml` drawing files and five printer-setting `.bin` files are outside that envelope. VML is XML markup with an unsupported suffix, not proof of executable binary content. |
| Relationship fidelity | Printer settings, comments, VML and external-link relationships are outside the editable allowlist. Unknown relationships restrict snapshots; external targets reject admission. |
| Advanced structures | Calculation chain, shared formulas and conditional formatting produce preservation findings. Structural insertion additionally rejects all names, merges, filters/hidden dimensions and unsupported formulas. These features are present and relevant to row movement, formatting, comments/anchors, print context and references. |
| Names and formulas | Structural scope rejects all defined names, including broken names/print settings, and shared/cross-sheet/unsupported expressions. Thousands of names and existing broken references need dependency classification, not deletion or global reconstruction. |
| Profiles/layout | Current workflow analysis requires one complete unique header row, markers in the entity column, an unambiguous subtotal per section, complete bounded coverage and literal text identities. The actual layout violates several of these assumptions. |

File size, expanded bytes, entry count, tab count and total style count are **within** their respective editable resource limits. There is no dedicated “11,075 names is too many” editable count rule; names instead affect metadata budgets, dependency complexity and the structural rejection envelope. None of these facts waives the first cell-count failure.

Accounting interpretation has a distinct, verified failure. `InspectionSession.Format` requires explicit `numFmt` definitions to have IDs **>=164**. This workbook explicitly defines **ID 43**, alongside IDs 164–168. Its 47-character format definition fails that current admission condition, triggering the stylesheet-wide generic raw-value fallback. The stylesheet is within size/count/format-length limits, with no duplicate format IDs; the fallback is a limitation of the current parser, not a finding that the entire stylesheet is corrupt.

As a result, **all 23,452 nonempty numeric cells were reported as Unsupported/Number: zero supported numeric date/accounting interpretations in this run**. Raw values remain available. The six format codes and 193 nonzero `xfId` references also need conservative grammar/inheritance handling; accepting ID 43 alone would not prove native financial rendering. The Step 50 over-precision rejection remains untouched.

Formula viewing is partial: targeted probes returned three ordinary stored-formula statuses, five reconstructed shared formulas and ten explicitly unsupported shared-formula statuses. Counts describe that sample, not all 17,183 formulas. All caches retain an unverified status. The external subtotal's shared expression was reconstructed successfully through the normal viewport API.

## Real Payables layout feasibility

Target worksheet ordinal 1 matches the previously described selection, checked privately. The generic fields are identifiable, with repeated headers at rows **3 and 907**:

| Field | Column |
| --- | --- |
| Date received | B |
| Payee/vendor | C |
| Particulars | D |
| RFP identifier | E |
| Invoice identifier | F |
| Amount / other charges / VAT | G / H / I |
| Total amount / EWT / net amount | J / K / M |
| Payment mode / CV identifier | N / O |

Column L and other unrequested columns were not assigned invented business semantics. No record field was copied into a tracked fixture/profile.

Nine heading candidates were located in **column B**, while entity names occupy **column C**. The external heading is at row **765**. Its amount subtotal at **G900** is independently confirmed through a normal inspector viewport as `SUM(G766:G899)`, with shared-formula reconstruction and an unverified cached value. This confirms a useful external span, not an executable insertion plan. A future insertion before row 900 would need an explicit aggregation expansion plus all downstream summary/reference/anchor changes; merely shifting row addresses is insufficient.

Other totals include intermediate SUMs and summary dependencies. A simple “first matching contiguous SUM after heading” diagnostic selected an intermediate subtotal within a larger region and left 455 candidate records outside its provisional spans. That proposed partition is **rejected as incomplete**, not treated as established section boundaries. No full section tree or complete dependency graph was certified.

The private feasibility scan covered the selected sheet's stored XML and resolved the relevant text across the first 32 columns using existing 20x10 viewports. All nonempty serialized target-sheet content lies within those 32 columns. An independent ordinary-viewport audit then checked **all 1,608 rows** across the ten columns containing every identity and duplicate-detection field:

- **782 candidate records** have a literal text vendor and at least one literal nonblank RFP/invoice identifier, excluding headers/headings/summary markers.
- Those candidates contain **73 normalized vendor identities**, 46 repeated identities, and **one identity present in multiple provisional heading groups**. Names, punctuation and legal suffixes were not merged or guessed; no private alias was invented.
- **25 additional rows have identifiers but blank vendor cells.** No predecessor/vendor fill-down was inferred. These rows would violate the current literal-entity classification rule and prevent a complete vendor-match guarantee.
- **21 repeated same-vendor, same-field identifier groups** were observed among candidate records. These are potential review cases, not confirmed duplicate transactions; cross-field matching and the 25 unresolved identities were not declared fully analyzed.
- The independent identity audit found no formula-derived, truncated or overlength identities within the scanned role columns. It found one entity-only row, and no vendor-without-identifier row with literal date/amount context under the current context-field check.

These are conservative candidate/ambiguity counts, not an authoritative ledger reconstruction. No actual new vendor or payment request was supplied, so request-specific destination/duplicate planning remains unexecuted. The approved behavior remains: whole-selected-sheet search, after-last-match within one section, explicit choice across sections, absent-vendor placement before the configured external subtotal, and reviewer resolution of names/identifiers. The current inspection session cannot invoke production workflow planning, and no adapter or fake editable session was introduced to bypass that restriction.

The built-in synthetic profile cannot describe this workbook as supplied: its headers differ; headers repeat; section markers and vendors use different columns; complete stored extents exceed 32 columns; and ambiguous identifiers/section hierarchies are unresolved. Even a proposed profile fitting the data rectangle cannot silently discard the rest of the worksheet or resolve those ambiguities.

## Capability assessment

| Capability | Actual status |
| --- | --- |
| Metadata inspection | **Supported, tested** for this duplicate under existing bounds; not schema/preservation certification. |
| Read-only worksheet browsing | **Supported, tested** on all five tabs, first/distant/cached windows; merges, filters, hidden-row appearance, drawings and print layout are not rendered. |
| Raw values | **Supported, tested**; inspector retention limits still apply. |
| Interpreted accounting/date values | **Blocked for numeric cells in this run**, 0/23,452 supported because of stylesheet fallback. Raw values remain available. |
| Formula inspection | **Partial, tested**; ordinary/qualified shared expressions visible, other groups explicitly unsupported, caches unverified. |
| Section detection | **Partial**: fields/headings/external span found; complete hierarchical partition rejected/unproven. |
| Vendor matching | **Partial**: complete role-column scan measured candidate repetitions; 25 missing identities and uncertain boundaries block reliable production decisions. |
| Editable workbook | **Blocked**, actual guard failure at 50,001 cells, with further observed package restrictions. No editable session was created. |
| Working copies / ordinary editing | **Blocked** by inspection capability and editable/snapshot eligibility. No copy/edit attempt on private content. |
| Structural insertion | **Blocked** by production policy and observed structural envelope failures. |
| Structural Merge | **Blocked** in inspection and public review-only flow; no operation attempted. |
| Safe export | **Blocked for this workbook**; inspection has no export. Synthetic new-file commit tests do not qualify exporting this source. |

## Performance and resource evidence

The final five-sheet initial probe measured metadata **101.4921 ms**, first view **115.9527–165.9379 ms**, distant view **104.9994–156.6489 ms**, and repeated cached view **0.0090–0.3995 ms**. Total initial probe time was **1,551.2509 ms**; process-lifetime peak working set **66,076,672 bytes**, cumulative managed allocations **608,043,448 bytes**. Earlier cold probes are retained privately and were slower; these values are one observed run, not a latency guarantee.

The separate complete diagnostic pass took **43,867.8823 ms**, with **29,078,590,064 cumulative allocated bytes** and process-lifetime peak working set **96,931,840 bytes**. This allocation counter measures churn over hundreds of full-sheet rescans, not simultaneously retained memory. The target's text projection used 96 normal viewports in approximately **7,590.1165 ms**. The independent 81-viewport identity audit took **7,030.2769 ms**.

These measurements include a private diagnostic harness and some concurrent development verification. Initial browsing and bulk feasibility analysis are different workloads. Repeated ZIP scans have a measurable cost despite modest retained memory; recent-window cache speed does not establish a full-sheet search speedup. All existing 10-second per-operation, byte/node/cell budgets and eight-window/2 MiB accounted cache remained unchanged; no private disk index was created. No clean-machine/manual responsiveness or Excel-rendering benchmark is claimed.

## Engineering decision and Step 54

The workbook is not a resource-only case. Raising 50,000 cells would still leave links, names, shared formulas, VML/comments, printer settings, merges, filters, conditional formatting and ambiguous sections unsupported. Whole-workbook reconstruction, stripping links/names, deleting blank tails or reserializing through a broad library is not a safe workaround.

The smallest next milestone is **read-only compatibility and layout diagnostics**: independently test the observed low-ID format-definition category and preserve per-format ambiguity/over-precision rejection; show precise raw-value reasons; define explicit repeated-header/marker-column/hierarchical boundary rules; surface missing-vendor identities without automatic fill-down; prove complete role-column coverage and dependency reporting. This does not require raising editing limits or enabling Apply.

For an initial future posting deliverable, a **separate reviewed transaction/output schedule** has the smallest preservation burden because it leaves this complex package unchanged. It is a proposed workflow, not an enabled inspection export. If the original workbook must eventually receive rows, investigate **targeted OOXML changes inside an isolated, independently validated transaction** only after that read-only model is proven. Its proof must cover shared formulas, subtotal/summary intent, local/global names, existing broken references, print/comment/VML anchors, relationships/content types, untouched opaque bytes, external-link non-resolution, baseline conflicts and source immutability. The existing narrow synthetic structural oracle does not establish that proof for this package. See the [updated strategy](real-workbook-compatibility-strategy.md).

## Regression and preservation verification

| Check | Final result |
| --- | --- |
| Locked solution/evaluation/native/managed NuGet restores | Passed with unchanged locks, `--locked-mode --ignore-failed-sources` |
| .NET 10 Release solution build | Passed, zero warnings/errors |
| Complete solution suite | **359 passed / 0 failed / 0 skipped**: Application 47, Workbooks 299, Desktop 13 |
| Reference provenance/native comparisons | Existing reference tests passed in the full suite, no skips; no native Excel automation used |
| Existing checkout-portability/evidence safeguards | **5 passed** |
| Frontend typecheck/build/tests | Passed; **35 tests** |
| Toolchain / evaluation safety | **3 / 13 passed**, evaluation used synthetic inputs only |
| Native / managed commit harnesses | **600 / 390 passed**, zero failed |
| Self-contained win-x64 publish | Passed to `artifacts/step53-win-x64`, runtime 10.0.14 |
| WPF/WebView2 | All **13 Desktop tests** passed with actual bundled React/bridge and synthetic selector seams |
| Existing workbook/logo/lockfile fingerprints | **88 checked, zero changed** |
| Actual private source fingerprints | Starting/diagnostic/audit/final hashes match; hashes retained privately only |
| Private tracking/privacy | Source/report paths ignored and untracked; no business records, link targets or private hashes copied into tracked reports/fixtures or ordinary logs |

No new synthetic feature or production diagnostic code was added. The one-off ignored runner references the existing workbook project and reuses `WorkbookInspection`/`ReadViewport`. For additional counts it calls the inspector's existing bounded XML reader/budget/text/interpreter methods by reflection against its held read-only archive. It retains the same reader limits and explicitly bounds cells/metadata nodes. That is local qualification instrumentation, not a new public API, editable-session authorization or planner bypass. Guard-only inspection returns before any workbook model is constructed. Its temporary project/lock/output are ignored; production/test dependency graphs did not change.

During diagnostic development, the first header matcher missed the compound vendor label and a simple SUM heuristic was insufficient. Those provisional results were not accepted as final layout proof; the refined generic-field mapping and independent full-role scan exposed the 25 ambiguous rows. A diagnostic width-label error was corrected before the final audit. No regression test failure or skip remained. None of these diagnostic iterations altered the source.

The historical library research matrix remains **22 passes / 24 known failures / 26 unexecuted**, not rerun. Five evaluation-only ImageSharp advisories remain visible and unresolved (three high/two moderate). Manual native dialogs, clean-machine offline installation, full Excel appearance/calculation fidelity, complete dependency resolution and safe private-workbook mutation remain unexecuted/unproven.

Created this report; updated the compatibility strategy, analyzer, Payables workflow, PLANS and README. Detailed private qualification artifacts include `qualification-report.json`, source safety/final verification, metadata, scan/performance reports, names/relationships, style/formula probes and target identity audit. No source/test/fixture/branding/dependency file was changed in Step 53.

Production Payables Apply stays disabled; Step 45 approvals stay permanently review-only. Inspection copies/editing/Merge/export, Replace/Create New, source/existing-file overwrite, autosave, PDF OCR, cloud services and databases remain disabled. **Phase 2 remains incomplete.**
