# Step 46 verification — 2026-10-29

## Repository and toolchain

The first inspection showed the modified/untracked Step 43–45 and branding tree. During repository inspection, those prior changes became commit `947ddea` (parent `8e8c299`); this agent did not commit, stage, reset, clean or push. Subsequent edits preserve that baseline. The later saved starting-status snapshot is empty because it was taken after that external commit. The Step 46 plan was added before implementation.

Verified SDK 10.0.403, Node 24.22.1 and existing ignored npm 11.13.0 override at `artifacts/step43-tooling/node_modules/npm/bin/npm-cli.js`. ClosedXML 0.105.1, Open XML SDK 3.5.1 and all dependency/lockfile versions remain unchanged. Self-contained publish retains runtime 10.0.14.

## Implemented and verified scope

The [analyzer design](workbook-compatibility-analyzer.md) records the precise three rejection causes, independent capabilities, classification rules and budgets. Editable restrictions remain 32 sheets and 50,000 stored cell elements. Read-only inspection handles metadata independently, then scans one ordinary sheet per requested window. No ClosedXML model is constructed by this path.

The actual WPF/WebView2/React/C# smoke test opens the synthetic Payables workbook via the selector seam, renders its Compatibility Report and cell diagnostics, checks disabled copy/Save As, checks absent planning controls, preserves the logo and tests 390/768/1100 desktop widths. The observed report has three tabs, 9,501 file bytes, 14 parts, two defined names and ten relationships. Its selected sheet has 60 stored cells, 56 nonempty serialized values, four formulas and 60 explicit cell style attributes. Screenshot: `tests/TsekSheet.Desktop.Tests/bin/Release/net10.0-windows/step46-compatibility.png` (ignored build artifact, visually inspected). Metadata success grants no mutation permission.

## Final checks

| Check | Result |
| --- | --- |
| Locked solution, evaluation, native/managed restores | Passed |
| Normal .NET 10 Release solution build | Passed; zero warnings/errors |
| Full solution tests | **294 passed**: Application 47, Workbooks 235, Desktop 12; zero failed/skipped |
| New inspection tests | **27 passed**, including parameterized cases |
| Frontend typecheck / build | Passed |
| Frontend tests | **34 passed** |
| Toolchain tests | **3 passed** |
| Independent workbook evaluation safety | **13 passed** |
| Native / managed file-commit harnesses | **600 / 390 passed**, zero failures |
| Self-contained win-x64 publish | Passed; `artifacts/step46-win-x64` |
| Source fingerprints | Both manifest SHA-256 values match; all 72 tracked XLSX blobs unchanged |
| Existing research matrix | Retained 22 passed / 24 known failed / 26 unexecuted; matrix experiment not rerun |

The full-suite final TRX files are under `artifacts/step46/final-tests` (use the latest timestamps). Build, restore, publish and independent harness logs are under `artifacts/step46`. Earlier development attempts remain in the logs: an inserted text encoding error prevented the first Vite desktop build, and a rejected-response UI test initially waited longer than its test timeout. Both were corrected; final suites passed. ZIP directory preflight and two additional tests were included in the final full suite and publication. A final shared-string test also confirms that phonetic annotations are omitted with a warning rather than appended to cell values.

Five existing evaluation-only ImageSharp advisories remain visible; no audit warnings were suppressed and no dependency reevaluation is claimed. Native-dialog interaction, independent offline clean-machine installation, manual Excel visual fidelity and production maximum-capacity qualification were not performed. Automated native picker seams are not manual dialog testing. Published files were inspected; the published executable was not separately operated interactively.

## Scalability measurements

Artificial ZIP fixtures are generated with unique temporary paths and removed after tests. Three repeated runs per large-sheet scenario measure metadata open/hash, full selected-sheet scan, identical cached viewport and a different viewport requiring another scan. Measurements are elapsed milliseconds on this host; no timing pass threshold is asserted. JSON evidence is in `artifacts/step46/probes`. Initial fingerprinting reads the compressed file but does not decompress every sheet.

