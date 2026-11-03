# TsekSheet - Goals

## Product

TsekSheet turns scanned, photographed and poor-quality PDF documents into human-verified rows
in existing Microsoft Excel workbooks, fully offline on a Windows laptop.

First workflow: accounting payables/reimbursements. The core stays generic: workflows are
expressed as profiles (fields, sections, column mappings), not hard-coded accounting logic.

**The product is fast, trustworthy manual verification.** OCR accelerates it. If OCR returns
nothing, the user can still finish the job quickly by reading the document and typing.

## Users

- Accounting and admin staff who process stacks of receipts, invoices and request forms into
  existing Excel workbooks.
- They already use Excel daily, keep files on local disk, and need formulas and formatting
  to stay intact.

## Non-negotiable features

1. **OCR for poor-quality documents.** Printed text on scanned, photographed, skewed, faded or
   noisy pages. Local engine only. Handwriting is read by the user, with the app making manual
   entry fast (zoom, region select, keyboard flow).
2. **Easy field extraction into Excel.** Document fields (date, payee, amount, reference no.,
   description, etc.) are reviewed side by side with the source and written into the mapped
   workbook columns.
3. **Multiple workbooks open at once** for copy/paste between spreadsheets. Provided natively by
   Excel windows that TsekSheet coordinates.
4. **Formulas retained.** Writes never break existing formulas; subtotal/aggregate ranges grow to
   include inserted rows.
5. **Multiple sections in one sheet.** A worksheet can contain several independent sections
   (e.g. per payee group or per month), each with its own header, data rows and subtotal.
   Users can read, define and append into each section without disturbing the others.

## MVP scope

The MVP is complete when a user can:

1. Open one or more workbooks in Excel through TsekSheet.
2. Add a batch of PDFs/images to a queue.
3. For each document: see the page, get OCR proposals for printed fields, correct or type
   values, and mark each field verified.
4. Choose a destination section (defined once per workbook layout and saved as a profile).
5. Preview the row(s) to be inserted, then insert them through Excel. Subtotals and formula
   columns update; neighbouring sections do not move.
6. See the inserted rows highlighted in Excel and save (or Save As) explicitly.
7. Close the app mid-batch and resume review without losing verified fields.

## Constraints

- **Microsoft Excel desktop (Microsoft 365 or 2021+, 64-bit preferred) is required.**
- Windows 10 22H2 / Windows 11 x64.
- Fully offline after install. No cloud OCR, no telemetry, no paid services.
- All dependencies free and AGPL-3.0 compatible. Project license: AGPL-3.0.
- Local persistence allowed for review state, profiles, OCR cache and the action log
  (embedded SQLite or JSON files). No server database.
- Source documents and original workbooks are never modified by the app. Workbooks change only
  through Excel, after a backup, and are saved only by explicit user action.

## Hardware targets

|         | Minimum                                                    | Recommended (reference machine)  |
| ------- | ---------------------------------------------------------- | -------------------------------- |
| CPU     | Intel Core i3 12th gen (e.g. i3-1215U) / AMD Ryzen 3 5300U | ASUS TUF Gaming A15 (dev laptop) |
| RAM     | 8 GB                                                       | 24 GB                            |
| Storage | SSD, 10 GB free                                            | SSD, 20 GB free                  |
| GPU     | Integrated; OCR runs on CPU                                | Optional; never required         |

Budgets on the minimum machine (to be measured, adjusted after the OCR bake-off):

- OCR worker peak RAM <= 1.5 GB, one page at a time.
- Typical A4 page OCR <= 10 s in the background; the UI never blocks.
- Inserting a 20-row batch into a workbook <= 5 s.

## Success criteria (measured on real redacted documents and workbooks)

- Processing a batch through TsekSheet takes less time than the current manual process.
- Zero formula losses and zero changes outside the target range on the redacted workbook set
  (checked by Excel readback).
- Printed-field OCR accuracy reported per field type; low-quality pages still reviewable.
- Interrupted sessions resume with no loss of verified fields.

## Non-goals (MVP)

- Re-implementing Excel: no custom grid, formula engine or OOXML writer.
- Handwriting recognition.
- Cloud OCR, Google Sheets, sync or multi-user features (future).
- Full accounting, approval workflows or ERP features.
- macOS/Linux.
