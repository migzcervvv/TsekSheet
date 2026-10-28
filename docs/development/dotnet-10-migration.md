# .NET 10 migration and environment repair

Status: **Step 40 .NET 10 migration complete for the existing supported application scope**. Stable SDK **10.0.403**, Core/WindowsDesktop **10.0.14**, standard Release build, required regression suites, self-contained publish and source-integrity checks pass. Existing uncommitted Merge and environment-repair work is preserved. Phase 2 and product-release acceptance remain incomplete; no spreadsheet feature was added.

Subsequent [Step 41 security review](dependency-security-review.md) patches the evaluation-only Xml/Pkcs servicing versions and reduces the 19 advisory findings recorded below to five unresolved ImageSharp findings. The original Step 40 counts/lock descriptions below are historical migration evidence. The full matrix remains unchanged and all regression/publish checks pass after that patch; no production package or target changed in Step 41.

## Step 40 implementation and verification

The user installed the SDK. Inspection confirmed 10.0.403 under `C:\Program Files\dotnet\sdk` and Core/WindowsDesktop runtime 10.0.14 alongside .NET 8. Before touching targets, the standard .NET 8 Release solution build was rerun successfully: 0 warnings, 0 errors, normal frontend target, no diagnostic override. Branch remains `main`, starting/latest commit `c828394a580be35cb7065d5b82ac4557765e1688`. At Step 40 start the working tree had 20 tracked modified and 5 untracked files from Merge and environment repair; none was reset/discarded.

The existing six-step plan below was followed: preserve the baseline, migrate pin/targets, regenerate and review lock graphs, require locked restores, run core/native/frontend/desktop regressions, publish and verify original hashes. A local rollback snapshot of all **19** pre-migration SDK/project/lock files is retained at `artifacts/step40-pre-migration`, including the repaired Desktop configuration. It is ignored build evidence, not a new commit or replacement for source control.

### SDK, targets and preserved settings

`global.json` pins installed stable **10.0.403** with `rollForward: disable`. The two Windows projects below move to `net10.0-windows`; the other seven move to `net10.0`:

| Project | Current target |
| --- | --- |
| src/TsekSheet.Application | net10.0 |
| src/TsekSheet.Workbooks | net10.0 |
| src/TsekSheet.Desktop | net10.0-windows |
| tests/TsekSheet.Application.Tests | net10.0 |
| tests/TsekSheet.Workbooks.Tests | net10.0 |
| tests/TsekSheet.Desktop.Tests | net10.0-windows |
| tests/WorkbookLibrary.Evaluation | net10.0 |
| tests/WindowsFileCommit.Evaluation | net10.0 |
| tests/WindowsFileCommit.NativeEvaluation | net10.0 |

An automated comparison against the snapshot verifies that **only the TFM changed in each csproj**. WPF, x64, win-x64, nullable, implicit-usings, all PackageReferences and Step 39's `call npm.cmd` discovery/overrides are retained. Evaluated Desktop properties confirm C# **14.0**, Nullable **enable**, UseWPF **true**, PlatformTarget **x64**, RuntimeIdentifiers **win-x64**. No preview, warning suppression, compatibility relaxation, analyzer disabling, trimming, AOT or single-file setting was added. No application, frontend, test-logic or native interop source changed in Step 40.

### Package and breaking-change findings

All direct package versions remain unchanged: ClosedXML 0.105.1, Open XML SDK/Framework 3.5.1, WebView2 1.0.4314.35, MSTest 4.5.2 and Test SDK 18.11.0; NPOI 2.7.6 remains isolated to evaluation. All **nine NuGet lockfiles** were regenerated with `--force-evaluate`, reviewed against the snapshot, then verified with locked restores. Frontend manifests and lockfile were not changed by Step 40.

