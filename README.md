<p align="center">
  <img src="src/TsekSheet.Frontend/public/branding/TsekSheet.svg" alt="TsekSheet" width="120">
</p>

# TsekSheet

**README** · [Changelog](CHANGELOG.md)

**Turn scanned documents into verified Excel rows, offline.**

TsekSheet is a Windows desktop app for people who process stacks of receipts, invoices and
request forms into existing Excel workbooks. It shows each document next to the fields you
need, proposes values with local OCR, lets you correct and verify them quickly, and inserts the
verified rows into the right section of your workbook through Microsoft Excel. Formulas,
formatting and other sections stay intact. Nothing leaves your machine.

First workflow: accounting payables and reimbursements. Workflows are defined by profiles
(fields, sections, column mappings), so the same app works for other document-to-spreadsheet
jobs.

> **Status: early development.** Not ready for production accounting work.
> See [Roadmap](#roadmap) for what works today.

## How it works

```text
PDF / photo / scan
      |
      v
 Render page  ->  Local OCR (printed text)  ->  Proposed fields (unverified)
                                                     |
                               You correct, type handwriting, verify
                                                     |
                                                     v
                       Pick workbook + section  ->  Preview row
                                                     |
                     Backup, then insert through Excel (formulas and subtotals update)
                                                     |
                                                     v
                         Rows highlighted in Excel, you review and save
```

Key principles:

Choose **Open workbook** to establish an Excel-backed workspace and enter **Process
Receipts**. Choose **Add receipts** for one or multiple PDFs/images, edit the fields,
select a sheet and section, **Check row**, then explicitly **Add row to Excel**.
The queue uses the active workbook without reopening it. Review and
save explicitly in Excel. **Read-only inspection** is a secondary tool with advanced
compatibility findings. Pending receipt work blocks workspace switches. Working-copy
editing, candidate generation, Merge and the legacy export controls have been removed.

- **Excel does the spreadsheet work.** TsekSheet never rewrites workbook files itself; it drives
  your installed Excel, so formulas, formatting and features behave exactly as in Excel.
- **Humans verify everything.** OCR output is a proposal. Only verified fields can be inserted.
- **Originals are protected.** A backup is made before the first change, changes are visible in
  Excel, and nothing is saved without your action.
- **Fully local.** No cloud OCR, no telemetry, no accounts.

## Roadmap

| Milestone  | What you get                                                                              | Status      |
| ---------- | ----------------------------------------------------------------------------------------- | ----------- |
| Foundation | Desktop shell, workbook inspection, document viewer, manual field review and verification | Done        |
| M1         | Verified fields inserted into a workbook section through Excel, with backup and preview   | In progress |
| M2         | Local OCR for printed text on poor-quality scans and photos                               | Planned     |
| M3         | Batch queue, resume after restart, define sections by selecting them in Excel             | Planned     |
| M4         | Installer, CI, offline clean-machine validation                                           | Planned     |

Details and tickets: [PLANS.md](PLANS.md). Product goals: [GOALS.md](GOALS.md).

### Works today

- Open an active workbook in Excel and enter its receipt queue directly; inspect workbooks
  separately with the read-only tool (sheets, values, formulas, number formats).
- Load local PDF, PNG and JPEG documents and page through them.
- Enter, correct, verify and retract document fields against a workflow profile.
- Read-only section analysis and placement preview for the sample Payables and Laboratory
  profiles.
- Preview and insert verified receipt fields into a profile-defined section of a workbook
  open in TsekSheet's visible Excel instance. Inserted rows are highlighted and listed in the app.
- Process an in-memory PDF/image receipt queue with retained destinations and account choices,
  draft previews, explicit Add Row confirmation, automatic advance and skip/review.
  See [Receipt entry and measured timing](docs/receipt-entry.md).

- Request local OCR proposals in the receipt queue, inspect source word boxes, select a region
  for re-OCR, and correct drafts before explicit Add Row confirmation. Local assets require
  configuration; see [Receipt entry](docs/receipt-entry.md#product-ocr-t4).

### Not yet

- Owner validation on redacted workbooks (M1), broader OCR validation, session recovery (M3), installer (M4).
- The older built-in cell editing / Merge / Save As path is being retired in favour of Excel.

## Requirements

**To run**

- Windows 10 22H2 or Windows 11, x64.
- Microsoft Excel desktop (Microsoft 365 or 2021+, 64-bit recommended). Required from M1.
- .NET 10 Windows Desktop Runtime and Microsoft Edge WebView2 Runtime.

|         | Minimum                                          | Recommended                               |
| ------- | ------------------------------------------------ | ----------------------------------------- |
| CPU     | Intel Core i3 12th gen / AMD Ryzen 3 5000 series | 6+ cores (reference: ASUS TUF Gaming A15) |
| RAM     | 8 GB                                             | 16-24 GB                                  |
| Storage | SSD, 10 GB free                                  | SSD, 20 GB free                           |
| GPU     | Not required                                     | Not required                              |

**To build**

- .NET SDK **10.0.403** x64 (pinned in `global.json`, no roll-forward).
- Node.js **>=24.16 <25** and npm **>=11.13 <12**, with `node` and `npm.cmd` on PATH.
- Git. Visual Studio 2026 or VS Code. (Visual Studio 2022 does not support .NET 10.)

## Build, test, run

From the repository root in Windows CMD:

```cmd
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore --filter "TestCategory!=Excel"
```

Frontend checks:

```cmd
pushd src\TsekSheet.Frontend
call npm.cmd run typecheck
call npm.cmd run test -- --run
popd
```

Run the app:

```cmd
src\TsekSheet.Desktop\bin\Release\net10.0-windows\TsekSheet.Desktop.exe
```

Notes:

- The desktop build runs `npm ci` and bundles the React UI into the output folder; no dev
  server is needed. Stop any running Vite/Vitest process before building.
- Keep NuGet lockfiles and `package-lock.json` committed.
- Tests marked `[TestCategory("Excel")]` need Excel installed and run locally only:
  `dotnet test tests\TsekSheet.Excel.Tests -c Release --filter TestCategory=Excel`.
  CI excludes this category. Test setup authors synthetic workbooks through Excel in
  ignored `fixtures/excel/t1-*/` run directories; it never opens real accounting workbooks.
- In Receipt review, enter and verify the fields and at least one record identifier. Choose
  **Open workbook in Excel**, a workbook and profile section, **Preview Excel insert**, then
  **Insert verified row**. Save in Excel explicitly. Closing TsekSheet leaves open Excel
  workbooks visible; no insertion saves the original automatically.
- A hash-verified backup precedes the first modification. Each insert checks Excel readback,
  highlights the new row, and adds it to the app's inserted-row list. Formula columns fill from
  the preceding row. Stale previews require a fresh preview; an unknown submission outcome
  requires checking Excel before another submission.
- Section definitions live in each workflow profile's `excelSections`: sheet name, header-row
  anchor, field-to-column map, formula columns, subtotal marker/column, and `tableRow`,
  `entireRow`, or `columnsOnly` mode. The embedded sample layouts use sheets `Columns`,
  `Stacked`, and `Table`, header row 2, fields A–F, formula column G, and the `Subtotal` marker
  in column A. The table layout uses ListObject `Records`. User-defined selection remains T6.
- Only same-sheet A1 range endpoints equal to the old last data row are extended in that
  section's moved subtotal row. Unsupported formulas remain verbatim and are listed for manual
  review. Native Excel owns table-row extension and structural shifts. Value writes protect
  formulas and reject merged/array/spill cells; table writes target the newly added data row.
- Date values honor the workbook's 1900 or 1904 date system. Verified decimals that would
  change when limited to Excel's fifteen significant digits are rejected before insertion.
- Excel tests clean up their own synthetic hosts in code and wait up to ten seconds for process
  exit. No GUI action, manual window cleanup, process kill, or unrelated Excel attachment is used.
- Excel exposes number formats and fills as scalar metadata (null for mixed ranges), so the
  adapter partitions mixed rectangles while transferring cell values/formulas in bulk arrays.

## Project structure

```text
src/
  TsekSheet.Desktop/       WPF + WebView2 host, native dialogs
  TsekSheet.Frontend/      React + TypeScript UI and bridge client
  TsekSheet.Application/   Workflows, verification, documents, bridge validation (no UI/COM)
  TsekSheet.Workbooks/     Read-only workbook inspection
  TsekSheet.Excel/         Excel automation adapter (M1)
  TsekSheet.Renderer/      Isolated PDF/image page renderer
tests/                     MSTest suites per project, shared fixtures
fixtures/excel/            Excel-authored synthetic workbooks
experiments/archive/       Retired research and proofs of concept (not built)
docs/                      Architecture, decisions, history
```

## Tech stack

| Area                | Choice                                                                          |
| ------------------- | ------------------------------------------------------------------------------- |
| Desktop             | WPF on .NET 10 (`net10.0-windows`), Windows x64                                 |
| UI                  | React 19, TypeScript, Vite, hosted in WebView2 at `https://tseksheet.local`     |
| Spreadsheet engine  | Microsoft Excel via COM automation                                              |
| Workbook inspection | ClosedXML, Open XML SDK (read-only)                                             |
| OCR                 | PP-OCRv5 Mobile CPU via ONNX Runtime, no preprocessing (Tesseract retained for evaluation) |
| Tests               | MSTest, Vitest, React Testing Library                                           |

## Data and privacy

- Documents and workbooks are processed locally. The app makes no network calls.
- Do not commit real receipts or workbooks. Redacted test copies belong in
  `fixtures/redacted/`, which is gitignored.

## Contributing

Work happens in small tickets defined in [PLANS.md](PLANS.md), following the rules in
[AGENTS.md](AGENTS.md): one branch and one PR per ticket, targeting `main`. Changes are listed
in [CHANGELOG.md](CHANGELOG.md).

## License

[GNU Affero General Public License v3.0](LICENSE). Third-party components keep their own
licenses. New dependencies must be AGPL-3.0 compatible.

The build-only `Microsoft.Office.Interop.Excel` 16.0.18925.20022 NuGet package declares no
license and describes itself as an unsupported repackaging of Office assemblies; AGPL
compatibility cannot be confirmed from that metadata. `ExcelDna.Interop` 16.0.0 supplies the
Office core enum PIA under package-declared MIT terms. Both references embed interop types
and disable runtime/copy-local assets: neither `Microsoft.Office.Interop.Excel.dll` nor
`office.dll` is distributed in the app output. A licensed desktop Excel installation remains
required. Package metadata: [Excel interop](https://www.nuget.org/packages/Microsoft.Office.Interop.Excel/16.0.18925.20022),
[Excel-DNA interop](https://www.nuget.org/packages/ExcelDna.Interop/16.0.0).
