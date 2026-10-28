# Workbook editing foundation

Status: implemented for the bounded in-memory scope below. Phase 2 remains incomplete.

## Implementation plan

1. Define bounded typed edits, revision checks, and worksheet-scoped working-copy contracts. Preserve the original baseline and source fingerprint.
2. Build each candidate from in-memory package bytes. Make only a supported single-cell change, reopen it, compare the affected cell and unaffected package content, then adopt atomically. Reject ambiguous features, formula overwrites, stale revisions, resource limits and failed validation.
3. Create independent full-context working copies scoped to one selected worksheet, retain stable worksheet identity and source revision, and record changes. Keep unrelated worksheets read-only and never rewrite self-references.
4. Prove edits, validation failure and rollback, independence, cancellation, resource limits and source hashes with synthetic fixtures before connecting the interface.
5. Add a minimal working-copy editor to the existing bounded bridge and desktop viewer, with explicit discard confirmation and no saving or integration.
6. Run the solution, frontend and workbook regression suites and a real WPF/WebView2 smoke test; record results and limitations.

## Supported operations and boundaries

The existing .NET 8 workbook adapter retains an immutable copy of the original workbook bytes and its SHA-256 fingerprint. The mutable session tracks its own revision and dirty state. It can apply a single typed change to an existing populated cell on a supported worksheet. It has no filesystem save operation; neither opening nor editing passes the source file path to a serialization API.

The accepted value types are:

- **Text:** up to 1,024 characters, stored as an inline string. Text beginning with `=` remains literal text.
- **Number:** a finite floating-point number parsed using an invariant decimal point. The existing cell format is retained.
- **Date:** `YYYY-MM-DD` on or after 1900-03-01, in a cell already recognized as date-formatted. The workbook's 1900/1904 date system is respected; dates before the 1904 epoch are rejected in a 1904 workbook. The existing number format remains unchanged.
- **Formula:** an explicit formula operation. This milestone accepts numeric literals and valid cell references, optionally sheet-qualified and with absolute/mixed anchors, joined by `+`, `-`, `*` or `/`. Referenced sheets and worksheet bounds are checked. Functions, parentheses, names, strings, external references, array/shared formulas and other syntax are rejected. Formula text is not evaluated or rewritten.

An existing formula cell rejects text, number and date edits. A formula can be changed only through the explicit formula type. Existing populated cells inside merged ranges, blank cells, cells with unsupported metadata, and workbooks with detected advanced/unknown structures or truncated content are rejected. Sheet identity, coordinates and the expected session revision are validated before mutation. A successful edit increments the session revision and marks it dirty; stale revisions, cancellation, validation failure and capacity errors do neither.

The application contract assembly remains independent of ClosedXML, Open XML SDK, WPF and WebView2. The desktop bridge permits edits only to working copies, even though the engine contract also supports controlled in-memory edits to a main session. The original workbook view is not editable in the interface. No native file paths cross into React.

## Working copies

A working copy is a separate, independently reopened full-workbook context based on the main session's current serialized bytes and source revision. Its selected source worksheet keeps the same worksheet ID and name; other sheets remain available for context and viewing but reject edits. The copy has its own session ID, revision, dirty state, immutable creation-time base bytes and bounded change history. It also retains the original source fingerprint and immutable original-byte baseline. Creating a copy does not edit or rename a sheet, so an explicit self-qualified formula continues to refer to that same named worksheet *inside the copy's workbook context*. References to other sheets remain unchanged. This is distinct from importing a new sheet and requires no formula string rewriting.

The desktop permits at most two working copies of one active workbook. The engine's existing limit of four active sessions includes the main session, copies, snapshots and an editing candidate; candidate preparation and loaded candidates also reserve estimated memory. A copy is limited to 1,000 recorded edits. These are admission limits and estimates, not a hard operating-system memory quota. A copy created at an older main-session revision stays isolated; future integration must detect that divergence rather than assuming it can merge safely.

Full-session snapshots likewise reopen a byte-for-byte copy of the current serialized package, rather than resaving the live ClosedXML model. A snapshot of a working copy keeps the same selected-sheet restriction, revision, dirty state and change history, while receiving an independent session/model identity. Unsupported snapshot features remain blocked.

The user can switch between original and working-copy views, select an existing cell in the copy's selected sheet, apply a typed value, and see pending-change status. Formula protection errors and unsupported edits are shown. Viewports are cleared when changing session, sheet, page or revision so a stale cell cannot be selected under a new view. Closing the workbook with pending changes requires an explicit discard action. Closing the desktop window or reloading a page with pending changes also requests confirmation. A renderer/process crash may discard in-memory work without a prompt; automatic persistence and recovery are intentionally absent.

