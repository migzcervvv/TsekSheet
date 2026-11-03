# TsekSheet

Step 56 improves [read-only spreadsheet navigation and Section Review](docs/development/payables-section-review.md): scrollbars, wheel/trackpad browsing, Go To Cell, keyboard movement, useful-data bounds and six focused review categories. Discarding in-memory decisions requires confirmation, including worksheet/workbook changes and native window closing. Internal blank rows remain visible; the full stored range remains available. Editing, insertion, Merge and export remain unavailable in inspection. See [verification](docs/development/step56-verification.md). Phase 2 remains incomplete.

Step 54 adds [conservative format isolation and separate read-only section diagnostics](docs/development/step54-verification.md). The real source is unchanged; 401 solution tests pass. Numeric support remains narrow (21 qualified of 23,452), and the incomplete hierarchy/25 blank vendors remain unverified and non-actionable. The inspector explains source/effective formats; no inspection editing, Apply or export is enabled. [A reviewed separate-output workflow](docs/development/reviewed-payables-output-design.md) is documented only.

Step 53 [qualifies the actual authorized Payables duplicate](docs/development/step53-verification.md): all five worksheets browse read-only, source fingerprint unchanged, and 359 solution tests pass. Editing is blocked by measured cell counts and complex package preservation requirements; numeric interpretation currently falls back to raw values. Section/vendor analysis remains partial, and production Apply stays disabled.

Step 52 repairs [Git provenance portability](docs/development/step52-verification.md): 359 solution tests pass with zero failures/skips, and independent Windows checkout verification passes. Retained native evidence and exact hash checks are preserved. Real-workbook qualification remains **UNEXECUTED** until an authorized duplicate is supplied in `local-test-workbooks/`; editing restrictions are unchanged.

Step 51 records [diagnostic readiness](docs/development/step51-verification.md) and a [conditional compatibility strategy](docs/development/real-workbook-compatibility-strategy.md). The authorized input directory is absent, so real-workbook qualification remains **UNEXECUTED**. Current regression results include one provenance failure and one native-reference skip; production mutation restrictions are unchanged.

Step 50 adds actual synthetic Excel precision evidence and rejects ambiguous over-precision monetary displays while retaining raw values. Real workbook qualification remains unexecuted. See [verification](docs/development/step50-verification.md).

Step 49 retains actual synthetic native Excel accounting displays and corrects escaped saved formats and built-in percentages. See [verification](docs/development/step49-verification.md).

Step 48 adds a restricted read-only accounting-number display subset and preserves intentionally blank formatted zeros. Real workbook qualification remains unavailable. See [verification](docs/development/step48-verification.md).

Step 47 adds [read-only date/time and shared-formula interpretation](docs/development/read-only-cell-interpretation.md), cell details and a bounded recent-viewport cache. [Verification](docs/development/step47-verification.md) distinguishes synthetic evidence from untested real accounting files. Inspection never authorizes mutation.

Step 46 adds **Inspect workbook read-only**, a separate bounded package analyzer and selected-sheet raw-value viewer. It reports compatibility without granting editing or export eligibility. See [compatibility analyzer](docs/development/workbook-compatibility-analyzer.md) and [verification](docs/development/step46-verification.md).

TsekSheet is an offline-first Windows desktop application for safely working with existing Excel workbooks. Its longer-term goal is to turn scanned receipts and PDFs into human-verified spreadsheet entries using local OCR and configurable templates.

The first intended workflow is accounting reimbursement. The architecture is designed for other spreadsheet workflows as well, without accounting-specific rules in the workbook engine.

## Development status

TsekSheet is an early development foundation, not a finished reimbursement or OCR product. The desktop shell, limited workbook editing, conservative in-memory Merge, and new-file-only Save As are implemented. Phase 0 evaluation and repository work remain in progress, Phase 1 acceptance still has outstanding checks, and Phase 2 is incomplete.

The [.NET 10 migration](docs/development/dotnet-10-migration.md) is verified on this Windows PC: standard Release build, all regression suites, native file-commit checks and self-contained publish pass with SDK 10.0.403. Existing workbook safety behavior is retained. Phase 2 and clean-machine release acceptance remain incomplete.

