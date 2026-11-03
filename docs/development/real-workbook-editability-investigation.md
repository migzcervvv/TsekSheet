# Real workbook editability investigation

Date: 2026-11-03, Asia/Shanghai. **Investigation only; implementation decision pending.**

OCR, worksheet-eligibility UI work and new editing features remain paused under
this request. No product code, business logic, admission gate, dependency manifest,
workbook file or UI was changed. Existing uncommitted work was preserved. Nothing
was committed, pushed or deployed.

## 1. Findings and recommendation

The reported Payables rejection is reproduced: **LIMIT_EXCEEDED at 50,001 matching
cell elements against 50,000**. This is a TsekSheet admission policy, not an Excel
file-format maximum. It is neither a demonstrated filesystem permission problem
nor proof that Excel cannot edit the workbook. The first rejection occurs inside
`xl/worksheets/sheet1.xml`, before ClosedXML constructs a workbook model.

The three supplied workbooks have different first blockers. All can be inspected;
none passes current editable admission. Raising the cell limit alone does not
make any of them eligible for the requested record insertion workflow.

A separate counting defect is confirmed: the guard counts **all spreadsheet-
namespace `<c>` elements across package XML**, including calculation-chain entries.
It does not restrict that counter to worksheet cells. The breakdown workbook has
49,834 actual worksheet cells but 55,152 matching elements once its 5,318
calculation-chain entries are included. Its actual first error is the independent
32-tab limit, not that cell counter. No counter or restriction was changed here.

**Recommended architecture to qualify: C combined with B.** Keep the bounded
read-only OOXML inspector; introduce, only after approval and a preservation spike,
an application-owned Excel-native editing adapter operating on an isolated **full
workbook** copy. Authorize a particular worksheet operation against exact source,
copy, destination and revision identities, with workbook-wide dependency and output
validation. Keep the existing narrow OOXML adapter and its gates intact. This is
an engineering recommendation, not evidence that Interop is already safe or an
approved implementation.

The confirmed requirement that every user has Microsoft Excel changes the adapter
tradeoff: implementing another general Excel formula/layout engine is less useful
than qualifying Excel's own behavior for these templates. Native Excel remains
subject to security, resource, cancellation, semantic-preservation and explicit-
save requirements.

## 2. Scope, inputs and evidence quality

The user explicitly selected these available local files in
`C:\Users\mcs\Downloads\`. Percent signs in the Payables filename are literal;
the filename was not URL-decoded.

| Input | Bytes | Worksheets | Current first editable rejection |
| --- | ---: | ---: | --- |
| `Copy of EXTI LIQUIDATION & REIMBURSEMENT.xlsx` | 43,021 | 2 | `UNSUPPORTED_FEATURE`: printer settings part |
| `EVOXTERRA%20PAYABLES%20UPDATED.xlsx` | 4,092,532 | 5 | `LIMIT_EXCEEDED`: 50,001 matching cell elements, limit 50,000 |
| `EXTI_LIQUIDATION_&_REIMBURSEMENT_BREAKDOWN_1.xlsx` | 406,291 | 47 | `LIMIT_EXCEEDED`: 33 worksheet declarations, limit 32 |

**Observed** below means measured on these supplied files. **Code-derived** means
an observed feature intersects an unchanged rule, without bypassing earlier gates
to execute the later operation. **Untested** means preservation or behavior has
not been demonstrated. These categories are not interchangeable.

### Read-only method and privacy

- An investigation-only standard-library XML census streamed ZIP entries. It used
  native read-only handles with writer/deletion exclusion, streamed SHA-256 before
  and after analysis, and reopened read-only to compare the final fingerprint.
  All three checks succeeded for every final scan. Sizes and last-write timestamps
  also remained unchanged. No original was opened in Excel, normalized, saved,
  repaired, copied into a new workbook or resaved.
- Limits were explicit: 128 MiB compressed; 512 MiB expanded/actual XML reads;
  4,096 entries after central-directory preflight; 64 MiB per XML part; 4 MiB
  metadata parts; 512 tabs; 16,384 relationships; 500,000 shared strings; 2,000,000
  cells per selected sheet; depth 64; 12,000,000 XML elements; 10 seconds per part;
  32,768 characters per transient cell field. DTDs/external entities, unsafe paths,
  reparse paths and excessive compression were rejected. There was no full
  workbook DOM, unbounded workbook load, external-link resolution or recalculation.
- Shared-string text was discarded after classifying empty/whitespace/nonempty and
  generic total markers. Formula text was examined transiently for structure and
  then discarded. Reports retain counts, ranges, standard function categories and
  requested worksheet names, not accounting values, raw formula/name expressions,
  comments, external targets, receipt contents or source hashes. Workbook text was
  treated as data, never as instructions.
- A separate helper loaded the **unmodified compiled product engine**, including
  its actual Windows Desktop framework and existing dependencies. It called
  `WorkbookEngine.Open` with default limits and `Inspect` separately. No limit was
  raised and no editing API was called. The normal inspector successfully scanned
  one bounded viewport on **all 54 sheets**. Each scan traverses its selected sheet;
  its complete stored-cell count independently matched the census on every sheet.
- Evidence and helpers are ignored local investigation artifacts under
  `artifacts/real-workbook-editability/`: `structure-results-qualified.json`,
  `engine-results-final.json`, the streaming census, its synthetic in-memory
  verification and the standalone engine probe. They are not product code or new
  backend dependencies. The requested Markdown report is the sole deliverable.

The current Payables input is not assumed byte-identical to the historical Step 53
duplicate: this scan measures **439,430 worksheet cells**, whereas the older report
measured 439,321. Current counts and rejection results below supersede historical
figures for these particular supplied files.

## 3. Existing admission implementation

### Definition, enforcement and counting boundary

[`WorkbookLimits.cs`](../../src/TsekSheet.Workbooks/WorkbookLimits.cs), line 9,
sets `MaxCells = 50000`. [`PackageGuard.cs`](../../src/TsekSheet.Workbooks/PackageGuard.cs),
lines 30–31 and 87–88, creates one `cells` counter outside the ZIP-entry loop and
increments it for every XML element satisfying:

```csharp
element == "c" && reader.NamespaceURI ==
    "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
