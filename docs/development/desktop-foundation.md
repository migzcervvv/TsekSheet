# Desktop Foundation: Build, Test, and Run

Status: Implemented foundation; Phase 1 acceptance remains open.

This implements the remaining shell/transport work in [the Phase 1 plan](../../plans/phase-01-desktop-foundation.md). It retains the accepted WPF/WebView2, React/TypeScript, plain C# application layer, and local-first boundaries. No Excel, template, worksheet integration, Python, or OCR processing is implemented. No workbook can be opened or saved by the command surface.

## Prerequisites and Pinned Dependencies

Use Windows x64, installed .NET SDK **8.0.427** selected by root `global.json`, and Visual Studio 2022-compatible project files. WPF targets `net8.0-windows`; Application targets `net8.0`. The Desktop and desktop tests use x64. The full dependency inventory remains in [toolchain.md](toolchain.md).

Use Node **24.22.1**, npm **11.19.1**, and the retained `package-lock.json`. The frontend preinstall check rejects other versions. On this machine the adjacent npm CLI is 11.19.1 while the `npm.cmd` launcher reports 10.9.8; commands below deliberately invoke the documented CLI directly. No global npm installation or PATH change is required.

The build defaults `NpmCliPath` to `%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js`. For another Node installation, pass `/p:NpmCliPath="C:\path\to\npm\bin\npm-cli.js"` to MSBuild commands. `node` must resolve to the pinned x64 runtime. End users need neither Node nor npm.

NuGet package versions remain WebView2 **1.0.4314.35**, MSTest framework/adapter **4.5.2**, and Microsoft.NET.Test.Sdk **18.11.0**. Each .NET project now retains a `packages.lock.json`; reviewed restores can use locked mode. No new production framework is introduced. Frontend tests use the documented Vitest **5.0.5**, jsdom **30.1.3**, Testing Library React **16.3.3**, and DOM **10.4.2**. Optional jest-dom remains omitted because of its previously observed declaration conflict; built-in assertions cover these tests.

The development machine's installed WebView2 Runtime **155.0.4283.62 x64** passed the current hosting/messaging tests with this SDK. This is execution evidence for the APIs used, not a claim that every SDK-156 API works on runtime 155. The documented **156.0.4314.42** runtime reference was not installed. Runtime absence is handled with a native error; the app never bootstraps a runtime download. Supply an approved standalone offline runtime installer for clean deployments. Runtime distribution and servicing still need clean-machine acceptance.

.NET 8 support ends **November 10, 2026**. A maintained release beyond that date requires an upgrade; self-contained publishing does not extend support. Do not install .NET 10 as part of these instructions.

## CMD Build and Test Instructions

All command blocks use Windows Command Prompt. Paths containing spaces must remain quoted. Commands assume the normal Node location documented above; substitute the actual pinned CLI location if needed. Stop after any command failure. In a `.cmd` wrapper, use `if errorlevel 1 exit /b 1` after each step.

```bat
cd /d C:\Users\mcs\Desktop\Projects\TsekSheet
dotnet --version
node --version
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build
```

Expect SDK 8.0.427, Node v24.22.1, npm 11.19.1. The desktop build performs `npm ci --no-audit --no-fund`, strict typechecking, and a Vite production build, then replaces only its generated output `frontend` directory with current `dist` files. Publish does the same for its generated `frontend` directory, preventing stale hashed bundles from accumulating. Never put user files in these generated folders. Errors stop the build. Design-time builds do not invoke npm. No development server is started, and no frontend source or node_modules is copied into the desktop distribution.

Stop any local Vite/Vitest process using the frontend folder before building Desktop. On Windows, `npm ci` replaces node_modules and cannot replace a native Rolldown module held by a preview or test process. During this task, stopping the preview resolved an EPERM error. Do not work around this with dependency upgrades or force-deleting locked files.

Frontend-only checks:

```bat
cd /d C:\Users\mcs\Desktop\Projects\TsekSheet\src\TsekSheet.Frontend
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" ci
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run typecheck
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run test -- --run
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run build
```

Browser preview is optional development tooling. Because the production CSP disallows remote connections and eval, use the production preview rather than assuming development HMR works. A browser without WebView2's native transport shows **Desktop host unavailable** and disables native controls. It never reports a successful C# connection.

## Release Windows x64 Distribution

```bat
cd /d C:\Users\mcs\Desktop\Projects\TsekSheet
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.33 -o artifacts\desktop-win-x64
artifacts\desktop-win-x64\TsekSheet.Desktop.exe
```

