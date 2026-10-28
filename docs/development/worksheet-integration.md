# Worksheet integration

Status: conservative Merge implemented with backend, bridge, frontend and real WPF/WebView2 regression evidence. Replace and Create New remain disabled. [Step 39 environment repair](dotnet-10-migration.md) subsequently restored the standard .NET 8 Release build; the prerequisite mismatch and diagnostic-build results below describe the earlier integration run. Phase 2 remains incomplete.

Current follow-up: [Step 40](dotnet-10-migration.md) migrates all projects to .NET 10 and reverifies the unchanged Merge implementation, rollback/conflict tests and real desktop workflow. Current build/runtime requirements come from that report; the earlier milestone's environment and counts below remain historical.

Step 42 follow-up: [workflow proposals](workflow-profiles.md) perform read-only section/entity analysis. [Structural insertion](safe-row-insertion.md) remains disabled and proposals cannot be replayed as recorded-cell Merge edits. Existing Merge and new-file Save As guarantees are unchanged; Phase 2 is incomplete.

## Implementation plan

1. Add application-owned operation, request, preview and result contracts with frozen copy, base and destination revisions.
2. Implement Merge only for the existing recorded text/number/date/formula edits. Require the exact parent session, worksheet identity, base revision and package baseline. Reject any destination revision divergence conservatively, including unrelated edits.
3. Collapse repeated edits to their final cell value; omit cells reverted to base. Build an isolated package, validate each permitted cell delta and all unaffected package content, reopen, compare expected content, then adopt once in memory.
4. Add synthetic regression tests for identity/revision rejection, rollback, formulas, preservation, multiple copies, no-ops and Save As.
5. Investigate Replace/Create New against existing evaluation evidence. Leave them rejected unless reference/resource preservation can be demonstrated. Never delete/recreate a worksheet.
6. Connect bounded preview/confirmation/apply desktop workflow after backend regression success. Keep paths native and saving separate.
7. Run locked restore, Release build, solution/evaluation/frontend checks, fixture fingerprints and desktop smoke where prerequisites permit. Record blockers without changing pinned dependencies.

## Safety policy

Merge preserves the working copy after both success and failure. Successful nonempty integration advances the main revision once and marks it dirty; it does not save. A retained copy becomes stale after integration and must not be replayed. A no-op leaves revision and dirty state unchanged. Copy edit history is retained for review; preview reports the number of distinct effective changed cells.

Any destination change since copy creation rejects the entire request; there is no automatic conflict resolution. Formula references retain existing sheet names and identities; no reference translation is performed. Only the engine's existing small arithmetic formula-edit grammar is accepted. Formula caches may be stale.

## Supported operations and conflicts

Application-owned contracts define `WorksheetIntegrationOperation`, `WorksheetIntegrationRequest`, `WorksheetIntegrationPreview` and `WorksheetIntegrationResult`. Preview and apply both validate the same frozen request; apply repeats every check under locks for the destination and source copy. The workspace serializes commands, while the engine independently enforces session locks and provenance.

Merge supports only previously validated recorded edits to populated, unmerged cells: text, finite numbers, supported dates and explicit formulas. It changes no formatting or coordinates. Destination lookup uses the stable application worksheet GUID, never a selected index or user-supplied sheet name. The copy must belong to the exact main session, retain the original source SHA-256, and target its original selected sheet. Snapshots and foreign copies are rejected.

- `STALE_COPY_REVISION`: the copy changed after preview.
- `STALE_REVISION`: the destination revision differs from the reviewed revision.
- `DESTINATION_CONFLICT`: copy base revision differs from the current destination, the requested base is wrong, or the complete base package bytes differ. This rejects even nonoverlapping edits and edits subsequently reverted in the destination.
- `UNKNOWN_WORKSHEET` / `INVALID_WORKING_COPY`: mismatched destination or provenance.
- `UNSUPPORTED_OPERATION`: Replace, Create New, or any unrecognized operation value.

