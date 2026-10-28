# Safe row insertion foundation — Step 42

Status (2026-10-29): profile-driven analysis, pure batch proposals and manual desktop preview are implemented and tested. Production structural insertion remains disabled until a dependency-aware candidate editor and an independent structural preservation oracle pass. Phase 2 is incomplete.

## Implementation plan

1. Preserve the .NET 10 working tree and security-review changes. Locate copy.xlsx read-only; use synthetic workbooks if absent. Never add private accounting inputs to Git.
2. Add a small versioned, strict JSON workflow-profile contract in Application, independent of WPF and workbook libraries. Include approved built-in synthetic payables and non-accounting profiles, bounded scans, header/section/field rules, conservative normalization/aliases, duplicate policies and mandatory review requirements.
3. Implement read-only worksheet capture and pure section analysis. Require unique headers/headings, a unique subtotal before the next section, bounded nonoverlapping regions and explainable record classification. Reject incomplete scans and ambiguous records/boundaries.
4. Implement pure vendor matching and batch insertion proposals. Search every recognized section, expose all multi-section choices, require explicit similar-name selection, flag duplicate identifiers without discarding records, freeze session/profile/revision identities and compute proposed row effects without modifying workbook bytes.
5. Investigate structural insertion against existing failed raw-insertion probes and scoped repaired-range experiments. Keep a backend rejection gate and disabled desktop Apply unless generic reference, range, merge, table and style preservation can be proved. Never replay shifted coordinates as ordinary Merge edits.
6. Add synthetic regression tests, including moved sections/subtotals, aliases, duplicates, batches, stale requests, cancellation, blocked insertion, source hashes, names/tables/merges and existing Merge/Save As invariants. Then connect manual profile-driven planning through the bounded bridge and React UI.
7. Run locked restores, normal .NET 10 Release build, all solution/evaluation/frontend/toolchain/native checks, publish and desktop smoke where supported. Document implemented versus blocked versus unexecuted behavior in the four Step 42 documents and roadmap.

## Structural safety boundary

The existing single-cell adapter validates permitted value/formula changes by masking only one stationary cell. It cannot validate a shifted sheet or prove correct formula/name/table expansion. The research harness's raw insertion leaves five declared boundaries wrong, and its successful repair uses fixture-specific coordinates/formulas. Neither is a generic structural editor. A planning proposal is not an executable or validated structural change.

The Step 42 matrix reproduces the raw ClosedXML failure: two subtotal formulas, a cross-sheet SUM, the `DataValues` defined range and `SyntheticTable1` extent omit the newly intended data row. The laboratory example reproduces the same five errors at different boundaries. Repeated insertion fails too. The scoped `insert-declared-ranges` probe succeeds only by explicitly repairing fixture-specific formulas/names/table ranges. It is tested research, not an enabled production operation.

## Implemented versus blocked

| Status | Scope |
| --- | --- |
| Implemented and enabled | Approved profile catalog, bounded section capture/analysis, exact/alias and explicit similar-entity selection, all-section lookup, duplicate warnings, pure 1–20 record batch proposals, desktop one-record manual preview |
| Implemented and tested rejection | Invalid/ambiguous/oversized analysis, stale profile/session revisions, cancellation, invalid fields/choices, blocked structural mutation including forged apply flags; no frontend apply command |
| Tested research only | Raw/repeated insertion failures and scoped fixture-specific range repair in the library matrix |
| Planned and unexecuted in production | Isolated structural candidate creation, general reference repair, row-template hygiene, serialize/reopen structural oracle, atomic row adoption, successful insertion followed by Merge/Save As |
| Unsupported | Structural insertion, worksheet Replace/Create New, existing-file replacement, OCR, arbitrary profile import, generic accounting tax/payment formulas, external/broken/ambiguous reference repair |

Existing `.xlsx` compatibility guards were not relaxed. Calculation-chain workbooks can be inspected/planned but remain uneditable when the guard rejects them. Synthetic supported fixtures explicitly omit the optional calculation chain; a separate rejection fixture retains it. Formula identity fields reject cached values; other formula context is marked unverified. Profiles are not allowed to request an enabled insertion policy.