- Open XML SDK now selects its existing **net10.0** assets. Its published nuspec requires **System.IO.Packaging 10.0.2** for this target, replacing 8.0.1 in core/test/evaluation lock graphs. This is a target-driven transitive dependency change, not an unrelated direct upgrade. WindowsDesktop provides packaging in the WPF runtime, so Desktop/Desktop.Tests prune that package from their graphs.
- Framework-provided transitive packages such as DiagnosticSource, and evaluation-only legacy platform/Unsafe/CodePages dependencies, are pruned where applicable. No other resolved package version changed. Content hashes for unchanged package versions remain intact.
- Microsoft’s [.NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0) and [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10) breaking-change lists were reviewed. ZIP compression/encoding changes, JSON metadata checks, WPF resource/grid behavior, SDK audit/pruning and native library packaging were relevant review areas. Existing tests pass without a source workaround. The prior investigation below supplies the detailed test obligations.
- The original exact source hashes, unaffected package-part hashes/canonical XML checks, formula text/cache handling, reference identities, candidate reopening, atomic adoption and no-overwrite commit tests are unchanged and pass. No assertion was removed or weakened to accept a runtime difference.
- WebView2's existing managed/native package assets work in the real .NET 10 WPF tests and folder publish. The selected native loader is AMD64. Native rename layout remains root/length/name offsets **8/16/20**; UTF-16 buffers, retained handles, identity checks and cancellation behavior pass the existing tests.
- The published .NET 10 runtimeconfig sets `System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization` to **false** (the earlier .NET 8 WPF output set it true). No application BinaryFormatter call exists; the migrated regressions and published lifecycle pass without re-enabling it.
- .NET 10's transitive auditing emits **19 NU1902/NU1903 warnings in the independent evaluation project** for its existing ImageSharp 2.1.11, Cryptography.Pkcs 8.0.1 and Cryptography.Xml 8.0.2 dependency tree. Audit checks remain enabled. This is unresolved evaluation-dependency security work, not a production build warning or evidence of a new functional regression. Production publish excludes NPOI and ImageSharp. Do not broaden this migration into an unreviewed dependency upgrade; separately remediate/requalify that research graph before using it with anything beyond controlled synthetic inputs.

### Final .NET 10 results

| Check | Result |
| --- | --- |
| Selected SDK | 10.0.403, stable, exact pin |
| Locked restores | Solution and all three independent harness projects pass; no lock mismatch |
| Standard Release solution build | **Pass, 0 warnings / 0 errors**, normal frontend installation/typecheck/build; no DesignTimeBuild override |
| Full solution tests | **98 passed / 0 failed / 0 skipped**: 16 Application, 74 Workbooks, 8 Desktop |
| Workbook evaluation harness tests | **13 passed / 0 failed / 0 skipped** |
| Workbook library matrix | **22 passed, 24 known failures, 26 unexecuted, 0 execution errors**; identical to .NET 8 baseline per case, including semantic differences and schema errors |
| Native file-commit harness | **600 passed / 0 failed**, including 200 expected rejections of unsupported relative mode |
| Managed file-commit harness | **390 passed / 0 failed** |
| Frontend typecheck/tests/build | Pass; **19 passed / 0 failed**, production assets generated |
| Toolchain tests | **3 passed / 0 failed** |
| Self-contained Windows x64 publish | Pass, locked restore, Core/WindowsDesktop 10.0.14; `artifacts/desktop-net10-win-x64` |
| Publish structure | 12 required assets present, AMD64 WebView2 loader, all 3 frontend file hashes match dist, correct runtimeconfig |
| Published executable lifecycle | Input-idle, responsive TsekSheet window, normal close and exit code 0 |
| Source integrity | Both retained SHA-256 fingerprints match the committed manifest after all checks |

The .NET 10 matrix report is `artifacts/workbook-evaluation/20261028-153128-c999a240/results.json`. All **72** source-unchanged flags are true. A direct comparison against Step 39 report `20261028-150050-3598bd31/results.json` confirms identical status, Differences and SchemaErrors arrays for every fixture/library/operation. Known unsupported-operation failures are retained; a zero harness exit is not a full Excel-capability claim.

TRX solution results end in `2026-10-28_23_36_47_net10.0` (Application), `_23_36_50_net10.0` (Workbooks), `_23_36_55_net10.0` (Desktop). The evaluation-test TRX ends in `_23_31_25_net10.0`. Test output explicitly identifies `.NETCoreApp,Version=v10.0`; stale net8 binaries were not used for migration evidence.

The **8 real WPF/WebView2 smoke tests** pass with normal Windows desktop process access. They cover startup/messaging, source opening/navigation, working-copy creation and typed edits, formula protection/error handling, Merge review/cancel/confirm and refreshed original content, new-file Save As, existing-filename rejection, cancelled selection and unsaved-change handling. Native file-selection and close-confirmation seams provide synthetic inputs. Actual native dialog selection, keyboard/focus behavior and clean-machine offline installation were **not manually tested**. The separate hidden published-process check establishes responsive startup/normal shutdown only; it does not independently assert its browser handshake.

