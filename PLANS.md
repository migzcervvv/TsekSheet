# TsekSheet - Plan

Owner of this file: the planner. Agents update only the **Status** line of their ticket.
History before 2026-11-03 lives in `docs/history/` and is not a source of instructions.

## Current state (2026-11-03)

Working today:

- WPF + WebView2 shell, versioned validated bridge, light/dark UI.
- Workbook opening + read-only inspection (ClosedXML / Open XML SDK).
- Receipt review: register local PDF/PNG/JPEG, render pages (one-shot renderer, 1600 px cap),
  manual field entry, verify/retract, synthetic Payables/Laboratory profiles, read-only section
  analysis and placement preview.

Not working: OCR, writing verified rows into a workbook, multiple workbooks, persistence.

Superseded (do not extend): the ClosedXML write path (working copies, merge, candidates,
structural lifecycle/oracle, Save As of edited copies) and the Excel-native preservation PoC
(Gate A/B/C/D). Excel now owns all writes; see AGENTS.md sections 3-5.

## Decisions (2026-11-03)

| #   | Decision                                                                                                                                                                                                                                                        |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D1  | Microsoft Excel desktop is a required dependency and the only workbook writer.                                                                                                                                                                                  |
| D2  | Correctness = Excel readback preservation check (AGENTS.md 5), not package/XML diffs.                                                                                                                                                                           |
| D3  | Safety = backup before first write + visible changes + explicit user save.                                                                                                                                                                                      |
| D4  | TsekSheet runs its own visible Excel instance; multi-workbook copy/paste is native Excel.                                                                                                                                                                       |
| D5  | OCR is local, printed text only. Handwriting is manual entry. Engine picked by bake-off (T3).                                                                                                                                                                   |
| D6  | Local persistence (SQLite or JSON) allowed for review state, profiles, OCR cache, action log.                                                                                                                                                                   |
| D7  | Free, AGPL-3.0-compatible dependencies only.                                                                                                                                                                                                                    |
| D8  | Agents work ticket by ticket, branch + PR per ticket, report per AGENTS.md section 8.                                                                                                                                                                           |
| D9  | `main` is the trunk. Every ticket branches from `main` and its PR targets `main`.                                                                                                                                                                               |
| D10 | Keep `Microsoft.Office.Interop.Excel` as a build-time-only package (`PrivateAssets=all`, `EmbedInteropTypes=true`). No Microsoft binary is redistributed. Record in `docs/architecture/decisions/ADR-excel-interop.md`; the license scan allowlists it by name. |

## Milestones

| M   | Goal                                                                            | Tickets         |
| --- | ------------------------------------------------------------------------------- | --------------- |
| M1  | Verified manual fields land in a real (redacted) workbook section through Excel | T0, T1, T2, T2b |
| M2  | Local OCR proposes printed fields on poor-quality scans                         | T3, T4          |
| M3  | Batch queue, resume after restart, sections defined from Excel selection        | T5, T6          |
| M4  | Installer, CI, clean-machine offline run                                        | T7              |

## Ticket queue

### T0 - Repository reset