```

It throws when `++cells > limits.MaxCells`, so the rejection reports **50,001**,
not the full count. The check is independent of the selected worksheet, viewport,
worksheet visibility, row visibility, data type, value presence and style.

For worksheet XML this includes explicit populated cells, numeric zero, text,
error/boolean values, formula masters/followers, formulas without caches, styled
blanks and unstyled blank `<c/>` records. A hidden row or hidden tab changes none
of those counts. Empty shared-string references still contain stored cells.
Formatting attached only to a row or column creates no implicit cells for this
counter. A large `<dimension>` range or a merge range also does not allocate/count
every coordinate inside it; only serialized matching elements count.

**Implementation precision defect:** the condition does not check the owning
part/parent. A calculation-chain `<c>` uses the same namespace and is also counted.
It represents a calculation dependency entry, not a second worksheet cell.

| Input | Actual worksheet `<c>` elements | Calculation-chain `<c>` entries | Full matching-element counter, if scanning reached the end |
| --- | ---: | ---: | ---: |
| Reimbursement copy | 2,335 | 150 | 2,485 |
| Payables | 439,430 | 17,183 | 456,613 |
| Breakdown | 49,834 | 5,318 | 55,152 |

In Payables the first sheet alone contains 87,400 cells; the actual 50,001st match
is in that sheet before any calculation-chain contribution. The counting defect
therefore **does not explain away its observed failure**. In Breakdown, if earlier
rejections did not stop scanning, its 167th calculation-chain entry would push the
counter from 49,834 to 50,001. That conditional observation is not a replay with
the sheet-count or printer gates disabled.

### Why 50,000 exists, and what is not established

The foundation documentation calls the limits **conservative admission limits**
and explicitly says they are not a hard CLR heap ceiling and need further
large-workbook measurement. The earliest available file history already contains
50,000, along with the 256 MiB estimated budget; no inspected code, test or decision
record derives that exact value from an accounting-template benchmark.

The rationale for bounding a full in-memory model and independent candidates is
supported; the exact numeric threshold is **not an empirically proven maximum**.

- [`WorkbookEngine.cs`](../../src/TsekSheet.Workbooks/WorkbookEngine.cs), line 102,
  estimates each model as `3 × compressed bytes + 4 × expanded bytes +
  2,048 × counted elements + 4 MiB`. Reservations share a 256 MiB engine budget,
  with at most four sessions/snapshots. Candidate preparation and another complete
  model require additional concurrent reservations.
- At 50,000 counted elements, the cell term alone is about **97.66 MiB per model**;
  source, preparation reservation and clone can coexist. A cell threshold by
  itself is consequently not a promise that a maximum-sized workbook can be
  copied or edited under the aggregate memory budget.
- The [historical performance samples](../research/evidence/performance-samples.json)
  tested a synthetic workbook with 10,000 four-column main-sheet data rows, small
  context sheets and the library's load/save/reopen path. ClosedXML's sample took
  2,439 ms, allocated 254,539,432 bytes in total and ended with a 129,286,144-byte
  working set. Those figures are **not peak live memory**, a 50,000-cell boundary
  qualification, a realistic accounting-template benchmark or repeated insertion/
  copy evidence. See the [evaluation caveats](../research/workbook-library-evaluation.md).
- `WorkbookEngineTests.FilePackageAndSessionLimitsAreEnforcedBeforeLoading`
  verifies an injected `MaxCells=1` rejection. The newer synthetic opening tests
  exercise 50,001 cells and inspection fallback. Neither justifies 50,000 as the
  optimum threshold or tests general preservation above it. No existing test was
  weakened or modified in this investigation.

### First-failure behavior and independent safeguards

Admission processes ZIP entries in archive order and throws immediately at a hard
rejection. An early cell error conceals later feature checks, final unknown-part
classification, SDK validation and model loading. There is no complete editable-
compatibility report behind that first error. Independent inspection can inventory
more features, but its categories are not preservation certification.

The unchanged safeguards include:

| Boundary | Independent requirements |
| --- | --- |
| Input/security | Local `.xlsx`; network/reparse path restrictions; read-only source access; duplicate/unsafe ZIP path and relationship rejection; no external resolution; DTD prohibition; bounded decompression/XML/depth |
| Full-model resources | 16 MiB file; 64 MiB expanded; 1,024 entries; 32 `sheet` declarations; 4,096 `xf` elements; 500,000 XML elements; aggregate estimated memory and session limits |
| Preservation admission | Reject external links/connections, external relationships, protection, macros/active content, signatures, non-XML binary/VML/media/embedded parts and unsupported tab types; validate through Open XML SDK before ClosedXML |
| Edit/copy eligibility | Advanced/unknown findings or truncated projections disable editing; calculation chains and shared/array formulas are advanced content; snapshots/copies require verified context and identity preservation |
| Ordinary cell edits | Existing populated cells only; selected-copy sheet restriction; no merged-cell edits; explicit formula handling; revision/change-count checks; candidate-only transformation and unchanged-part/structure validation |
| Structural insertion | Production path rejects with `ROW_INSERTION_DISABLED`; experimental flags do not become user permissions. The independent oracle has a much narrower envelope, described below |
| Application/session | Inspection cannot create working copies or map/plan receipt placement; dirty-state, session ownership, expected revision and review/source authority checks remain separate |
| Output | Inspection export denied; qualified narrow Save As is explicit, new-file-only, main-session-only, candidate-reopened and native-commit checked. No source/existing-file overwrite, autosave or automatic integration |

The [structural oracle](structural-preservation-oracle.md) additionally limits
packages to 8 MiB/16 MiB expanded/128 entries; rows to 2,000 and columns to 32;
insertions to 1–20 records at one boundary; formulas to its exact short same-sheet
A1/addition/SUM grammar. It rejects all defined names, merges, tables, filters,
hidden/grouped dimensions, shared/array and cross-sheet formulas, formulas outside
the selected sheet, comments/drawings/unknown parts and formula-bearing record
templates. A complete section SUM and uniform approved template are required.
None of the supplied files has been shown to satisfy this insertion contract.

## 4. Actual workbook characteristics

### Count definitions

“Populated/formula” below means an explicit cell with a non-whitespace value after
shared-string resolution, **or any formula**, including an expressionless shared
follower. It includes headers, errors and cached values; it is not a verified count
of accounting records or business-relevant entries. Formula counts overlap this
column. “Blank styled” means no such content/formula but an explicit `s` attribute;
“blank unstyled” means neither content/formula nor `s`. Style zero still counts as
an explicit style. Inherited row/column styles are not cell `s` attributes.

For every sheet: `stored = populated/formula + blank styled + blank unstyled`.
All declared dimensions matched the bounds of explicit stored cells. These are
serialized ranges, **not native Excel `UsedRange` measurements**. Populated bounds
can include isolated distant data/error cells; they do not authorize trimming.

**All 54 worksheet tabs are visible.** Hidden rows/columns within visible tabs are
reported separately; there are no hidden or very-hidden tabs in these inputs.

### 4.1 Reimbursement copy

| Worksheet | Stored | Populated/formula | Blank styled | Blank unstyled | Formula cells (shared / array) | Declared and stored bounds | Populated bounds |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| VENDOR | 829 | 829 | 0 | 0 | 0 (0 / 0) | A1:D276 | A1:D276 |
| MARIA | 1,506 | 370 | 1,136 | 0 | 150 (0 / 0) | A1:AH72 | A1:AH67 |

Both sheets fit the existing cell and tab thresholds. The workbook has 16 package
entries, 195,383 expanded bytes and 79 `xf` elements. Its observed first rejection
is one of **two binary printer-setting parts**. It also contains a calculation
chain with 150 entries and two local built-in filter defined names. No external
relationships/link parts, macros, embedded objects, merges, conditional formatting
or protected-sheet markup were observed in the inventory.

`MARIA` has 150 formula cells with stored caches. Standard function observations
include SUM and ROUND; 80 formula cells fail even the existing structural oracle's
basic formula-text/attribute grammar. Its 34-column stored range exceeds that
oracle's 32-column bound. It has four hidden-column and ten outlined-column range
declarations, a filter over `B2:AF50`, and explicit row-height content. `VENDOR`
has a filter over `A1:C276`. There are no hidden row elements in this workbook.

**Code-derived additional blockers:** calculation chain → `ADVANCED_CONTENT` and
denied copies/edits/export if model loading were reached; filter names, AutoFilters,
grouped/hidden columns and incompatible formula forms → structural rejection.
Printer-setting preservation, a no-op native save and row insertion are untested.

### 4.2 Payables

| Worksheet | Stored | Populated/formula | Blank styled | Blank unstyled | Formula cells (shared / array) | Declared and stored bounds | Populated bounds |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| Unpaid RFPs - SEP | 87,400 | 10,160 | 27,681 | 49,559 | 3,379 (2,134 / 0) | A1:XEX1603 | A1:T908 |
| Unpaid RFPs - AUG | 87,256 | 10,105 | 27,588 | 49,563 | 3,397 (2,181 / 0) | A1:XEX1596 | A1:T901 |
| Unpaid RFPs - JUL | 88,274 | 10,183 | 28,537 | 49,554 | 3,441 (2,208 / 0) | A1:XFD1605 | A1:XFD913 |
| Unpaid RFPs - JUN | 88,234 | 10,190 | 28,485 | 49,559 | 3,471 (2,265 / 0) | A1:XEX1619 | A1:T929 |
| Unpaid RFPs - MAY | 88,266 | 10,268 | 44,781 | 33,217 | 3,495 (2,206 / 0) | A1:XEX1634 | A1:T1584 |

Totals: **439,430 stored; 50,906 populated/formula; 157,072 blank styled;
231,452 blank unstyled**. Every individual sheet exceeds 50,000 stored cells.
Even the whole workbook's populated/formula count exceeds 50,000; dropping blank
cells would neither be an authorized preservation strategy nor a complete fix.

Package observations: **611 entries; 27,883,830 expanded bytes; 1,130,294 XML/VML
elements in the full streaming audit; 647 `xf` elements; 25 differential formats**.
The file, entry, expanded-byte and style totals fit those particular editable
limits. The XML-node total exceeds the 500,000-element budget; the few VML elements
do not account for that excess. Full default editable validation never reaches
the end of this package.

Dependencies and advanced content observed:

- 290 external-link XML parts and 290 workbook external-reference declarations;
  **305 external relationships**: 286 `externalLinkPath`, eight
  `externalLinkLongPath`, eleven `xlPathMissing`. No target was followed.
- **11,075 defined names**: 11,070 global, five local. Of these, 9,399 expressions
  contain literal `#REF!`; 1,447 contain bracket notation consistent with external
  references. These are lexical observations, not a complete reference-resolution
  or repair assessment. Print-area/title/filter names are also present.
