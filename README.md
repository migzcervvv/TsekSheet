# TsekSheet

TsekSheet is a local-first Windows desktop app that turns scanned documents into human-verified rows in existing Microsoft Excel workbooks, fully offline. OCR will propose printed fields for review; users can also type and verify fields manually. Excel is the required workbook engine and will own all workbook writes.

## Current status

Working today:

- WPF + WebView2 shell, versioned validated bridge, light/dark UI.
- Workbook opening and read-only inspection using ClosedXML / Open XML SDK.
- Receipt review: register local PDF/PNG/JPEG files, render pages with a one-shot renderer capped at 1600 px, enter fields manually, verify/retract, and use synthetic Payables/Laboratory profiles with read-only section analysis and placement preview.

OCR, writing verified rows into a workbook, multiple workbooks, and persistence are not implemented. The legacy ClosedXML write path and Excel-native preservation PoC are superseded; the PoC is archived outside the solution and product build. Legacy product write code remains until ticket T2b.

[GOALS.md](GOALS.md) defines the product; [PLANS.md](PLANS.md) is the current ticket queue; [AGENTS.md](AGENTS.md) defines development and workbook safety rules. Earlier plans, progress, and README step history are retained in [docs/history/](docs/history/) as historical records, not current instructions.

## Prerequisites

- Windows 10 22H2 / Windows 11 x64.
- Microsoft Excel desktop, Microsoft 365 or 2021+; 64-bit preferred.
- .NET SDK **10.0.403 x64**, pinned by `global.json` with roll-forward disabled.
- Node.js **>=24.16.0 <25 x64** and npm **>=11.13.0 <12** on PATH.
- Locally installed x64 WebView2 Runtime. The app does not download a missing runtime.
- Git and access to NuGet/npm sources or complete local caches for the initial developer restore. Document processing and bundled UI assets run locally.

## Build, run, and test

Run from the repository root in Windows CMD:

```cmd
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore
src\TsekSheet.Desktop\bin\Release\net10.0-windows\TsekSheet.Desktop.exe
```

The desktop build installs locked frontend dependencies, typechecks, builds the React assets, and bundles them locally. Stop any Vite/Vitest process holding `node_modules` files before building. Keep the output folder, its `frontend` and `renderer` subfolders, and runtime dependencies together. The framework-dependent app needs .NET 10 Windows Desktop Runtime and WebView2 Runtime; no development server is required. Desktop smoke tests require an interactive Windows desktop.

Frontend checks:

```cmd
pushd src\TsekSheet.Frontend
call npm.cmd run test:toolchain
call npm.cmd run typecheck
call npm.cmd run test -- --run
popd
```

The isolated workbook-library evaluation is outside the solution:

```cmd
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj -c Release --no-restore
```

Use synthetic fixtures only. Redacted local inputs belong in gitignored `fixtures/redacted/`; never commit real receipts or accounting workbooks. A production installer and clean-machine offline validation are pending.

## License

TsekSheet is licensed under [AGPL-3.0](LICENSE). Third-party dependencies retain their own license terms.
