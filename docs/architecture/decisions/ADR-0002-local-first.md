# ADR-0002: Local-First Processing and Explicit Persistence

Status: Accepted

Date: 2026-10-28

## Context

[AGENTS.md](../../../AGENTS.md) and [GOALS.md](../../../GOALS.md) require local processing, no database or cloud services, human verification of OCR results, and preservation of original Excel workbooks until explicitly saved. The [architecture overview](../overview.md) defines in-memory sessions, isolated worksheet working copies, candidate validation, supervised Python OCR, and safe file commits. [ADR-0001](ADR-0001-desktop-framework.md) accepts WPF, WebView2, React + TypeScript, C#/.NET, and Python with locally packaged assets.

TsekSheet must process sensitive spreadsheet and scanned-document information without requiring a network connection or a hosted service. Workbook editing and structural changes can affect formulas, formatting, references, and content outside the selected worksheet. Persistence must therefore be a deliberate boundary, separate from extraction, verification, editing, and integration.

These requirements apply to generic spreadsheet workflows. They are not limited to the initial accounting reimbursement use case.

## Decision

### Offline Operation and Local Processing

TsekSheet operates entirely offline after installation. Package or provide offline installation for all required runtimes, frontend assets, PDF/OCR tools, language models, and other dependencies. Do not require first-run downloads, online activation, remote fonts, hosted frontend assets, or network model retrieval. Missing required local assets produce an actionable local error, never a remote fallback.

Use no database, cloud services, cloud processing, or paid AI APIs. Store templates as local versioned configuration files. Do not introduce a persistent session store disguised as a file-backed database.

C#/.NET processes Excel workbooks locally. The supervised Python child process renders scanned PDFs, preprocesses images, and performs OCR locally. It returns draft extraction results through the structured contract established in ADR-0001 and has no workbook-editing responsibility. Human verification remains mandatory before OCR-derived information is applied to worksheet data.

### In-Memory Sessions and Original-File Protection

Maintain active workbook sessions in memory, including the original package baseline, editable workbook state, revisions, compatibility findings, and independent working copies. Enforce explicit size and working-copy limits rather than silently spilling complete sessions to disk. No database, background session persistence, or automatic write to original workbooks is introduced.

Opening, navigation, OCR, verification, edits, merges, sheet replacement, new-sheet creation, and row insertion must not modify original workbook files. Integration approval changes in-memory state only. Structural operations run on isolated candidate workbooks; adopt them only after validation, including serialization and reopening where required by the overview. Reject unsupported changes and discard failed candidates without altering the prior active state.

### Necessary Temporary Files and Cleanup

Use temporary local files only when an operation requires them: OCR input staging, rendered pages needed for review, tool interchange, validation where streams are insufficient, and safe-save candidates or recovery backups. Prefer in-memory streams where supported and practical. Temporary files are not an alternate session database or an automatic export mechanism.

Create unique application-owned job directories with access restricted to the current user where supported. Keep original documents distinct from staged files. Track ownership and lifetime explicitly so cleanup cannot delete user-selected originals or another job's files.

Remove intermediate OCR files after processing once their consumer has finished. Keep rendered source pages only while required for human verification, then remove them when review or the associated session ends. On success, failure, cancellation, and normal shutdown, close file handles and stop relevant child processes before cleanup. If deletion fails, record a minimal local diagnostic and retry bounded cleanup of verified application-owned abandoned directories on startup. Do not claim immediate cleanup after a crash or remove files belonging to a still-running job.

Save candidates and backups have separate lifetimes. Remove obsolete artifacts after a confirmed successful commit. Retain a necessary recovery backup if a commit outcome is uncertain; report its local location and resolve it before cleanup. Never delete the only known recoverable copy merely to satisfy temporary-file cleanup.

### Explicit Save and Save As

Require an explicit Save or Save As action identifying the destination. A save action authorizes that operation, not indefinite background overwrites. Save As to a separate destination leaves the source unchanged; overwriting any existing destination requires explicit authorization. Pending working copies are never silently integrated by saving.

Freeze the session revision and serialize mutations during saving for the MVP. Write a candidate into the destination directory, validate it, and only then commit it. Never serialize directly into the original file or truncate an existing destination as a fallback. Use a tested atomic replacement with a recoverable backup for existing destinations and a same-directory rename for new destinations. Fail safely when the required commit behavior is unavailable.

Before overwriting, compare a content fingerprint of the current destination with the opened baseline or the destination state approved for that save. Modification timestamps alone are insufficient. A changed, missing, or replaced destination requires a fresh user decision; silent overwrite or automatic conflict resolution is prohibited. For a new destination, do not overwrite a file that appears after destination selection. Revalidate close to commit and implement coordination appropriate to supported Windows filesystems; a fingerprint check alone does not eliminate the race with an external writer identified in the overview.

After a confirmed commit, update the saved baseline, fingerprint, and revision state. Clear dirty state only for the saved revision. Define Save As rebinding behavior before implementation and keep it visible to the user; it does not authorize a write back to the previous source.

### Safe Failure and Cancellation

Validation failures, unsupported features, OCR crashes, cancellation, locked files, and disk errors must not leave partially applied workbook mutations or partially written originals. Keep the prior in-memory state on failed candidate adoption and retain unsaved edits after a failed save.

