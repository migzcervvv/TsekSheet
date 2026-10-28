# Read-only workbook engine foundation

This document describes the earlier read-only milestone. Later behavior is documented in [workbook editing](workbook-editing-foundation.md), [new-file Save As](workbook-safe-saving.md) and [Merge integration](worksheet-integration.md). Historical tool paths/counts below are superseded by the [current environment and migration handoff](dotnet-10-migration.md).

The current adapter targets .NET 10 after [Step 40](dotnet-10-migration.md); read-only opening, source fingerprints, compatibility rejection and all existing workbook regressions pass without changing their integrity checks.

Status: read-only foundation implemented. Phase 2 is not complete.

## Implementation plan

1. Add plain application contracts and one .NET 8 workbook adapter project, using ClosedXML 0.105.1 and Open XML SDK 3.5.1. No workbook package references in Application or Desktop.
2. Bound file/package/XML/cell resource consumption before library loading; reject malformed, protected, active or externally linked packages. Preserve source bytes and SHA-256 privately. Expose only bounded, read-only projections and explicit compatibility findings.
3. Implement disposable sessions and bounded independent serialization/reopening snapshots. No public mutable workbook objects, editing APIs or save paths. Prove lifecycle, source preservation and failure handling with both existing synthetic fixtures.
4. Build and run core/application tests before enabling desktop integration. Keep native selection paths within C#, scope sessions to the bridge lifetime, validate every viewport and identity, and dispose on navigation/closure.
5. Add a minimal read-only React viewer using existing interface conventions: compact worksheet navigation and a bounded cell table, with visible read-only/unsupported states. Preserve diagnostics and demonstration selection.
6. Run solution/frontend/experimental regression tests and a real WPF/WebView2 viewer smoke test. Record results, limitations and CMD instructions here.

No cell edits, sheet integration, row insertion, templates, OCR, Save or Save As are part of this implementation. Unknown preservation behavior must never become an implied editing capability.

## Architecture and operation

`TsekSheet.Application.Workbooks` defines plain .NET contracts, immutable data records and a small per-page workspace. `TsekSheet.Workbooks` implements those contracts with ClosedXML **0.105.1** and Open XML SDK **3.5.1** on **net8.0**. The application contract assembly references neither workbook library nor WPF/WebView2. The WPF host references the engine project for composition; workbook-library package references belong to the engine. Locked package graphs are retained, including ClosedXML's production-resolved SixLabors.Fonts **1.0.0**. License and dependency review must be repeated for releases and upgrades; the earlier experiment's graph included NPOI and resolved Fonts 1.0.1.

The native dialog selects a path, but the path is passed directly to the local C# workspace and engine; React sends no path and receives only the display name, session/worksheet identifiers and bounded inspection data. A successful open replaces the prior workspace session. Cancellation or failure retains it. Opening and viewport work run on a worker thread; workbook operations are serialized per workspace. The session is disposed on close, page navigation, host failure or shutdown. The existing demonstration file picker and diagnostics remain separate.

The engine opens a local `.xlsx` with read access and a sharing mode that excludes writers during the read. It hashes the bytes with SHA-256, closes the source file, and retains the original bytes in a private, read-only in-memory stream and baseline. Source files are never opened for writing. Each new session gets a GUID, revision 0 and `IsDirty=false`; each worksheet has an ID stable for that session and its descendant snapshots. Another independently opened session receives fresh IDs. No edit, integration or save capability is exposed and `EditingAllowed` is always false.

All worksheet tabs, including hidden and unselected worksheets, are loaded. A session returns name, order, visibility and content-based used extent, plus creator/title and compatibility findings. Viewports request at most **20 rows by 10 columns**, within Excel bounds. They contain literal values, formula text, value type, explicit number-format code and ID, bold/italic and a basic fill color. Cell text and formulas are capped at 1,024 characters and marked when truncated. The displayed data is never HTML. Rendering uses stored cached values and never requests recalculation; cached formula results can be absent or stale. Empty cells with only formatting are returned as blank/default-format cells, and used dimensions describe content rather than every formatted blank cell.

