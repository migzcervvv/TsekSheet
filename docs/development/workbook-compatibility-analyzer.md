# Workbook compatibility analyzer — Step 46

Step 54 adds conservative per-format isolation and a **separate bounded read-only analysis capture**: explicit source/session/sheet/revision binding, up to 2,000 rows/32 role columns/40,000 cells and 16 MiB accounted payload, with existing XML/time budgets, shared-string/formula handling and hidden/styled-row coverage. It does not widen the 20x10 public viewport, editable limits or mutation capabilities, and it creates no disk index. [Actual requalification](step54-verification.md) reports 21 qualified numeric displays and an explicitly rejected section/identity partition. [Structured section diagnostics](read-only-payables-section-analysis.md) remain independent of production planning; the desktop adds format details, not a section editor or inspection export.

Step 53 [qualifies the actual authorized duplicate](step53-verification.md): all five tabs browse read-only, source hash unchanged, 439,321 stored cells, extensive names/external links and advanced package content. Actual editable validation stops at 50,001 cells. Numeric interpretation is raw-only in this run because explicit number-format ID 43 triggers the current stylesheet fallback. Layout/vendor feasibility is partial; inspection planner/copy/edit/Merge/export gates are unchanged.

Step 52 repairs Git checkout portability and restores all provenance comparisons; it makes no analyzer or editing-envelope change. Real Payables qualification remains **UNEXECUTED** because the designated input directory is absent. See [verification](step52-verification.md).

Step 51 checks existing readiness only: no authorized real workbook was supplied. [Verification](step51-verification.md) records current failures and exact input instructions; [strategy](real-workbook-compatibility-strategy.md) lists detection rules and conditional preservation alternatives. No inspection or editing envelope was widened.

Step 50 narrows ambiguous financial display interpretation; metadata/view eligibility still grants no editing or export permission. The authorized real-input directory remains absent. See [verification](step50-verification.md).

Step 49 preserves every inspection/editing boundary; native accounting display support does not certify rewrite preservation. See [verification](step49-verification.md).

Step 48 adds restricted accounting display and preserves blank formatted values. Raw values and unsupported-format warnings remain available; inspection capabilities and all package/mutation restrictions are unchanged. See [verification](step48-verification.md).

Step 47 supersedes the raw-display/single-window limitations below with a narrow [cell interpretation subset](read-only-cell-interpretation.md), explicit worksheet-selection/structural-Merge capability entries and an eight-window/2 MiB accounted cache. Uncached scans and all package limits remain unchanged; no random-access row index is enabled.

## Separate inspection boundary

**Inspect workbook read-only** opens a local `.xlsx` through `WorkbookInspection`, independently of `PackageGuard`, Open XML DOM validation and ClosedXML. `workbook.inspect` accepts no paths from JavaScript; the native selector supplies the source. The workspace serializes operations and retains one active workbook. Opening another workbook still requires pending-change disposal. Inspection cannot be converted into an editable session: close it and use **Open workbook**, which repeats full existing validation.

The inspector keeps a read-only source handle with writer/deletion exclusion until close. It streams SHA-256, validates ZIP directory allocation bounds, inventories package entries, validates internal relationship targets and reads workbook metadata. No worksheet contents are loaded for metadata. Source files, embedded objects, images, macros and external targets are never written, executed or followed. Unknown unused parts remain untouched and unparsed. Metadata success is not a whole-package schema or preservation certificate.

## The three existing errors

`PackageGuard.Inspect` is the editable-session guard. `WorkbookEngine.Open` first reads the compressed source into a bounded byte array (16 MiB maximum); the guard then scans package XML before Open XML validation/DOM traversal and before `XLWorkbook` construction.

| Rejection | Actual trigger and meaning |
| --- | --- |
| Unsupported binary or embedded content | Any non-directory part without `.xml`, `.rels` or `.psmdcp` suffix. This includes ordinary images, not just executable content. Other checks separately reject VBA, ActiveX, external links/connections and signatures. The restriction protects rewrite fidelity. |
| Too many worksheets | The 33rd element with local name `sheet`; default limit 32. This is a streaming element count, not an inflated worksheet dimension. Early rejection does not measure the remaining total. |
| Too many stored cells | The 50,001st spreadsheet-namespace `c` element across package XML; default limit 50,000. Includes empty styled cells and formulas, excludes implied cells in dimension declarations and formatting applied only to rows/columns. |

