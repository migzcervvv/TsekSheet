# TsekSheet Goals

## Product Vision

TsekSheet is an offline-first Windows desktop application for turning scanned documents into human-verified spreadsheet entries while safely working with existing Excel workbooks.

The initial use case is an accounting reimbursement workflow: process scanned receipts and PDFs, verify extracted information, and apply approved entries to selected worksheets. The product must remain adaptable to other spreadsheet workflows through generic, configurable workbook templates rather than accounting-specific assumptions.

## Target Users

- Accounting and administrative staff preparing or reviewing reimbursements.
- Individuals and small teams that maintain existing Excel workbooks and need control over their formulas, formatting, and worksheet structure.
- Future users handling other document-to-spreadsheet workflows that require local processing and human review.

## MVP Goals

### Local Document Processing and Verification

- Import scanned receipts and PDFs and process them using local OCR.
- Present extracted information alongside its source for human verification and correction.
- Require human verification before OCR-extracted information is applied to worksheet data. Treat OCR output as a draft, not an authoritative record.

### Existing Workbook Editing

- Open and navigate existing Excel workbooks and edit worksheet data.
- Let users select the worksheets relevant to a task and focus the workflow on those sheets.
- Create independent working copies of selected worksheets so users can review and edit changes without altering the original sheets.
- Preserve formulas, formatting, worksheet structure, and workbook integrity throughout supported operations.

### Controlled Integration and Saving

- Allow users to explicitly choose whether to merge changes into an existing worksheet, replace an existing worksheet, or create a new worksheet from a working copy.
- Make the destination and effect of each integration operation clear before applying it.
- Keep original workbooks untouched during opening, OCR, verification, editing, and preparation of changes.
- Write changes to an original workbook only through an explicit save action authorized by the user. Support saving to a separate output workbook.
- Report unsupported workbook content or operations that cannot preserve integrity instead of silently discarding or changing it.

### Configurable Templates

- Support generic, configurable workbook templates that map verified information to worksheet fields and locations.
- Provide an initial configuration for accounting reimbursements without hard-coding that workflow into the core workbook or OCR logic.
- Keep MVP configuration focused on the mappings needed to complete the initial workflow safely.

## Future Features

These are extensions beyond the MVP, not requirements for its initial release:

- Additional template configurations for workflows beyond accounting reimbursements.
- Richer template configuration, including more complex field mappings and workflow-specific validation rules.
- Batch processing of larger document sets and coordinated work across multiple workbooks.
- More detailed change comparison and conflict resolution for complex worksheet merges.
- Improved local OCR extraction for a broader range of document layouts.

Future features must retain local processing, human verification, and explicit control over saving original workbooks.

## Technical Constraints

- Target Windows desktop using WPF, WebView2, React, TypeScript, C#, and Python for offline OCR.
- Core workflows must function without an internet connection. Document contents and workbook data must remain local.
- Do not introduce a database, cloud processing, cloud services, or paid AI APIs.
- Use a modular architecture with clear separation of responsibilities. Keep business logic independent of WPF and WebView2.
- Preserve workbook formulas, formatting, worksheet integrity, and unaffected content when manipulating supported workbooks.
- Test workbook manipulation, including working-copy independence, merge, replacement, new-sheet creation, and explicit-save behavior.
- Keep implementation simple, maintainable, and testable. Document compatibility limitations and report failed or unexecuted tests.

## Project Non-Goals

- A complete replacement for Microsoft Excel or support for every Excel feature in the MVP.
- A full accounting, bookkeeping, reimbursement approval, or enterprise resource planning system.
- An accounting-only architecture that prevents reuse for other spreadsheet workflows.
- Fully autonomous extraction or unverified posting of OCR results.
- Automatic modification, background saving, or overwriting of original workbooks without explicit user authorization.
- Database-backed storage, cloud synchronization, hosted document processing, or paid AI integration.
- Real-time multi-user collaboration or non-Windows applications in the MVP.

## MVP Success Criteria

- A user can process a scanned receipt or PDF locally, verify and correct extracted information, and map it into a selected worksheet's independent working copy.
- A user can review edits and explicitly choose to merge changes, replace a sheet, or create a new sheet before saving the result.
- Original workbooks remain unchanged until the user explicitly saves to them.
- Supported workbook operations preserve formulas, formatting, and workbook integrity, with limitations surfaced clearly.
- The reimbursement workflow is expressed through configurable templates that leave room for other spreadsheet workflows.