Retained fixture fingerprints remain:

- Reimbursement: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- Laboratory: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

### Windows CMD and VS Code — current instructions

Install the official stable SDK **10.0.403 Windows x64** on another development PC and keep its WindowsDesktop runtime available. Use an installed WebView2 Runtime and stable Node **>=24.16.0 <25**, npm **>=11.13.0 <12** on PATH; current tested pair is 24.16.0/11.13.0. Provision approved package sources/caches for restore. No Excel/Python installation is required for current features. This task does not certify a disconnected clean-machine restore.

Open the repository folder in VS Code and use an integrated **Command Prompt** terminal with the same PATH as external CMD. Microsoft C# extension/C# Dev Kit is optional editing/debugging tooling; the .NET CLI provides the authoritative Windows WPF build. No VS Code extension or IDE installation was changed here. Visual Studio 2022 does not officially support .NET 10 targeting; Visual Studio 2026 is optional, not required for CLI development.

Run from the repository root, stopping after any failure:

```cmd
where dotnet
dotnet --version
where node
where npm.cmd
node --version
call npm.cmd --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore --logger trx
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj -c Release --no-restore
dotnet run --project tests\WorkbookLibrary.Evaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.NativeEvaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.Evaluation -c Release --no-restore
pushd src\TsekSheet.Frontend
call npm.cmd run test:toolchain
call npm.cmd run typecheck
call npm.cmd run test -- --run
call npm.cmd run build
popd
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -p:RestoreLockedMode=true -o artifacts\desktop-net10-win-x64
artifacts\desktop-net10-win-x64\TsekSheet.Desktop.exe
certutil -hashfile tests\WorkbookLibrary.Evaluation\Fixtures\reimbursement-synthetic.xlsx SHA256
certutil -hashfile tests\WorkbookLibrary.Evaluation\Fixtures\laboratory-synthetic.xlsx SHA256
```

`dotnet --version` must report **10.0.403**. Framework-dependent launch path is `src\TsekSheet.Desktop\bin\Release\net10.0-windows\TsekSheet.Desktop.exe`; it needs .NET 10 WindowsDesktop locally. The self-contained output includes Core/Desktop **10.0.14**, still requires WebView2, and must be distributed as a complete folder. Do not reuse the old net8 launch path. Stop frontend processes before desktop build/publish because npm ci replaces node_modules. Explicit npm overrides from Step 39 remain supported.

### Step 40 modified files and rollback

Modified by this continuation:

- `global.json` — exact SDK pin 8.0.427 → 10.0.403; roll-forward remains disabled.
- The **nine `.csproj` files in the table above** — only net8.0/net8.0-windows → net10.0/net10.0-windows.
- Each corresponding **`packages.lock.json`** — migrated target graphs, target-selected packaging dependency and framework pruning.
- `README.md`, `PLANS.md`, `docs/development/toolchain.md`, `desktop-foundation.md`, `dotnet-10-migration.md` — current requirements, paths, CMD/VS Code guidance, findings and results.
- `docs/development/worksheet-integration.md`, `workbook-safe-saving.md`, `workbook-editing-foundation.md`, `workbook-engine-foundation.md` — current migration cross-references; their earlier milestone results remain historical.

No source workbook, application logic, frontend code, safety guard, test assertion, direct dependency version, npm file or Git history changed during migration. Existing Merge and environment-repair modifications remain uncommitted and reviewable. No commit/push was created.

For local rollback, copy **only** global.json, the nine csproj files and their nine lockfiles from the matching paths in `artifacts/step40-pre-migration` back into the workspace, as one coordinated operation. That snapshot preserves the repaired Desktop build settings and earlier user work. Select SDK 8.0.427, run locked restores and the full .NET 8 baseline, and update current documentation accordingly. Never reset/clean the checkout or restore all files from HEAD: HEAD predates the uncommitted Merge and repair work. Keep separate net8/net10 publish folders; both SDKs remain installed.

### Completion boundary and next milestone

The requested SDK/TFM migration acceptance is satisfied for the existing supported scope: normal .NET 10 Release build, required applicable regressions, native commit checks, publish/lifecycle checks and original fingerprints pass. No diagnostic build or ignored integrity failure is used as acceptance.