- 17,183 formula cells, all with stored caches; **10,994 shared-formula cells**,
  including 10,208 without their own expression text. There are 117 serialized
  formula expressions with literal `#REF!` and 193 cells of stored error type.
  Shared followers were not expanded for this error census. Cached values were
  neither recalculated nor certified current.
- 61 merged ranges; 25 conditional-formatting elements; five AutoFilters;
  three comment parts; three VML drawings; five binary printer-setting parts;
  a 17,183-entry calculation chain. No conventional VBA, ActiveX, embedded object,
  image, pivot, table, connection/query-table or signature parts were observed.
- 16,468 formula cells fail even the narrow oracle's initial text/attribute
  grammar. SUM occurs extensively; SUBTOTAL and ROUND also occur. A SUM token or
  generic total label is not proof of a valid accounting insertion boundary.

Hidden-row structures are explicit, not inferred from tab visibility:

| Worksheet | Hidden rows | Hidden row intervals |
| --- | ---: | --- |
| Unpaid RFPs - SEP | 1,516 | 4–449; 503; 506–655; 669–1587 |
| Unpaid RFPs - AUG | 0 | None serialized |
| Unpaid RFPs - JUL | 1,558 | 4–720; 752–1592 |
| Unpaid RFPs - JUN | 1,595 | 4–901; 912–1608 |
| Unpaid RFPs - MAY | 1,618 | 4–728; 730–746; 748–764; 766–1624 |