Restore/build can acquire packages using approved developer sources. This is separate from running the published app offline. Disconnected rebuilds additionally require a complete NuGet/npm cache and RID-specific runtime packs; this task does not claim those have been provisioned on a clean machine. `Directory.Build.props` explicitly includes the `win-x64` runtime graph so ordinary build and Windows publishing use consistent lockfiles. Review intentional dependency/runtime changes before regenerating locks.

Keep the **entire** publish directory together. It contains the native executable, .NET/Core and WindowsDesktop 8.0.33 files, x64 WebView2 loader, managed dependencies, and `frontend/index.html` plus hashed JS/CSS assets. WebView2 Runtime is a separate prerequisite. Node/npm, Vite, Visual Studio, Excel, and Python are unnecessary to run it.

No installer or system update is included. Missing assets produce a native WPF error before browser initialization. Missing runtime and initialization failures produce native errors with retry; renderer failure instructs the user to close/reopen rather than silently replaying operations. Shutdown and page navigation cancel diagnostics and clear native selection state.

Asset preflight checks `index.html` and every script/style reference it contains; a partially copied distribution is rejected rather than leaving an empty React view.

## Implementation File Map

- Repository/build: `.gitignore`, `Directory.Build.props`, `TsekSheet.sln`, and per-project `packages.lock.json` files.
- Application: `src/TsekSheet.Application/Foundation/BridgeSession.cs` implements the plain protocol/session and diagnostic behavior. Existing metadata service and architecture checks remain in use.
- Desktop: `TsekSheet.Desktop.csproj` builds/copies frontend output; `MainWindow.xaml` and `MainWindow.xaml.cs` host the view/native errors; `Properties/AssemblyInfo.cs` permits narrow test access to initialization options. Options are internal test composition, not a user-facing native command.
- Frontend: `index.html`, `package.json`, `scripts/check-toolchain.mjs`, `src/App.tsx`, `src/styles.css`, and `src/bridge/client.ts`. Existing exact dependencies and package lock are retained.
- Tests: `tests/contracts/protocol-v1.json`, `BridgeSessionTests.cs`, the Application test project's fixture-copy entry, the new Desktop test project and `DesktopSmokeTests.cs`, plus `src/bridge/client.test.ts` and `src/Connection.test.tsx`.
- Development documentation: this file. Architecture and product scope were not replaced.

The interface follows the frontend design skill's Framed sheet direction (seed 1575436647), retaining local Segoe UI and the existing restrained palette. Its main question is whether the local host connection works; the native progress bar is the product-specific evidence. There are no invented workbook data, vanity metrics, remote fonts, or decorative dependencies. Source design lint passed with zero findings; the standalone inspector could not capture its browser, so the actual embedded-browser screenshot and responsive smoke assertions were used for visual verification.

## Host and Security Boundaries

- WebView2 initializes asynchronously after asset/runtime checks, with bounded waits and close guards.
- `https://tseksheet.local/index.html` maps only to the bundled `frontend` directory. It is a local virtual origin, not a listening HTTPS server.
- Native messages require the exact HTTPS host and default port, no userinfo, a valid version-1 envelope, allowlisted command, and validated payload. Only the top-level message handler is registered.
- New windows, downloads, permissions, child-frame navigation, foreign navigation, and foreign resource requests are blocked. Production CSP permits only local scripts/styles/images/fonts, and denies network connections, frames, objects, forms, and eval. Host objects, autofill, password storage, context menus, developer tools, and browser accelerators are disabled.
- File selection uses a Windows dialog and returns a random selection ID and basename. A single path is retained only in C# memory for the page lifetime. The handler never reads, copies, parses, or edits the file. Cancellation is an ordinary result. Selection does not authorize later file operations.
- WebView2 uses `%LOCALAPPDATA%\TsekSheet\WebView2` for its required runtime profile. No application document/session data is written to browser storage or logs. Browser-managed files may persist; do not delete an active profile or claim secure erasure. Diagnostic work is in memory, and no telemetry is implemented.
- Browser/runtime background behavior is distinct from application asset requests. No global Windows network setting was changed. A system-level network/retention audit remains open.

## Protocol Version 1

Requests contain exactly `protocolVersion`, `requestId`, `command`, and `payload`. IDs allow 1–64 ASCII letters/digits, hyphens, and underscores. Maximum JSON size is 16 KiB UTF-8; maximum parse depth is 8. Duplicate keys, unknown fields, wrong types, unknown versions/commands, and invalid payloads are rejected. Malformed/oversized uncorrelatable input and untrusted origins are dropped without native dispatch.

Responses contain version, `type: response`, matching `requestId`, `ok`, and either `result` or a structured `error` with stable code and safe message. No exception details or paths are returned. Progress events contain version, `type: event`, `name: diagnostics.progress`, operation ID, completed count, and total.