This does not complete Phase 2 or certify a release. Evaluation-only vulnerability warnings require a separate dependency-remediation/requalification task. Native Excel repair/recalculation, arbitrary workbook fidelity, representative user-approved files, clean-machine offline installation, manual native dialogs, actual disk exhaustion/power loss and other previously documented limits remain unverified. Replace, Create New, structural rows, templates, OCR, existing-file overwrite, autosave, databases and cloud behavior remain disabled/out of scope.

Recommended next milestone: separately remediate and requalify the evaluation dependency graph, then perform clean-machine/manual-dialog acceptance before expanding spreadsheet behavior.

## Historical Step 39 record

Everything below records the earlier .NET 8 environment-repair phase, its then-missing SDK, baseline commands and deferred migration plan. Those historical status/target/lock statements are superseded by Step 40 above and are retained for provenance and rollback context; they are not current developer instructions.

## Implementation plan

1. Inventory the checkout, SDKs, tool resolution, locks and safety regression suites. Record the existing failed .NET 8 build before changing any target.
2. Replace the machine-specific default npm path with PATH discovery and retain an explicit CLI override. Accept a bounded Node 24/npm 11 range justified by locked package engines and regression evidence; preserve all dependency versions and integrity records.
3. Run the standard .NET 8 Release build, all solution tests, frontend checks, workbook and native filesystem evaluations, publish checks and source fingerprints. Do not use design-time compilation for acceptance.
4. Identify the stable official .NET 10 x64 SDK and document installation without modifying system tools or Visual Studio. Keep .NET 8 installed.
5. Only when that SDK is available, pin its exact installed version with roll-forward disabled; migrate all nine projects to net10.0/net10.0-windows, review runtime/language/analyzer changes and update affected NuGet lock target graphs without unrelated upgrades.
6. Repeat the complete verification under .NET 10 before claiming migration completion. If the SDK remains absent, retain the consistent .NET 8 targets/pin and record the external prerequisite, remaining commands and rollback procedure.

The prior Merge implementation, disabled Replace/Create New, candidate validation, source-file protection and new-file-only Save As restrictions are outside the implementation scope of this environment repair and must remain intact.

## Starting checkout and environment

Inspected on 2026-10-28, Asia/Manila. Branch `main`, starting/latest commit `c828394a580be35cb7065d5b82ac4557765e1688`. The working tree already contained the previous Merge implementation, tests and documentation: 12 modified tracked files and 3 untracked files. They were retained without reset, cleanup, branch changes, commits or pushes.

| Component | Actual observation |
| --- | --- |
| OS/process | Windows build 10.0.26200, win-x64 |
| Selected SDK | 8.0.427, MSBuild 17.11.69, `rollForward: disable` in global.json |
| Installed SDKs | 3.1.426; 5.0.408/416; 6.0.428; 7.0.410; 8.0.427; 9.0.312. **No 10.x SDK**. |
| Installed WindowsDesktop runtimes | 3.1.32, 5.0.17, 6.0.36, 7.0.20, 8.0.25/33, 9.0.14. **No 10.x runtime**. |
| .NET resolution | `C:\Program Files\dotnet\dotnet.exe` |
| Node/npm | Node 24.16.0, npm 11.13.0; `C:\nvm4w\nodejs\node.exe` and `npm.cmd` |
| Node installation | NVM symlink to `C:\Users\migzc\AppData\Local\nvm\v24.16.0`; another Codex-bundled Node is later on PATH |
| WebView2 Runtime | Registry version 156.0.4314.50; real desktop tests pass |
| Python | WindowsApps alias previously failed; Python/OCR is not a current build prerequisite |

All nine .NET projects still target net8.0 except Desktop and Desktop.Tests, which target net8.0-windows. All applicable implicit-usings, nullable, WPF, x64 and RID settings remain unchanged. There are no explicit LangVersion, analyzer-level, trimming, Native AOT or single-file settings in the projects. Default C# remains 12 for the current target. A future net10.0 target normally selects C# 14; review resulting compiler/analyzer warnings then, rather than introducing preview settings or unrelated language rewrites now.

## Original build issue and repair

The original standard build failed because Desktop defaulted `NpmCliPath` to `%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js`, a path from the earlier PC. The exact frontend guard also rejected the installed 24.16.0/11.13.0 pair. Both failures were recorded in the preceding integration verification before target changes; targets remain unchanged in this task.

