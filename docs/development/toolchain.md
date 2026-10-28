# Windows x64 Development Toolchain

## Current requirements — Step 40 (2026-10-28, Asia/Manila)

The [.NET 10 migration and environment repair](dotnet-10-migration.md) supersedes the old PC inventory and exact Node/npm restrictions below. Current verified baseline: Windows x64 build 26200, stable .NET SDK **10.0.403**, Core/WindowsDesktop runtime **10.0.14**, net10.0/net10.0-windows, Node 24.16.0, npm 11.13.0, WebView2 156.0.4314.50. Standard Release build, complete regression suites and self-contained win-x64 publish pass on this PC. No design-time build exception is needed.

- .NET SDK selection pins **10.0.403** in `global.json`, with `rollForward: disable`. All nine project targets and NuGet lock graphs are migrated. .NET 8 remains installed for comparison/rollback. WPF, x64, nullable and win-x64 settings are preserved; the SDK selects C# 14.0 without preview settings.
- Stable Node `>=24.16.0 <25` x64 and npm `>=11.13.0 <12` are accepted. The reference pair recorded in `packageManager` is npm 11.13.0 with Node 24.16.0. The previous 24.22.1/11.19.1 pair is also accepted. Other accepted patches are not individually execution-qualified.
- All 104 locked package Node-engine declarations accept Node 24.16.0. Exact patch equality was a provisioning policy, not a requirement demonstrated by the packages or regression suite. Major boundaries and a tested minimum remain enforced; prereleases and alternate managers are rejected.
- Dependency versions, URLs, integrity records and npm lockfile version 3 are unchanged. Always use `npm ci`; the permitted tool range guarantees neither byte-identical asset generation across runtimes nor acceptance without regression checks.
- MSBuild uses `call npm.cmd` from PATH. In CMD run `where node`, `where npm.cmd`, `node --version`, and `call npm.cmd --version`. Explicit absolute `NpmExecutable` or legacy `NpmCliPath` overrides are optional. No `%ProgramFiles%` layout is assumed. Restart the build shell after an intentional tool switch.
- .NET CLI and VS Code are the .NET 10 development paths; Visual Studio 2022 does not officially support .NET 10 targeting. Visual Studio 2026 is optional. The user installed the SDK; this agent did not change an IDE installation.

The remainder is historical inspection evidence from the previous PC. Its machine paths, SDK inventory, no-.NET-10 instruction and exact Node/npm recommendations are not current prerequisites.

## Historical P1-01 assessment

Assessment date: 2026-10-28 (Asia/Shanghai)

Scope: P1-01 in [the Phase 1 plan](../../plans/phase-01-desktop-foundation.md), governed by [AGENTS.md](../../AGENTS.md). This document records an inspection and a proposed exact baseline. No dependencies were installed, projects scaffolded, package locks written, or application code generated.

## Result and Evidence Labels

- **Locally verified:** A command, registry inspection, or installed-file inspection produced the finding on this machine.
- **Source verified:** Official documentation or publisher/package-registry metadata supports the finding. This does not establish that the package runs in TsekSheet.
- **Recommended:** An exact proposed choice for subsequent implementation, not an installed or integration-tested dependency.
- **Unverified:** Not established in this task; requires a later environment or implementation check.

P1-01 inventory and baseline recommendations are documented. The selected .NET SDK 8.0.427 is installed, but the combined baseline is not proven by builds. Do not treat P1-01's full compatibility exit gate or Phase 1 as passed: SDK selection/pinning, supported-OS validation, dependency restore/build, and desktop smoke checks remain outstanding. No other plan or roadmap status is changed.

## Installed Environment: Locally Verified