Preview returns all detected conflicts and the distinct effective cell count; a conflicting count is informative only. Apply rejects the entire request on any conflict. There is no partial integration or overwrite option. Repeated edit records collapse to the final edit per coordinate. A cell whose typed content/formula and supported format equal the destination/base is omitted. Formula-cache differences alone are omitted. A fully reverted or empty copy produces zero changes, no revision increment and no dirty-state change. Existing dirty state is never cleared by integration.

## Formula and reference behavior

An explicit formula edit uses the existing arithmetic grammar: numeric literals and in-bounds cell references joined by `+`, `-`, `*`, `/`, with supported same-sheet, self-qualified or existing cross-sheet references. Formula edits never translate names or references. Existing functions, names and other formulas elsewhere are preserved as package content, not evaluated. Merged-cell edits, new cells, structural row/column changes and format edits are outside scope. Re-entering the same formula does not merge a removed cached result. Changes to input cells may leave other formula caches stale; the interface continues to label cached values accordingly.

## Candidate validation and atomic adoption

1. Validate operation, exact parent/copy/destination identities, compatibility and all expected revisions. Compare the immutable copy base package to current destination bytes.
2. Freeze both sessions with locks and reserve bounded preparation memory. Start from destination package bytes in memory.
3. Replay only effective final edits with the existing Open XML cell adapter, validating each typed edit again. Every step creates isolated serialized bytes; no serializer receives a source pathname.
4. Compare the package entry set and SHA-256 of every unaffected part after each edit. For the selected worksheet, compare canonical XML with only that cell's allowed value/formula representation masked. Cell style and other attributes, other cells, merges, relationships, names, tables, ordering and visibility must remain unchanged.
5. Run bounded package/schema inspection and reopen the complete candidate through the engine. Compare worksheet identities/metadata, structural facts, every stored cell count, every expected changed cell and every unchanged cell, including displayed basic formatting.
6. Check cancellation, prepare the response/state and transfer the validated candidate into the original session once. Advance the destination revision once. Dispose the previous model and release its reservation.

Failures dispose only the candidate and retain both authoritative sessions, copy base/history, revision, dirty state and source file. Cancellation before adoption rolls back the whole multi-cell operation, including when earlier isolated edits succeeded. After atomic in-memory adoption there is no partial cancellation rollback. No automatic save or export occurs.

## Working-copy lifecycle and desktop workflow

Create a copy from the original view and edit supported cells. From that copy choose **Review Merge**. The page identifies the selected copy, original destination name, recorded edit count and effective pending count. The review dialog shows conflicts, offers **Cancel merge**, and enables **Confirm merge in memory** only for a valid preview. Replace and Create New have explanatory text and no working controls. The native bridge accepts identities/revisions only; paths remain in C#.

Success refreshes the original session view and reports the applied count. Failure leaves the copy selected and its edits available. Unknown apply outcomes block further edits until explicit discard/reopen. Save As is a separate native action and exports integrated content to a brand-new file under the existing safe-commit policy. Earlier exports remain unchanged. Existing-file saving remains unavailable.

Copies are intentionally retained unchanged after success. Their historical edits still appear pending; a successful nonempty merge makes them stale. Other copies from that base are also stale, even for different cells/sheets. Create a fresh copy from the new main revision for additional integration. The existing two-copy UI admission limit still applies; there is no individual copy-discard or rebase workflow yet. Closing/discarding releases all sessions. No-op integration leaves the copy reusable.

## Environment and verification (2026-10-28, Asia/Manila)

Starting checkout: clean `main`, commit `c828394a580be35cb7065d5b82ac4557765e1688`. No branch change, pull, rebase, commit, push or dependency upgrade was performed.

Observed environment: Windows build 10.0.26200, x64; .NET SDK 8.0.427 / MSBuild 17.11.69; projects target net8.0 and net8.0-windows. Installed SDKs include 3.1.426, 5.0.408/416, 6.0.428, 7.0.410, 8.0.427 and 9.0.312. Desktop runtime 8.0.33 is installed. Node 24.16.0/npm 11.13.0 differ from the exact repository requirement 24.22.1/11.19.1. The configured default npm CLI path is absent. Git is 2.47.1.windows.1. WebView2 registry version is 156.0.4314.50. The Python WindowsApps alias fails to start; Python/OCR is not used in this slice. Historical toolchain documents refer to the prior PC.