Desktop now executes **`call npm.cmd` through the build shell's PATH**. This supports the observed NVM installation without changing PATH or npm itself. Use `-p:NpmExecutable="C:\absolute\path\npm.cmd"` to select an explicit launcher; the existing `-p:NpmCliPath="C:\absolute\path\npm-cli.js"` override is retained and invokes `node` from PATH. An explicitly missing CLI file receives an actionable error. Default discovery never assumes a Program Files layout.

The first repair attempt quoted the bare name `"npm.cmd"`. On this Windows installation that caused npm's `%~dp0`-based script lookup to use the frontend working directory, producing missing npm-prefix.js/npm-cli.js errors. Read-only CMD probes reproduced that behavior. `call npm.cmd` fixes it; absolute override paths remain quoted. No npm-generated launcher was edited.

The accepted stable frontend range is now Node **>=24.16.0 <25**, npm **>=11.13.0 <12**. The reference package-manager metadata records npm 11.13.0, the tested current-PC version. This is a development-tool policy change, not an application dependency upgrade. Reasons:

- All **104** locked packages with Node engine declarations accept 24.16.0, checked with npm's installed semver implementation. Examples: locked Vite accepts newer Node 22+, Vitest accepts Node 24, and jsdom accepts 24.15+. No package requirement demonstrated a need for exact equality to the old PC's patch versions.
- The complete standard build, regression suites and publish succeed on 24.16.0/11.13.0. The preceding integration work also established frontend execution on that pair.
- A focused three-case Node test suite checks current/previous pairs, lower and unreviewed majors, missing/malformed identities, prereleases and alternate managers. Other allowed patch combinations remain subject to verification.

Deterministic dependency selection remains based on **`npm ci`**, lockfile version 3, exact direct versions and retained transitive resolutions/integrity hashes. Only the root `engines` metadata changed in package-lock.json; no dependency version, URL or integrity entry changed. No NuGet lockfile changed. Accepted tool ranges do not promise byte-identical artifacts across different tool/runtime versions; record the exact pair for release evidence.

The initial toolchain test filename ended in `.test.mjs`, so Vitest also discovered it even though it used Node's test runner. That run had 19 passing frontend tests but one failed discovery suite. Renaming the Node-only file to `check-toolchain.node-tests.mjs` separated the runners without excluding or suppressing a test. Final Node and Vitest runs pass.

## .NET 10 prerequisite and installation handoff

**Installed/selected .NET 10 SDK: none.** The recommended candidate is stable **10.0.403 Windows x64**, listed by Microsoft's [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) on this assessment date, including Desktop runtime 10.0.14. This is a candidate selection, not an installed or execution-qualified SDK. The repository continues selecting 8.0.427; do not pin a missing SDK or leave targets at net10.0 without a working compiler.

To supply the external prerequisite:

1. Open the [official 10.0.403 Windows x64 SDK installer page](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.403-windows-x64-installer). Choose the **SDK**, not only a runtime, and do not select preview/RC .NET 11 downloads.
2. Check the downloaded installer's Microsoft signature and compare its SHA-512 with the publisher-provided value before running it. No binary was downloaded, signature verified or installer executed during this task.
3. Run the installer interactively with the appropriate machine-owner/admin approval. Leave SDK 8.0.427 and WindowsDesktop 8.0.33 installed. Do not change Visual Studio or uninstall older SDKs for this migration.
4. Open a fresh CMD and run `where dotnet`, `dotnet --list-sdks`, and `dotnet --list-runtimes`. Confirm x64 SDK 10.0.403 and a 10.x WindowsDesktop runtime are visible. `dotnet --version` inside this repository will still show **8.0.427** until the reviewed pin change is made.

Microsoft's [Windows installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/windows) requires Visual Studio 2026 for official .NET 10 targeting in Visual Studio. **Visual Studio 2022 does not officially support .NET 10 development.** VS Code with an installed SDK and the .NET CLI is a supported alternative; Visual Studio 2026 is optional, not a prerequisite for this task. No IDE installation is necessary to run CLI builds.

## Deferred project and package migration

After installation, verify the exact SDK and change global.json to the installed stable version while retaining `rollForward: disable` and excluding previews. For the current candidate, the intended future pin is `10.0.403`; it has **not** been applied.