The session builds a bounded C# inspection projection for stored content. Repeated blank-viewport reads do not create new ClosedXML cells. React receives only a small window, initially 20 by 6, with row and column paging. Viewport identities, coordinates, shape and contents are checked by the frontend and the C# engine. Native commands are limited to `workbook.open`, `workbook.readViewport` and `workbook.close` within the existing versioned, origin-checked bridge. `workbook.open` accepts an empty payload only; arbitrary frontend paths and save/edit commands are rejected.

## Input validation and compatibility

The default limits are **16 MiB compressed**, **64 MiB expanded**, **1,024 ZIP entries**, **50,000 stored cells**, **32 worksheets**, **4,096 style records**, **500,000 XML elements**, **64 nesting levels**, and **four active sessions/snapshots** per engine. An estimated shared session budget is **256 MiB**. Actual decompressed bytes as well as ZIP metadata are counted. Admission is serialized to avoid unbounded concurrent opening; XML parsing prohibits DTDs and external entity resolution. These are conservative admission limits, not a guaranteed hard process-memory ceiling. The limits and typical large-workbook performance require further measurement.

ZIP entry names and relationships are checked for unsafe paths, duplicates, external targets and missing parts. Open XML SDK validates structure before ClosedXML loads the workbook. Files with legacy/compound or unsupported extensions, encryption, protection, external relationships, macros/active content, chart sheets, signatures, binary/embedded content, malformed package/XML, links or reparse points in the selected path, or network locations are rejected with a fixed actionable error. Some ordinary workbooks containing images, hyperlinks, or other advanced features are therefore rejected even for viewing. No external resource is fetched, macro run, formula recalculated, or user file written. Checking filesystem paths before opening is not a defense against a malicious concurrent local filesystem change.

Unknown package parts/relationships and detected advanced content are surfaced as findings and restrict the session to inspection. A read-only finding is always present. This mechanism is conservative and does not prove it detects every possible Excel feature. Unknown preservation behavior remains a blocker for future mutation or saving; successfully opening a workbook does not certify its fidelity. Workbook contents and errors are not written to application logs.

## Snapshots and isolation

`CreateSnapshot` opens a fresh model from the immutable baseline, serializes it into an in-memory candidate, reopens it as a separate ClosedXML workbook, and gives it a new session ID with the source session's ID as parent. This avoids repeated serialization mutating the live read-only model. Worksheet IDs remain stable across that lineage. Snapshot preparation and the candidate each reserve against the shared estimated memory budget; the candidate is checked by the SDK. Before return, the engine compares worksheet identity, order, visibility and content extent; stored cell values, formula text and basic styles; defined names; merged ranges; table and autofilter extents; the workbook date system; creator/title and custom properties; and retained custom XML bytes. It rejects a mismatch instead of adopting a damaged snapshot. Advanced or unrecognized content and truncated cell projections block snapshots.

These comparisons cover only the implemented inspection contract. They are not byte-identical serialization or an Excel feature-preservation guarantee. They do not validate every format, cached calculation, style, relationship, drawing, feature extension or rendered appearance. The immutable source baseline is the original package, not the reserialized snapshot. The model is not available through public application contracts. Automated tests use a private test probe to mutate one snapshot's cell and style and verify that the source and a second snapshot have independent workbook objects. Production exposes no mutation API.

Disposal releases the ClosedXML model, its in-memory source stream, baseline and projection and returns its admission reservation. A snapshot remains usable after its parent closes. Invalid session/worksheet IDs, disposed objects, excessive windows and limits fail without source changes. Cancellation is checked during package scanning, before/after ClosedXML loading and during snapshot validation; a synchronous library load/serialization already in progress cannot be forcibly interrupted.

## Development commands