| Item | Finding | Qualification |
| --- | --- | --- |
| Windows | Windows 10 Pro, Professional edition, 22H2, build `10.0.19045.6456` | Registry and `ver` agree on build. This is the observed host, not an accepted support baseline. ESU enrollment and patch compliance were not checked. |
| .NET architecture | Host x64; RID `win-x64` | `dotnet --info`. An additional x86 .NET installation is registered; use the x64 tool explicitly when needed. |
| .NET executable | `C:\Program Files\dotnet\dotnet.exe` | First result from `where dotnet`. |
| .NET SDKs | `5.0.416`, `8.0.427`, `9.0.300` | No .NET 10 SDK appears in the enumerated installation. |
| Default .NET SDK | `9.0.300` | Selected from this repository; `global.json` was not found. Bundled MSBuild reports `17.14.5+edd3bbf37`. |
| Desktop reference packs | `3.1.0`, `5.0.0`, `6.0.36`, `8.0.16`, `8.0.33`, `9.0.5` | `Microsoft.WindowsDesktop.App.Ref` directories. `Microsoft.NET.Sdk.WindowsDesktop` exists under SDK 9.0.300. Presence is not a successful WPF compilation. |
| Node.js | `v24.22.1`, x64 | First executable is `C:\Program Files\nodejs\node.exe`. Another Node executable is on PATH under Codex's runtime cache. |
| npm through CMD launcher | `10.9.8` | `npm --version` under CMD and explicit `C:\Program Files\nodejs\npm.cmd` agree. |
| npm adjacent package | `11.19.1` | Package metadata under `C:\Program Files\nodejs\node_modules\npm`; invoking its `bin\npm-cli.js` directly also returns 11.19.1. Launcher and package disagree; effective dispatch is unresolved. |
| Git | `2.52.0.windows.1` | Resolved through Codex's bundled Git under `C:\Users\mcs\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe`. Availability in a standalone CMD outside Codex is unverified. |
| Visual Studio 2022 | Enterprise `17.14.0`, installation `17.14.36109.1` | `vswhere` reports complete, launchable, non-prerelease, no reboot required. Installed at `C:\Program Files\Microsoft Visual Studio\2022\Enterprise`. IDE launch was not tested. |
| Visual Studio 2019 | Enterprise and Build Tools, both `16.11.47` / `16.11.36107.64` | Present but not selected for the recommended stack. |
| .NET desktop workload | Registered in VS 2022 Enterprise and VS 2019 Enterprise | `vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop`. Do not infer its presence solely from `dotnet workload list`, which lists different workloads. |
| Visual Studio MSBuild | `17.14.8.25101` | Explicit VS 2022 MSBuild executable was run. `msbuild` and `devenv` were absent from ordinary PATH; this does not mean they are uninstalled. |
| WebView2 Runtime | `155.0.4283.62`, x64 | Registry client `{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`, executable version, and PE machine `0x8664` agree. Installed below `C:\Program Files (x86)\Microsoft\EdgeWebView\Application`; the folder name does not imply x86 binaries. |
| Edge browser | Registry reports `156.0.4314.42` | Separate from WebView2 Runtime. Do not substitute this value for the installed WebView2 version. |

Enumerated shared runtimes:

| Runtime family | Installed versions |
| --- | --- |
| `Microsoft.NETCore.App` | 2.1.30, 3.1.32, 5.0.17, 6.0.36, 8.0.16, 8.0.33, 9.0.5 |
| `Microsoft.WindowsDesktop.App` | 3.1.32, 5.0.17, 6.0.36, 8.0.16, 8.0.33, 9.0.5 |
| `Microsoft.AspNetCore.App` | 2.1.30, 3.1.32, 5.0.17, 6.0.36, 8.0.16, 8.0.33, 9.0.5 |
| `Microsoft.AspNetCore.All` | 2.1.30 |

ASP.NET runtimes are inventory only; TsekSheet needs no web server. No installed components were removed or updated.

## Official Verification and Compatibility Findings

### Windows and .NET

**Project decision:** Use installed x64 .NET SDK **8.0.427**, target **net8.0-windows** for WPF and **net8.0** for application libraries, and retain Visual Studio 2022 compatibility. Do not install .NET 10 for TsekSheet. This decision supersedes the earlier .NET 10 recommendation; it does not change other technology choices.

Microsoft's lifecycle table gives **November 10, 2026** as the end of .NET 8 support. An upgrade to a supported .NET release will be required for a maintained release beyond that date. Record this as a release-maintenance requirement, not authorization to install another SDK now. The inspected policy lists runtime **8.0.33**, already present locally. [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

