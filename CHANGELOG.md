# Changelog

[README](README.md) · **Changelog**

- 2026-11-05 — T3a: Add a local OCR evaluation console with synthetic Philippine receipts, supervised Tesseract/PP-OCRv5 CPU workers, shared preprocessing/extraction, opt-in high-resolution PDF rendering, model-free tests and reproducible accuracy/time/memory reports; no engine decision or product integration.

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