These limits are unchanged. Error messages now explain their measurements and direct users to separate inspection. Other editable guards retain 64 MiB expanded data, 1,024 entries, 500,000 XML elements, depth 64, 4,096 style `xf` elements, four sessions and a shared estimated 256 MiB admission budget. The estimate is not a CLR heap quota. Broken relationships, DTDs, unsafe paths, expansion and XML budgets protect parsing/resources; unsupported parts and advanced feature restrictions also protect preservation. None is waived for mutation.

## Capabilities

| Operation | Inspection session | Existing validated session |
| --- | --- | --- |
| Metadata | Bounded package facts | Existing workbook information |
| Worksheet browsing | Conditional ordinary XML prototype, selected request can be rejected | Existing bounded ClosedXML viewport |
| Cell editing | Disabled | Existing qualified working-copy edits |
| Working copies | Disabled | Existing eligibility/admission checks |
| Structural insertion | Disabled | Production Apply remains disabled |
| Merge | Disabled | Existing recorded-cell checks; experimental structural gates unchanged |
| Export | Disabled | Existing new-file-only validated Save As |

Backend snapshot/copy/edit/export methods reject inspection directly. Default review/integration operations reject it; workspace workflow planning is also blocked to prevent repeated whole-sheet scans through that API. The UI disables copy/Save As and omits Payables planning for inspection. Public Step 45 tickets remain review-only. Replace/Create New, overwrite and autosave are not enabled.

## Observable report

Reports source filename/SHA-256/file bytes, compressed and declared expanded part totals, part count, ordered tab names and visibility, defined-name count, relationship count, external relationship presence, feature categories and explicit capability reasons. Categories distinguish images, drawings/charts, OLE/embedded objects, VBA/macros, ActiveX/controls, external data, unknown binaries, other parts and preservation-sensitive relationships. Categories are presence/count observations, not content authentication. Workbook extensions are reported; unused XML is not fully inspected.

Initial worksheet dimensions/counts are explicitly unmeasured. A successful selected-sheet scan reports declared dimension, measured stored-cell extent, stored cell elements, nonempty serialized `v`/text content, formula elements and explicit cell `s` attributes. A shared-string index counts as nonempty serialized content even if the referenced string is empty; inherited row/column styles are not counted as cell style attributes. Dimension declarations never control allocation. A heuristic flags declared extents or styled tails more than ten times the measured/value extent, subject to row/column floors; it is not proof of accidental formatting.

## View prototype and budgets

Each uncached request streams the complete selected worksheet and retains at most 200 requested cells. It does not materialize other sheets. One last viewport is cached; a different window rescans the sheet. Row/column inputs permit direct sparse navigation. Ordinary numeric/string/boolean/ISO-date/error values and formula text/caches are displayed as stored. Numeric dates remain serials; styles, merges, drawings, tables and rich appearance are not rendered. Shared/array followers are not reconstructed. No recalculation or current-cache claim is made.

Shared strings are scanned only when requested cells need them; only requested indices are retained. Large collections, malformed data or unsupported ordering reject that view while metadata stays available. Unknown worksheet features produce local warnings. This prototype requires explicit, ordered, unique A1 addresses and transitional OOXML. It is not an Excel rendering engine.

| Default bound | Value |
| --- | --- |
| Concurrent inspection sessions, process-wide | 2 |
| Compressed source file | 128 MiB |
| Declared total expansion / actual bytes read per operation | 512 MiB |
| ZIP entries / central directory | 4,096 / 4 MiB, checked before ZipArchive entry allocation |
| Tabs / relationships | 512 / 16,384 |
| Metadata XML part / metadata node count | 4 MiB / 100,000 per part |
| Selected XML part | 64 MiB |
| Selected-sheet stored cells | 2,000,000 |
| Shared strings scanned | 500,000 |
| XML depth / streamed elements | 64 / 12,000,000 per operation |
| Time budget | 10 seconds per operation, cooperative checks |
| Viewport / retained field text | 20 × 10 / 1,024 characters |
| Compression ratio | Reject above 1,000:1 for entries larger than 1 MiB |

Split, ZIP64 and encrypted archives are outside this prototype. Central directory counts/lengths, unsafe or duplicate/encoded paths, dangling or case-mismatched internal targets, DTDs and malformed required XML fail closed. XML readers disable external resolution. Actual decompressed reads are bounded independently of ZIP size declarations. Local I/O calls are synchronous; time checks are cooperative, not OS hard timeouts. Closing/disposal cancels scans and releases the archive/source handle and viewport cache. Metadata parsing uses bounded small DOMs after depth/node validation; worksheet parsing streams.

Bounds are rejection thresholds, not tested maximum-capacity promises. Measurements and remaining limitations are in [Step 46 verification](step46-verification.md).