| Project | Current target | Deferred target |
| --- | --- | --- |
| TsekSheet.Application | net8.0 | net10.0 |
| TsekSheet.Workbooks | net8.0 | net10.0 |
| TsekSheet.Desktop | net8.0-windows | net10.0-windows |
| TsekSheet.Application.Tests | net8.0 | net10.0 |
| TsekSheet.Workbooks.Tests | net8.0 | net10.0 |
| TsekSheet.Desktop.Tests | net8.0-windows | net10.0-windows |
| WorkbookLibrary.Evaluation | net8.0 | net10.0 |
| WindowsFileCommit.Evaluation | net8.0 | net10.0 |
| WindowsFileCommit.NativeEvaluation | net8.0 | net10.0 |

Preserve WPF, x64 and win-x64 RID settings and the existing solution format. Regenerate the nine NuGet lock target graphs only as required by new TFMs, inspect resolved-version changes, then require locked restores again for solution and independent harnesses. Do not use an unlocked restore as acceptance, disable audit/compatibility checks, or upgrade packages merely to reduce warnings. Keep nullable enabled and investigate genuine compiler/analyzer changes.

Local package inventory gives a provisional compatibility basis, not .NET 10 runtime proof:

| Existing dependency | Available assets / assessment |
| --- | --- |
| ClosedXML 0.105.1 | netstandard2.0/2.1 assets; retain the library and run all preservation checks under the new runtime. |
| Open XML SDK 3.5.1 | Includes net8.0 and net10.0 assets. Moving to its net10.0 asset can change behavior even without a package version change; validate XML/package output and reopen semantics. |
| WebView2 1.0.4314.35 | Contains WPF managed assets under lib_manual, including net8.0-windows10.0.17763.0, plus native loaders. Verify the SDK's MSBuild selection and real browser startup on net10.0-windows; metadata alone does not qualify it. |
| MSTest framework/adapter 4.5.2; Test SDK 18.11.0 | Existing tests pass on net8.0; net10.0 runner/discovery remains unexecuted. Framework includes net8.0/net9.0/netstandard assets. |
| NPOI 2.7.6 | Evaluation-only dependency. Keep known experiment failures visible; never add it as a production fallback. |

No package compatibility failure has justified a library upgrade. No NuGet version, source, native file-commit code, workbook guard or frontend application code changed in this task.

## Breaking-change investigation

Review both Microsoft's [.NET 9 breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0) and [.NET 10 breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10), since this migration skips an intermediate runtime. The following are inspection findings and required verification, not claims that .NET 10 tests passed:

- **ZIP representation:** .NET 9 can change ZIP central-directory compression flags when adding entries with CompressionLevel. TsekSheet compares package entry sets, uncompressed unaffected-part hashes and canonical XML for cell edits; source/staging/final byte hashes still protect their separate boundaries. Do not demand identical ZIP containers between runtime-generated outputs or relax unaffected content checks. Compare semantic preservation under each runtime and keep source-byte preservation exact. [Microsoft ZIP change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/compressionlevel-bits).
- **JSON/XML:** .NET 10 tightens JSON property-name conflict handling and changes XmlSerializer's handling of obsolete properties. The bridge uses System.Text.Json Web defaults and plain DTOs; no direct production XmlSerializer use was found. SDK/library serialization remains a regression risk. Run protocol fixtures, duplicate-property rejection, formula/cache tests and complete candidate validation on the new assets/runtime.
- **WPF:** inspect the .NET 10 DynamicResource and empty-grid-definition changes. Current XAML uses neither DynamicResource nor empty RowDefinitions/ColumnDefinitions attributes. Smoke-test actual host lifecycle rather than inferring success from compilation.
- **SDK/NuGet:** .NET 10 changes transitive audit defaults and package pruning. Review warnings and regenerated lock graphs; retain security checks. Existing PackageReference versions are explicit. Keep `.sln` rather than regenerating into a new default solution format.
- **Native interop:** P/Invoke uses explicit Unicode, sequential layouts and SafeFileHandle. Rename layout must remain root/length/name offsets **8/16/20**, with a UTF-16 NUL terminator excluded from the native byte-length field. Repeat all native and workbook-save races, identity, held-handle, cancellation and failure checks on .NET 10. No marshalling rewrite is planned without evidence.
- **Runtime/publishing:** there is no production BinaryFormatter call, trimming, AOT or single-file configuration. The current WPF runtimeconfig includes its framework BinaryFormatter compatibility switch; that is not evidence of an application dependency on the removed serializer. Inspect the new runtimeconfig and native DLL layout, test the normal folder publish and do not enable packaging optimizations during migration.

