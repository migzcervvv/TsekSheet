# Step 47 verification — 2026-10-29

## Starting state and environment

Started on `main`, commit `947ddea`, with all Step 46 modified/untracked files still present. The starting inventory is retained in `artifacts/step47/starting-status.txt`. No reset, cleanup, staging, commit or push occurred. Step 47 changes extend the existing dirty files; prior work, source fixtures and branding are preserved.

Verified .NET SDK 10.0.403, Node 24.22.1, existing local npm override 11.13.0, ClosedXML 0.105.1 and Open XML SDK 3.5.1. Dependency versions and lockfiles are unchanged. Host CPU: Intel Core i3-14100, four cores/eight logical processors; reported physical RAM 16,908,730,368 bytes. Windows x64, Release .NET 10 test host. Hardware enumeration initially failed inside the sandbox, then succeeded with approved read-only access. This is a development host under concurrent activity, not an isolated benchmark machine.

## Real accounting workbook status

Filename-only searches in the repository, attachments, Desktop, Documents and Downloads found no `copy.xlsx`. No real accounting workbook was opened, modified, uploaded, logged or added to a corpus. Real-world qualification remains **unexecuted**, with no claimed metadata/view/edit eligibility for that missing file. The [local desktop qualification procedure](read-only-cell-interpretation.md#capabilities-and-private-file-procedure) explains how to inspect an authorized file without opening Excel or exposing its data externally.

## Implemented scope

- Bounded lazy style reading and explicit raw/display/type/confidence/warning fields. Both date systems, fictitious serial 60, time fractions, date-time, invalid/negative/range cases and a small numeric-format subset have independent literal expectations.
- Separate shared-formula parser for qualified same-sheet arithmetic and SUM references. Relative/absolute/mixed transformations are tested independently; malformed/overlapping/incomplete groups and unsupported cross-sheet/function/spill syntax remain explicit controls.
- Row validation and an eight-window/2 MiB accounted LRU. No ZIP random-access row index is claimed. Uncached reads continue to scan only the selected worksheet.
- Existing viewer cell details show address, raw/display values, format, formula text and unverified cached values. Only inspection row-label width changes (48 to 76 pixels) to show distant row numbers. Layout, branding and existing workflows remain intact.
- Nine capability entries distinguish metadata, selection, browsing, editing, copies, ordinary Merge, structural insertion, structural Merge and export. Inspection grants only the first three within its budgets.

Exact formats, grammar, budgets and trust boundaries are documented in [read-only interpretation](read-only-cell-interpretation.md). Production Apply and every Step 45 review-only/adoption/export boundary remain unchanged. Source overwrite, Replace/Create New and autosave remain disabled.

## Verification results

| Check | Result |
| --- | --- |
| Locked solution/evaluation/native/managed restores | Passed |
| Normal Release solution build | Passed, zero warnings/errors |
| Full .NET solution | **340 passed**: Application 47, Workbooks 280, Desktop 13; zero failed/skipped |
| Inspection tests, including Step 46 cases | **72 passed**; 45 additional interpretation/navigation cases |
| Frontend typecheck/build/tests | Passed; **35 tests** |
| Toolchain tests | **3 passed** |
| Independent evaluation safety tests | **13 passed** |
| Native / managed commit harnesses | **600 / 390 passed**, zero failures |
| Self-contained win-x64 publish | Passed, runtime 10.0.14; `artifacts/step47-win-x64` |
| Source preservation | All 72 tracked XLSX blobs unchanged; retained manifest SHA-256 checks pass |
| Branding | Both SVG hashes unchanged; bundled assets verified |

Final logs/TRX and probe JSON are under `artifacts/step47`; the final complete rerun is under `final-confirmation`. One intermediate run timed out in the existing 22-point candidate-review test with "Copy/profile unavailable". Both candidate-review cases passed on an isolated recheck without code or timeout changes. This intermittent UI-test timing failure is retained as a test reliability limitation; its root cause was not established. Existing Step 45 native reference/oracle/Merge/export regressions ran with the solution. The research matrix remains 22 passed / 24 known failures / 26 unexecuted; its separate 72-case experiment was not rerun. Five evaluation-only ImageSharp advisories remain visible and unresolved. No dependency warning was suppressed.

The actual WPF/WebView2 test opens an artificial two-tab ZIP fixture through a native selector seam, checks interpreted 1900-03-01 against raw 61, reconstructs follower `A2+$A$1`, exposes cached 123 as possibly stale, displays an unsupported-format warning, selects hidden context and navigates to row 900000. It verifies close/reopen, logo visibility, no dirty state and disabled copy/export/planning controls. Screenshot: `tests/TsekSheet.Desktop.Tests/bin/Release/net10.0-windows/step47-interpretation.png`, visually inspected. This is not manually operated native-dialog testing. The published executable was not independently operated on a clean offline machine.

## Navigation measurements

Three repeated synthetic runs use 125,000 stored cells. Metadata includes streamed source hashing. First, sequential and distant reads each scan the complete selected sheet; returning to the first window hits the bounded cache. No index construction or lookup time exists because no row-location index is enabled.

| Operation | Observed elapsed milliseconds |
| --- | --- |
| Initial metadata | 0.969–1.393 |
| First viewport | 149.486–195.961 |
| Sequential window | 151.254–209.584 |
| Forward jump to row 124980 | 151.139–244.753 |
| Return to recently viewed first window | 0.009–0.014 |
| Session disposal | 0.035–0.054 |

Three uncached reads plus metadata allocated about 258.8 MB cumulatively per run. Observed process-lifetime peak working set was 189.1 MB. This includes fixture creation and the test host; it is **not an isolated scanner peak or hard heap bound**. An additional 100-sheet fixture with 1,000 cells per sheet measured metadata at 1.891 ms and ten distinct selected-sheet reads at 1.033–1.747 ms; process-lifetime peak was 193.7 MB. Exact run records remain in `step47-navigation.json` and `step47-many-sheet-navigation.json`.

Step 46's 125,000-cell scans measured 132.896–242.173 ms, with single-window identical-request hits below 0.227 ms. Step 47 adds interpretation/row/group validation and permits return to several recent windows. The current measurements do not prove a speedup for uncached scans: environment, warmup and concurrent processes affect timing. Linear scans remain the navigation boundary. Cancellation mid-scan, resource limits, cache eviction, repeated open/close, stale IDs and Windows writer exclusion are tested. Synthetic fixtures are not universal capacity claims.

## Fingerprints and compatibility findings

Retained inputs match their manifest:

- reimbursement-synthetic.xlsx: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- laboratory-synthetic.xlsx: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

New synthetic tests verify before/after hashes. The changed-file test intentionally changes only its owned synthetic fixture **after closing**, then proves reopening gets a different hash and rejects stale IDs. Attempts to acquire write access during inspection fail. No real source bytes are changed.

Date interpretation and shared-formula expansion now cover explicitly tested subsets. This does not change Excel rewrite compatibility: styles beyond the whitelist, ambiguous inheritance, arrays/dynamic spills, cross-sheet shared formulas, arbitrary currency/locale formats and unverified extensions remain unsupported. No new Microsoft Excel capture was performed; native equivalence is not claimed.

## Files and remaining work

Created: `InspectionCellFormatting.cs`, `InspectionSharedFormula.cs`, `InspectionInterpretationTests.cs`, artificial `tests/Fixtures/InspectionFixture.cs`, this report and `read-only-cell-interpretation.md`.

Extended: Step 46 `WorkbookInspection.cs` and `WorkbookInspectionTests.cs`; application `WorkbookContracts.cs`; frontend `WorkbookViewer.tsx`, its tests, `bridge/client.ts`, and one scoped CSS rule; desktop smoke tests/csproj (synthetic helper link only); PLANS.md, README.md, compatibility-analyzer and engine documentation. Other pre-existing dirty files are retained and are not attributed to Step 47.

Step 48 should qualify authorized real accounting files using sanitized technical evidence, expand independently tested style inheritance/date/currency and shared-formula coverage, and evaluate seekable staging/indexing only with explicit private-data lifecycle and isolated memory measurements. Manual native dialogs, clean-machine offline installation and maximum-capacity qualification remain open. Phase 2 is incomplete; production structural Apply remains disabled.