[GOALS.md](GOALS.md) defines the product direction; [PLANS.md](PLANS.md) defines the roadmap. Architecture documents describe accepted decisions, while development documents record individual implementation milestones. Older milestone descriptions may predate the latest capabilities described here and in [workbook-safe-saving.md](docs/development/workbook-safe-saving.md).

## Implemented features

- WPF desktop host with React and TypeScript bundled locally in WebView2 at `https://tseksheet.local`; no Vite server is needed to run the application.
- Validated, versioned React-to-C# messaging with request correlation, restricted origins, diagnostic progress and cancellation, and native file dialogs.
- Bounded local `.xlsx` opening, worksheet navigation, hidden-sheet inspection, and viewport rendering.
- In-memory workbook sessions with original package bytes and SHA-256 fingerprints, revision checks, compatibility inspection, and resource limits.
- Independent full-context worksheet working copies. Only the selected worksheet is editable; surrounding sheets remain read-only context.
- Controlled text, number, date, and explicit formula edits to supported populated cells, with formula-overwrite protection and isolated candidate validation.
- Reviewed Merge of recorded cell changes into the original worksheet in memory, with stable identities, strict base/revision checks, conflict rejection, candidate validation and atomic adoption.
- [Manual workflow planning](docs/workflows/payables-processing.md) with approved versioned JSON profiles, bounded whole-worksheet section detection, explicit multi-section/vendor choices, duplicate warnings and batch proposals. **Structural insertion remains disabled.** The synthetic Payables and Laboratory profiles share one generic planner.
- [Structural capability diagnostics](docs/development/structural-preservation-oracle.md) distinguish blocked structures and unresolved review from experimental package preflight. An independent synthetic preservation oracle and internal row candidate editor are tested; desktop Apply and structural Merge/export remain disabled.
- [Candidate lifecycle review and Excel reference evidence](docs/development/excel-reference-corpus.md) distinguish planning from generation, oracle verification, approval, adoption, integration and export. A separately gated internal structural Merge/export path is tested; production Apply remains disabled and native Excel comparisons retain explicit formatting limitations.
- [Interactive candidate review](docs/development/step45-verification.md) builds isolated candidates, displays actual oracle/native-format findings and records explicit review-only approval for the narrow qualified height policy. Approval leaves workbook data/revisions unchanged; public Apply/adoption remain disabled. Seventeen additional actual Excel captures retain exact formatting successes and failures.
- **Save As to a separate, brand-new `.xlsx` file only**, using protected staging, a no-overwrite commit, and final file identity/content verification on supported local fixed NTFS paths.
- Automated application, workbook, protocol, frontend, filesystem, and actual Windows desktop smoke tests, with artificial reimbursement and laboratory fixtures.

The desktop editor changes working copies only. Review and confirm Merge to integrate supported edits into the original main session. **Save As exports the main session and excludes unintegrated working-copy edits.** Integration does not save automatically. Working copies cannot be saved directly.

## Planned features

- Worksheet Replace and Create New integration, pending independently verified reference/resource preservation.
- Generic versioned JSON templates, reusable worksheet bindings, and template-driven row insertion.
- Offline PDF/image processing and Python OCR, followed by mandatory human verification.
- A separately proven existing-file save/replacement strategy with conflict detection and recovery safeguards.
- Broader workbook compatibility evidence, installer and upgrade work, clean-machine offline validation, and release hardening.

These features are planned; their presence in an architecture document or experimental project does not mean they are available in the desktop application. No database, cloud processing, or paid AI API is used.

## Technology stack

Approved logo assets live in `src/TsekSheet.Frontend/public/branding/`: `TsekSheet.svg` and `TsekSheet-No-BG.svg`. The header uses the transparent version; Vite copies both unchanged into the desktop's bundled frontend assets.

| Component | Current choice |
| --- | --- |
| Desktop | WPF, `net10.0-windows`, Windows x64 |
| Embedded browser | Microsoft.Web.WebView2 1.0.4314.35 |
| Application and workbook code | C# 14/.NET 10; application libraries target `net10.0` |
| Workbook processing | ClosedXML 0.105.1 and Open XML SDK 3.5.1 |
| Interface | React 19.3.1, TypeScript 7.0.2, Vite 8.3.5 |
| Tests | MSTest, Vitest, React Testing Library |
| OCR | Python process integration is planned, not implemented |