Per-sheet shared-formula counts are 2,134 / 2,181 / 2,208 / 2,265 / 2,206 in the
tab order above. Merge counts are 7 / 7 / 7 / 20 / 20. The July populated range
actually reaches **XFD**; the other wide serialized tails also contain explicit
cells. These facts rule out assuming that every far-right coordinate is safely
discardable formatting.

**Confirmed policy blockers beyond the first error:** external-link parts would
trigger `UNSUPPORTED_FEATURE`; external relationship targets would trigger
`EXTERNAL_RESOURCE` where reached; VML/printer binary parts would trigger
`UNSUPPORTED_FEATURE`; calculation chain/shared formulas/conditional formatting
would produce `ADVANCED_CONTENT`; comments/VML/printer relationships are outside
the narrow preservation allowlist. Names, merges, filters and hidden layout also
violate the structural envelope. These are code-derived consequences of measured
features, not successful attempts to run past the initial cell rejection.

As a separate **hypothetical current-policy calculation**, scanning through all
matching elements would produce a model reservation estimate of **1,063,150,644
bytes (1,013.90 MiB)**, versus 256 MiB available, before another candidate/copy.
Correcting the calculation-chain overcount alone would still estimate roughly
980 MiB. This is an estimate from the unchanged formula, **not measured actual
heap usage**, and it is never reached in the default failing open.

### 4.3 Liquidation/reimbursement breakdown