Allow cancellation during extraction, candidate preparation, and validation. Immediately before the file commit, establish a short commit boundary: cancellation must not interrupt replacement midway. If cancellation arrives after commit begins, finish or recover the commit and report the actual outcome rather than claiming no save occurred. A process crash or filesystem failure requires a recoverable outcome under the supported commit strategy; do not promise guarantees beyond validated filesystem behavior.

## Alternatives Considered

| Alternative | Reason not selected |
| --- | --- |
| Cloud OCR, hosted processing, or online AI APIs | Violates offline operation and local-data requirements and introduces a service dependency. |
| Embedded or server database for sessions and extraction history | Conflicts with the no-database constraint and introduces persistence and migration work unnecessary for active sessions. |
| Edit original files in place or autosave every operation | Removes the explicit-save boundary and increases the risk of irreversible partial changes. |
| Persist every working copy and intermediate result to disk | Expands sensitive-data retention and cleanup obligations; active state belongs in memory. Necessary job and safe-save files remain permitted. |
| Keep all intermediates only in memory | Cannot be assumed compatible with every selected OCR/PDF tool or the destination-local safe-save protocol. Use narrowly scoped temporary files when necessary. |
| Overwrite based only on timestamps or silently resolve external edits | Can miss changes or discard another application's work. Require content checks and a fresh user decision. |

## Consequences and Trade-offs

- Core workflows remain available without network access, and document processing does not depend on accounts, service availability, or usage-based processing fees.
- Bundled runtimes and models increase installer size and require local dependency maintenance. Installation and upgrades must preserve an offline operating path.
- In-memory baselines and workbook clones consume memory. Define supported workbook sizes and copy limits; reject oversized operations predictably.
- Unsaved sessions can be lost on process termination. Persistent crash recovery remains a separate future decision and must not introduce a database or weaken explicit saving.
- Local OCR quality and performance depend on packaged tools and the user's hardware. Human verification is required regardless of confidence scores.
- Candidate validation, external-change detection, and safe commits cost time, memory, and destination disk space. Temporary backups require a defined recovery and cleanup policy.
- Local-only operation does not provide cloud synchronization, shared editing, or remote recovery. Workbook compatibility, formula recalculation, filesystem support, and Save As rebinding remain implementation decisions subject to tests.

## Security and Privacy Considerations

Do not upload documents, workbook contents, extraction results, or diagnostics. Do not introduce application telemetry or network-dependent processing. Configure the embedded frontend to use bundled assets, restrict navigation and remote requests, and avoid persisting document data in browser storage. Disable workbook external-link refresh and any automatic retrieval of remote resources; preserve supported link metadata without fetching its target.

Treat workbook files, PDFs, OCR text, and template configuration as untrusted inputs. Validate sizes and structures, enforce processing limits, render extracted text as text, and do not execute document macros or template scripts. Launch Python without a shell using controlled arguments and bounded jobs. Validate returned paths and result structures before using them.

Enforce filesystem access through C# application boundaries and validated bridge commands. Do not grant React arbitrary path access. Scope temporary-file cleanup to verified application-owned paths; account for path redirection before deleting files. Keep logs minimal and exclude document contents, extracted values, and unnecessary full paths.

Local processing does not guarantee confidentiality from other software running under the same user, administrators, operating-system paging, or user-selected synchronized folders. Do not claim that normal file deletion securely erases data. TsekSheet must not initiate synchronization; choosing an output location does not give it control over external backup or sync software. Dependency maintenance and local access controls remain necessary despite offline operation.

## Validation Requirements

Acceptance requires implementation evidence, not only mocked tests:

1. Install required dependencies through the supported offline path and run the complete workbook, scanned-PDF OCR, verification, editing, integration, and save workflow with networking disabled. Verify no application-initiated remote requests or model downloads.
2. Compare original file bytes before and after opening, OCR, verification, working-copy edits, merge, replace, new-sheet creation, repeated row insertion, cancellation, and session closure without saving. Originals must remain unchanged.
3. Test workbook-copy isolation and candidate adoption with real fixtures covering formulas, formatting, merged ranges, names, cross-sheet references, hidden sheets, and lower content shifted by repeated row insertion. Unsupported structures must be rejected without state changes.
4. Verify explicit Save writes only the authorized destination, Save As preserves the source, and saving never silently integrates pending working copies. Validate serialized outputs and baseline/dirty-state updates.
5. Change, delete, or replace a destination externally before saving, including a content change with unchanged timestamps. Verify conflict detection, no silent overwrite, and safe handling of a destination created during Save As. Exercise the external-writer race near commit.
6. Inject locked files, permission errors, insufficient disk space, serialization failures, failed validation, and commit failures. Verify original integrity, retained unsaved state, and usable recovery backups where required. Test cancellation before commit and during the commit boundary with accurate outcome reporting.
7. Test OCR timeout, malformed results, process failure, cancellation, normal completion, and verification completion. Confirm cleanup after consumers release files, bounded retry after deletion failure, and safe startup cleanup after a crash. Ensure originals, active jobs, unrelated files, and necessary recovery backups are not deleted.
8. Inspect logs, temporary directories, frontend storage, and packaged dependencies for unintended retention or network requirements. Verify processing limits and predictable rejection of oversized jobs.
9. Test application and domain rules without WPF, using replaceable OCR and file-commit boundaries for deterministic failures, then run focused desktop and real-filesystem integration checks. Report every failed or unexecuted check and document filesystem limitations.

This ADR records the accepted policy. It does not claim that these validation requirements have been implemented or passed.
