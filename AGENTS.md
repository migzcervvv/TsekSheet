# TsekSheet - Agent Rules

TsekSheet is a local-first Windows desktop app (WPF + WebView2 + React/TypeScript + C#/.NET 10)
that turns scanned documents into human-verified rows in Microsoft Excel workbooks.
Read GOALS.md (what and why) and PLANS.md (current ticket queue) before any work.

## 1. How work is assigned

- Work only on the ticket you are given. The ticket text is the plan. Do not write a separate
  plan document unless the ticket says so.
- One ticket = one branch `t/<ticket-id>-<slug>` from `main` = one or more small commits = one PR
  targeting `main`.
- Commit at least once per ticket. Never end a session with uncommitted work.
- Do not start the next ticket on your own. Stop after the report.
- If the ticket is ambiguous, pick the simplest interpretation that meets its acceptance
  criteria and state the assumption in the report.

## 2. When to stop and when not to

Stop and report only when continuing would risk one of these:

- Changing or deleting a user's original file.
- Writing wrong data into a workbook (wrong cell, wrong row, wrong value, lost formula).
- Shipping a security hole (arbitrary path, shell injection, unvalidated bridge input).

Everything else is a **risk, not a blocker**. Record it in the report's Risks section and keep
going. Do not invent new gates, qualification phases, oracles or approval steps. Adding a new
blocking gate requires the planner's sign-off in the ticket.

## 3. Architecture rules

1. **Excel is the workbook engine.** All workbook writes go through the Excel adapter
   (`IWorkbookWriter` / Excel COM). Do not write new OOXML mutation code. ClosedXML/Open XML SDK
   stay for read-only inspection only.
2. **Excel owns file bytes.** Do not compare package bytes or XML to judge correctness.
   Correctness is judged by reading back through Excel (see section 5).
3. Business logic stays independent of WPF, WebView2 and COM. COM objects never leave the
   Excel adapter; it exchanges plain DTOs.
4. All Excel calls run on one dedicated STA thread with a message pump, through a serialized
   command queue. No COM calls from thread-pool threads.
5. OCR runs out of process in a supervised worker (Job Object, memory cap, timeout,
   cancellation, no network, no shell). OCR output is always an unverified proposal.
6. React never receives native file paths. Bridge messages stay versioned, origin-checked and
   validated.
7. Local only: no cloud services, no telemetry, no paid APIs, no hosted assets. Dependencies must
   be free and compatible with AGPL-3.0 (MIT, BSD, Apache-2.0, MPL-2.0, LGPL, GPLv3/AGPLv3).
8. Prefer fixing and extending existing code over rewriting it. Delete code only when a ticket
   says so.

## 4. Excel COM rules (mandatory)

- Use typed interop (`Microsoft.Office.Interop.Excel`, `EmbedInteropTypes=true`). No `dynamic`.
- No chained calls (`a.B.C`). Assign every COM object to a local and release it in `finally`
  in reverse creation order with `Marshal.ReleaseComObject`.
- No `foreach` over COM collections. Index with `for` and release each item.
- Do COM work inside its own method; after it returns call
  `GC.Collect(); GC.WaitForPendingFinalizers();` twice.
- Read and write ranges in bulk (`object[,]`), never cell by cell in loops.
- Wrap batches with ScreenUpdating/EnableEvents/Calculation changes and restore them in `finally`.
- Handle "application busy" rejections with a bounded retry (max 10 s) and then a user-visible
  "Excel is busy" state. Never retry forever.
- TsekSheet starts its own **visible** Excel instance (`Visible=true`, `UserControl=true`) and
  never attaches to, closes or kills an Excel instance it did not start. It never calls `Quit`
  while the user has workbooks open in that instance.
- Open files with `UpdateLinks=0` and `AutomationSecurity=msoAutomationSecurityForceDisable`.
- Run Excel integration tests in **Release** configuration.

## 5. Workbook safety rules

- Before the first write in a session, create a timestamped backup copy next to the original
  (or in the configured backup folder) and verify its hash.
- Writes happen in the workbook open in the user's visible Excel. Inserted/changed cells are
  highlighted and listed in the app.
- Saving the original happens only by explicit user action (user saves in Excel, or confirms
  Save in TsekSheet). Save As to a new file is always available.
- Never overwrite formula cells with OCR or manual values. Values starting with `=` are written
  as text unless the target column is declared a formula column.
- **Preservation check** (used in tests and after each write batch): read back through Excel,
  for every sheet, the used range values and formulas, number formats, merged areas,
  defined names, ListObject ranges, data-validation and conditional-format counts.
  Everything outside the intended target must be identical; the target must equal the intended
  values. This check replaces package/XML diffing.
- Real accounting workbooks are never opened by agents. Use files in `fixtures/excel/`
  (Excel-authored synthetic) and `fixtures/redacted/` (redacted real copies, gitignored, local
  only). Fixtures must be authored by Excel, never hand-generated OOXML.

## 6. Tests

- Test behavior the ticket asks for. Do not add tests for hypothetical scenarios the ticket did
  not mention.
- Unit tests must run without Excel. Excel-dependent tests use `[TestCategory("Excel")]` and
  are excluded from CI.
- Run the affected test projects before committing. Report exact pass/fail/skip counts from the
  final run only; mention sandbox/environment reruns in one line under Risks.
  Report any failing or skipped test; never hide or delete one to get green.

## 7. Documentation budget

- Do not create per-step verification docs, progress trackers or long status reports.
- Per ticket: one line in `CHANGELOG.md`, update `PLANS.md` ticket status, and update a doc
  only if behavior a user sees changed.
- ADRs only for decisions that change an architecture rule above.

## 8. Report format (max 30 lines)

```
Ticket: <id> <title>
Branch / PR: <branch> / <PR link or "not pushed">
Done: <what a user can now do, 1-3 lines>
Changed: <files or areas, 1-5 lines>
Tests: <project: passed/failed/skipped> per project
Acceptance: <each criterion: met / not met + one-line reason>
Assumptions: <if any>
Risks: <new risks found, one line each>
Next: <what the planner should decide, if anything>
Commit Message: (Only write if push is recommended)
```