| Worksheet | Stored | Populated/formula | Blank styled | Blank unstyled | Formula cells (shared / array) | Declared and stored bounds | Populated bounds |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| TEMP | 2,358 | 308 | 2,050 | 0 | 211 (127 / 27) | A1:AK68 | A1:AK68 |
| VENDOR | 829 | 829 | 0 | 0 | 0 (0 / 0) | A1:D276 | A1:D276 |
| MARIA | 1,506 | 370 | 1,136 | 0 | 150 (124 / 0) | A1:AH72 | A1:AH67 |
| VIV | 2,358 | 524 | 1,834 | 0 | 233 (192 / 0) | A1:AH100 | A1:AH95 |
| VN#10396 | 1,365 | 305 | 1,060 | 0 | 134 (98 / 1) | A1:AH68 | A1:AH63 |
| VN#10382 | 1,957 | 421 | 1,536 | 0 | 177 (136 / 3) | A1:AH87 | A1:AH82 |
| VN#9.2 | 690 | 207 | 483 | 0 | 98 (67 / 7) | A1:AH23 | A1:AH23 |
| VN# | 661 | 168 | 493 | 0 | 90 (53 / 5) | A1:AH22 | A1:AH22 |
| VN#10088 | 1,490 | 340 | 1,150 | 0 | 172 (111 / 23) | A1:AH72 | A1:AH67 |
| VN#100219 | 843 | 182 | 661 | 0 | 81 (48 / 2) | A1:AH51 | A1:AH46 |
| VN#9953 | 783 | 141 | 642 | 0 | 74 (44 / 1) | A1:AH49 | A1:AH44 |
| VN#9934 | 1,772 | 355 | 1,417 | 0 | 149 (117 / 9) | A1:AK53 | A1:AI53 |
| VN#9712 | 2,702 | 529 | 2,173 | 0 | 227 (126 / 26) | A1:AK79 | A1:AI79 |
| VN#9867 | 661 | 162 | 499 | 0 | 90 (53 / 5) | A1:AH22 | A1:AH22 |
| VN#9866 | 661 | 168 | 493 | 0 | 90 (53 / 5) | A1:AH22 | A1:AH22 |
| VN#9845 | 661 | 163 | 498 | 0 | 90 (53 / 5) | A1:AH22 | A1:AH22 |
| VN#9707 | 347 | 98 | 249 | 0 | 45 (0 / 3) | A1:T23 | A1:R23 |
| VN#9627 | 348 | 102 | 246 | 0 | 46 (0 / 4) | A1:T23 | A1:T23 |
| VN#9661 | 948 | 245 | 703 | 0 | 111 (71 / 9) | A1:AH32 | A1:AH32 |
| VN#9651 | 1,443 | 318 | 1,125 | 0 | 139 (69 / 13) | A1:AH47 | A1:AH47 |
| VN#9020 | 850 | 214 | 636 | 0 | 121 (81 / 0) | A1:AE31 | A1:AE31 |
| VN#9630 | 674 | 173 | 501 | 0 | 94 (71 / 6) | A1:AH23 | A1:AH23 |
| VN#9623 | 872 | 219 | 653 | 0 | 115 (71 / 11) | A1:AH29 | A1:AH29 |
| VN#9405 | 653 | 178 | 475 | 0 | 94 (74 / 7) | A1:AE31 | A1:AE30 |
| VN#9431 | 2,261 | 405 | 1,856 | 0 | 172 (128 / 19) | A1:AE78 | A1:AE78 |
| VN#9448; VN#9447 | 706 | 161 | 545 | 0 | 84 (74 / 1) | A1:AE26 | A1:AE26 |
| VN#9208 | 2,163 | 419 | 1,744 | 0 | 182 (123 / 21) | A1:AD79 | A1:AD79 |
| VN#9427 | 765 | 131 | 634 | 0 | 70 (49 / 1) | A1:AE28 | A1:AE28 |
| VN#9426 | 852 | 171 | 681 | 0 | 74 (50 / 2) | A1:AE31 | A1:AE31 |
| VN#9326 | 584 | 157 | 427 | 0 | 85 (59 / 3) | A1:AF21 | A1:AF21 |
| VN#9325 | 650 | 180 | 470 | 0 | 95 (72 / 7) | A1:AE24 | A1:AE24 |
| VN#9306 | 570 | 153 | 417 | 0 | 86 (70 / 7) | A1:AD22 | A1:AD22 |
| VN#8942 | 2,098 | 416 | 1,682 | 0 | 193 (122 / 0) | A1:AE74 | A1:AE74 |
| VN#9154 | 735 | 138 | 597 | 0 | 78 (60 / 0) | A1:AE27 | A1:AE27 |
| VN#9008 | 613 | 164 | 449 | 0 | 92 (65 / 0) | A1:AF22 | A1:AF22 |
| VN#8974 | 590 | 153 | 437 | 0 | 85 (71 / 0) | A1:AE22 | A1:AE22 |
| VN#8972 | 590 | 159 | 431 | 0 | 94 (65 / 0) | A1:AE22 | A1:AE22 |
| VN#8962 | 735 | 157 | 578 | 0 | 97 (74 / 0) | A1:AE27 | A1:AE27 |
| VN#8964 | 899 | 249 | 650 | 0 | 141 (89 / 0) | A1:AC35 | A1:AC35 |
| VN#8911 | 735 | 134 | 601 | 0 | 77 (61 / 0) | A1:AE27 | A1:AE27 |
| VN#8886 | 1,227 | 313 | 914 | 0 | 155 (85 / 0) | A1:AE44 | A1:AE44 |
| VN#8884 | 735 | 197 | 538 | 0 | 113 (74 / 0) | A1:AE27 | A1:AE27 |
| VN#8878; VN#8876 | 1,345 | 247 | 1,098 | 0 | 112 (101 / 0) | A1:AG45 | A1:AG45 |
| VN#8776 | 1,605 | 308 | 1,297 | 0 | 152 (108 / 0) | A1:AE57 | A1:AE57 |
| VN#8764 | 648 | 146 | 502 | 0 | 86 (72 / 0) | A1:AE24 | A1:AE24 |
| VN#8760 | 648 | 136 | 512 | 0 | 78 (64 / 0) | A1:AE24 | A1:AE24 |
| VN#8721; VN#8720; VN#8719 | 648 | 148 | 500 | 0 | 86 (72 / 0) | A1:AE24 | A1:AE24 |

Totals: **47 visible tabs; 49,834 stored cells; 11,561 populated/formula cells;
38,273 blank styled cells; zero blank unstyled cells**. Every individual sheet is
small; the largest has 2,702 stored cells. This is strong evidence that a global
tab/cell threshold is not a good standalone measure of a selected operation's cost.
It does not establish that the complete workbook can safely be rewritten.

Package observations: 153 entries; 1,970,064 expanded bytes; 81,008 XML elements;
130 `xf` elements; **47 binary printer-setting parts**, a calculation chain and
an Excel metadata part. There are eleven local built-in filter names and no
external relationships or external-link parts observed. No merges, conditional
formatting, table parts, protection, VBA/ActiveX, embedded objects, images or
signatures were observed by the package/worksheet inventory.