Run from the repository root in Windows CMD with SDK **8.0.427**, Node **24.22.1**, npm **11.19.1**, and an installed WebView2 Runtime. Package restore may access configured NuGet sources during development; the application uses locally bundled frontend assets and processes workbooks locally.

```cmd
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set TESTINGPLATFORM_TELEMETRY_OPTOUT=1
dotnet --version
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --no-restore
cd src\TsekSheet.Frontend
node "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js" run typecheck
node "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js" run test -- --run
cd ..\..
src\TsekSheet.Desktop\bin\Release\net8.0-windows\TsekSheet.Desktop.exe
```

In the running desktop application, choose **Open workbook**, select a local `.xlsx`, choose a worksheet and page through the viewport. **Close workbook** releases the session. This is inspection only. The actual native dialog needs an interactive Windows desktop; the automated end-to-end viewer test injects fixture paths through an internal host test seam rather than operating the dialog.

## Tests and remaining work

The workbook tests cover both retained synthetic fixtures, source hashes before and after open/snapshots/close, all sheet names and hidden state, cross-sheet formulas, formatting/metadata, two independent snapshots, resource and viewport bounds, disposal, cancellation, unsupported extensions/network paths, protected, corrupt, macro, externally linked, DTD, deeply nested, duplicate-entry and dangling-relationship cases. An advanced-content fixture is made only in a temporary test directory and proves inspection remains available while snapshots are blocked. Architecture checks prohibit UI dependencies in the engine/contracts and workbook library dependencies in the contract assembly. Workspace/bridge tests cover session lifetime, failed/cancelled selection, concurrency, paths, unauthorized origins and disabled save commands.

The desktop smoke test hosts real WPF and WebView2, loads the bundled React interface, opens both synthetic workbooks using the internal selector seam, verifies the rendered formula viewport, switches to a hidden worksheet, tests 390/768/1100-pixel layouts, closes each session and verifies the fixture hashes. The normal native picker requires manual interaction and is not exercised by that automation. Native Excel opening/repair, pixel-perfect Excel rendering, calculation fidelity, arbitrary feature preservation and offline operation on a separate clean machine are unverified.

Verification on 2026-10-28: the Release solution build succeeded with **0 warnings and 0 errors**. The solution suite passed **14 application**, **19 workbook**, and **7 desktop** tests, with **0 failed and 0 skipped**. The independent earlier workbook evaluation suite passed **13** harness tests; the frontend suite passed **11** tests. The pinned SDK reported **8.0.427**. The standalone Release executable started with a responsive UI thread and a window; the test then closed that process. SHA-256 hashes of both original synthetic fixtures matched their retained manifest after the tests.

The browser-based app-screen inspection of the bundled interface reported **0 failures and 1 warning**: a 41-pixel gap in the pre-existing diagnostics panel at desktop width. The workbook viewer's empty and populated states were inspected at narrow, tablet and desktop widths. The primary job and the selected workbook are visible in the reader, data remains inside its own horizontally scrollable table, and the native host is honestly shown as unavailable in ordinary browser preview. At 390 pixels the table begins below the first screen; the intended primary environment is the Windows desktop. The extra diagnostics spacing is retained rather than redesigning that existing feature in this slice.

Files added: this document; application workbook contracts and workspace; the `TsekSheet.Workbooks` project, implementation and lock file; the workbook test project, tests and lock file; application workspace/bridge tests; and the React workbook viewer and its tests. Files changed: the solution, bridge session, desktop project and native host, desktop dependency lock and smoke tests/project, React app/bridge/styles, and the desktop-test dependency lock. Generated assets, test results and screenshots are build artifacts.

Future work remains: strengthen workbook feature classification and malformed-input/resource-limit testing; test realistic and adversarial workbooks; resolve worksheet identity/reference semantics and complete candidate-preservation checks; design and test editing, independent worksheet copies, merge/replace/create-new, row insertion, external-change detection, cancellation and safe file commits before enabling any corresponding UI. Workbooks with unsupported structures must remain blocked from those operations. No production save path exists in this slice.
