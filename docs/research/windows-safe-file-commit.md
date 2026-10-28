# Windows safe file commit investigation

## Follow-up: new-file implementation evidence

The later native harness at `tests/WindowsFileCommit.NativeEvaluation` reproduced the old missing-target and apparent collision failures. Directory enumeration exposed renamed files such as `target.bin.bin` followed by stray Unicode characters. The filename allocation had no UTF-16 NUL terminator. Adding two zero bytes, while keeping `FileNameLength` equal to the filename bytes excluding the terminator, removed those outcomes in repeated absolute-path tests. This establishes a defect in the old native buffer rather than evidence that Windows silently overwrote the colliding target: the observed existing target remained unchanged while the stage moved to a different, unintended filename.

The x64 layout is checked at runtime (root at 8, length at 16, name at 20). The directory-relative variant consistently returns error 87 and is explicitly unsupported. The final harness treats that documented experimental rejection separately from successful absolute-path commits and real collision rejection. Two final runs each completed 600 checks with zero failures: 200 expected relative-mode rejections, 200 successful absolute/NT-path new-file renames, and 200 existing-target collisions per run. Earlier uncorrected-buffer runs failed and are not acceptance evidence.

The production new-file adapter uses a terminated standard absolute path, retained staging handle, no-replace rename, directory handles that exclude write/delete sharing, final file-identity verification, and exact byte hashes. Production tests add permissions, junctions, cancellation, cleanup, uncertain outcomes and workbook fixtures. See [current saving behavior and limitations](../development/workbook-safe-saving.md). Existing-file replacement remains blocked. The earlier sections below describe the preceding managed-move research and its then-unresolved limitations.

Historical status of the preceding investigation: research only, with saving disabled at that time. The follow-up above supersedes that status for new-file Save As. Results below do not establish a complete safe-save implementation or crash durability.

## Scope and findings

The .NET 8 harness at `tests/WindowsFileCommit.Evaluation` operates only on synthetic `.bin` files in a unique local temporary directory, removes only its own files, and does not reference production code or Excel fixtures. The test volume was local NTFS on the Windows development machine. Three runs completed: 390 scenarios each, 1,170 total, zero failures. The second and third runs included NTFS volume/file identity checks for new-file moves and existing-target collisions; the third used a separate process for deletion and rename. Every scenario checks a separate synthetic source and unrelated file. Stage content, destination content and backup content are checked in scenarios where applicable.

The original handle-based rename experiment was removed before this investigation. Its inconsistent success/missing-output and collision behavior cannot be attributed to a demonstrated root cause from the retained evidence. It is not valid to infer the cause from the later managed `File.Move` results. Handle ownership, the native buffer layout, path interpretation, cleanup timing, and error capture remain possibilities requiring an exact retained reproduction. The harness closes and flushes its staging handle before moving, so it does not test the old exclusive-handle rename implementation.

The restrictive-sharing failure has an established cause: `File.Replace` fails while a destination handle omits delete sharing in this environment. Windows sharing rules treat delete access as including rename; a conflicting open is rejected until the handle is closed. See [CreateFile sharing semantics](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew) and [moving and replacing files](https://learn.microsoft.com/en-us/windows/win32/fileio/moving-and-replacing-files).

The replacement race is also demonstrated: hash a synthetic destination, have another process change it, then call `File.Replace` without another check. The call replaces the changed file; the backup contains the other process's bytes. A check before commit alone cannot authorize replacing the later content.

## APIs and candidate strategies

- [.NET File.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-8.0) with `overwrite: false` rejects a pre-existing destination. Its documentation states that cross-volume moves can be copy-and-delete, so a future design must constrain staging and destination to the same verified volume and directory. The tests show correct content and identity on this NTFS system; they do not prove behavior for all filesystems or concurrent directory redirection.
- [FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info) specifies failure on an existing target when `ReplaceIfExists` is false. [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle) requires appropriate access and reports Win32 errors. A return value is not enough: validate native structure layout and length, preserve error codes, and verify resulting destination bytes and identity. This API was not tested in the retained harness.
- [ReplaceFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew) can create a backup, but does not supply a compare-and-replace operation conditioned on the destination fingerprint. Holding a handle that denies deletion prevents the observed replacement; allowing deletion leaves a race. Backup creation does not authorize overwriting an external edit.
- `Flush(true)` is used for synthetic staging writes. [Windows file caching](https://learn.microsoft.com/en-us/windows/win32/fileio/file-caching) describes separate data and metadata flushing. A successful flush and reopen do not prove survival of power loss or hardware failure.

**New destination:** a same-directory, create-new stage followed by a no-overwrite move is a candidate for further implementation research. It must retain a trustworthy stage identity through validation and commit, guard path redirection and parent-directory races, scope cleanup to owned objects, handle cancellation and uncertain outcomes, and prove expected bytes and identity after reopen. The harness closes the stage before the move and therefore leaves a stage-substitution window. This strategy is not yet ready to enable production saving.

**Existing destination:** blocked. Do not implement overwrite or use delete-and-recreate. A future strategy must demonstrate that the exact authorized identity and content cannot be changed between fingerprint verification and replacement, preserve a recoverable backup, and correctly report ambiguous failures. No such strategy is established here.

## Results and limitations

Each run executes 30 repetitions per scenario:

| Scenario | Per-run result |
| --- | --- |
| New target; reopen exact bytes | 30 passed |
| Existing target rejected | 30 passed |
| Other process creates target before move | 30 passed |
| Simultaneous create-new versus move | 30 passed |
| External change after hash detected by a later hash | 30 passed |
| Locked target rejects replacement | 30 passed |
| Target deleted or renamed after hash | 30 each passed |
| Cancellation before commit | 30 passed |
| Injected staging, validation, and precommit failures | 30 each passed |
| Replacement after an external change overwrites that change and backs it up | 30 demonstrations |

The child-process boundary case is synchronized by waiting for creation before the move; the separate race starts both processes without deterministic scheduling. Cancellation and error cases are controlled pre-operation simulations, not kernel interruption, actual disk-full, permission-denied, or post-commit fault tests. Deletion and rename cases verify observed state, not a production conflict detector. Existing-target lock was exercised in-process, not against every possible third-party sharing mode. Tests did not cover crashes, power loss, live directory swaps, hard links, reparse points, adversarial stage modification, user ACL differences, antivirus interference, network/cloud-synced volumes, FAT/exFAT/ReFS, or all Windows versions. Race tests cannot exhaust all interleavings.

## Reproduction

From Windows CMD at the repository root, with .NET SDK 8.0.427:

```cmd
dotnet run --project tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj -c Release
dotnet test TsekSheet.sln
```

If the process is interrupted, inspect the uniquely named `TsekSheet-commit-evaluation-*` directory in `%TEMP%`; normal completion deletes its own synthetic files and directory. Do not delete other directories. Full workbook and desktop save validation remains a separate milestone.
