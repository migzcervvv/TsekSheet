# Step 50 verification

## Baseline and real-file gate

Started on dirty `main` at `947ddea`; all existing Step 46–49 modifications and evidence are preserved. Status is recorded in ignored `artifacts/step50/starting-status.txt`. Verified SDK 10.0.403, Node 24.22.1 and existing local npm override 11.13.0. No dependencies/locks were upgraded; no commit, push or cleanup occurred.

The only designated private directory, `local-test-workbooks/`, is absent. No unrelated folders were searched. Real metadata, viewing, date/amount interpretation, section/vendor discovery, editable-session, copy, ordinary editing, structural insertion/Merge and export qualification are all **UNEXECUTED**, not passed. No actual Payables headings, vendors, subtotals or blockers can be reported. No private workbook was opened, copied, logged, uploaded, repaired or passed to Excel automation.

For a future qualification, supply an explicitly authorized regular copy inside the ignored directory. Verify its parent and individual path with `git check-ignore`, reject reparse points and escapes, hash it, use **Inspect workbook read-only**, record only technical findings in ignored private artifacts, close and rehash. Existing inspection does not authorize planner bypass or mutation. The approved business logic remains unchanged.

## Independent precision corpus

`scripts/Capture-PrecisionDisplays.ps1` creates only new owned synthetic inputs, accepts no input paths and follows the Step 49 capture procedure. Actual Excel **16.0 build 20326** captured 33 cases in each date system: **66 observations**. The new bundle is `ReferenceCorpus/PrecisionDisplay/`, containing output XLSX files and capture JSON with Text, Value2, NumberFormat, Formula, independent expected strings, input/output SHA-256 and provenance. Old Step 49 native files remain untouched.

The additions cover positive/negative half boundaries, 2/3/4 decimals, tiny amounts, rounded negative zero, comma grouping, percentage boundaries, just-below/above half values, peso zero blanks, four-section rejection and long leading-zero text. Formula cached amounts and both date systems remain covered by the seed cases. No numeric tolerance or display normalization is used in comparisons. Inputs are hash-checked around read-only reopen/capture; outputs are hash-bound by regression tests. Excel is quit through owned COM lifecycle; synthetic artifacts are intentionally retained for evidence.

The initial literal expectation for a tiny negative value was `-0.00`; actual Excel Text was `0.00`. This failed expectation is retained as `originalExpected` in capture JSON and in the failed test log. Corrected expectation follows the actual observation, not a claim that signed zero universally behaves this way.

Several native saved numeric values contain more than 15 significant digits even when captured Value2 appears shorter. Direct decimal formatting produced financially misleading half-boundary displays (for example native 1.01 versus inspection 1.00). The implementation now explicitly rejects non-General numeric display interpretation when the stored decimal exceeds 15 significant digits, retaining raw text and a warning. It does **not** round those values to 15 digits or infer a universal Excel conversion.

For the remaining supported numeric subset, display rounding is explicit decimal midpoint-away-from-zero at the requested decimal precision after percentage scaling. Negative-section selection still uses the original sign. Rounded zero in a one-section format has no negative sign. Percent detection includes parenthesized negative sections. Raw values, source format codes and formula caches are never rewritten.

Final new corpus result: **42 exact matches, 24 explicit rejections, zero supported divergences**. Rejections are 20 over-precision observations, two locale controls and two four-section controls. The retained Step 49 corpus now yields **26 exact matches / four rejections**, rather than its historical 28/2: two formerly accepted observations now fail the stricter over-precision policy. Evidence is unchanged, and the policy narrowing is explicit. Tests assert exact match/rejection counts and original hashes; rejecting everything cannot pass. This does not prove full Excel binary rounding equivalence.

## Verification

| Check | Final result |
| --- | --- |
| Locked solution/evaluation/native/managed restores | Passed |
| Normal .NET 10 Release build | Passed |
| Full solution tests | **359 passed**: Application 47, Workbooks 299, Desktop 13 |
| Native precision comparison | One added test exercises 66 native observations and fingerprints |
| Frontend typecheck/build/tests | Passed; **35 tests** |
| Toolchain / evaluation safety | **3 / 13 passed** |
| Native / managed commit harnesses | **600 / 390 passed** |
| Self-contained win-x64 publish | Runtime 10.0.14; `artifacts/step50-win-x64` |

Logs, failed development comparisons, final TRX and probe JSON are retained under `artifacts/step50`. Earlier negative results are not hidden. Actual WPF/WebView2 selector-seam tests cover raw/interpreted cells, formulas/warnings, hidden sheets, distant navigation, close/reopen, logos and disabled mutation controls. Manual native dialogs and clean-machine offline installation remain unexecuted. Candidate-review readiness reliability is monitored through the unchanged post-Step-49 seam.

The full suite reruns the 125,000 populated/styled-empty, 60,000-formula, 200,000 shared-string, 512-tab, 100-sheet navigation, sparse-row, cancellation, stale-session and adversarial package cases. Exact current elapsed/peak-memory measurements are in probe JSON. Measurements use the existing i3-14100/approximately 16 GB Windows development host and include fixture generation/test-host activity; process-lifetime peaks are not scanner heap maxima. No maximum-capacity or uncached-speedup claim is made. The eight-window, 2 MiB accounted cache, all package budgets and sequential ZIP reads remain unchanged; no disk-backed private-data index exists.

Existing tracked XLSX blobs and retained manifest SHA-256 values are checked unchanged; both logo hashes remain unchanged. New native outputs are synthetic, hash-bound test resources excluded from production publish. Real-source fingerprint checks are unexecuted because no real input exists.

## Scope and next milestone

Created `Capture-PrecisionDisplays.ps1`, `NativePrecisionDisplayTests.cs`, the synthetic precision bundle and this report. Extended numeric interpretation, existing native comparison policy assertions, workbook test content links, PLANS.md, README.md and interpretation/fidelity/compatibility/Payables documentation. Other dirty files predate this step and are preserved.

No UI redesign or workflow changes. Production Apply, Step 45 permanent review-only tickets, inspection editing/copies/Merge/export, Replace/Create New, source overwrite and autosave remain restricted. Editable limits remain 32 sheets/50,000 stored cells. The research matrix retains 22 passes/24 known failures/26 unexecuted; it was not rerun. Five evaluation-only ImageSharp advisories remain visible and unresolved. Phase 2 is incomplete.

Step 51 should qualify explicitly supplied private copies and expand native precision cases before replacing the conservative over-precision rejection. Any broader financial display policy requires independent literal/native controls; safe viewing remains separate from mutation eligibility.

Measured 125,000-cell navigation probe ranges (milliseconds except working-set bytes): metadataMs 1.0207–1.3917; firstMs 119.038–398.2995; distantMs 99.6964–397.0869; recentMs 0.0152–0.0238; processPeakWorkingSet 123777024–123863040.
