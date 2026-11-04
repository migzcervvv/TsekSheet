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
