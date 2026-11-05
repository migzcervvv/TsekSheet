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
| D11 | Excel exit latency is diagnostic, not acceptance. No owned EXCEL.EXE may remain alive 120 s after Quit, and no host start may find a previous owned instance alive. Only the dedicated lifecycle test waits for exit; other Excel tests rely on the start guard. Log latency without asserting it. The observed median 2.9 s and 2/30 exits near 60.2 s are attributed by the planner to an Office-internal 60 s wait, with no leak evidence. Production cleanup remains unchanged. |
| D12 | Amounts are written as entered; VAT conversion requires owner decision. |

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

Status: PROVISIONALLY ACCEPTED (synthetic; reopened T2 fixes merged via PR #5 at 41922aa; Phase 0 follow-up isolates the legacy bridge test from local profiles; final Release regressions: Excel 21/0/0, Application 632/0/0, Desktop 25/0/0, Workbooks 457/0/0, Frontend 85/0/0 passed/failed/skipped; EXCEL.EXE 0 before/0 after, 33 exits observed with none alive at 120 s; follow-up merge and resulting-main verification precede T2e; real-workbook validation remains manual)

- Prerequisites from T1 review:
  - D11 supersedes the T1/T2 ten- and thirty-second exit criteria: latency is logged,
    not asserted. Only the dedicated lifecycle test waits, with a 120 s deadline from
    Quit. Functional tests do not block on exit and rely on the start guard; no host
    start may find a previous owned instance alive. Production cleanup stays unchanged.
  - Allow writes into ListObject data rows created by `ListRows.Add` (T1 rejects table targets).
    Keep rejecting merged, array and spill targets.
  - Excel tests must clean up in code only. No GUI/computer-use or manual cleanup. Report the
    EXCEL.EXE count before and after two consecutive full Excel-suite runs.
  - Write the ADR for D10.
- Section definition in the workflow profile: sheetMatch header anchors (fixed sheet names
  remain supported for built-ins), marker-based data/subtotal boundaries, column map
  (field -> column letter), formula columns, subtotal row marker, insert mode
  `tableRow | entireRow | columnsOnly`.
  - `tableRow`: section is a ListObject -> `ListRows.Add`; Excel extends formulas and totals.
  - `entireRow`: stacked sections, nothing beside the section.
  - `columnsOnly`: side-by-side sections; shift only the section's columns
    (`Range.Insert(xlShiftDown, xlFormatFromLeftOrAbove)`).
- Insert position: first blank data row when fillBlankRowsFirst is enabled, otherwise
  directly above the subtotal row. Ignore formula columns when evaluating blankRowRule.
  **Known Excel behavior:** inserting
  directly above a total row does NOT extend `SUM(B3:B4)`. After inserting, for formula cells in
  that section's subtotal row only, extend A1 range references whose end row equals the old last
  data row to the new last data row. Use a real reference parser limited to A1 ranges on the
  same sheet; if a formula cannot be parsed, do not change it and report the cell as needing
  manual review (do not guess).
- Formula columns in new rows: fill from the row above via Excel (FillDown / AutoFill).
  Reused blank rows retain their existing formulas without FillDown or input writes.
- Bridge: `excel.listWorkbooks`, `excel.previewInsert`, `excel.insert`. UI: from a verified
  receipt, pick workbook + matching sheet + section, see the preview row, click Insert; inserted rows are
  highlighted in Excel and listed in the app. Unverified fields cannot be inserted.
- Backup before the first insert; preservation check after every insert (expected change set =
  inserted rows + shifted region + subtotal formulas).
- Reopened scope: Excel opening/insertion uses IExcelHost independently of legacy package
  admission. Package features never gate insertion. Reject protected target sheets,
  merged/array/spill targets, or unmatched profile layouts. Load local JSON profiles from
  %LOCALAPPDATA%/TsekSheet/profiles; support header anchors, marker-based sections,
  account-driven amount columns, formula/protected rows and blank-row-first placement.
  Let the user choose matching sheet and section at insert time. D12 applies.
- Replica acceptance (Excel-authored fake data, two matching sheets plus VENDOR, PageSetup):
  Expenses fills rows 29 then 30, then inserts above TOTAL; totals include all three,
  N/O/AF fill and grand total recalculates. Only the selected account receives the amount.
  Credits/grand-total structure, VENDOR and the other matching sheet are preserved.
  A hosted desktop test opens/inserts despite printer settings. Existing criteria 1-4
  remain; full Release Excel suite passes twice plus Application/Desktop/Frontend.
- Acceptance (Excel tests on Excel-authored fixtures with stacked sections, side-by-side
  sections and a ListObject section):
  1. Subtotals include the new rows; formula columns filled; values correct.
  2. Neighbouring sections unchanged in values/formulas (side-by-side ones not moved).
  3. Formula cells are never overwritten; a value starting with `=` is stored as text.
  4. 5 repeated inserts into the same section stay correct.
  5. Same checks pass on the redacted real workbooks in `fixtures/redacted/` (local run, report
     results; skipped in CI). If the folder is empty, report "not run: no redacted fixtures"
     and continue; this criterion is then re-run by the owner.
  6. D11: no owned EXCEL.EXE alive 120 s after Quit; no host start finds a previous owned
     instance alive. Only the dedicated lifecycle test waits for exit. Quit-to-exit
     latency is diagnostic data, not an assertion or acceptance threshold.

### T2b - Remove the superseded write path

Status: IN REVIEW (legacy write APIs/bridge/UI and their exclusive tests removed; evaluation projects archived; .NET 10 only; Release build 0 warnings/errors; final Application 498/0/0, Workbooks 188/0/0, Desktop 23/0/0, Excel 24/0/0, Frontend 70/0/0 passed/failed/skipped; hosted 20-receipt Excel flow passed; initial retirement-test and workbook-switch-wait failures corrected with results retained)

- Remove ClosedXML editing/candidate/structural code and their bridge commands
  (`workbook.createCopy/editCopy/discard/merge/previewMerge/generateCandidate/
approveCandidate/cancelCandidate/saveAsNew`) and tests that only cover them. Keep
  read-only inspection. Report removed line counts.
- Move the evaluation-only projects `tests/WorkbookLibrary.Evaluation`,
  `tests/WindowsFileCommit.Evaluation` and `tests/WindowsFileCommit.NativeEvaluation` to
  `experiments/archive/` (removes the ImageSharp advisories from the active build).
- .NET 10 only: remove obsolete target frameworks, generated outputs, retained
  pre-migration comparison/rollback files and outdated runtime references in docs/README.

### T2c - Verify all

Status: TODO (after T2 accepted)

- One button plus Ctrl+Enter verifies every non-empty field after a confirmation listing
  them; blocked if a required field is empty.

### T2d - Vendor lookup

Status: TODO (after T2 accepted)

- Typing/OCR of vendor name or TIN offers matches from the profile's vendorMaster sheet
  and fills name/TIN/address as unverified proposals.

### T2e - Fast Receipt Entry Loop

Status: DONE (T2 verification checkpoint satisfied; in-memory fast receipt loop implemented; 20-receipt synthetic UI/Excel acceptance passed; final Release regressions: Application 638/0/0, Workbooks 457/0/0, Desktop 26/0/0, Excel 23/0/0 in both full runs, Frontend 90/0/0 passed/failed/skipped; old/new measured overhead 41.866/41.339 s; real-workbook acceptance remains manual)

- Phase 0: PR #4 merged at 502de1c; PR #5 already integrated all five reopened T2
  commits through e4a4f96 into main at 41922aa. Their source trees are identical;
  no reopened commits need cherry-picking. Audit the changes and run regressions.
  A follow-up integration PR isolates a legacy synthetic bridge test from local
  profile configuration. Do not start T2e until that PR merges and main is verified.
- Branch from updated main: `t/T2e-fast-receipt-loop`. Use exti-liquidation as the
  reference; keep visible, changeable workbook identity/profile/sheet/section/account
  context across a multi-file PDF/image receipt queue with Next/Previous/Skip/Review.
- Keep document preview, fields, mapped row preview and Add Row connected. New receipts
  inherit the active context; successful insertion notifies and advances to the next
  pending receipt. Skipped receipts remain reviewable. Destination changes must not
  silently redirect pending data.
- Manual edits remain draft and OCR proposals unverified until explicit confirmation.
  Permit one explicit Add Row confirmation after valid required values and destination
  review. Block ambiguous totals, duplicates, stale targets and missing information
  with specific reasons. Preserve mandatory verification and formula protections.
- Preserve Expenses/Credits markers, N/O/AF formulas, account amount destinations,
  blank-row-first placement and insertion above subtotal. D12: no VAT conversion.
  Preserve VENDOR; no automatic save/vendor-master writes, unverified Undo, cloud,
  retained original uploads, OCR changes, persistence, legacy cleanup or mutation
  algorithm changes. Revalidate identity/placement immediately before each write.
- Before behavior changes, measure the executable old workflow. Compare 20 synthetic
  receipts before/after: workbook opening, context selection, receipt navigation,
  field entry/verification, preview, Excel insertion and post-insertion navigation.
  Report per-stage elapsed times, median and slowest; separate user-input time from
  application processing. No arbitrary one-second threshold.
- Acceptance: one destination setup supports 20 correct sequential inserts/accounts;
  automatic advance and skip/review work; destination changes are explicit; formulas,
  totals and neighbors remain correct; full affected tests pass with failures and
  unexecuted tests reported; timing report compares old/new overhead. Automated tests
  use synthetic fixtures; real-workbook acceptance is a separate manual step.

### T2e-FIX - Post-merge duplicate handling and preview performance

Status: DONE (respect explicit duplicate policy; preserve source-hash/restart-safe submission guards; instrument and remove redundant preview readback; median 0.861 -> 0.515 s on 20 synthetic previews; final Application 645/0/0, Workbooks 457/0/0, Desktop 26/0/0, Frontend 90/0/0, Excel 24/0/0 passed/failed/skipped; two full Release Excel runs executed, with an initial table-preservation mismatch not reproduced by the isolated three-mode rerun or final full run; checks unchanged, owner review of this risk remains)

Diagnostics status: IN REVIEW (fresh integration PR; failure snapshots include explicit ListObject range differences and an intentional synthetic preservation-difference test; original failure classification remains inconclusive).

Build hotfix status: IN REVIEW (`t/fix-excel-tests-build`; restore the diagnostic contract removed by merge 17eb02b; Release build 0 errors, Excel twice 25/0/0, Application 498/0/0, final Desktop 23/0/0 passed/failed/skipped; initial Desktop receipt-timing failure did not reproduce unchanged, cause unresolved).

### T3 - OCR bake-off (can run in parallel with T1/T2)

Status: SPLIT into T3a (synthetic harness) and T3b (redacted evaluation).

Split: T3a supplies the offline synthetic evaluation harness; T3b evaluates redacted
documents and supports the planner's later engine decision. No engine is selected by T3a.

### T3a - Offline OCR evaluation harness

Status: IN REVIEW (120 synthetic images, 1,440 comparisons, zero runtime errors;
OcrEval 24/0/0, Application 498/0/0, Desktop renderer 5/0/0 passed/failed/skipped;
summary in `docs/ocr-bakeoff.md`; no engine decision. Release solution build exposes
the unchanged Excel-test CS0815 recorded under Risks.)

- `tools/OcrEval`, .NET 10 Windows console; folder inputs PDF/PNG/JPG and
  `truth.csv` (`file,vendor,tin,invoice,date,amount`), usable on redacted inputs unchanged.
- Existing renderer: opt-in 300 DPI, higher embedded-image native resolution; preview unchanged.
- Generate 20 fake Philippine receipts with clean/blur/noise/skew 3–8 degrees/JPEG q20/
  low-contrast variants (120 images), truth and a vendor list.
- Tesseract 5 process, tessdata_best eng, OEM 1, PSM 3/6/11; PP-OCRv5 mobile
  detection/recognition ONNX on CPU. Local configured assets only; setup records sources,
  versions, hashes and licenses. Supervised workers; no product UI or Excel changes.
- Shared extraction rules and preprocessing: none; deskew/adaptive threshold;
  deskew/denoise/CLAHE. Report CER, field exact match, time and peak working set by
  engine/preprocess/variant, with WMI machine metadata.
- Run results under `artifacts/ocr-eval/<run>/`; one summary table in
  `docs/ocr-bakeoff.md`. Unit tests for extraction and CER require no models.
- Acceptance: both engines complete the synthetic evaluation offline, summary generated,
  model-free unit tests pass. Stop before T3b/T4; no engine decision.

### T3b - Real-document OCR evaluation

Status: IN PROGRESS (T3b-FIX on PR #14; prior extraction/scoring results superseded, not removed; private three-packet source review and fictional regressions complete, 47/0/0 model-free tests and Release harness build 0 warnings/errors; early partial 205-observation run stopped for header/multiple-body-row correction and retained; final corrected owner-set matrix running; engines/models/UI/Excel unchanged; no engine decision or T4).

- Extend the harness for BOM/RFC4180 packet truth: page classification/selection,
  cardinal orientation, own-TIN exclusion, labelled amounts, digits-only invoice comparison
  and report-only invoice/RFP/voucher/2307 cross-checks.
- Run Tesseract PSM6 (best T3a field accuracy) and Paddle v5 mobile across all three
  preprocessing profiles; report each field, page pick, correction count, time/memory and
  handwritten subsets separately. No engine decision.
- Append Real documents tables to `docs/ocr-bakeoff.md`; artifacts contain numeric
  metrics/fixed categories only. Never commit inputs, OCR text, values or crops. Stop before T4.

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
| intermittent preservation mismatch (1 occurrence, unreproduced; snapshots enabled) | Classification: inconclusive; retain snapshots on recurrence |
| Release solution build: existing Excel-test CS0815 at `SectionInsertTests.cs:99` | Present unchanged on main: `TablePreservationDiagnostics.WriteAsync` returns `Task`, but the test assigns its awaited result; outside T3a, no Excel changes made |
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