The Windows installation/support table restricts Windows 10 support to specified LTSC/Enterprise editions; this host is Pro 22H2. It lists Visual Studio 2022 17.8 or newer for .NET 8; the installed 17.14.0 meets that documented minimum. Exact SDK/IDE build integration remains untested. Retaining VS 2022 compatibility does not establish support for the current OS. [Microsoft Windows/.NET requirements](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

**Recommendation:** Use a serviced Windows 11 x64 environment, initially the 25H2 release, for development acceptance and clean-machine validation. Exact Windows patch/build and the product's minimum supported OS remain unverified until an actual test environment is available. Do not claim Windows 10 Pro support based on installed binaries executing. No OS change is performed or implied by this recommendation.

### Node, Frontend, and Test Packages

The official Node archive provides Node **24.22.1** Windows x64 and lists npm **11.19.1** with that release. This matches the installed Node and adjacent npm package, but not the effective npm launcher. [Node 24.22.1 archive](https://nodejs.org/en/download/archive/v24.22.1).

Direct read-only HTTPS queries to the npm registry returned the exact package versions below, their engine ranges, peer ranges, and licenses. Direct registry metadata was preferred over stale search results: for example, an indexed React page showed 19.3.0 while the registry returned 19.3.1. The registry's `latest` endpoint was used for discovery only; future manifests must pin exact reviewed versions.

Declared compatibility checks:

- Node 24.22.1 satisfies Vite/plugin-react's `^20.19.0 || >=22.12.0`, Vitest's `^22.12.0 || ^24.0.0 || >=26.0.0`, and jsdom's `^22.22.2 || ^24.15.0 || >=26.0.0` engine ranges. Vite also documents the Node requirement. [Vite guide](https://vite.dev/guide/).
- plugin-react 6.1.2 declares Vite `^8.0.0`; Vitest 5.0.5 accepts Vite `^6.4.0 || ^7.0.0 || ^8.0.0`. Vite 8.3.5 meets both.
- react-dom 19.3.1 requires React `^19.3.1`; use matching runtime versions. Testing Library React 16.3.3 accepts React/React DOM 19 and corresponding major-19 typings, and requires Testing Library DOM `^10.0.0`.
- React DOM typings 19.3.0 require React typings `^19.3.0`. Their patch versions need not equal the runtime patch.
- npm 11.19.1 declares Node `^20.17.0 || >=22.9.0`; Node 24.22.1 satisfies it. TypeScript 7.0.2 declares Node `>=16.20.0`.
- Optional compiler/canvas/browser/coverage peers are not a reason to install additional features. A real lockfile restore must verify optional-peer handling and transitive dependencies.

These are metadata-level checks, not a completed resolver, native-package, TypeScript, CSP, or WebView integration test.

### WebView2 and .NET Tests

Microsoft lists stable SDK **1.0.4314.35** for Runtime **156**, and Runtime **156.0.4314.42** as a released runtime. The installed runtime is 155.0.4283.62. Recommend the documented 156 pair for the new baseline; do not assume every new API works on 155. The required API-specific runtime floor still needs an audit during hosting/security implementation. [SDK release notes](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/sdk/), [runtime release notes](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/runtime/).

Microsoft documents Evergreen standalone offline installation and Fixed Version distribution. Recommend Evergreen x64 supplied through a standalone offline installer, with the actual installed runtime recorded for every smoke run. Evergreen can change independently; an exact test reference is not a permanent freeze of an automatically serviced runtime. Offline operation must not wait for a bootstrap download. Maintenance/update policy and runtime-originated traffic remain separate validation items. [WebView2 distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

Direct NuGet flat-container metadata confirmed stable WebView2 1.0.4314.35, MSTest framework/adapter **4.5.2**, and Microsoft.NET.Test.Sdk **18.11.0**. Some gallery search snapshots showed MSTest 4.5.1; direct metadata was newer. Choose MSTest for both headless and Windows tests, keeping the same framework version. Actual runner compatibility, STA dispatcher handling, and interactive-desktop execution remain untested. [MSTest framework metadata](https://api.nuget.org/v3-flatcontainer/mstest.testframework/index.json), [adapter metadata](https://api.nuget.org/v3-flatcontainer/mstest.testadapter/index.json), [test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk).

## Recommended Exact Baseline

The .NET 8 SDK, target frameworks, and Visual Studio 2022 compatibility are **project decisions**. Other entries remain **recommendations**, even where the same tool is already installed. Package existence and selected peer/engine constraints were source verified; combined execution is unverified.

| Component | Exact choice | Purpose and qualification |
| --- | --- | --- |
| .NET SDK x64 | `8.0.427` | Installed and selected by project decision. Later pin exactly in `global.json`; do not silently select installed SDK 9.0.300. Do not install .NET 10. |
| .NET runtime/WPF | `8.0.33` | Installed runtime/reference pack; WPF supplied by .NET, not an independent UI NuGet framework. .NET 8 support ends November 10, 2026. |
| Target frameworks | Desktop `net8.0-windows`; Application `net8.0` | WPF only in Desktop. Windows runtime target `win-x64`; Windows minimum must be documented separately. |
| C# | .NET 8 target default C# 12 | No preview language features. |
| IDE | Visual Studio 2022, installed `17.14.0` | Retain VS 2022 compatibility and its registered desktop workload. CLI development remains available; verify actual build/test integration. No IDE upgrade required by this decision. |
| WebView2 SDK | `Microsoft.Web.WebView2` `1.0.4314.35` | Stable WPF package; native x64 loader and required APIs need later build/smoke validation. |
| WebView2 Runtime test reference | `156.0.4314.42` x64 | Recommended reference, not installed. Evergreen standalone offline distribution; record resolved version at every test run. |
| Node.js x64 | `24.22.1` | Installed; retain this LTS release for the initial build baseline. |
| npm | `11.19.1` | Align with the Node release package after launcher ambiguity is resolved. Do not silently use effective 10.9.8 to generate the baseline lockfile. |
| Git | `2.52.0.windows.1` | Retain observed working version initially. This is not a claim of newest release or a vulnerability assessment. Verify standalone CMD availability. |
| React / React DOM | `19.3.1` / `19.3.1` | Matching runtime pair. |
| TypeScript | `7.0.2` | Stable registry release; native compiler/tooling compatibility must be tested before accepting frontend scaffolding. |
| React typings | `@types/react` `19.3.0`; `@types/react-dom` `19.3.0` | Declared peer ranges align. |
| Node typings | `@types/node` `24.19.2` | Highest stable major-24 version found by enumerating registry metadata; do not use major-26 typings with this baseline. |
| Build | `vite` `8.3.5`; `@vitejs/plugin-react` `6.1.2` | Keep React compiler and additional transform plugins out unless required. |
| Frontend tests | `vitest` `5.0.5`; `jsdom` `30.1.3` | DOM emulation does not replace real WebView2 tests. |
| Component tests | `@testing-library/react` `16.3.3`; `@testing-library/dom` `10.4.2`; `@testing-library/jest-dom` `7.0.1` | For render/interaction/assertion tests; use Vitest integration. |
| .NET tests | `MSTest.TestFramework` `4.5.2`; `MSTest.TestAdapter` `4.5.2`; `Microsoft.NET.Test.Sdk` `18.11.0` | Start with ordinary test projects and one runner path; no extra test SDK framework or UI automation platform yet. |

Use a self-contained Release `win-x64` development smoke artifact initially, with runtime 8.0.33 resolved and recorded. This removes the end user's separate .NET runtime requirement but increases distribution size and transfers .NET servicing responsibility to application releases. Self-contained publishing does not extend .NET 8 support beyond November 10, 2026; a maintained release requires an upgrade. WebView2 remains a separate prerequisite. Do not enable trimming, Native AOT, or single-file complexity in the initial WPF baseline. Installed apps need no Node/npm, IDE, Python, or Excel for Phase 1.

### Publisher Metadata Links and Licenses

Each URL identifies the precise metadata used for frontend recommendations:

| Package | Evidence | Reported license |
| --- | --- | --- |
| React | [19.3.1](https://registry.npmjs.org/react/19.3.1) | MIT |
| React DOM | [19.3.1](https://registry.npmjs.org/react-dom/19.3.1) | MIT |
| TypeScript | [7.0.2](https://registry.npmjs.org/typescript/7.0.2) | Apache-2.0 |
| React typings | [19.3.0](https://registry.npmjs.org/@types%2freact/19.3.0), [DOM 19.3.0](https://registry.npmjs.org/@types%2freact-dom/19.3.0) | MIT |
| Node typings | [24.19.2](https://registry.npmjs.org/@types%2fnode/24.19.2) | License text still to be checked for this exact version |
| Vite / React plugin | [8.3.5](https://registry.npmjs.org/vite/8.3.5), [6.1.2](https://registry.npmjs.org/@vitejs%2fplugin-react/6.1.2) | MIT |
| Vitest / jsdom | [5.0.5](https://registry.npmjs.org/vitest/5.0.5), [30.1.3](https://registry.npmjs.org/jsdom/30.1.3) | MIT |
| Testing Library | [React 16.3.3](https://registry.npmjs.org/@testing-library%2freact/16.3.3), [DOM 10.4.2](https://registry.npmjs.org/@testing-library%2fdom/10.4.2), [jest-dom 7.0.1](https://registry.npmjs.org/@testing-library%2fjest-dom/7.0.1) | MIT |
| npm | [11.19.1](https://registry.npmjs.org/npm/11.19.1) | Full license/notice review outstanding; npm latest metadata reported Artistic-2.0 |

These reported identifiers are not a completed distribution-license review. Archive exact .NET, Node, Git, NuGet, WebView2 SDK/runtime terms and transitive notices before distribution. WebView2's proprietary runtime terms must not be inferred from an open-source package license. No installers were downloaded or their signatures/hashes verified here.

## Missing Prerequisites and Open Checks

| Item | State and required evidence |
| --- | --- |
| .NET 8 SDK selection and maintenance | SDK 8.0.427 and runtime/reference pack 8.0.33 are installed. Pin SDK selection in a later authorized implementation change, verify builds, and plan an upgrade for a maintained release after November 10, 2026. .NET 10 installation is not a prerequisite and must not be performed. |
| Supported Windows x64 environment | Current Windows 10 Pro is not covered by the inspected .NET support table. A serviced Windows 11 x64 developer/clean-test environment is needed for the recommended supported path; exact build remains open. |
| npm dispatch consistency | Launcher 10.9.8 and adjacent CLI 11.19.1 disagree. Confirm resolution in a fresh standalone CMD before locking dependencies. No PATH/configuration changes were made. |
| WebView2 156 test runtime | Installed x64 155 is present; recommended 156 reference is absent. API-specific floor and offline installer availability/signature still need confirmation. Do not treat installed Edge 156 as runtime installation. |
| Visual Studio 2022 integration | Installed VS 2022 17.14.0 and desktop workload are retained. Validate solution load, SDK 8.0.427 builds, and test discovery; no new IDE installation is required. |
| Standalone Git PATH | Git works through this Codex environment. Independent installation/PATH availability is not established. |
| Offline artifacts | Exact NuGet/npm restore caches, .NET/WebView2 offline distributions, signatures, hashes, and complete notices have not been assembled or checked. |
| Resolver/build verification | No package install/restore or compilation was performed. Native Windows frontend dependencies, TypeScript/plugin compatibility, peer resolution, x64 loader output, and CSP/local-origin behavior remain unverified. |
| Automated test/desktop mechanism | MSTest metadata selected; actual .NET 8/VS 2022 test discovery, Windows STA dispatcher/message pump, and interactive desktop smoke execution remain for implementation. Package versions are unchanged; compatibility must be validated. |
| Product compatibility | Windows minimum, workbook support, OCR, and clean-machine packaging acceptance are not established by P1-01. Python/Excel/OCR tools are intentionally not prerequisites now. |

## CMD Inspection Commands

Run from Windows Command Prompt. These are read-only reproduction instructions; do not paste PowerShell syntax. Use `call` for npm in a batch file so execution returns to the caller.

```bat
cd /d C:\Users\mcs\Desktop\Projects\TsekSheet
ver
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v DisplayVersion
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v CurrentBuild
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v UBR
where dotnet
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
dir /b "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref"
where node
node --version
node -p "process.arch"
where npm
call npm --version
call "C:\Program Files\nodejs\npm.cmd" --version
node "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js" --version
where git
git --version
where msbuild
where devenv
"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -all -products * -format json
"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -all -products * -requires Microsoft.VisualStudio.Workload.ManagedDesktop -property installationPath
"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" -version -nologo
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv
reg query "HKCU\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv
dir /b "C:\Program Files (x86)\Microsoft\EdgeWebView\Application"
```

A missing per-user runtime registry key is normal when the runtime is machine-installed. `where msbuild` failure does not override successful explicit-path inspection. Some initial nested-CMD probes failed because of tool-wrapper quoting; registry/file/Visual Studio findings above were obtained by successful follow-up inspections. Those failed probes are not evidence of missing installations.

For read-only metadata rechecks without npm cache/package changes, use the installed Node executable's HTTPS fetch support:

```bat
node -e "fetch('https://registry.npmjs.org/vite/8.3.5').then(r=>r.json()).then(p=>console.log(JSON.stringify({version:p.version,engines:p.engines,peers:p.peerDependencies})))"
node -e "fetch('https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/index.json').then(r=>r.json()).then(p=>console.log(p.versions.filter(v=>!v.includes('-')).slice(-3)))"
```

## Future Build/Test Commands: Not Executed

These CMD-compatible commands describe the intended Phase 1 flow after authorized scaffolding, dependency provisioning, and creation of the pinned SDK/package configuration. Files and npm scripts below do not exist yet. They are not successful compatibility probes and must not be run as if the repository were already scaffolded.

```bat
cd /d C:\Users\mcs\Desktop\Projects\TsekSheet
dotnet --version
pushd src\TsekSheet.Frontend
call npm ci
call npm run typecheck
call npm run test -- --run
call npm run build
popd
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test tests\TsekSheet.Application.Tests\TsekSheet.Application.Tests.csproj -c Release --no-restore
dotnet test tests\TsekSheet.Desktop.Tests\TsekSheet.Desktop.Tests.csproj -c Release --no-restore --filter "TestCategory=WindowsSmoke"
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.33 -o artifacts\desktop-win-x64
```

Require `dotnet --version` to resolve 8.0.427 and `npm --version` to resolve 11.19.1 before generating locks. SDK 8.0.427 must be pinned in the later project configuration; its installed presence alone does not override the currently observed SDK 9.0.300 selection. Initial reviewed lock generation is a separate implementation action; `--locked-mode` and `npm ci` assume lockfiles already exist. RID-specific publish packs must also be acquired and recorded before claiming an offline reproducible publish. In a batch runner, check `errorlevel` after each command and stop on failure rather than continuing to publish a failed build.

Use the STA-capable interactive Windows harness created later for `WindowsSmoke`; the example filter does not create that harness. Validate the published executable with networking disabled and all prerequisites supplied locally. Keep `https://tseksheet.local` as the exact mapped frontend origin. No installer, test harness, package manifest, or lockfile is created in this task.

## Checks Performed and Not Performed

**Performed:** Installed SDK/runtime enumeration, Windows registry/build inspection, Node version/architecture, npm launcher versus direct-CLI comparison, Git resolution/version, Visual Studio/workload discovery, explicit MSBuild version, desktop-pack presence, WebView2 registry/file version and x64 PE inspection, official documentation review, and direct npm/NuGet metadata queries.

**Not performed:** Installation, update, project generation, package restore, WPF compilation, frontend type check/build, automated tests, WebView2 initialization/API calls, clean-machine run, runtime offline installer verification, security/network observation, or dependency signature/license audit. These limitations prevent claiming an execution-verified frozen stack. The recommended exact versions are recorded for the next authorized implementation stage.
