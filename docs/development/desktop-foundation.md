# Desktop Foundation: Build, Test, and Run

Status: Implemented foundation; Phase 1 acceptance remains open.

This records the shell/transport milestone in [the Phase 1 plan](../../plans/phase-01-desktop-foundation.md). Later workbook editing, Merge and new-file Save As are documented separately. The [Step 40 migration report](dotnet-10-migration.md) records the current .NET 10 standard build and regression results; older validation counts below are historical. Phase acceptance remains open.

## Prerequisites and Pinned Dependencies

Use Windows x64 and stable .NET SDK **10.0.403** selected by root `global.json`. WPF targets `net10.0-windows`; Application targets `net10.0`. Desktop and desktop tests remain x64. Use CMD or VS Code plus the SDK; Visual Studio 2026 is optional, while Visual Studio 2022 does not officially support .NET 10 targeting. The full dependency inventory remains in [toolchain.md](toolchain.md).

Use stable Node **>=24.16.0 <25**, npm **>=11.13.0 <12**, and the retained `package-lock.json`. Current-PC verification uses 24.16.0/11.13.0; the earlier 24.22.1/11.19.1 pair is also accepted. No global npm installation is required.

The build discovers `npm.cmd` on PATH with `call npm.cmd`. An explicit absolute `NpmExecutable` can select a launcher, or the legacy `/p:NpmCliPath="C:\path\to\npm\bin\npm-cli.js"` selects a CLI through `node` on PATH. The same toolchain check applies. End users need neither Node nor npm.

NuGet package versions remain WebView2 **1.0.4314.35**, MSTest framework/adapter **4.5.2**, and Microsoft.NET.Test.Sdk **18.11.0**. Each .NET project now retains a `packages.lock.json`; reviewed restores can use locked mode. No new production framework is introduced. Frontend tests use the documented Vitest **5.0.5**, jsdom **30.1.3**, Testing Library React **16.3.3**, and DOM **10.4.2**. Optional jest-dom remains omitted because of its previously observed declaration conflict; built-in assertions cover these tests.

Current-PC .NET 10 desktop tests pass with WebView2 Runtime **156.0.4314.50 x64**. Earlier counts below used runtime 155.0.4283.62 on the prior PC. Runtime absence is handled with a native error; the app never bootstraps a runtime download. Supply an approved standalone offline runtime installer for clean deployments. Runtime distribution and servicing still need clean-machine acceptance.

The repository now targets .NET 10 LTS. Self-contained releases include their own runtime and require runtime servicing; keeping older SDKs installed does not change which SDK the repository pin selects. See the [migration report](dotnet-10-migration.md) for rollback and compatibility evidence.

## CMD Build and Test Instructions

All command blocks use Windows Command Prompt from the repository root. Paths containing spaces must remain quoted. Tools resolve from PATH. Stop after any command failure. In a `.cmd` wrapper, use `if errorlevel 1 exit /b 1` after each step.

```bat
dotnet --version
node --version
call npm.cmd --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build
```

Expect SDK 10.0.403 and an accepted Node/npm pair. The desktop build performs `npm ci --no-audit --no-fund`, strict typechecking, and a Vite production build, then replaces only its generated output `frontend` directory with current `dist` files. Publish does the same for its generated `frontend` directory, preventing stale hashed bundles from accumulating. Never put user files in these generated folders. Errors stop the build. Design-time builds do not invoke npm and are not build acceptance evidence. No development server is started, and no frontend source or node_modules is copied into the desktop distribution.

Stop any local Vite/Vitest process using the frontend folder before building Desktop. On Windows, `npm ci` replaces node_modules and cannot replace a native Rolldown module held by a preview or test process. During this task, stopping the preview resolved an EPERM error. Do not work around this with dependency upgrades or force-deleting locked files.

Frontend-only checks:

```bat
pushd src\TsekSheet.Frontend
call npm.cmd ci --no-audit --no-fund
call npm.cmd run test:toolchain
call npm.cmd run typecheck
call npm.cmd run test -- --run
call npm.cmd run build
popd
```

Browser preview is optional development tooling. Because the production CSP disallows remote connections and eval, use the production preview rather than assuming development HMR works. A browser without WebView2's native transport shows **Desktop host unavailable** and disables native controls. It never reports a successful C# connection.

## Release Windows x64 Distribution

```bat
dotnet publish src\TsekSheet.Desktop\TsekSheet.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -p:RestoreLockedMode=true -o artifacts\desktop-net10-win-x64
artifacts\desktop-net10-win-x64\TsekSheet.Desktop.exe
```

Restore/build can acquire packages using approved developer sources. This is separate from running the published app offline. Disconnected rebuilds additionally require a complete NuGet/npm cache and RID-specific runtime packs; this task does not claim those have been provisioned on a clean machine. `Directory.Build.props` explicitly includes the `win-x64` runtime graph so ordinary build and Windows publishing use consistent lockfiles. Review intentional dependency/runtime changes before regenerating locks.

Keep the **entire** publish directory together. It contains the native executable, .NET/Core and WindowsDesktop 10.0.14 files, x64 WebView2 loader, managed dependencies, and `frontend/index.html` plus hashed JS/CSS assets. WebView2 Runtime is a separate prerequisite. Node/npm, Vite, Visual Studio, Excel, and Python are unnecessary to run it.

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