If an edit or copy request times out or returns an inconsistent result, the interface treats its outcome as unknown, blocks further editing, and requires explicit discard before closing or reopening. The native host remains the authority for dirty state and rejects ordinary close/open commands while changes are pending.

## Candidate validation and adoption

The edit path uses Open XML SDK to modify only the selected worksheet cell in an isolated in-memory package, with SDK autosave disabled. It leaves the target cell's style and non-value attributes intact. Before adoption it:

1. Compares the package entry set and SHA-256 hashes of every unaffected part against the prior in-memory package. For the edited worksheet it compares normalized XML after masking only the selected cell's value/formula representation; other cells, attributes, names, merges and worksheet structures must match.
2. Runs the existing bounded package and Open XML validation, then reopens the candidate in a new ClosedXML model. It checks the requested typed value or formula, target basic format, workbook structural facts and existing worksheet identities.
3. Checks cancellation again, then swaps the validated candidate into the same session and increments its revision. The old session remains authoritative until that point. Failure or cancellation before adoption disposes the candidate and preserves the prior revision, dirty state, working-copy state and original bytes.

Unchanged parts are compared by their uncompressed content, not by ZIP container bytes or timestamps. The target worksheet comparison ignores inconsequential XML namespace prefix/attribute ordering and the selected cell's permitted value representation. The experiment checks a deliberately altered unrelated package part and a separate altered cell in the same worksheet; both are rejected and the prior session remains unchanged. It does not establish arbitrary Excel feature fidelity, Excel-equivalent recalculation, or a safe save protocol. Existing formula caches elsewhere may become stale after an input edit; the interface labels cached results accordingly.

## Commands

Run from the repository root in Windows CMD with .NET SDK 8.0.427 and the documented Node/npm/WebView2 prerequisites:

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

Choose **Open workbook**, select a local `.xlsx`, select its worksheet, then choose **Create working copy**. Select a populated cell, choose its value type, enter the new value and apply it. The view selector switches between the original session and copies. Closing with changes displays a discard confirmation. The native picker still requires an interactive Windows desktop; automated desktop tests inject synthetic fixture paths instead of operating the dialog.

## Verification performed

On 2026-10-28, SDK **8.0.427** and locked restore succeeded. The Release solution build passed with **0 warnings and 0 errors**. Final automated results were **15 application**, **30 workbook**, **7 desktop**, **14 frontend**, and **13 workbook-evaluation harness** tests passed, with **0 failed and 0 skipped**. The frontend typecheck and production build ran as part of the solution build.

The real WPF/WebView2 smoke test opened both synthetic workbooks, edited a reimbursement working-copy cell, switched back to the unchanged original value, checked the in-page discard confirmation, and verified both fixture hashes. It also exercised the native window's close-cancellation path with an injected confirmation response. The actual interactive file dialog and the real native confirmation dialog were not manually operated. Both retained source fixtures matched their SHA-256 manifest after the final run.

Automated regression cases cover typed edits, explicit formula protection, stale revisions, cross-sheet formulas, self-reference identity, multiple copies, creation-time base revision/bytes, selected-sheet restrictions, 1900/1904 dates, names/merges/table extent preservation, cancellation, admission limits, disposal, and candidate corruption in both an unrelated package part and another cell on the edited sheet. Fault injection is an internal test-only seam; no production bridge command exposes it. The app-screen source lint reported no failures or warnings. The desktop viewport was inspected through WebView2; native Excel rendering was not assessed.

## Files

Added this document, `src/TsekSheet.Workbooks/CellCandidate.cs`, and `tests/TsekSheet.Workbooks.Tests/WorkbookEditingTests.cs`. Modified the earlier workbook-foundation document; application workbook contracts, workspace and bridge; workbook engine, session, package guard and project; desktop host; React workbook viewer, bridge, application text, styles and component tests; and application, workbook and desktop regression tests. No package version or solution membership changed.

## Remaining work and risks

No worksheet merge, replace, create-new integration, structural row insertion, templates, OCR, Save, Save As, or recovery files are implemented. File saving must remain disabled until separately designed and tested external-change detection, destination authorization, serialization validation, fault handling and safe commits are complete. Unsupported features must remain blocked rather than silently dropped.

The compatibility classifier cannot identify every possible Excel construct. Styles on blank cells, arbitrary formatting, rich content, macros, protection, external links, drawings, advanced formulas and complex workbook features are outside this editing contract or rejected. The tests use synthetic `.xlsx` files; native Excel open/repair and recalculation behavior, representative real workbooks, large-workbook peak memory, and installed operation on a separate offline machine remain unverified. The existing .NET 8 support deadline and dependency-review requirements still apply.