## Candidate and adoption acceptance gate

No structural candidate is constructed in this milestone. The only insertion contract checks cancellation, session/worksheet/source identity, exact revision, approved profile hash and required reviews, then throws `ROW_INSERTION_DISABLED`. No flag or approval can adopt rows. Failure leaves sessions, copies, dirty flags and source bytes unchanged. The existing stationary-cell candidate pipeline remains unchanged.

A future bounded editor must freeze the same identities and section boundaries, construct an isolated candidate from in-memory bytes, insert using an explicit clean template, update every supported dependency, serialize/reopen, independently compare permitted shifts and unchanged content, and atomically adopt only after cancellation/revision/preservation checks pass. Do not mask whole changed sheets or accept all library output as expected. General cross-sheet/self-qualified references, named ranges, table totals/extents, merges, lower summary rows, styles, relationships and unsupported references need precise expected transformations and negative tests. Never copy a neighboring vendor's confidential values. Structural insertion cannot be represented as ordinary recorded-cell Merge until coordinate/reference semantics are independently designed.

## Verification on this PC

Starting branch `main`, commit `a711216be151b5d6caefa9304cb10ccf546dfeb1`; prior uncommitted Step 41 audit/documentation/lockfile changes preserved. No branch change, dependency upgrade, commit or push. SDK 10.0.403, .NET runtime 10.0.14, Windows x64, Node 24.16.0/npm 11.13.0. Project targets remain `net10.0` / `net10.0-windows`; WPF/WebView2 configuration and repaired PATH discovery are retained.

| Check | Result |
| --- | --- |
| Locked solution + three independent harness restores | Passed; evaluation retains its five visible ImageSharp advisories (3 high, 2 moderate), unchanged from Step 41 |
| Standard `dotnet build TsekSheet.sln -c Release --no-restore` | Passed, 0 warnings, 0 errors; runs locked npm installation, typecheck and production build |
| Full solution tests | 141 passed, 0 failed/skipped: Application 47, Workbooks 85, Desktop 9 |
| New Step 42 backend coverage | 31 Application cases and 11 actual-XLSX cases, including two shifted-boundary fixtures, profile/choice rejection, cancellation/concurrent change, copy/dirty/source preservation, Merge and new-file Save As regression |
| Independent evaluation safety tests | 13 passed, 0 failed/skipped |
| 72-case library research matrix | 22 passed, 24 known preservation failures, 26 unexecuted; 0 execution errors; unchanged outcomes and source bytes |
| Frontend typecheck / tests / toolchain tests / production build | Passed; 25 frontend tests, 3 toolchain tests, no failures |
| Native Windows no-overwrite commit harness | 600 passed, 0 failed |
| Managed file-commit harness | 390 passed, 0 failed |
| Self-contained win-x64 publish | Passed to `artifacts/step42-win-x64`, pinned runtime 10.0.14 and locked dependencies |
| Published hidden-process startup probe | Reached input idle and reported responding; hidden launch exposed no main-window handle, so normal-close/UI acceptance was not established and only the owned process was stopped |
| Actual WPF/WebView2 smoke | All 9 desktop tests passed; new workflow used real bundled React/C# host, explicit section choice, duplicate display, disabled Apply, clean state, source hash and 390/768/1100-width overflow checks |

Desktop tests use **selector/confirmation seams** instead of interacting with real native file dialogs. They exercise actual WPF/WebView2 and real workbook services, but are not manual desktop acceptance or native Excel testing. The published-process probe is startup evidence only, not a second workflow smoke. Clean-machine offline installation, manual native dialogs, accessibility review, maximum-size responsiveness and native Excel fidelity remain unverified.

An initial test-fixture run correctly failed copy/edit because ClosedXML generated a calculation chain; generator options were corrected and a specific guard-rejection test added. The first full build also found an implicit `System.IO` import missing when sharing the fixture with WPF tests; adding the explicit import fixed it. Final suites above have no unresolved regression failures. Research matrix failures and unexecuted cases remain visible and are not counted as passes.

