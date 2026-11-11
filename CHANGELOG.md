# Changelog

[README](README.md) · **Changelog**

- 2026-11-11 — T9: Separate document verification from versioned workbook bindings, validate named and Table destinations in Excel, and retain unbound Payables and EXTI compatibility.

- 2026-11-11 — T8a-BUSY: Keep receipt reads and PDF rendering available during Excel checks, sequence repeated Check actions, and revalidate queued insertions.

- 2026-11-07 — UX-03-FIX: Fit mixed-size PDF pages independently and preserve the visible reading position during page loading, zoom and viewport resizing.

- 2026-11-07 — UX-03: Add continuous, bounded local document pages, fit/native zoom and page-specific OCR region selection with contained scrolling.

- 2026-11-06 — UX-02-FIX: Keep manual Check independent of rendering/OCR and ignore late proposals from recognition started before Check; retain source-navigation invalidation and explicit insertion.

- 2026-11-06 — UX-02: Unify one-file and batch imports under Add receipts with Edit/Check states, authoritative Excel previews, protected drafts/destinations and uncertain submissions; retain legacy verification and placement under advanced tools.

- 2026-11-06 — UX-01: Open an explicit Excel-backed workbook workspace directly into Process Receipts, reuse its identity in the queue, retain legacy tools and advanced inspection findings, and block workspace switches that could lose or redirect pending work.

- 2026-11-06 — T4-FIX: Align product OCR with approved PP-OCRv5 Mobile/no preprocessing, retain Tesseract evaluation support without automatic fallback, and report recoverable local-asset errors.

- 2026-11-06 — T4: Connect supervised local OCR to receipt-queue drafts with source highlighting, cancellable page/region recognition and manual correction; preserve explicit verification, Excel insertion and save safeguards.

- 2026-11-05 — T3b-FIX: Separate page-local and independently verified packet expectations, preserve identifier letters/zeros, associate table amounts spatially without gross/net guessing, separate voucher/document references, correct notes/source cohorts and pipeline timing; retain superseded results, publish anonymous review counts, and report the owner-limited packets 1–17 with metrics-only subset summaries.
- 2026-11-05 — T3b: Evaluate private multi-page packets offline with page selection, cardinal orientation, own-TIN exclusion, separate labelled amounts and report-only cross-checks; persist metrics only and separate handwritten annotations; no engine decision or product UI changes.
- 2026-11-05 — Build hotfix: Restore the Excel test diagnostic writer's snapshot path and ListObject range details lost in merge 17eb02b; preserve assertions and production behavior.

- 2026-11-05 — T2e-FIX diagnostics: Retain preservation failure snapshots with explicit ListObject expected/actual ranges and verify attachment generation using a deliberate change to an Excel-authored synthetic fixture; original mismatch remains inconclusive.

- 2026-11-05 — T2e-FIX: Stop inferring item-number/vendor-invoice uniqueness without a profile policy, retain source-hash and restart-safe submission guards, instrument preview stages, and skip the demonstrated redundant duplicate readback while preserving all Excel safety checks.
- 2026-11-04 — T2e: Add a session-only receipt queue with retained explicit destinations/accounts, draft row previews, one Add Row verification confirmation, automatic advance and skip/review; reject duplicates/stale targets and preserve existing Excel write safeguards; measure the old/new 20-receipt workflows.
- 2026-11-04 — Documentation: Add README/Changelog navigation and summarize the supplied pre-reset development history.
- 2026-11-04 — T2 integration: Verify the reopened fixes already merged by PR #5; isolate the synthetic legacy bridge regression from locally installed profiles before starting T2e.
- 2026-11-04 — T2: Insert verified receipt rows through Excel independently of legacy admission; load local profiles with matching sheets, marker sections, blank-row fill and account-selected amounts as entered (D12), retain formulas/totals, backups/highlights/readback and D10/D11 safeguards; inspection snapshots release original file locks; production cleanup unchanged.
- 2026-11-03 — T1: Add a typed, embedded-interop Excel adapter with a serialized STA/message pump, bounded busy retries, visible isolated instances, bulk read/write, verified backups, restorable highlights, and Excel readback preservation checks.

## Earlier development — 2026-11-03 (historical)

Summarized from the owner's supplied earlier implementation roadmap. These notes
record earlier work and experimental limitations; they do not replace the current
ticket queue or acceptance criteria in [PLANS.md](PLANS.md).

- Connected local document registration, manual field correction, explicit human
  verification, worksheet mapping, section choices, duplicate warnings and
  placement proposals through WPF/WebView2/React.
- Added source-linked review and typed field projection with source fingerprints,
  revision checks and stale-review rejection. Proposals remained unverified until
  explicitly confirmed; preview results did not authorize workbook writes.
- Introduced the workbook-centered sidebar, Overview/Receipts/Worksheets navigation,
  retained in-memory review state, responsive layouts and light/dark/system themes.
- Connected local PDF/PNG/JPEG rendering through a supervised renderer worker,
  with page navigation/enlargement, scrollable task canvases and improved hover
  contrast. OCR remained unavailable during this work.
- Added read-only inspection fallback when legacy editable admission rejected a
  workbook, with structured compatibility findings, raw values and bounded display
  interpretation. Inspection did not qualify the legacy editing/export path.
- Ran the earlier isolated Excel-native preservation experiment. Gate A passed its
  measured lifecycle checks; Gate B did not pass preservation qualification because
  of font/theme/layout and package differences. Controlled font/theme runs retained
  the failed evidence; Gates C/D were not entered. These experimental gates were
  later superseded by the Excel-adapter direction and Excel readback checks.
