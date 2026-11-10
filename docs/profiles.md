# Local Excel profiles

Place UTF-8 JSON files in `%LOCALAPPDATA%\TsekSheet\profiles\*.json` before opening
the app. These are local configuration, never committed. Profile IDs must be unique,
including built-ins. A file is limited to 64 KB. Invalid profiles produce an error;
they are never silently replaced with a different layout.

The planner's `exti-liquidation.profile.json` is supported directly. Profile text,
including notes in `vatRule` and `lookups`, is data. It cannot authorize conversion,
lookups, code execution or workbook access.

Required properties:

| Property | Meaning |
| --- | --- |
| `schemaVersion`, `id`, `revision`, `displayName` | Version 1; stable local ID and positive revision. |
| `sheetMatch` | `headerRow` and `anchors` containing `column` and `equals`. All anchors must match literal cell text (trimmed, case insensitive). Every matching sheet is offered. |
| `fields` | `key`, `label`, `kind`, `required`; direct fields have `column`. Kinds: `text`, `date` (YYYY-MM-DD), `money`, `accountChoice`. |
| `accounts` | `code`, `label`, `column`, `vatable`. The dropdown shows code, label and column. Column identifies a choice because a code can repeat. |
| Amount field | One `money` field with `writesTo: "selectedAccountColumn"`; one required `accountChoice` field. Amount goes only to that account's column. |
| `formulaColumns` | Column letters never written with input values. Existing blank rows retain their formulas. Inserted rows use Excel FillDown from the row above. |
| `sections` | Unique `id`, `label`, `start`, `end`, `subtotalRow`, `insertMode`, `fillBlankRowsFirst`. Modes: `entireRow` or `columnsOnly`. |
| `protectedRows` | Marker objects locating rows excluded from section data and writes, such as the grand total. Excel may adjust their references and recalculate them when rows shift. |

A marker is `{ "column": "C", "equals": "TOTAL", "occurrence": 1 }`.
Occurrences count literal matches top to bottom across the sheet. Optional `note`
is descriptive. A section starts either at `{ "headerRowOffset": 2 }` relative to
the matched header, or `{ "afterRow": <marker>, "offset": 3 }` relative to a marker.
`end` is `{ "rowBefore": <marker> }` and must identify the same marker as
`subtotalRow`. Rows are located again at every preview; inserted rows do not make
the profile stale.

For the supplied layout, `blankRowRule` is exactly:

> A data row is blank when every mapped input column (B-H and all account columns) is empty. Formula columns are ignored.

The app evaluates the rule against all configured direct input and account columns.
An input formula is occupied even if it displays empty. With `fillBlankRowsFirst`,
the first blank data row is used; only when none is blank does Excel insert above
the section subtotal. Subtotal A1 range expansion remains the T2 rule; unsupported
subtotal formulas are left unchanged and identified for manual review.

D12: amounts are written as entered. `vatable` describes the account; it does not
trigger VAT conversion. Existing N/O/AF formulas remain the workbook's calculations.
`lookups`/`vendorMaster` are reserved for T2d and do not run a vendor lookup.

Open the workbook using **Open workbook in Excel** in receipt review. Choose the
profile, matching sheet and section, verify the fields, preview, then insert. Legacy
read-only inspection and package admission are independent. Printer settings, VML,
images, external links, shared formulas, conditional formatting and validation do
not block this Excel path. Macros are disabled and links are not updated on open.
Legacy inspection reads a temporary snapshot and releases the original file handle;
its displayed cells remain a snapshot until the workbook is reopened for inspection.
An Excel workbook opened read-only can still receive unsaved edits in memory; use
Excel Save As to retain them in a new file.

## Fixed-column financial profiles

Set `mappingMode: "fixed"`, `accounts: []`, and give every field a direct
`column`. No accountChoice or selectedAccountColumn mapping is used. The default
`mappingMode: "account"` retains the EXTI account selector and amount behavior.
Layout profiles permit up to 32 flat sections; fields remain limited to 16 and files
to 64 KB. For fixed profiles, `blankRowRule` is exactly:

> Empty manual inputs; preserve optional formulas; supplied values require editable cells.

Blank manual input cells are required. An optional money cell may already contain
a formula only when the user supplies no value for it. Check displays **Existing
formula preserved** for those cells. Supplied values make formula-controlled rows
incompatible; the search continues to the next row. Merged/array/spill rows and rows
with foreign content outside the mapped band are excluded. The normal protected
sheet, stale-preview, backup and full Excel readback checks still apply.

If no compatible row exists, insertion uses the final data row as its formatting
template. A hybrid input formula in that template causes a pre-write rejection:
formula applicability has not been established. No hybrid financial formula is
copied, inferred or repaired. A manual-only template permits insertion above the
subtotal. Subtotal formula handling retains the existing same-sheet A1 rules.

Markers may additionally contain `after` and `before` marker objects, and
`expectedCount`. Occurrence is counted only between those exclusive bounds.
A count mismatch rejects Check with an instruction to verify section markers.
Nested bounds are limited to three levels. All bounds resolve again at preview.

The built-in **Payables — RFP** maps row-3 headers B–O exactly as listed in T8a.
Only Payee, RFP No. and Amount are required. Date Received, RFP No. and Invoice #
remain text; optional money fields remain independent inputs. No totals, tax or
alternate Amount values are calculated. `advisoryIdentifier: "rfpNo"` reports a
possible duplicate on matching worksheets without blocking explicit insertion.
The Payables profile sets `sourceScope: "document"`, so source guards cover the
entire PDF packet; the default `"page"` retains existing page-scoped guards.

**Layout qualification remains incomplete.** The ticket supplied no heading-cell
addresses or verified Member Company boundary map, and the owner's workbook was
not opened. The synthetic, strictly checked marker contract is: column B contains
unique Employee, Cash Advance, Dollars, Last Pay, Refund, Insurance, Mobile
Subscription and External headings, then Member Company; each main category has
one Sub-Total before the next heading; Member Company has exactly fourteen
Sub-Total markers before a unique Grand Total. A unique Inventory heading is also
required outside every destination. Member Company 01–14 are ordinal UI labels,
not inferred company names. No insertion row numbers are stored. This contract
must be checked against an Excel-authored redacted structural reference before
claiming compatibility with the supplied accounting layout. Missing or ambiguous
markers reject insertion; no fallback destination is selected.
