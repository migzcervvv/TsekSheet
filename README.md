# TsekSheet

TsekSheet is an offline-first Windows desktop application for safely working with existing Excel workbooks. Its longer-term goal is to turn scanned receipts and PDFs into human-verified spreadsheet entries using local OCR and configurable templates.

The first intended workflow is accounting reimbursement. The architecture is designed for other spreadsheet workflows as well, without accounting-specific rules in the workbook engine.

## Development status

TsekSheet is an early development foundation, not a finished reimbursement or OCR product. The desktop shell, limited workbook editing, and new-file-only Save As are implemented. Phase 0 evaluation and repository work remain in progress, Phase 1 acceptance still has outstanding checks, and Phase 2 is incomplete.

[GOALS.md](GOALS.md) defines the product direction; [PLANS.md](PLANS.md) defines the roadmap. Architecture documents describe accepted decisions, while development documents record individual implementation milestones. Older milestone descriptions may predate the latest capabilities described here and in [workbook-safe-saving.md](docs/development/workbook-safe-saving.md).

## Implemented features

- WPF desktop host with React and TypeScript bundled locally in WebView2 at `https://tseksheet.local`; no Vite server is needed to run the application.
- Validated, versioned React-to-C# messaging with request correlation, restricted origins, diagnostic progress and cancellation, and native file dialogs.
- Bounded local `.xlsx` opening, worksheet navigation, hidden-sheet inspection, and viewport rendering.
- In-memory workbook sessions with original package bytes and SHA-256 fingerprints, revision checks, compatibility inspection, and resource limits.
- Independent full-context worksheet working copies. Only the selected worksheet is editable; surrounding sheets remain read-only context.
- Controlled text, number, date, and explicit formula edits to supported populated cells, with formula-overwrite protection and isolated candidate validation.
- **Save As to a separate, brand-new `.xlsx` file only**, using protected staging, a no-overwrite commit, and final file identity/content verification on supported local fixed NTFS paths.
- Automated application, workbook, protocol, frontend, filesystem, and actual Windows desktop smoke tests, with artificial reimbursement and laboratory fixtures.

The desktop editor changes working copies only. **Save As exports the original main session and excludes pending working-copy edits.** There is currently no UI operation to integrate those edits into the main session or save a working copy directly.

## Planned features

- Previewed worksheet Merge, Replace, and Create New integration.
- Generic versioned JSON templates, reusable worksheet bindings, and template-driven row insertion.
- Offline PDF/image processing and Python OCR, followed by mandatory human verification.
- A separately proven existing-file save/replacement strategy with conflict detection and recovery safeguards.
- Broader workbook compatibility evidence, installer and upgrade work, clean-machine offline validation, and release hardening.

These features are planned; their presence in an architecture document or experimental project does not mean they are available in the desktop application. No database, cloud processing, or paid AI API is used.

## Technology stack

| Component | Current choice |
| --- | --- |
| Desktop | WPF, `net8.0-windows`, Windows x64 |
| Embedded browser | Microsoft.Web.WebView2 1.0.4314.35 |
| Application and workbook code | C#/.NET 8; application libraries target `net8.0` |
| Workbook processing | ClosedXML 0.105.1 and Open XML SDK 3.5.1 |
| Interface | React 19.3.1, TypeScript 7.0.2, Vite 8.3.5 |
| Tests | MSTest, Vitest, React Testing Library |
| OCR | Python process integration is planned, not implemented |

Dependency versions are pinned in project/package manifests and retained lockfiles. The SDK is fixed by [global.json](global.json). See [toolchain.md](docs/development/toolchain.md) for the baseline and [desktop-foundation.md](docs/development/desktop-foundation.md) for build/runtime details.

## Prerequisites

- Windows x64 with an interactive desktop for WPF/WebView2 smoke tests. A supported clean Windows environment remains a release-validation requirement.
- **.NET SDK 8.0.427**. SDK roll-forward is disabled; another SDK version does not satisfy `global.json`.
- **Node.js 24.22.1 x64** and **npm 11.19.1** for frontend builds and tests.
- A locally installed x64 WebView2 Runtime. The development tests used runtime 155.0.4283.62; clean-machine runtime distribution and servicing still need validation. The app does not download a missing runtime.
- Git. Visual Studio 2022 with the .NET desktop workload is optional for CLI development and remains compatible with the solution.
- Approved NuGet/npm sources or complete local caches for dependency provisioning. An initial developer restore/build can require network access; application document processing and bundled UI assets are local.

Excel and Python are not prerequisites for the currently implemented application. **.NET 8 support ends November 10, 2026**; a maintained release beyond that date requires an upgrade. This repository does not require installing .NET 10.

## Build and test from Windows CMD

Run these commands from the repository root. Stop after any failed command. They assume the normal Node installation under `%ProgramFiles%\nodejs`; adjust `NpmCliPath` if Node is installed elsewhere.

```cmd
dotnet --version
node --version
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --no-restore
```