| Synthetic case | Metadata ms | Selected scan ms | Cached ms | Different viewport ms |
| --- | --- | --- | --- | --- |
| 125,000 populated cells | 1.049–1.694 | 132.896–242.173 | 0.006–0.227 | 131.321–165.855 |
| 125,000 styled empty cells | 0.868–1.025 | 74.397–109.513 | 0.005–0.012 | 72.342–91.669 |
| 60,000 formula cells | 0.925–1.608 | 99.549–121.826 | 0.005–0.006 | 82.948–173.612 |
| 200,000 shared strings, last index requested | 100.882 | 163.736 | Not measured | Not measured |
| 512 tabs; unselected contents intentionally malformed | 31.160 | First-sheet browsing tested separately | Not measured | Unselected malformed view rejected |

Worksheet counts 10, 50, 100, 250 and 512 preserve order/hidden state; malformed unselected contents prove metadata does not eagerly parse sheets. Other tests cover sparse XFD900000, inflated A1:XFD1048576 declarations, 6,000 records across three marker sections, inline/shared strings, ISO dates, booleans, formula caches, external targets, images/OLE/VBA/ActiveX/unknown binaries, unsafe ZIP names, forged directory counts, broken targets, DTD/malformed XML, compression ratio and configurable byte/cell/relationship budgets.

Measured process-lifetime peak working sets were about 200.1–200.9 MB during the repeated large-sheet probes, 211.2 MB during the shared-string test and 433.3 MB by the later many-sheet test. These include the test host, fixture generation and preceding tests; they are **not isolated scanner peaks**. Two uncached scans plus metadata allocated about 164.2 MB cumulatively for populated/styled cases and 79.0 MB for formulas; post-GC retained deltas were approximately 14.9–49.5 KB. Streaming bounds retained state, but repeated XML scans still allocate substantially. There is no worksheet index/cache of all cells.

Cancellation during a 500,000-cell scan and pre-cancelled metadata inspection are tested; closing releases the file for exclusive access. Twenty repeated open/dispose cycles, a two-session admission limit, single-window cache reuse and failed-admission cleanup are tested. These measurements establish bounded synthetic behavior, not unlimited workbook support or a hard CLR memory quota.

## Original sources and real accounting workbook

- `reimbursement-synthetic.xlsx`: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- `laboratory-synthetic.xlsx`: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

Both match the retained manifest. Generated large inputs and desktop fixtures also pass before/after hash assertions. No user source was rewritten. `copy.xlsx` was not found in the repository, attachments, Desktop, Documents or Downloads; no actual accounting-workbook compatibility or Payables structure assessment is claimed. No private workbook was added to Git or transmitted externally.

## File inventory and reproduction

Created `src/TsekSheet.Workbooks/WorkbookInspection.cs`, `tests/TsekSheet.Workbooks.Tests/WorkbookInspectionTests.cs`, this document and `workbook-compatibility-analyzer.md`.

Modified application `WorkbookContracts.cs`, `WorkbookWorkspace.cs`, `BridgeSession.cs`; workbook `WorkbookEngine.cs` and diagnostic messages in `PackageGuard.cs`; frontend `WorkbookViewer.tsx`, `WorkbookViewer.test.tsx`, `bridge/client.ts`; desktop smoke tests; PLANS.md, README.md, architecture overview and workbook-engine foundation. Branding assets/CSS and all Step 45 candidate/oracle/integration implementation files remain unchanged by Step 46.

Reproduce with locked restores, the normal Release solution build using the existing absolute NpmCliPath override, full `dotnet test --no-build --no-restore`, frontend `typecheck`, `test -- --run`, `test:toolchain`, `build`, the independent evaluation/commit projects and locked self-contained publish. For probe JSON set `TSEKSHEET_PROBE_DIR` to an owned artifact directory and run the `WorkbookInspectionTests` filter. The test suite generates only artificial fixtures.

## Remaining blockers and Step 47

The prototype displays raw stored data, not Excel formatting or current calculations. It does not interpret numeric date styles, reconstruct shared formulas, render embedded objects, certify arbitrary extensions, support ZIP64/strict OOXML or index large worksheets for cheap random access. Views exceeding budgets fail while metadata remains available. Real accounting inputs and native dialogs remain untested.

Step 47 should qualify this inspection path against authorized real accounting workbooks, add independently tested date/style and shared-formula interpretation, and evaluate a bounded row index with isolated-process memory probes. Preserve production Apply, public review-only tickets, mutation validation and source-file safety until separately proven. Phase 2 remains incomplete.