There are **5,318 formula cells**, all with stored caches: **3,647 shared** and
**233 array-formula cells**; 3,399 formula cells have no independent expression
text. Stored error-type cells total 38, and 32 serialized formula expressions
contain literal `#REF!`. Known direct worksheet references to `VENDOR` appear in
433 ordinary/master expression texts; this is an incomplete dependency census
because shared followers and named/dynamic references were not resolved. The
workbook therefore cannot safely be reduced to independently detached tabs.

`VENDOR` has 273 hidden rows: 2–224, 226–263 and 265–276. Across the workbook there
are 313 hidden-column range declarations and 213 outlined-column declarations;
these are declarations, not unique physical-column counts. All other sheet row
elements are visible. Basic cell styles, SUM/ROUND/VLOOKUP forms, array metadata
and hidden/grouped layout need preservation beyond simple literal-cell editing.
4,970 formula cells fail even the narrow oracle's basic grammar/attribute check.

**First blocker:** the 33rd tab declaration. **Additional code-derived blockers:**
printer binary parts; calculation chain/shared/array formulas; unknown metadata/
printer relationships; global structural name/filter/hidden-layout restrictions;
formulas in nonselected sheets; and the structural 128-entry limit (153 observed).
The calculation-chain counter defect described above is another resource-policy
issue that would remain if earlier rejection conditions were addressed. None was
waived to test later admission stages.

## 5. Intended destination compatibility and unresolved questions of fact

The request does not identify a destination tab, subtotal row, record template or
proposed row count. No workbook's active tab is promoted into an authorization
decision. Consequently, **no exact insertion operation can be certified here**.
The per-sheet census supplies facts for choosing and qualifying one later.

| Possible target family | What is established | Current status |
| --- | --- | --- |
| Any Payables tab | Each individually exceeds 50,000 cells; shared formulas, merges and workbook-wide external/name/opaque dependencies exist | Inspection only; raising just the global cell limit or selecting one tab is insufficient |
| `MARIA` in the small reimbursement copy | Only 1,506 cells; no external links observed; formulas/filters/hidden grouped columns, printer settings and calcChain remain | Good smaller preservation-spike candidate, not currently editable or insertion-qualified |
| `TEMP`, `MARIA`, `VIV` or a particular `VN#…` tab in Breakdown | Small individual tabs, but 47-tab context, shared/array formulas, hidden/grouped layout and `VENDOR` dependencies remain | Operation qualification could reduce work; full-context preservation still mandatory |
| `VENDOR` lookup tabs | Reference-table structure; in Breakdown most rows are hidden and other tabs reference it | Context to preserve; no basis for assuming it is the intended receipt destination |

An insertion assessment must establish the selected section's exact record bounds,
template row, subtotal identity, enclosing and downstream totals, column meanings,
duplicate/verification rules and reference impacts. Generic TOTAL labels and SUM
locations were counted without disclosing contents, but no complete business
partition, aggregation intent or formula correctness was inferred.

Unproven: full SDK schema validity after all original gates; ClosedXML load/clone/
round-trip preservation; native Excel no-op save behavior for these exact inputs;
native record insertion and subtotal expansion; calculation results/cache freshness;
resolution of every defined/external/dynamic reference; drawing/comment/print
anchor movement; arbitrary malformed-content safety; reliable COM cancellation;
supported Excel-version/locale coverage; and accounting-owner acceptance.

Existing `#REF!` observations are baseline defects to preserve and distinguish
from new damage. They are not permission to repair formulas, delete names or
silently simplify the workbook.

## 6. Editing approaches

### A. Raise or redesign resource admission in the existing adapter

**Potential use:** measured enlargement for genuinely simple, larger workbooks;
correctly distinguish worksheet cells, calcChain entries, populated cells, styled
blanks, XML work and retained model/candidate memory. The counter precision defect
deserves its own planned change and synthetic controls, rather than being hidden
inside a numerical increase.

**Limits and risks:** Payables also exceeds the XML-element and estimated-memory
policies, and every supplied workbook contains independently blocked preservation
features. The full-context copy/edit path still parses and validates the entire
workbook. A higher threshold does not make the serializer preserve links, names,
shared formulas, anchors, formatting or print context. Removing blank cells to fit
a quota could remove intentional formatting/layout and is not acceptable.

Required evidence: cold/warm load, complete context copies, cell candidates,
repeated insertion, validation and output reopening at realistic bounds; peak
private bytes/working set, allocations, latency and cancellation under simultaneous
reservations; unchanged-source checks; independent package/semantic oracles for
every newly permitted feature. Adjusting only `MaxCells` is **not recommended**
for these templates.

### B. Qualify individual worksheet operations with whole-workbook safeguards

**Potential use:** represent permissions as an operation bound to source hash,
workbook/copy revision, destination worksheet and insertion boundary. Distinguish
inspection, existing-cell edit, row insertion, integration and output eligibility.
The 47-tab breakdown shows why selected-operation budgeting can be practical.

**Limits and risks:** selecting a small tab does not isolate global names, external
links, formulas on other sheets, shared styles/strings, print areas or anchors.
A partial library model that drops context is unsafe. An independent worksheet
copy/import can also change identity or turn references into links. Full-package
baseline retention and dependency classification must remain mandatory.

