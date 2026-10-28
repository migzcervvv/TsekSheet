# Workbook safe-saving foundation

Status: new-file-only Save As is enabled for the supported Windows x64/local fixed NTFS policy. Existing-file Save and replacement remain unavailable. Phase 2 is incomplete.

## Implementation sequence

1. Reproduce native rename errors and establish the filename buffer contract.
2. Verify a no-replace rename with a retained staging handle and locked ancestor directories.
3. Test workbook candidates, source preservation, races, faults, cancellation and state transitions.
4. Connect native destination selection and the bounded bridge command only after backend tests pass.
5. Run real WPF/WebView2 smoke tests and record filesystem limitations.

## Supported operation and authorization

Save As exports the main session to a separate, brand-new `.xlsx` path selected in a native dialog for each operation. React supplies only a session ID and expected revision through `workbook.saveAsNew`; it cannot supply paths. The dialog does not offer an overwrite confirmation. Its `CreateTestFile` is explicitly false, preventing the default destination-side probe described by [Microsoft's dialog documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.savefiledialog.createtestfile?view=windowsdesktop-8.0). Existing selections are rejected, and the backend independently rejects any destination that already exists or appears during commit.

The source path is forbidden even if the source was deleted externally. Working copies and snapshots cannot be exported directly. Pending working-copy changes are excluded. The desktop permits Save As only from the original-session view for a workbook within the existing compatibility scope. The desktop editor still edits working copies only; it does not integrate them.

## Candidate and session state

The session lock freezes the expected revision. The candidate is an independent copy of the current serialized package bytes, including previously validated in-memory cell edits. It is not reconstructed from the viewport or reserialized through a high-level library that could change unrelated parts.

The engine validates and reopens the candidate, checks the package's structural facts, worksheet identities, metadata, opaque-part hashes, and every stored cell's value, formula and displayed format against the frozen session. Exact candidate/staging/final SHA-256 equality protects the complete serialized package. These checks retain the existing supported-feature limits and do not establish full Excel compatibility or recalculate formulas.

Save As does **not rebind** the session. The original source path, original bytes and source fingerprint remain the baseline. On confirmed success, `LastExportedRevision` and `LastExportedSha256` record the export; the receipt also reports the output filename and fingerprint. Revision does not advance. Dirty state remains relative to the original source: a changed main session remains dirty, while an unchanged main session remains clean. Pending copies stay pending. A later edit retains the last exported revision as historical information.

On rejection, cancellation before commit, or an uncertain result, the session and unsaved changes remain intact and export metadata is not advanced. No silent retry or overwrite fallback exists.

## Verified commit strategy

`NewFileCommit` supports Windows x64, absolute drive-letter paths on local fixed NTFS volumes, a maximum path length of 220 characters, and regular `.xlsx` filenames. UNC/network paths, device paths/names, alternate data streams, relative/aliased paths, trailing dots/spaces, and linked or redirected ancestor directories are rejected.

It opens the drive root and each ancestor directory with read-attributes access, without write/delete sharing, and with `FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS`. It checks each held directory's attributes and final resolved path. Handles stay open through cleanup and commit. This blocks ordinary directory rename/replacement and incompatible opens for reparse modification; it does not claim protection against administrators, kernel drivers, process-handle duplication, or drive remapping by privileged software.

The candidate is written to a unique `.tseksheet-<guid>.tmp` file in that directory using `CREATE_NEW`. A single native READ/WRITE/DELETE handle allows read sharing only and remains owned until completion. No other ordinary writer can modify, delete or rename the staging object. The stream is flushed, rewound and SHA-256 checked before commit.

The commit calls `SetFileInformationByHandle(FileRenameInfo)` on that handle, with a NULL root, absolute destination, and replacement disabled. The checked x64 structure has root/length/name offsets 8/16/20. Its UTF-16 filename includes an allocated zero terminator; the byte-length field excludes the terminator. The former missing terminator caused the earlier nondeterministic filenames. The unsupported directory-relative variant is not used.

After rename, the original handle still blocks writes/deletion. A compatible second read handle reopens the requested destination; its volume/file index must equal the staging identity and its bytes must hash identically. Only then is success reported. Verification is a point-in-time guarantee: after handles close, other software can change or lock the exported file.

See [the native rename contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), and [CreateFile sharing rules](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew).

## Failures, cancellation and recovery

- `DESTINATION_EXISTS`: no replacement; choose a new filename. This includes a file created concurrently.
- `ORIGINAL_DESTINATION`: choose a separate path; the original source binding cannot be recreated or replaced.
- `UNSUPPORTED_DESTINATION` / `INVALID_DESTINATION`: choose a regular new filename on a supported drive and path.
- `SAVE_FAILED`: an ordinary preparation/commit error occurred with the staging object still at its verified old name. Check permissions, available space and locks. Session state is retained.
- `SAVE_UNCERTAIN`: rename may have occurred or final verification/closing failed. Do not assume success or failure; inspect the selected destination and any staging file before retrying. No automatic rollback or deletion of a possible committed destination occurs.
- `TEMP_CLEANUP_FAILED`: an uncommitted owned stage could not be deleted. Inspect the selected folder for the reported filename pattern. Do not delete unrelated files.

Cancellation is checked during preparation and immediately before rename. Once rename begins it is not interrupted; the receipt reports a cancellation that arrived after commit. Cleanup of an uncommitted file uses deletion on its held handle, never a potentially substituted path. If a native rename fails, cleanup requires the held object's final path to still match the staging name; otherwise the object is retained and the outcome is uncertain.

A crash before rename can leave a complete or partial owned staging file. A crash during or after rename can leave an exported destination without updated in-memory export metadata. Inspect both names and validate any recovered workbook before using it. There is no persistent session recovery or automatic startup cleanup. No old destination is overwritten, so this capability needs no replacement backup. It does not promise power-loss durability, hardware-failure recovery, or secure deletion. Staging files inherit the selected directory's permissions; local privacy depends on those permissions.

## Test evidence and limits

Backend tests cover repeated distinct-name commits, 100 target-creation races and 100 simultaneous create-new races per run, 20 separate-process boundary creations per run, stage write/delete/rename attempts, directory rename attempts, junction rejection, existing/locked destinations (including one created and locked at the boundary), actual create-file ACL denial, invalid/network paths, candidate hash failure, injected preparation/commit errors, precommit cancellation, cancellation after rename, uncertain outcomes, and cleanup failure.

Both reimbursement and laboratory workbooks export and reopen with matching cell windows (including formulas and basic formats), metadata and source hashes. Tests retain dirty main/copy state, reject stale revisions/copy exports, exclude pending copies, protect a deleted original source path and preserve state after failed/uncertain commits.

The real Windows smoke test loads React in WebView2, edits a copy, switches to the original session, exports it, verifies exact source/output byte equality, rejects a repeated filename, and reports cancelled selection while retaining pending changes. It uses the internal native-selector seam; visible Save File dialog interaction remains a manual check.

Crash/power-loss tests, actual disk exhaustion, FAT/exFAT/ReFS, live network shares, privileged drive remapping, and kernel/filter-driver failures were not executed. Unsupported-volume enforcement is a policy check; these filesystems have not been qualified. Fault injection does not replace those tests.

## Windows CMD commands

```cmd
dotnet --version
dotnet build TsekSheet.sln
dotnet test TsekSheet.sln
dotnet run --project tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj -c Release
cd src\TsekSheet.Frontend
npm run typecheck
npm run test -- --run
npm run build
```

SDK 8.0.427, ClosedXML 0.105.1, and Open XML SDK 3.5.1 remain unchanged. Existing-file replacement still requires a separately proven strategy and acceptance tests.

## Recorded verification and changed files

The final solution regression run passed 80 tests: 16 Application, 56 Workbooks, 8 Desktop, with zero failures/skips. The independent workbook-library evaluation tests passed 13/13; React passed 16/16. Release solution build, frontend typecheck and production build passed; Release build reported zero warnings/errors. Two final native-harness runs each passed 600 checks (including 200 expected rejections of the unsupported relative mode); earlier missing-terminator runs failed and led to the correction. One dialog test initially expected a leading dot from WPF's normalized extension getter; its expectation was corrected and the final suite passed. The dialog's no-test-file and no-overwrite settings are asserted.

Original fixture SHA-256 values remained:

- Laboratory: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.
- Reimbursement: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.

Production files added/modified: `src/TsekSheet.Workbooks/NewFileCommit.cs`, `WorkbookEngine.cs`, `WorkbookSession.cs`; `src/TsekSheet.Application/Workbooks/WorkbookContracts.cs`, `WorkbookWorkspace.cs`; `src/TsekSheet.Application/Foundation/BridgeSession.cs`; `src/TsekSheet.Desktop/MainWindow.xaml.cs`; `src/TsekSheet.Frontend/src/bridge/client.ts`, `WorkbookViewer.tsx`.

Tests/research: `tests/TsekSheet.Workbooks.Tests/NewFileCommitTests.cs`; `SafeSaveDisabledTests.cs` renamed/reworked as `SafeSaveWorkbookTests.cs`; `tests/TsekSheet.Application.Tests/WorkbookWorkspaceTests.cs`; `tests/TsekSheet.Desktop.Tests/DesktopSmokeTests.cs`; `src/TsekSheet.Frontend/src/WorkbookViewer.test.tsx`; `tests/WindowsFileCommit.NativeEvaluation/Program.cs`, its project file and generated package lock; this document and `docs/research/windows-safe-file-commit.md`. No retained fixture or dependency version was changed.
