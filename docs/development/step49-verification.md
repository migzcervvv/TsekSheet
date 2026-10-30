# Step 49 verification

## Baseline and private qualification

Started on dirty `main` at `947ddea`, preserving all prior Step 46–48 modified/untracked files. Status is in `artifacts/step49/starting-status.txt`. Verified SDK 10.0.403, Node 24.22.1 and local npm 11.13.0. No dependency upgrades, lock changes, cleanup, commit or push.

The explicitly authorized `local-test-workbooks/` directory is absent. No unrelated location was searched and no private workbook was opened or copied. Real metadata/view/date/amount/vendor-section discovery, editable-session/copy/insertion/Merge/export qualification therefore all remain **unexecuted**, with no inferred eligibility. Real Payables structural blockers cannot be reported without an input. Existing generic profile behavior is covered only by synthetic regressions.

To supply a future input, place an explicitly authorized regular local copy in `local-test-workbooks/`. Verify each path with `git check-ignore` and reject reparse points, tracked paths or escapes before inspection. Use **Inspect workbook read-only**, select representative sheets/rows, retain only technical diagnostics and before/after hashes in ignored local artifacts. Do not automate Excel on private files. This milestone creates no private report, sidecar, raw-data cache or private screenshot.

## Native references and correction

[Accounting display fidelity](accounting-display-fidelity.md) records exact commands, provenance and limitations. Actual Excel **16.0 build 20326** captured 30 cases across two date systems. All native Text observations match predeclared literals. TsekSheet now matches 28 exactly and explicitly rejects the two locale-format controls. The proven gaps were escaped saved format punctuation and built-in percentages; the correction retains raw source format/value information. No formula grammar, calculation or editing support expanded.

Initial capture attempts failed because PowerShell COM setters cached numeric argument types for subsequent text values. Explicit reflection-based Value2 assignment corrected capture; failures produced no accepted references. One overlapping test build encountered a locked testhost DLL; the later sequential rebuild/recheck passed. Neither is hidden as native compatibility evidence.

## Verification

| Check | Result |
| --- | --- |
| Locked solution/evaluation/native/managed restores | Passed |
| Normal Release solution build | Passed |
| Full .NET solution | 358 tests: Application 47, Workbooks 298, Desktop 13 |
| New native comparison | One test checks 30 native observations, fingerprints, 28 matches and two rejections |
| Frontend typecheck/build/tests | Passed; 35 tests |
| Toolchain / evaluation safety | 3 / 13 passed |
| Native / managed commit harnesses | 600 / 390 passed |
| Self-contained publish | Windows x64, runtime 10.0.14, `artifacts/step49-win-x64` |

Latest logs/TRX and bounded probe JSON are in `artifacts/step49`; final whole-solution results are in `confirmation`, and the post-readiness-fix desktop run is in `desktop-final`. Actual WPF/WebView2 selector-seam tests cover metadata, dates, formulas/warnings, hidden-sheet navigation, distant rows, close/reopen, logo visibility and unavailable mutation controls. Manual native-dialog and clean-machine offline tests are unexecuted. The earlier candidate-review timeout recurred at working-copy creation. Inspection of the selector seam found that its enabled-button wait could precede viewport completion, followed by a non-atomic click on a disabled button or while a read was in progress. The seam now waits for an existing viewport and checks enabled state atomically with the click, using the original timeout. This addresses a concrete readiness race without changing product safety gates; broader timing reliability still requires observation. Private workbook qualification is not counted as a passing test.

The existing 125,000-cell populated, styled-empty and formula-heavy probes, 100-sheet navigation, 512-tab admission, sparse distant cells, shared-string, cancellation and stale-session regressions are rerun. For the 125,000-cell navigation probe, first windows took 192.9–433.9 ms, distant windows 151.9–640.1 ms and recent-cache hits 0.011–0.043 ms. Their process-lifetime peak memory and other current timings are retained in probe JSON; they include fixture/test-host costs and are not isolated scanner heap maxima. No faster index was introduced: compressed XML offsets remain unsuitable for ZIP seeking. The eight-window, 2 MiB accounted cache remains the only retained view cache; no private disk spooling exists.

## Preservation and scope

Existing tracked XLSX files, retained SHA-256 manifest inputs and logo hashes are checked. New native output hashes are asserted by the comparison test; generated native input hashes are checked around read-only capture. Only artificial outputs and capture evidence are added to test resources, not production assets.

Production Apply, Step 45 review-only tickets, inspection editing/copies/Merge/export, source replacement and autosave remain restricted. Editable limits remain 32 sheets/50,000 stored cells. The research matrix retains 22 passes/24 known failures/26 unexecuted; that experiment is not rerun. Five evaluation-only ImageSharp advisories remain unresolved and visible. Phase 2 is incomplete.

Created: `scripts/Capture-AccountingDisplays.ps1`, `NativeAccountingDisplayTests.cs`, synthetic `ReferenceCorpus/AccountingDisplay/` bundle, this report and `accounting-display-fidelity.md`. Extended: inspection formatting and built-in format mapping, desktop candidate-review selector readiness, workbook test content links, PLANS.md, README.md, interpretation/compatibility and Payables documentation. All other dirty work is preserved.

Step 50 should qualify explicitly supplied private copies and expand native negative/rounding/boundary controls before claiming broader financial fidelity. Diagnose candidate-review timing on recurrence; preserve conservative mutation and private-data boundaries.