Required evidence: complete affected/unaffected dependency classification, shared/
array/table/name semantics, off-sheet inbound references and static/dynamic
unknowns, package retention, revision binding and serialization checks. Unresolved
dependencies must deny the operation. **Recommended as the permission model**, not
as a way to bypass the current full-model guard.

### C. Excel Interop adapter on isolated full-workbook copies

**Potential use:** native Excel is the closest engine to the user's actual edit
workflow and avoids reimplementing general reference, style and layout semantics.
The user confirms Excel is required. A read-only registry check on this host also
reports x64 Office, version **16.0.20326.20232**, `O365BusinessRetail`; this is not
proof of COM startup, activation or native edit correctness. Excel was not launched.

Interop is **not automatically preservation-safe**. The following design constraints
are proposed for a qualification spike:

1. **Copy before Excel opens anything.** Capture an immutable, fingerprinted source
   baseline through the bounded native boundary, then create an application-owned
   byte-identical full-workbook working file. Excel opens only that isolated copy,
   retaining original sheet identities and complete context. Do not detach one
   worksheet as the initial working-copy mechanism. Microsoft's
   [Worksheet.Copy documentation](https://learn.microsoft.com/en-us/office/vba/api/excel.worksheet.copy)
   describes creation of a new workbook and retention of worksheet code; it is not
   a guarantee of unchanged workbook-wide dependency semantics.
2. **Keep COM outside WPF/business logic.** Use an adapter behind application-owned
   contracts and a supervised desktop worker with its own serialized STA/message
   pump. Establish ownership of the exact Excel instance/PID before use. Do not
   attach to the user's active Excel instance, operate on their open documents or
   kill arbitrary Excel processes. Do not assume COM activation always yields a
   safely isolated process. Microsoft documents STA/global-resource, dialog and
   stability concerns for Office automation; this proposal is a local interactive
   desktop workflow, not a server/service or unattended Office deployment.
   [Microsoft automation considerations](https://learn.microsoft.com/en-us/office/client-developer/integration/considerations-unattended-automation-office-microsoft-365-for-unattended-rpa).
3. **Qualify security before opening.** Programmatic opening can enable macros by
   default. Apply `AutomationSecurity=ForceDisable` immediately around open and
   disable workbook events; retain independent preflight denial for XLM/macros,
   ActiveX, embedded/unknown active content and other unqualified features. The
   AutomationSecurity setting explicitly does **not** disable Excel 4.0 macros;
   `DisplayAlerts=false` is not a macro-security policy. Preserve protection and
   downloaded-file security metadata; do not remove restrictions or change global
   Trust Center/registry settings to make automation work.
   [Workbooks.Open](https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open),
   [AutomationSecurity](https://learn.microsoft.com/en-us/office/vba/api/excel.application.automationsecurity).
4. **Treat links and calculation as explicit policies.** `UpdateLinks=0` prevents
   normal workbook-reference updates at open; it is not proof of complete network
   isolation for arbitrary content, connections, DDE, add-ins or future calculation.
   Inventory link/connection types, never call refresh implicitly, reject unqualified
   active sources, and verify offline/no-target-access behavior. Query-table refresh
   has its own documented behavior. Establish manual-calculation/cached-result
   handling, then separately qualify explicit local calculation; do not assume
   inserting a row or native saving leaves all cached results unchanged.
   [Open link policy](https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open),
   [query refresh](https://learn.microsoft.com/en-us/office/vba/api/excel.querytable.refreshonfileopen),
   [calculation mode](https://learn.microsoft.com/en-us/office/vba/api/excel.application.calculation).
5. **Use native full-row insertion, but verify accounting intent independently.**
   Bind the operation to an approved subtotal and record template; specify the
   inserted row range and formatting origin. `Range.Insert` exposes shifting and
   `CopyOrigin`, but that API documentation does not establish that every subtotal,
   nested total, array/spill, name, merge, comment anchor or print boundary is
   correct for this template. Verify inclusion of each new amount, downstream
   references, row height, styles, hidden/grouped layout, validations and unrelated
   records. Copy only approved formula/style patterns and new literals; do not copy
   old record identifiers/statuses. Do not manually rewrite unsupported formulas
   to emulate Excel. [Range.Insert](https://learn.microsoft.com/en-us/office/vba/api/excel.range.insert).
6. **Define cancellation honestly.** A managed cancellation token cannot be assumed
   to interrupt a synchronous blocked COM call. Use bounded commands, timeouts,
   worker supervision, version checks and discardable isolated copies. Stop between
   operations where safe; an in-flight timeout must invalidate/retain the candidate
   as an unknown outcome, without automatic mutation replay. Forced termination
   requires proven exclusive process ownership and prior testing. Excel's
   [EnableCancelKey](https://learn.microsoft.com/en-us/office/vba/api/excel.application.enablecancelkey)
   concerns user/VBA interrupts; it is not a general .NET cancellation guarantee.
7. **Separate candidate serialization from authorized output.** Excel saves only to
   application-owned staging/copy paths. Reopen and independently validate before
   adoption. A later explicit output selection authorizes only a separate new
   destination, committed through a no-replace boundary. No Excel save API receives
   the original path or an unreserved user output path. SaveCopyAs does not change
   the open workbook in memory, but still serializes a candidate; it is not the
   initial byte-copy operation. Suppressing alerts can make SaveAs overwrite an
   existing destination, so Excel's prompt behavior cannot enforce TsekSheet's
   no-overwrite policy. [SaveCopyAs](https://learn.microsoft.com/en-us/office/vba/api/excel.workbook.savecopyas),
   [SaveAs](https://learn.microsoft.com/en-us/office/vba/api/excel.workbook.saveas),
   [DisplayAlerts overwrite behavior](https://learn.microsoft.com/en-us/office/vba/api/excel.application.displayalerts).

Native saving can normalize XML, calculation chains, caches, metadata or layout.
Existing [native formatting evidence](step45-verification.md) already shows no-op
Excel saves changing certain serialized row heights on synthetic fixtures. The
current exact-unaffected-part oracle must **not** be loosened globally to admit
native output. A separate adapter-specific preservation contract must distinguish
documented benign serialization changes from business/content/layout damage, with
independent expected results. Broken names/references must not be silently repaired,
and Excel successfully reopening a file is not sufficient acceptance.

Compared with A, C has a better functional fit for these existing templates, but
adds Office servicing/version/locale, activation, modal-dialog, add-in/security,
COM lifetime, crash recovery and test-environment risks. Neither the installed
Excel requirement nor a successful native insertion on one synthetic sheet would
qualify arbitrary workbooks.

## 7. Phased validation and implementation plan — proposed, not executed

| Phase | Bounded work | Required exit evidence |
| --- | --- | --- |
| 0. Review decision and operation contract | Select A/B/C direction; identify one exact destination, subtotal, template row, insertion size and allowed output behavior; retain current product gates | Reviewed scope; no implied write authority from inspection or registry discovery |
| 1. Synthetic baseline preservation | Build artificial fixtures reproducing calcChain accounting, 47-tab context, styled/unstyled tails, external names/links, broken refs, shared/array formulas, filters, hidden/grouped layout, comments/VML and printer settings. Exercise an adapter-specific native no-op save on isolated copies | Original hashes unchanged; independent semantic/package baseline; explicit classification of native normalization and unsupported features; no hidden repairs |
| 2. Secure isolated Excel worker spike | Qualify owned Excel instance, STA lifecycle, x64/version policy, macro/XLM/active-content denial, no link refresh/network access, modal dialogs, busy COM calls, timeout/cancellation/crash cleanup | No original write/link resolution; no interaction with user Excel; bounded resources; truthful unknown outcomes; zero blind mutation retries |
| 3. One operation before one subtotal | Start with the smaller reimbursement context if its actual target is approved. Insert one synthetic reviewed record, then a batch and repeated operations; preserve all sheets | Expected formulas/subtotal inclusion, row/style/height/format and downstream reference results; unchanged unrelated content; stale-source/revision and duplicate/correction tests |
| 4. Complex dependency qualification | Reproduce Payables links/names/merges/comments/hidden layout and Breakdown's VENDOR/shared/array/metadata context; qualify affected and unaffected dependency classes | Operation-bound permission matrix; independently verified graph/style/anchor/calculation effects; unresolved dependencies remain denied |
| 5. Transaction and output integration | Plan application/adapter changes first; adopt validated candidates only; retain explicit separate-output authorization and existing source/no-overwrite protections | Reopen/validation/commit failures, destination races, permission/lock/disk faults, crash/timeout/cancel boundaries and original preservation verified end to end |
| 6. Representative accounting acceptance | After separate authorization, test disposable full copies of selected real templates; validate supported Excel versions, bitness, locale and offline behavior | Accounting-owner verification of sections, formulas, totals, formatting and usable workflow; preservation/performance evidence before enabling UI capabilities |

Resource redesign and a corrected cell counter can be addressed as separately
reviewed changes in those phases. Measure full-model/candidate peaks and operation
costs first; do not derive an editing budget from the fast streaming-read timings.
Keep worksheet-eligibility UI implementation and OCR paused until the requested
architecture review determines the next approved work.

## 8. Verification record and limitations

Executed in this investigation:

- Default product admission reproduced all three first rejections without modified
  limits; independent normal inspection succeeded for all three sources and all
  54 sheet scans. Streaming census and product inspector counts matched **54/54**.
- An artificial **in-memory** package verified the census's blank-style partition,
  whitespace/empty shared strings, numeric zero, cacheless formula, hidden row,
  direct-sheet dependency and calculation-chain overcount invariants. No workbook
  fixture was written to disk for that check.
- All final source before/after SHA-256 comparisons passed; file sizes/timestamps
  stayed unchanged. A hash manifest of all existing tracked/unignored repository
  files verified that investigation work changed none of them. Only this report
  is added outside ignored local evidence.
- The first standalone helper lacked the runtime dependency context used by the
  desktop app. It misleadingly returned `FILE_UNAVAILABLE`; first-chance tracing
  identified missing `System.IO.Packaging`, not source access failure. The helper
  was corrected to reference the installed Windows Desktop framework and load the
  existing product dependencies, then rerun. Those provisional results are excluded
  from workbook conclusions. This did not change product dependencies or code.
  The helper restore emitted NU1900 because NuGet vulnerability metadata was
  unreachable; no product package was added/upgraded and no audit setting changed.

Not executed: product changes or product regression suites (none were warranted
for this report-only task); Excel/COM startup or automation; any worksheet edit,
row insertion, save/no-op save, output creation, reconstruction or cleanup of
original workbooks; real-workbook round-trip/preservation acceptance; performance
qualification of an enlarged editing envelope; clean-machine deployment; or a
production safety claim. This investigation stops at the report for strategy review.