Local test evidence: `artifacts/step42-tests/step42-verified_*.trx`, independent evaluation TRX in the same directory, and `artifacts/workbook-evaluation/20261028-163240-7af9e601/results.json`. Desktop captures include `tests/TsekSheet.Desktop.Tests/bin/Release/net10.0-windows/payables-planning.png`. Artifacts are local/ignored, not committed.

## Original fingerprints

Both retained synthetic originals match `tests/WorkbookLibrary.Evaluation/Fixtures/sha256.json` after verification:

| File | SHA-256 |
| --- | --- |
| reimbursement-synthetic.xlsx | `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308` |
| laboratory-synthetic.xlsx | `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00` |

New Payables fixtures are artificial and generated with `FileMode.CreateNew` in owned temporary directories. Their before/after hashes are checked by workbook and desktop tests, including failed/cancelled proposals and ordinary Merge/Save As. Original real accounting workbook fingerprint verification was not possible because `copy.xlsx` was unavailable. No real workbook was changed.

## Step 42 file inventory

Created:

- Application: `src/TsekSheet.Application/Workflows/WorkflowProfile.cs`, `WorksheetAnalysis.cs`, `WorkflowPlanning.cs`, `Profiles/synthetic-payables.workflow.json`, `Profiles/synthetic-laboratory.workflow.json`.
- Frontend: `src/TsekSheet.Frontend/src/PayablesProcessing.tsx`, `PayablesProcessing.test.tsx`, `bridge/workflows.ts`.
- Tests: `tests/Fixtures/PayablesFixture.cs`, `tests/TsekSheet.Application.Tests/WorkflowPlanningTests.cs`, `tests/TsekSheet.Workbooks.Tests/WorkflowWorkbookTests.cs`.
- Documentation: `docs/workflows/payables-processing.md`, `docs/development/workflow-profiles.md`, `worksheet-section-detection.md`, `safe-row-insertion.md`.

Modified for this milestone:

- `src/TsekSheet.Application/Foundation/BridgeSession.cs`, `Workbooks/WorkbookContracts.cs`, `Workbooks/WorkbookWorkspace.cs`, `TsekSheet.Application.csproj`.
- `src/TsekSheet.Frontend/src/WorkbookViewer.tsx`, `bridge/client.ts`, `styles.css`, `App.tsx` (correct the existing obsolete Saving-unavailable footer).
- `tests/TsekSheet.Desktop.Tests/DesktopSmokeTests.cs`, `TsekSheet.Desktop.Tests.csproj`, `tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj`.
- `README.md`, `PLANS.md`, `docs/development/worksheet-integration.md`.

Preserved pre-existing Step 41 changes in README/PLANS, `docs/development/dotnet-10-migration.md`, `toolchain.md`, `dependency-security-review.md`, `docs/research/evidence/dependency-security-review.json` and `tests/WorkbookLibrary.Evaluation/{README.md,WorkbookLibrary.Evaluation.csproj,packages.lock.json}`. No new package/SDK/target-framework migration was needed for Step 42. Temporary build/test/publish outputs are ignored local artifacts.

## Reproduce checks

Use the Windows CMD / VS Code .NET 10 setup in [toolchain.md](toolchain.md). From the repository root:

```cmd
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore
dotnet restore tests\WorkbookLibrary.Evaluation --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation -c Release --no-restore
dotnet run --project tests\WorkbookLibrary.Evaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.NativeEvaluation --locked-mode
dotnet run --project tests\WindowsFileCommit.NativeEvaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.Evaluation --locked-mode
dotnet run --project tests\WindowsFileCommit.Evaluation -c Release --no-restore
cd src\TsekSheet.Frontend
npm.cmd run typecheck
npm.cmd run test -- --run
npm.cmd run test:toolchain
npm.cmd run build
cd ..\..
dotnet publish src\TsekSheet.Desktop -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -p:RestoreLockedMode=true -o artifacts\step42-win-x64
```

Do not use the evaluation harness's `--generate` switch against retained originals. Do not run destructive experiments on a user workbook. Save As remains separate new-file-only, no-overwrite, and never automatic.
