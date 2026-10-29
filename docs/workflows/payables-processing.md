# Payables processing — Step 42

## Step 45 desktop workflow

The manual flow now supports **Generate candidate for review** from a clean selected working copy after a resolved preview. The final review shows source/profile/workbook/worksheet, vendor and section, exact rows/fields/template, formula/subtotal and style impacts, actual oracle acceptance, native-format blockers, fingerprints and intended independent copy/destination revisions. A plan never appears as a generated candidate.

Only explicitly fixed 21-point templates with tested plain defaults may receive review-only approval. General and duplicate-warning acknowledgments are explicit; the backend independently validates all bindings. Height 22 remains blocked because Excel saves 21.95 even without insertion. Input/choice/profile/view/revision changes invalidate the review; cancellation retracts tickets and late replies are discarded.

**Approve reviewed candidate records review metadata only. Production Apply is disabled for every case.** No workbook rows, dirty state, accepted revision, integration or export change. Public tickets cannot use internal flags as an adoption bypass. Cell editing, ordinary Merge and separate new-file Save As retain their restrictions. Manual entry remains single-record; backend batch support and native batch evidence do not add a batch-entry editor. See [Step 45 evidence and remaining gates](../development/step45-verification.md); earlier descriptions below are historical.

Step 44 adds a lifecycle review to the manual preview. It explicitly separates planner resolution, preflight, candidate generation, oracle verification, approval, working-copy adoption, integration and export. Desktop plans always report **candidate not generated / oracle not run / approval not requested / adoption not performed**; an experimental preflight is never shown as a completed insertion. A future generated candidate must identify exact bytes, field/section/row mappings, template and formula impacts and independent intended copy/destination revisions before approval.

The internal generated-candidate contract and one-operation Merge/export experiment are tested, including explicit multi-section choice and duplicate approval, but have no production bridge command or UI approval button. Apply remains disabled. Four actual Excel reference cases provide qualified evidence, with measured row-height differences still unresolved. [Step 44 verification](../development/step44-verification.md) and [reference provenance](../development/excel-reference-corpus.md) define the current boundary; older milestone paragraphs below are historical.

Step 43 adds structural capability diagnostics to the preview: unresolved destination, duplicate review, unsupported package structure, stale proposal or experimental preflight only. **Apply remains disabled for every desktop operation.** An independent OOXML oracle and internal synthetic candidate editor exist, but production structural Merge/export and native Excel compatibility are unverified. A passing package preflight does not mean the candidate passed validation. See [structural envelope](../development/structural-preservation-oracle.md) and [current verification](../development/step43-verification.md). The original accounting workbook is still unavailable for compatibility qualification.

Status (2026-10-29): manual analysis and proposals are implemented. Structural insertion is disabled in both the application contract and desktop. No OCR or automatic posting is available. Phase 2 is incomplete.

## Reference and business rules

The supplied workflow describes `copy.xlsx`, worksheet `Unpaid RFPs - SEP`, with vendor, particulars, RFP/invoice identifiers, amounts, VAT, EWT, net amount and payment fields. The reported external heading `2025 PAYABLES (EXTERNAL)` near row 765 and subtotal near row 900 are observations supplied by the user, not universal coordinates. The file was not found in the local Desktop/attachment search; its contents, defined names, external links and broken references were **not independently inspected** in Step 42. No private workbook was added to Git. Synthetic workbooks represent the structure only.

The user-approved rules are:

1. Search the entire selected worksheet across every recognized section. Headings, subtotals and entity-only notes are not vendor records. Reject a partial scan or record-shaped content outside configured sections.
2. A vendor in one section gets a proposal immediately after that vendor's last record there. Other vendor groups keep their relative order.
3. A vendor in multiple sections returns every matching section with its last matching row and entry count. An explicit section selection must be submitted in a subsequent validated request. Never choose the first or external section implicitly.
4. An absent vendor is proposed immediately before the configured external subtotal. The profile supplies heading/year/section identity; the engine has no payables names or absolute row coordinates.
5. Normalize conservatively and support explicit profile aliases. Similar names are suggestions only. The reviewer must choose an existing candidate or explicitly retain the submitted name as a distinct entity; legal suffixes and punctuation are preserved.
6. Same normalized entity plus any matching nonblank RFP/invoice identifier generates a potential-duplicate warning. Contextual amounts/dates are shown, not used to suppress a warning. Search existing records across all sections and every other batch record. Never discard or post a duplicate automatically.
7. Review vendor identity, destination, mapped values, affected rows and every warning before any future structural modification. Approval cannot bypass the current insertion block.

## Desktop workflow

Open a supported `.xlsx`, select its worksheet/view, and choose an approved workflow profile under **Payables Processing**. Enter fields manually and select **Preview payable destination**. The built-in synthetic Payables profile maps vendor, particulars, RFP, invoice, amount and date; VAT/EWT/net/payment calculations are not implemented. It is an example rather than a production accounting template.

Review all matching sections, duplicate explanations, field-to-column mappings, base insertion row and proposed final row. If choices are ambiguous, choose explicitly and preview again. Changed choices mark the previous proposal outdated. Changing input, worksheet, view or revision invalidates the displayed proposal; late responses are ignored. Analysis errors retain input for correction. Native paths stay in C#; no workflow data is persisted in browser storage.

**Apply row insertion is unavailable.** Preview does not edit, dirty, merge or save a workbook. Planning works read-only even when an existing compatibility finding prevents editing. Existing working-copy cell editing, reviewed Merge and main-session new-file-only Save As retain their restrictions. There is no structural adoption to refresh, and pending structural records cannot be exported as inserted rows.

## Boundaries and next work

Only approved embedded JSON profiles are selectable. Import/editing of profiles, repeated header layouts, complex accounting rows without literal identifiers, OCR, structural insertion, worksheet Replace/Create New and existing-file replacement remain unsupported. Formula-derived vendor/identifier values are rejected; cached formula results are not trusted as identity. See [profiles](../development/workflow-profiles.md), [section detection](../development/worksheet-section-detection.md) and [insertion safety and verification](../development/safe-row-insertion.md).

Step 43 supplies the bounded specification, independent oracle and internal synthetic candidate tests. Step 44 requires external Excel reference evidence and a separately verified structural Merge/export transaction before production Apply. Names, tables and merges remain rejected rather than implicitly supported.