## Verification results — .NET 8 baseline only

| Check | Result |
| --- | --- |
| Solution and all three independent harness locked NuGet restores | Passed; all NuGet lockfiles unchanged |
| Standard Release solution build | **Passed: 0 warnings, 0 errors**, SDK 8.0.427, normal BuildFrontend target and locked npm ci; no DesignTimeBuild override |
| Full solution tests | **98 passed:** 16 Application, 74 Workbooks, 8 Desktop; zero failures/skips |
| Workbook evaluation harness tests | **13 passed**, zero failures/skips |
| Workbook evaluation matrix | **22 passed, 24 known compatibility failures, 26 unexecuted**, zero execution errors. All 72 source fingerprints unchanged. Report: `artifacts/workbook-evaluation/20261028-150050-3598bd31/results.json` |
| Native file-commit harness | **600 passed, 0 failed**, rename offsets 8/16/20. Includes 200 expected rejections of the unsupported directory-relative mode. |
| Managed file-commit harness | **390 passed, 0 failed**; synthetic replacement-race experiments do not enable production overwrite saving |
| Frontend typecheck | Passed |
| Frontend regression tests | **19 passed**, zero failures/skips after correcting Node-runner discovery |
| Toolchain tests | **3 passed**, zero failures/skips |
| Frontend production build | Passed |
| Self-contained Release win-x64 publish | Passed with locked restore and runtime 8.0.33, output `artifacts/step39-baseline-win-x64` |
| Publish structure | 11 required application/runtime/frontend assets checked; Core/Desktop runtimes 8.0.33 in runtimeconfig; x64 WebView2 loader present; all 3 frontend files hash-identical to dist |
| Published executable startup | Input-idle and responsive, window title TsekSheet, normal CloseMainWindow exit code 0. Hidden process startup check only; not a manual browser/dialog test. |
| .NET 10 restore/build/tests/publish | **Not executed: SDK/runtime absent** |

The solution tests used normal Windows desktop process access. TRX results end in `2026-10-28_23_00_56` (Application), `_23_00_57` (Workbooks), `_23_01_02` (Desktop). The desktop suite exercised real WPF/WebView2 startup, workbook opening/navigation, copy creation/editing, Merge preview/cancel/confirm, refreshed original-session content, Save As/new-file and existing-name rejection, cancellation and unsaved handling. Native file-selection and close-confirmation seams supply synthetic inputs; interactive native-dialog selection, focus and keyboard navigation were not manually tested. The published startup check does not assert the browser handshake; the real WebView2 test suite does.

Original synthetic fixture fingerprints match the committed manifest after all runs:

- Reimbursement: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- Laboratory: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

The known library-matrix failures/unexecuted cases remain explicit. They continue to block unsupported operations; a successful harness exit is not a claim that all library features passed. No original workbook was modified or automatically saved.

## VS Code and Windows CMD instructions

Open the repository folder in VS Code. The Microsoft C# extension/C# Dev Kit can provide editing and debugging; the CLI builds the authoritative WPF solution. Extension installation and WPF designer support were not tested or installed here. Use an integrated **Command Prompt** terminal on Windows with the same tool PATH as external CMD. Microsoft's [C# in VS Code guidance](https://code.visualstudio.com/docs/languages/csharp) describes extension-based C# development.

Current reproducible .NET 8 baseline commands, from the repository root; stop after any failure:

```cmd
where dotnet
dotnet --version
dotnet --list-sdks
where node
where npm.cmd
node --version
call npm.cmd --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore --logger trx
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj -c Release --no-restore
dotnet run --project tests\WorkbookLibrary.Evaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.NativeEvaluation -c Release --no-restore
dotnet restore tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.Evaluation -c Release --no-restore
pushd src\TsekSheet.Frontend
call npm.cmd ci --no-audit --no-fund
call npm.cmd run test:toolchain
call npm.cmd run typecheck
call npm.cmd run test -- --run
call npm.cmd run build
popd
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.33 -p:RestoreLockedMode=true -o artifacts\step39-baseline-win-x64
artifacts\step39-baseline-win-x64\TsekSheet.Desktop.exe
certutil -hashfile tests\WorkbookLibrary.Evaluation\Fixtures\reimbursement-synthetic.xlsx SHA256
certutil -hashfile tests\WorkbookLibrary.Evaluation\Fixtures\laboratory-synthetic.xlsx SHA256
```

