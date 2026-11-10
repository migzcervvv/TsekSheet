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

If no compatible row exists, the default `hybridNewRowMode: "template"`
continues to reject financial formulas in the final data-row template. The
Payables profile explicitly selects `hybridNewRowMode: "blank-inputs"`: Excel
inserts a row with formatting from above, with ordinary range formula extension
(`ExtendList`) disabled for the entire write batch and restored in `finally`.
This mode requires a fixed-column profile with no formula-copy columns. Before
writing, the adapter reads the inserted band back and requires every cell to be
empty and formula-free. It never clears an unexpected formula to make room for a
value. Existing formulas above and below the new row remain protected by the full
Excel preservation check. No accounting formula is copied or inferred.

Ordinary formula-bearing templates, including the Member Company 01 I196 case,
can therefore supply formatting for a new manual row. Optional financial inputs
remain blank unless entered explicitly. Merged, protected, array/spill and table
templates, foreign content outside the input band, ambiguous structure and stale
previews remain blocked. An unexpected Excel-populated new row stops before any
supplied values are written and requires manual review of the unsaved insertion.
Subtotal formula handling retains the existing same-sheet A1 rules.

Markers may additionally contain `after` and `before` marker objects, and
`expectedCount`. Occurrence is counted only between those exclusive bounds.
A count mismatch rejects Check with an instruction to verify section markers.
Nested bounds are limited to three levels. All bounds resolve again at preview.
Fixed profiles also validate every section subtotal scope before selecting a row;
a malformed unrelated category cannot silently redirect the destination.

The built-in **Payables — RFP**, revision 2, maps row-3 headers B–O. C3 must be
`PAYEE (VENDOR NAME)`; the displayed field label remains Payee.
Only Payee, RFP No. and Amount are required. Date Received, RFP No. and Invoice #
remain text; optional money fields remain independent inputs. No totals, tax or
alternate Amount values are calculated. `advisoryIdentifier: "rfpNo"` reports a
possible duplicate on matching worksheets without blocking explicit insertion.
The Payables profile sets `sourceScope: "document"`, so source guards cover the
entire PDF packet; the default `"page"` retains existing page-scoped guards.

**T8a-FIX structural coverage:** the Excel-authored synthetic reference reproduces
the planner-verified headings, subtotal positions and I196 formula-bearing row.
The category order is Employee, Cash Advance, Dollars, Last Pay, Refund, Member
Company 01–14, Insurance, Mobile Subscription, External. All subtotals use column F.
The fourteen Member Company markers are counted exclusively between the Member
Company and Insurance headings; the fourteenth destination cannot reach Insurance.
Inventory subtotals are outside every Payables scope.

Column-B category markers use `match: "year-category"`. The entire literal heading
must follow `YYYY PAYABLES (CATEGORY)`, case insensitive with flexible whitespace;
the year may differ between categories. Bare category names, extra trailing text
and a changed category fail. This is a fixed grammar, not a user-supplied regex.
A missing heading, wrong order or unexpected subtotal count blocks preview without
fallback. Member Company labels are numbered choices, not inferred company names.

The column-C overall-total heading is protected as the unique nonempty literal
text between External's subtotal and the unique column-B Inventory heading
(`match: "nonempty-text"`, with both bounds and count 1 required). This avoids
inventing the unspecified total caption. Both structures move with Excel insertions
and are resolved again. Observed heading/subtotal row numbers are fixture assertions,
never insertion addresses.

Owner-side validation still covers the original workbook's exact heading grammar,
styles, merged/array/spill regions, validation/conditional formats and accounting
intent. The original workbook has not been opened by the agent. The tests prove
structural resolution and preservation on fictional Excel-authored data, including
both blank VAT and explicitly entered VAT above the I196 template formula. They do
not certify private workbook content or accounting calculations.