The desktop build runs the pinned npm CLI with `ci --no-audit --no-fund`, then typechecks and builds React and copies its assets into the desktop output. This reinstalls the locked frontend dependencies; stop any Vite/Vitest process holding files in `node_modules` before building. NuGet lockfiles and `package-lock.json` must remain under version control.

Frontend tests are separate from `dotnet test`:

```cmd
pushd src\TsekSheet.Frontend
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run typecheck
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run test -- --run
node "%ProgramFiles%\nodejs\node_modules\npm\bin\npm-cli.js" run build
popd
```

The direct npm CLI path avoids a documented local launcher-version mismatch. For a nonstandard location, add `-p:NpmCliPath="C:\path\to\npm-cli.js"` to the desktop/solution build command. The Node and npm versions must still match the pinned baseline.

Optional filesystem research checks use only their own synthetic temporary files:

```cmd
dotnet restore tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj -c Release --no-restore
dotnet restore tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj --locked-mode
dotnet run --project tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj -c Release --no-restore
```

## Launch

After the Release build:

```cmd
src\TsekSheet.Desktop\bin\Release\net8.0-windows\TsekSheet.Desktop.exe
```

Keep the output folder, including its `frontend` subfolder and runtime dependencies, together. The build output needs the .NET 8 Windows Desktop Runtime and WebView2 Runtime locally. No development server is required. Self-contained Windows x64 publishing instructions are in [desktop-foundation.md](docs/development/desktop-foundation.md); a production installer is not available yet.

For a safe demonstration, open either workbook under `tests\WorkbookLibrary.Evaluation\Fixtures`, create a working copy, edit a supported populated cell, and switch back to the unchanged original-session view. Working-copy edits remain in memory. Closing a session discards unsaved changes after confirmation. Save As from the original view creates a separate export and does not include those pending edits.

## Current limitations and safety restrictions

- Only a bounded subset of plain `.xlsx` workbooks is supported. Unsupported, protected, active, externally linked, malformed, or ambiguous content is rejected or restricted to inspection. Do not assume full Excel fidelity from the small synthetic fixtures.
- Formula text is preserved within the supported scope; formula results are cached and may be stale. Excel-equivalent recalculation is not implemented.
- The desktop does not provide unrestricted cell/format editing, worksheet integration, row insertion, templates, PDF processing, or OCR.
- Original workbooks are not overwritten. Existing-file Save and replacement are disabled, and a concurrently created Save As destination is rejected.
- New-file Save As requires a regular absolute path on a local fixed NTFS drive, with a maximum path length of 220 characters. Network paths, linked/redirected folders, device paths and unsupported volumes are rejected.
- Save As does not rebind the session or clear pending working-copy changes. Native paths stay inside C#.
- Unsaved sessions exist in memory only. There is no autosave or persistent session recovery. A crash can leave an owned staging file or a committed export with no in-memory receipt; uncertain results require inspection before retrying.
- Power-loss durability, disk exhaustion, unsupported filesystems, representative real workbooks, native dialog interaction, and clean-machine offline installation are not fully qualified. Some desktop tests use internal dialog-selection seams.
- Dependency notices, runtime redistribution, supported Windows versions, and release security review remain outstanding.

Detailed guarantees, error handling and recovery procedures are in [workbook-safe-saving.md](docs/development/workbook-safe-saving.md). Research evidence is in [windows-safe-file-commit.md](docs/research/windows-safe-file-commit.md) and [workbook-library-evaluation.md](docs/research/workbook-library-evaluation.md).

## Project structure

```text
TsekSheet.sln
global.json                       Pinned .NET SDK
GOALS.md / PLANS.md / AGENTS.md    Product direction, roadmap, development rules
src/
  TsekSheet.Application/           Plain application services, contracts, bridge validation
  TsekSheet.Workbooks/             Workbook engine, candidate checks, new-file commit
  TsekSheet.Desktop/               WPF/WebView2 host and native dialogs
  TsekSheet.Frontend/              React UI, TypeScript bridge, frontend tests
tests/
  TsekSheet.Application.Tests/
  TsekSheet.Workbooks.Tests/
  TsekSheet.Desktop.Tests/
  contracts/                      Shared protocol fixture
  WorkbookLibrary.Evaluation/     Isolated library research and synthetic .xlsx fixtures
  WindowsFileCommit.Evaluation/   Isolated filesystem research
  WindowsFileCommit.NativeEvaluation/
docs/
  architecture/                   Overview and accepted ADRs
  development/                    Milestone behavior and reproduction instructions
  research/                       Findings and retained synthetic research evidence
plans/                            Incremental implementation plans
```

Generated build output, installed dependencies, test results, local secrets and recovery files are excluded by `.gitignore`. The retained `.xlsx` fixtures contain artificial data only; do not add real receipts or user workbooks to the repository.

## License status

No project license has been selected or supplied in this repository. Do not infer a TsekSheet license from any dependency's license. Third-party components retain their own terms, and redistribution review remains incomplete. Existing dependency-license findings are recorded in [the workbook library evaluation](docs/research/workbook-library-evaluation.md) and [the toolchain baseline](docs/development/toolchain.md).
