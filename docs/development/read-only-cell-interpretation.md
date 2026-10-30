# Read-only cell interpretation — Step 47

Step 54 [isolates per-format failures](step54-verification.md). An explicit unqualified low-ID definition (including ID 43), duplicate/conflicting format, missing reference or unresolved inheritance no longer disables unrelated qualified styles. Globally malformed/over-budget stylesheets still fall back to raw. Source/effective format IDs, original bounded code, resolution, classification and warnings are visible in cell details. Only exact fixed built-in agreement and independently qualified direct/base cases are resolved; divergent or row/column inheritance stays raw. Conditional formatting is not evaluated. The accounting grammar and Step 50 over-precision policy are unchanged. See also [bounded read-only role/section analysis](read-only-payables-section-analysis.md); it grants no editing permission.

Step 50 rejects non-General numeric display interpretation above 15 significant stored decimal digits. Supported numeric formats use explicit midpoint-away-from-zero display rounding; this is not general Excel binary-rounding equivalence. Raw values and original formats remain accessible. See [precision evidence](step50-verification.md).

Step 49 qualifies 28 exact native displays and two rejected locale controls. Escaped parentheses, hyphens and spaces outside quotes are recognized only before the existing whitelist; percentage IDs 9/10 are mapped. See [native fidelity](accounting-display-fidelity.md).

Step 48 adds a restricted invariant accounting-number grammar with literal currency prefixes, percentages, negative parentheses and explicit zero sections. Unsupported locale/alignment syntax remains raw. Exact policy and evidence: [Step 48](step48-verification.md). This supersedes the earlier blanket currency/multi-section limitation below.

## Values and trust

Inspection retains `CellInfo.Value` as stored/resolved content and adds `CellInterpretation`: raw value, display value, resolved type, confidence, warning and formula status. Strings are resolved locally from shared strings; booleans retain the existing TRUE/FALSE representation. Numeric raw values and formula cached values remain unchanged. No calculation, mutation translator, ClosedXML model or external resource is used. Selecting an inspection cell shows details using the existing cell panel; it cannot create an edit.

Interpretation is an explicit supported subset, not Excel visual equivalence. Formula caches remain unverified, possibly blank or stale. No new native Excel reference capture/comparison was performed. Existing native Step 45 corpus regressions remain intact.