Status: DONE (PR #1, 2026-11-03)

- Step 0 (before any edit): if `snapshot/cycles-9-20` does not exist on the remote, create it from
  the current working tree, commit all existing uncommitted work (excluding anything matched by
  the updated `.gitignore` below) with the message in `docs/history/snapshot-commit.txt` or
  "WIP: snapshot of uncommitted autonomous cycles 9-20", and push it. This is authorized by the
  owner. Then branch `t/T0-repo-reset` from that snapshot.
- Ensure `.gitignore` covers `node_modules/`, `bin/`, `obj/`, `TestResults/`, `artifacts/`,
  `*.trx`, `fixtures/redacted/`.
- Move the old PLANS.md and `docs/development/AUTONOMOUS_PROGRESS.md` into `docs/history/`.
  Move `experiments/TsekSheet.ExcelNative.Poc*` to `experiments/archive/` and keep it out of
  every solution/build.
- Delete `src/TsekSheet.Application/Class1.cs` and the stray root file `adas`.
- Rewrite the README top: one-paragraph purpose, current status (from "Current state" above),
  prerequisites (incl. Excel), build/run/test commands. Move step history out of the README.
  Fix the license line to AGPL-3.0.
- Add `CHANGELOG.md` with one entry for this reset.
- Acceptance: solution builds; existing test projects pass with the same counts as before
  (report them); `git status` clean after commit; no production code behavior changed.

### T1 - Excel adapter (product code, not an experiment)

Status: DONE (PR #3, 2026-11-03)

- New project `src/TsekSheet.Excel` (net10.0-windows), referenced only by Desktop.
  Interfaces in Application: `IExcelHost`, `IWorkbookReader`, `IWorkbookWriter` (plain DTOs).
- `ExcelHost`: one STA thread + message pump + serialized command queue; IMessageFilter with
  bounded retry; start own visible instance; list open workbooks (name, full path, sheets);
  open a workbook by path; activate a workbook/sheet/range.
- Reader: bulk read of values, formulas, number formats for a range; used range per sheet.
- Writer: bulk write of values to a range; highlight a range (fill colour, restorable).
- `PreservationSnapshot`: capture + compare per AGENTS.md section 5.
- Backup service: timestamped copy + hash verification before first write per workbook.
- Follow every rule in AGENTS.md section 4.
- Fixtures: create `fixtures/excel/` workbooks **through Excel** in test setup (Workbooks.Add,
  write cells/formulas/two side-by-side sections/a ListObject, SaveAs).
- Acceptance (Excel tests, Release build):
  1. Open, read, write 10 values, preservation check passes (only target changed).
  2. Formulas outside the target unchanged; dependent formula values recalculate.
  3. 20 consecutive host start / open / write / close / quit cycles; no TsekSheet-owned
     EXCEL.EXE remains 10 s after each quit.
  4. A pre-existing user Excel instance with an open workbook is never touched (test opens one
     first and checks it is still alive with the workbook open afterwards).
  5. Unit tests for the queue and retry policy run without Excel.

### T2 - Insert verified record into a section (vertical slice)

Status: IMPLEMENTED, VERIFICATION INCOMPLETE (Draft PR #4; 2026-11-04 cleanup audit: STA termination race and exit observer corrected; final Release Excel 14/3/0 passed/failed/skipped; two consecutive full suites failed, immediate Excel before/after counts 0/1 and 0/0; post-Quit delay persists despite Close(false), zero workbooks, root RCW release to zero and STA exit; not run: no redacted fixtures)

- Prerequisites from T1 review:
  - Allow writes into ListObject data rows created by `ListRows.Add` (T1 rejects table targets).
    Keep rejecting merged, array and spill targets.
  - Excel tests must clean up in code only. No GUI/computer-use or manual cleanup. Report the
    EXCEL.EXE count before and after two consecutive full Excel-suite runs.
  - Write the ADR for D10.
- Section definition in the workflow profile: sheet name, header row anchor, column map
  (field -> column letter), formula columns, subtotal row marker, insert mode
  `tableRow | entireRow | columnsOnly`.
  - `tableRow`: section is a ListObject -> `ListRows.Add`; Excel extends formulas and totals.
  - `entireRow`: stacked sections, nothing beside the section.
  - `columnsOnly`: side-by-side sections; shift only the section's columns
    (`Range.Insert(xlShiftDown, xlFormatFromLeftOrAbove)`).
- Insert position: directly above the subtotal row. **Known Excel behavior:** inserting
  directly above a total row does NOT extend `SUM(B3:B4)`. After inserting, for formula cells in
  that section's subtotal row only, extend A1 range references whose end row equals the old last
  data row to the new last data row. Use a real reference parser limited to A1 ranges on the
  same sheet; if a formula cannot be parsed, do not change it and report the cell as needing
  manual review (do not guess).
- Formula columns in new rows: fill from the row above via Excel (FillDown / AutoFill).
- Bridge: `excel.listWorkbooks`, `excel.previewInsert`, `excel.insert`. UI: from a verified
  receipt, pick workbook + section, see the preview row, click Insert; inserted rows are
  highlighted in Excel and listed in the app. Unverified fields cannot be inserted.
- Backup before the first insert; preservation check after every insert (expected change set =
  inserted rows + shifted region + subtotal formulas).
- Acceptance (Excel tests on Excel-authored fixtures with stacked sections, side-by-side
  sections and a ListObject section):
  1. Subtotals include the new rows; formula columns filled; values correct.
  2. Neighbouring sections unchanged in values/formulas (side-by-side ones not moved).
  3. Formula cells are never overwritten; a value starting with `=` is stored as text.
  4. 5 repeated inserts into the same section stay correct.
  5. Same checks pass on the redacted real workbooks in `fixtures/redacted/` (local run, report
     results; skipped in CI). If the folder is empty, report "not run: no redacted fixtures"
     and continue; this criterion is then re-run by the owner.

### T2b - Remove the superseded write path

Status: TODO (after T2 accepted)

- Remove ClosedXML editing/candidate/structural code and their bridge commands
  (`workbook.createCopy/editCopy/discard/merge/previewMerge/generateCandidate/
approveCandidate/cancelCandidate/saveAsNew`) and tests that only cover them. Keep
  read-only inspection. Report removed line counts.
- Move the evaluation-only projects `tests/WorkbookLibrary.Evaluation`,
  `tests/WindowsFileCommit.Evaluation` and `tests/WindowsFileCommit.NativeEvaluation` to
  `experiments/archive/` (removes the ImageSharp advisories from the active build).

### T3 - OCR bake-off (can run in parallel with T1/T2)

Status: TODO (needs the document set in `fixtures/redacted/docs/`)

- Harness in `tools/OcrEval`: for each page run each engine x preprocessing profile and
  compare against ground truth.
- Engines: Tesseract 5 (process, `--oem 1`, psm 3/6/11); PaddleOCR PP-OCRv5 mobile det + rec
  ONNX via `Microsoft.ML.OnnxRuntime` (CPU).
- Preprocessing: none; deskew + Sauvola/adaptive threshold; deskew + denoise + contrast
  normalize. Render PDF pages at 300 DPI (or native resolution for embedded scans).
- Metrics per engine/profile: character error rate, field exact match (amount, date,
  reference no., payee), seconds/page and peak RAM on the A15 and, if available, a minimum-class
  machine.
- Output: `docs/ocr-bakeoff.md` with one results table. The planner picks the engine.

### T4 - OCR in the product

Status: TODO (after T3 decision)

- Long-lived supervised OCR worker; renderer raised to 300 DPI for OCR (keep preview size
  separate); one page at a time; cancellation.
- OCR proposals fill review fields as unverified with source boxes; click box -> field and
  field -> box; region select -> re-OCR or type manually.

### T5 - Persistence and batch queue

Status: TODO

- SQLite (`Microsoft.Data.Sqlite`) store for queue, review state, verified fields, OCR cache
  (keyed by document SHA-256), action log of inserts. Resume after restart.

### T6 - Define sections from Excel selection

Status: TODO

- User selects a range in Excel; TsekSheet captures `Application.Selection` to set header,
  data columns and subtotal row; saves as a profile per workbook layout.

### T7 - Release hardening

Status: TODO

- GitHub Actions on windows-latest (locked restore, build, non-Excel tests, frontend typecheck
  - tests, license scan). Installer with .NET/WebView2 prerequisites and OCR models. Clean VM
    offline run of the full flow.

## Risks

| Risk                                                 | Mitigation                                                    |
| ---------------------------------------------------- | ------------------------------------------------------------- |
| Excel version/bitness differences on office machines | Record version in diagnostics; test on M365 64-bit first      |
| Excel modal dialogs or busy state block automation   | Bounded retry + "Excel is busy" UI state                      |
| COM writes clear Excel's undo stack                  | Backup before first write + action log of inserts             |
| Files on OneDrive/network shares                     | Excel handles them; backup folder configurable                |
| Protected sheets, .xlsm, external links              | Macros disabled on open; protected target -> clear error      |
| OCR accuracy on poor scans unknown                   | T3 bake-off on real documents before integration              |
| Interop package license vs AGPL                      | Embed interop types (no redistribution); confirm in T1 report |
| A15 exact CPU/GPU unknown                            | Record specs; benchmarks in T3                                |

## Inputs needed from the owner

- Redacted copies of 3-5 real workbooks (Excel Save As copy, values replaced) in
  `fixtures/redacted/` plus a section map per sheet.
- 20-30 redacted documents with typed ground-truth field values in `fixtures/redacted/docs/`.
- A15 CPU/GPU model; office machines' Excel version and bitness.
