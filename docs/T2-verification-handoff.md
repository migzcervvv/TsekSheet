# T2 post-merge verification handoff

Recorded 2026-11-04. Checkpoint branch: `t/T2-verification-handoff`.

## Completed

- PR #6 was merged by the owner: https://github.com/migzcervvv/TsekSheet/pull/6
- Pulled main with `--ff-only`; verified commit `066e939` is included in main
  `6ce24934dad77c26adf4943ea3667b2ebd40f849`. Remote main still matched at the final check.
- Locked solution restore and full Release solution build passed: 0 warnings, 0 errors.
- Final test counts, passed/failed/skipped: Application **632/0/0**, Workbooks
  **457/0/0**, Desktop **25/0/0**, Excel Release **21/0/0**, Frontend **85/0/0**.
- `%LOCALAPPDATA%\TsekSheet\profiles\exti-liquidation.profile.json` was installed
  during these runs (19 accounts). Synthetic tests pass with it present.
- Excel process count before/after: **0/0**; final count **0**. All 33 observed
  owned exits met D11; 33 startup guards found zero previous-owned violations.
- No environmental failures, failed attempts or reruns occurred in this verification.
  Earlier integration work did have build contention; those are separate runs.
- No production or test code changed. The working tree was clean on verified main.
  This checkpoint adds only this handoff and the T2e status update in PLANS.md.
- T2e implementation and its 20-receipt timing baseline have **not** started.

Raw logs remain local and ignored under `artifacts/T2-main-verification/`:
`restore.log`, `build.log`, `application.trx`, `workbooks.trx`, `desktop.trx`,
`frontend.json`, and `run1/run1.trx`, `run1/counts.json`, exit records and diagnostics.
These artifacts are not pushed. No real accounting workbooks or receipts were opened
or committed; insertion tests used Excel-authored synthetic fixtures.

## Resume

1. Read AGENTS.md, GOALS.md, PLANS.md and this file. Never read docs/planner/.
2. The owner's verification request is complete. Report success if continuing that
   request; stop there. T2e is authorized as the next ticket, not already implemented.
3. When continuing with T2e, update main and create `t/T2e-fast-receipt-loop` from
   updated main. This checkpoint branch contains documentation only.
4. Before changing workflow behavior, record the executable old-workflow baseline
   for 20 synthetic receipts. Separate scripted input/user-input time from application
   processing; report per-stage elapsed time, median and slowest, without arbitrary
   one-second thresholds. Do not substitute estimates for measurements.
5. Implement the T2e ticket in PLANS.md: retained visible/changeable destination and
   account context; multi-file queue; Next/Previous/Skip/Review; explicit confirmation
   and verification; success notification and automatic advance; stale/ambiguous/
   duplicate/missing-data rejection; Excel readback and repeated-insert preservation.
6. Use the exti-liquidation layout and synthetic fixtures. Keep Expenses/Credits,
   markers, N/O/AF, account-specific columns and blank-row-first placement. D12 means
   amounts as entered, never automatic VAT conversion. Real-workbook acceptance is
   separate manual validation; do not request or open the owner's original workbook.
7. No automatic save, vendor-master writes, unverified Undo, cloud, retained receipt
   uploads, OCR changes, persistence, legacy cleanup or mutation-algorithm changes.
8. Run affected suites, compare measured old/new timing, update PLANS/CHANGELOG,
   commit/push and open the T2e PR against main. Stop after implementation/verification.

Run .NET builds serially; parallel tests may use `--no-build --no-restore` after the
solution build. Keep Desktop/Excel suites serial so their Excel instances do not
overlap. Use Release, code-only cleanup and D11; no killing processes, GUI cleanup
or raised timeouts. Report final-run counts only and disclose environmental reruns.