| Command | Payload | Result |
| --- | --- | --- |
| `foundation.handshake` | `supportedVersions`, 1–4 integers including 1 | Version 1, metadata, actual capability flags. Must precede other commands. |
| `foundation.getInfo` | Empty object | Name, foundation version, platform, and no-unsaved-work state. |
| `foundation.selectFile` | Empty object | Selected ID/display name or cancelled. Only one dialog at a time. |
| `diagnostics.run` | `steps` 1–100; `delayMs` 10–200; duration at most 20 seconds | Progress and exactly one completed/cancelled/error outcome while the page remains alive. One run at a time. |
| `diagnostics.cancel` | Target operation ID | Cancellation requested or not running. Target has its own terminal response. |

The host retains at most 4096 used request IDs per page and rejects reuse. The frontend permits at most 16 pending requests; timeout is 30 seconds, or 120 seconds for a file dialog. Timeout reports an unknown outcome, not implicit cancellation. Unknown/late/malformed replies and unrelated/out-of-range progress are ignored. Page closure disposes listeners/pending requests; navigation disposes the host session and cancels work. No operation is silently replayed.

The plain Application project owns validation/session/diagnostic behavior. WPF owns WebView2 lifecycle, UI-thread dispatch, and the dialog adapter. React owns presentation and its pending-request map. `tests/contracts/protocol-v1.json` is consumed by both test stacks to catch wire-format drift.

## Validation Record and Remaining Acceptance Work

Verification uses SDK 8.0.427, the existing Windows 10 Pro 22H2 machine, and installed x64 WebView2 155.0.4283.62. This is not a supported clean Windows 11 acceptance environment.

Latest executed verification:

| Check | Result |
| --- | --- |
| Release solution build | Passed; 0 compiler warnings, 0 errors |
| Application .NET tests | 11 passed, 0 failed, 0 skipped |
| Windows desktop tests | 6 passed, 0 failed, 0 skipped; 5 launch actual windows/browser and 1 checks incomplete asset preflight |
| Strict frontend typecheck | Passed |
| Frontend tests | 8 passed, 0 failed, 0 skipped across 3 files |
| Vite production build | Passed |
| Self-contained Release win-x64 publish | Passed using .NET 8.0.33 and locked NuGet graph |
| Locked solution restore | Passed after making win-x64 explicit; initial NU1004 graph mismatch was corrected |

Initial transient checks also exposed a test's Loaded-event timing, a Vitest fixture-path assumption, and a locked native build module while preview was running. Each was fixed before the passing runs above. No failing or skipped automated test is being counted as passed.

Implemented tests cover metadata/architecture, shared wire fixtures, untrusted origins, invalid commands/versions/payloads, request correlation/duplicates, malformed input, diagnostic progress/cancellation/page disposal, React host-unavailable/connected/interface states, timeout/disposal, and unexpected messages. Windows STA tests use actual WPF/WebView2 with unique temporary profiles and bounded waits, not mocked browser initialization.

Real Windows smoke checks include React rendering at the virtual origin; actual React-to-C# handshake/info; a diagnostic run and cancellation; remote fetch denial; network-emulated-offline reload/reconnection; responsive horizontal-overflow checks at 390/768/1100 widths; missing assets; a nonexistent browser-runtime folder; renderer failure; and closing during initialization. Browser network emulation does not disable the machine's adapters or prove every runtime service is offline. Test profiles contain synthetic foundation state only and may remain under `%TEMP%\TsekSheet-smoke` if browser handles delay cleanup.

The published self-contained executable was separately launched; its accessibility tree showed React's content and **Connected** at the mapped local origin. Native screen capture failed with capture timeouts, and pointer/keyboard injection was unavailable in this environment. An embedded-browser PNG from the smoke test was inspected instead. Do not claim the native file dialog, focus restoration, keyboard traversal, or clean-machine operation was manually verified.

Before declaring Phase 1 complete, verify:

1. On a serviced clean Windows x64 machine, provision the approved runtime offline and run the complete distribution with network access actually unavailable. Confirm no first-run downloads or hidden development dependencies.
2. Manually open/cancel/select a synthetic file through the native dialog, compare file bytes before/after, verify only its basename appears, and check focus returns correctly.
3. Check keyboard operation, resize behavior, shutdown while a dialog is open, and repeated initialization/renderer failure recovery with a human-visible native window.
4. Review runtime/profile retention, application/runtime network behavior, offline runtime servicing, exact redistribution licenses, signatures, and distribution artifacts.
5. Resolve the documented supported-OS and .NET 8 maintenance deadline before a maintained release. Installer/upgrade hardening remains Phase 5.

Read the final build/test output for current passing/failing/skipped counts; no unexecuted acceptance check counts as passed. Phase 1 remains open until these criteria have evidence.