| Check | Result |
| --- | --- |
| Solution and evaluation locked NuGet restores | Passed; locks unchanged. Initial sandbox SDK-cache access failed; approved retry succeeded. |
| Locked npm install | Failed: exact toolchain guard rejects Node/npm versions. Initial sandbox cache failure was retried with cache access. Packages unpacked before the guard failed; no pin or guard was changed. |
| Standard Release solution build | Failed: configured npm CLI path missing. Not claimed as passed. |
| Diagnostic desktop/test compilation | Passed, zero warnings/errors, with `-p:DesignTimeBuild=true` after separately compiling core projects and frontend assets. This skips the frontend target and is not normal build acceptance. |
| Solution tests on diagnostic assemblies | Final unrestricted solution run: 98 passed (16 Application, 74 Workbooks including 18 new integration cases, 8 Desktop), zero failures/skips. Final TRX filenames end in `2026-10-28_22_46_38`, `_22_46_40`, and `_22_46_45`. |
| Frontend typecheck / tests / production build | Passed on installed Node/npm, 19 tests, zero failures/skips. This is diagnostic evidence on the mismatched toolchain. |
| Workbook evaluation harness tests | 13 passed, zero failures/skips. |
| Library evaluation matrix | 22 passed, 24 known compatibility failures, 26 unexecuted, zero execution errors; all 72 source hashes unchanged. Report: `artifacts/workbook-evaluation/20261028-143915-ff737022/results.json`. |
| Real WPF/WebView2 smoke | 8/8 passed unrestricted, including edit, Merge preview/cancel/confirm, refreshed original viewport and unchanged source/earlier export. Native file-selection and close-confirmation seams were used; dialogs were not manually operated. |
| Sandboxed desktop run | 3 failed / 5 passed due to WebView2 browser-process crashes. Unrestricted solution rerun passed; these failures are retained as environment evidence. |
| Native Excel open/repair, calculation and visual fidelity | Not executed. |

The initial integration run had 2 failed assertions: one requested formula equaled the fixture base, so the effective change count correctly excluded it. The fixture edit was changed to a different formula; the final cases pass. A nullable test warning was corrected before final verification.

Regression coverage includes successful multi-cell Merge on both synthetic fixtures, explicit arithmetic/cross-sheet/self-qualified formulas, date/number/text content, formatting, names, table extents, merges and workbook structures through exact unaffected-package checks, worksheet order/visibility, repeated/no-op/cache-only edits, copy isolation, base/source/destination identity checks, frozen copy/destination revisions, destination conflicts, late cancellation, candidate tampering after an earlier successful candidate step, dirty states, bridge payload/path rejection, retained copies, source fingerprints, exported reopening and existing-destination rejection. Replace/Create New rejection tests verify no mutations. The broader existing workbook suite retains edit/merged-cell rejection and save fault/race coverage.

Retained input fingerprints match the committed manifest:

- `reimbursement-synthetic.xlsx`: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- `laboratory-synthetic.xlsx`: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

## Replace / Create New investigation

The re-executed library matrix reproduces ClosedXML replacement losing global named ranges and copy-sheet self-qualified formulas retaining the original sheet reference. Create-sheet passing in that matrix is a narrower experiment, not proof of arbitrary working-copy import with safe self-reference rebinding, local/global names, tables and relationship/resource collisions. There is no tested reference transformation/dependency layer or in-place replacement preservation oracle. Neither operation is enabled. Backend requests are explicitly rejected, including empty copies. No worksheet deletion/recreation implementation was added, and no name-selection control suggests new-sheet support.

## Remaining integrity risks and next milestone