Stop Vite/Vitest/other frontend consumers before a desktop build/publish, because npm ci replaces node_modules. Do not run tests against that directory while an install is in progress. For disconnected rebuilds, provision complete approved NuGet/npm/runtime-pack caches; current network-assisted restore evidence is not clean-machine offline acceptance.

After the SDK and target/lock migration is reviewed, repeat the commands under the exact .NET 10 pin. Change launch paths to `bin\Release\net10.0-windows`; never launch stale net8.0 output as migration evidence. The intended future publish uses a separate output, for example:

```cmd
rem FUTURE ONLY: requires installed SDK 10.0.403, reviewed net10.0 targets and updated locks.
dotnet --version
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -p:RestoreLockedMode=true -o artifacts\desktop-net10-win-x64
```

Verify runtime versions and frontend/native-loader contents, launch the published executable, and repeat real desktop tests. The proposed runtime is from the selected SDK's official release; it is not qualified here. A separate framework-dependent build would require a locally installed .NET 10 WindowsDesktop runtime. WebView2 remains separate for either deployment.

## Modified file inventory and rollback

New files for Step 39:

- `docs/development/dotnet-10-migration.md` — plan, evidence, missing SDK instructions and remaining migration work.
- `src/TsekSheet.Frontend/scripts/check-toolchain.node-tests.mjs` — focused validation of allowed/rejected tool identities.

Modified by Step 39:

- `src/TsekSheet.Desktop/TsekSheet.Desktop.csproj` — PATH-based npm execution and explicit overrides; no TFM/WPF/package change.
- `src/TsekSheet.Frontend/package.json`, `package-lock.json`, `scripts/check-toolchain.mjs` — bounded frontend policy, npm reference pair, test command and root engine metadata only; dependencies unchanged.
- `README.md`, `PLANS.md`, `docs/development/toolchain.md`, `desktop-foundation.md`, `workbook-engine-foundation.md`, `worksheet-integration.md` — current behavior, portable commands, historical-evidence boundaries and incomplete migration status. PLANS and worksheet-integration already had prior uncommitted changes; these were preserved.

All other working-tree changes are the pre-existing Merge work, not .NET migration modifications. `global.json`, all target frameworks, all NuGet versions/locks, workbook engine/native interop source and retained fixtures are unchanged by Step 39. No commit or push was created.

Rollback now requires no framework change: the repository already remains on 8.0.427/net8.0. To undo only this repair, reverse the reviewed Step 39 hunks in the listed files and remove only its two new files if desired. Do **not** reset to HEAD, clean the checkout, or discard the earlier uncommitted Merge implementation. Returning to the old npm path/version policy would restore the known new-PC build failure.

For a later .NET 10 migration rollback, retain an explicit before-migration copy of global.json, each csproj and lockfile, and separate net8/net10 publish folders. Reapply the exact 8.0.427 pin, original net8.0/net8.0-windows targets and matching lock graphs together, then run the full .NET 8 baseline again. Keep both SDKs installed; never edit or overwrite source workbooks to roll back application code.

## Remaining risks and completion gate

The external blocker is the absent .NET 10 SDK/runtime. .NET 10 asset selection, compilation, analyzer behavior, test-host compatibility, workbook XML/ZIP semantics, WPF/WebView2 lifecycle, native file-commit behavior and publishing remain **unverified**. Metadata/release-note review and passing .NET 8 regressions cannot establish them.

Current guarantees are preserved: source bytes/fingerprints remain immutable; all candidate, compatibility, revision, conflict and rollback checks are unchanged; Save As remains new-file-only with pending-copy exclusion. Replace/Create New, structural rows, templates, OCR, existing-file replacement, autosave, databases and cloud services remain disabled/out of scope. Native Excel repair/recalculation, real user workbooks, clean-machine offline deployment, disk exhaustion/power loss and manual native dialogs remain unexecuted.

Recommended next milestone: install the documented stable x64 SDK alongside .NET 8, then perform the coordinated SDK/TFM/lock migration and complete every .NET 10 build, regression, native, publish and desktop check before further product features. Phase 2 and the .NET 10 migration are not complete.