Dependency versions are pinned in project/package manifests and retained lockfiles. The SDK is fixed by [global.json](global.json). See [toolchain.md](docs/development/toolchain.md) for the baseline and [desktop-foundation.md](docs/development/desktop-foundation.md) for build/runtime details.

## Prerequisites

- Windows x64 with an interactive desktop for WPF/WebView2 smoke tests. A supported clean Windows environment remains a release-validation requirement.
- **Stable .NET SDK 10.0.403 x64**. SDK roll-forward is disabled; another SDK version does not satisfy `global.json`.
- Stable **Node.js >=24.16.0 <25 x64** and **npm >=11.13.0 <12** for frontend builds and tests. The current verified pair is 24.16.0/11.13.0; exact dependency versions remain locked. Put `node` and `npm.cmd` on PATH in the build shell.
- A locally installed x64 WebView2 Runtime. Current-PC smoke tests used 156.0.4314.50; clean-machine runtime distribution and servicing still need validation. The app does not download a missing runtime.
- Git. Use Windows CMD or VS Code with the installed .NET SDK for CLI development. Visual Studio 2026 is optional; Visual Studio 2022 does not officially support .NET 10 targeting.
- Approved NuGet/npm sources or complete local caches for dependency provisioning. An initial developer restore/build can require network access; application document processing and bundled UI assets are local.

Excel and Python are not prerequisites for the currently implemented application. Keep .NET 8 installed if you need the retained pre-migration comparison/rollback files; it is no longer the build SDK. [The migration report](docs/development/dotnet-10-migration.md) contains VS Code instructions and build guidance. [The dependency security review](docs/development/dependency-security-review.md) removes 14 of 19 evaluation-only advisories through a cryptography servicing patch; five ImageSharp advisories remain visible in the synthetic-only NPOI research graph. Production/solution and npm audits report no vulnerable packages on the queried feeds; that is not a production-readiness certification. Those research libraries are excluded from the production publish.

## Build and test from Windows CMD

Run these commands from the repository root. Stop after any failed command. The desktop build discovers `npm.cmd` on PATH, including NVM-managed installations.

```cmd
dotnet --version
node --version
where node
where npm.cmd
call npm.cmd --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --no-restore
```

The desktop build runs `call npm.cmd ci --no-audit --no-fund`, then typechecks and builds React and copies its assets into the desktop output. This reinstalls the locked frontend dependencies; stop any Vite/Vitest process holding files in `node_modules` before building. NuGet lockfiles and `package-lock.json` must remain under version control.

Frontend tests are separate from `dotnet test`:

```cmd
pushd src\TsekSheet.Frontend
call npm.cmd run test:toolchain
call npm.cmd run typecheck
call npm.cmd run test -- --run
call npm.cmd run build
popd
```

For an explicit launcher, use `-p:NpmExecutable="C:\path\to\npm.cmd"`. The legacy `-p:NpmCliPath="C:\path\to\npm-cli.js"` override is still supported and invokes that CLI through `node` on PATH. Overrides must meet the same toolchain range; they do not bypass locked installation.

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
src\TsekSheet.Desktop\bin\Release\net10.0-windows\TsekSheet.Desktop.exe
```

Keep the output folder, including its `frontend` subfolder and runtime dependencies, together. The framework-dependent build needs the .NET 10 Windows Desktop Runtime and WebView2 Runtime locally. No development server is required. Self-contained Windows x64 publishing instructions are in [desktop-foundation.md](docs/development/desktop-foundation.md); a production installer is not available yet.

For a safe demonstration, open either workbook under `tests\WorkbookLibrary.Evaluation\Fixtures`, create a working copy and edit a supported populated cell. Review Merge, check the destination and effective edit count, then confirm integration in memory. Save As from the original view creates a separate new export containing integrated edits. Unintegrated copies remain excluded. Closing a session discards unsaved changes after confirmation.

## Current limitations and safety restrictions

- Only a bounded subset of plain `.xlsx` workbooks is supported. Unsupported, protected, active, externally linked, malformed, or ambiguous content is rejected or restricted to inspection. Do not assume full Excel fidelity from the small synthetic fixtures.
- Formula text is preserved within the supported scope; formula results are cached and may be stale. Excel-equivalent recalculation is not implemented.
- The desktop does not provide unrestricted cell/format editing, worksheet Replace/Create New, row insertion, templates, PDF processing, or OCR. Merge rejects any destination revision divergence since copy creation.
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