Compatibility remains restricted to the engine's current classifier. Unknown/advanced package content, protected/macro/encrypted workbooks, external relationships and unsupported formula/structural features remain rejected where required by existing checks; those checks were not weakened. Untouched content can be preserved without supporting its manipulation. Classification completeness, native Excel behavior, cached calculations, representative user-approved workbooks and large/adversarial workbook behavior remain unverified. Repeated per-cell package serialization may be expensive near the 1,000-edit limit; no performance claim is made. Copies, candidate bytes and models share existing estimated-memory admission bounds, not a hard CLR quota.

Integration targets the in-memory destination. It never rereads or overwrites an externally changed source file; source fingerprint remains the opening baseline. The existing Save As policy leaves it untouched and rejects original/existing destinations. Existing-file overwrite, autosave, databases, cloud services, template mapping, structural insertion and OCR remain out of scope. Unsaved state is lost on process termination; persistent recovery is absent.

Next milestone: provide the exact pinned frontend toolchain and native CLI path on this PC and reproduce the standard Release build plus full regression/smoke suite without design-time compilation. Then add explicit working-copy retirement/rebase policy and independently prove reference/resource preservation for narrowly scoped in-place Replace or Create New before enabling either. Phase 2 acceptance remains open.

## Changed files

Created:

- `docs/development/worksheet-integration.md` — plan, contract, limitations and evidence.
- `src/TsekSheet.Workbooks/WorksheetIntegration.cs` — conservative Merge candidate/preview/adoption implementation.
- `tests/TsekSheet.Workbooks.Tests/WorksheetIntegrationTests.cs` — 18 synthetic integration cases, including the real workspace/bridge contract.

Modified:

- `PLANS.md`, `docs/development/workbook-editing-foundation.md`, `docs/development/workbook-safe-saving.md` — current implementation status and links.
- `src/TsekSheet.Application/Workbooks/WorkbookContracts.cs`, `WorkbookWorkspace.cs`, `src/TsekSheet.Application/Foundation/BridgeSession.cs` — typed contracts, serialized orchestration and bounded commands.
- `src/TsekSheet.Workbooks/WorkbookSession.cs` — partial class boundary; existing cell/save implementations retained.
- `src/TsekSheet.Frontend/src/bridge/client.ts`, `WorkbookViewer.tsx`, `WorkbookViewer.test.tsx`, `App.tsx` — validated bridge results, review/confirmation/error workflow, three new frontend cases and availability text.
- `tests/TsekSheet.Desktop.Tests/DesktopSmokeTests.cs` — real WPF/WebView2 Merge cancellation/confirmation and source/export preservation assertions.

No project, package, lockfile or retained fixture changed. `git diff --check` passes.

## Reproduction commands

Standard verification remains mandatory once prerequisites are corrected:

```powershell
dotnet restore TsekSheet.sln --locked-mode
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore --logger trx
dotnet restore tests/WorkbookLibrary.Evaluation/WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet test tests/WorkbookLibrary.Evaluation/WorkbookLibrary.Evaluation.csproj -c Release --no-restore
dotnet run --project tests/WorkbookLibrary.Evaluation -c Release --no-restore
# From src/TsekSheet.Frontend:
npm.cmd ci --no-audit --no-fund
npm.cmd run typecheck
npm.cmd run test -- --run
npm.cmd run build
```

For this run only, after the frontend commands succeeded on the mismatched installed toolchain and core project tests compiled the dependencies, diagnostic desktop compilation used:

```powershell
dotnet build src/TsekSheet.Desktop/TsekSheet.Desktop.csproj -c Release --no-restore -p:DesignTimeBuild=true
dotnet build tests/TsekSheet.Desktop.Tests/TsekSheet.Desktop.Tests.csproj -c Release --no-restore -p:DesignTimeBuild=true
dotnet test TsekSheet.sln -c Release --no-build --no-restore --logger trx
```

This diagnostic path does not satisfy the normal build prerequisite gate and does not change its configuration. Desktop tests need normal Windows desktop process access; the sandboxed browser crash is not counted as a passing run.