Microsoft documents [1900/1904 date systems](https://support.microsoft.com/en-gb/excel/date-systems-in-excel), the [1900 leap-year compatibility defect](https://learn.microsoft.com/en-US/office/troubleshoot/excel/wrongly-assumes-1900-is-leap-year) and [shared formula attributes](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-formulas). Implementation expectations below are separately specified literal synthetic cases, not output-derived expectations.

## Supported date and number formats

The workbook `date1904` flag is parsed strictly. Numeric cells are interpreted only using a qualified cell number format. Styles load lazily through bounded XML: 4 MiB maximum, 4,096 total `xf` records, 1,024 custom formats, 256 characters per custom format, existing depth/time/decompression limits. Malformed/over-budget styles produce a raw-value warning. Missing/out-of-range style indices, nonzero base-style inheritance and explicitly disabled number formatting are not guessed.

| Format | Interpretation |
| --- | --- |
| Built-in 14–17 | ISO date |
| Built-in 18–21 | 24-hour time |
| Built-in 22 | ISO date/time |
| `yyyy-mm-dd`, `yyyy/mm/dd`, `dd/mm/yyyy`, `mm/dd/yyyy`, `d/m/yyyy`, `m/d/yyyy`, `dd-mmm-yyyy` | ISO date |
| `yyyy-mm-dd hh:mm:ss`, `yyyy-mm-dd hh:mm`, `m/d/yy h:mm` | ISO date/time |
| `hh:mm:ss`, `h:mm:ss`, `hh:mm`, `h:mm`, `h:mm am/pm`, `h:mm:ss am/pm` | 24-hour time |
| `General` | Raw number |
| `0`, `0.00`, `#,##0`, `#,##0.00` | Invariant numeric display; raw precision retained |

Custom comparisons are case-insensitive exact matches. Conditional/color/locale sections, quoted currency symbols, elapsed `[h]` formats, bare ambiguous `mm`, other built-ins and arbitrary Excel format grammar are unsupported. These retain raw values with warnings. Currency-like accounting columns benefit from supported numeric separators/decimals, but no general currency formatter is claimed.

1900 serials 1/59/61 map to 1900-01-01/1900-02-28/1900-03-01. Serial 60 displays 1900-02-29 with an explicit **fictitious Excel leap day** warning. It is never represented as a valid Gregorian DateTime. 1904 serial zero maps to 1904-01-01. Unsupported 1900 date serial zero, negative serials and invalid/out-of-range values stay raw. Time-only fractions can use zero; whole days are omitted with a warning. Time display rounds to milliseconds, with rollover for date-time/time displays. Date-only display retains the floored calendar day and warns that fractional time remains in raw data. No locale, timezone or Excel rounding/rendering equivalence is asserted.

## Shared formula grammar and validation

`InspectionSharedFormula` is independent of mutation translation code. The parser accepts uppercase same-sheet A1 references with relative, `$` absolute or mixed anchors; decimal literals; unary/binary `+ - * /`; parentheses; and uppercase `SUM(reference)` / `SUM(reference:reference)`. Expressions are limited to 1,024 input/output characters, 128 factors and depth 32. It performs only reference translation, never evaluation.

Whitespace, sheet-qualified/quoted names, external references, named ranges, structured table references, other functions, comma-separated SUM arguments, strings, arrays and spill syntax are unsupported. Reference translation must remain within worksheet limits. Original ordinary formula text can still be displayed without claiming it is understood.

Each selected-sheet scan records at most 1,024 shared groups. A valid group has one nontruncated anchor at the top-left of a declared rectangular range, consistent `si`, complete unique membership inside the rectangle and no overlap with another group. Missing/duplicate anchors, holes, followers outside the range, malformed ranges or unsupported grammar yield explicit unsupported shared-formula status. Anchors outside the requested viewport are captured during the complete scan. Array/data-table formulas are shown as stored with warnings and are not reconstructed. Existing editable rejection rules remain unchanged.

## Navigation policy

No random-access row-location index is enabled. ZIP entry streams are decompressed sequentially; an XML reader position is not a usable compressed-file seek offset. A sparse row-number list would identify rows but would not avoid decompression. A seekable sidecar would introduce private-data staging, identity/cleanup and parser-boundary requirements not proven in this milestone.

Every uncached viewport therefore scans the selected sheet under the existing budgets. Row validation rejects malformed/out-of-order/duplicate declared rows, malformed nesting, mismatched cell addresses and inferred rows outside Excel bounds. Missing row numbers are inferred from their first stored cell, or sequentially for empty rows. Declared worksheet dimensions never allocate cells or drive an index.

An LRU cache holds at most eight windows and 2 MiB of conservatively accounted cell/string payload plus a fixed per-window allowance. This is an admission estimate, not a hard CLR heap quota. Cache entries belong to a single immutable inspection session and include sheet/range identity. Recent windows avoid rescans; different uncached windows retain linear cost. Closing clears cached views/styles and releases the read-only source handle. Windows sharing rules prevent writers/deletion while open; reopening obtains a new fingerprint and sheet IDs. Old IDs are rejected. No index files or workbook contents are persisted.

## Capabilities and private-file procedure

The report now explicitly lists metadata inspection, worksheet selection, bounded browsing, editing, working copies, ordinary Merge, structural insertion, structural Merge and export. Only the first three are potentially available in inspection. All prior package and editing limits remain unchanged. Production Apply, review-only tickets, source overwrite, autosave and structural integration gates are unchanged.

No authorized real accounting workbook was found in the searched locations. To qualify one locally: choose **Inspect workbook read-only**, review the report, select the required sheet and inspect representative date/formula cells using the details panel. Use the start-row/column inputs for distant records; close when finished. Record only technical counts/restrictions and local before/after SHA-256 fingerprints. A failed view does not authorize editing or repair of the source. No workbook needs to be opened in Excel or uploaded for this procedure.

See [Step 47 verification](step47-verification.md) for measured behavior and remaining blockers.
