# Receipt queue

Choose **Open workbook** in the sidebar. TsekSheet opens the selected workbook in its
visible Excel instance, makes that exact workbook the active workspace, and opens
**Process Receipts** directly. Choose **Add receipts** and select one or up to
100 synthetic or owner-approved local PDF, PNG or JPEG receipts. One file uses the same
queue and review experience as a batch; the selected profile starts review automatically.
The source files stay in their original locations; the app keeps only in-memory
references and review state. Closing the app discards the queue and drafts.

The queue uses the active workbook without opening it again. Choose a matching sheet
and section, then explicitly use that destination for this receipt and
new receipts. Previously visited receipts keep their own destination. To change the
profile, choose it and explicitly apply the change; matching typed fields become
unverified drafts and a fresh destination selection is required.

Workbook switches are blocked while any queue receipts (including skipped or inserted
receipts), drafts, or destination reviews remain in memory. Navigation retains those
drafts and receipt-specific bindings. Finish the current work and save in Excel before
starting another app session; there is no workspace restoration yet. Failed or cancelled
opening retains the current workspace and never falls back to inspection.

**Read-only inspection** is a separate secondary tool and never replaces the active
Excel workspace. Detailed compatibility findings are under **Advanced compatibility
findings**. Legacy tools are under **Settings → Troubleshooting → Advanced review tools**.
**Legacy receipt review** retains per-field Save/Verify/Retract and initial page association.
**Legacy placement tools** exposes the read-only placement, entity and duplicate warnings.
A populated queue blocks entry because these tools share the session's receipt state.

In **Edit**, read the source beside the profile fields, enter required values and choose
an account. Expand **Profile and destination** to explicitly change the profile, sheet or
section. The active workbook is fixed for the queue; it is never reopened or inferred from
list order. New receipts inherit the active account as a draft; revisiting an existing
receipt never replaces its account with a later selection.

**Check row** remains available during optional rendering or OCR when manual fields and
destination are ready. OCR proposals from a request started before Check are ignored after
Check begins; source navigation still invalidates the preview.

**Check row** validates the drafts and opens **Check** with the authoritative Excel row,
range, mapped columns, exact values and applicable warnings. It does not verify or write
them. **Back to edit** returns to retained drafts. Changing values, profile, destination,
or the source view invalidates Check and requires another authoritative preview.
**Add row to Excel** is the explicit human
confirmation of the displayed values and destination. Only then does the app verify
the fields and submit the existing Excel insert operation. Amounts are written as
entered; no VAT conversion is performed.

Success identifies the highlighted Excel row and advances to the next pending receipt.
Previous/Next retain drafts. Skip leaves a receipt reviewable in the Review selector;
automatic advance omits skipped and inserted receipts. Inserted receipts are read-only.
Review highlighted changes and save explicitly in Excel (including Excel's Save As).

Missing/invalid fields, ambiguous subtotal formulas, changed sources or targets and
duplicate submissions block Add Row. Equal source hashes are rejected in the queue.
Layout profiles, including exti-liquidation, do not declare a value-based duplicate
policy: item numbers and vendor/invoice values may repeat. Required fields and column
order never imply uniqueness. A profile explicitly declaring `entity-and-any-identifier`
rejects a matching configured identifier only within the same configured entity.
Successful or uncertain source/page submissions remain blocked even after restarting
the review or changing its profile; a known rejection before writing permits a fresh preview.
After a rejected or uncertain write, inspect Excel and use **Queue tools → Refresh queue** before
requesting another preview. A receipt with an uncertain submitted outcome cannot be
submitted again. The existing backup, formula protection and Excel preservation checks
remain in force. VENDOR is never written by the receipt queue.

The navigator reports backend Pending, Skipped and Inserted counts. Details and insert
history are secondary disclosures. OCR fills only empty draft fields, leaving typed
values intact; its failures leave manual entry available. An unknown insertion response
blocks resubmission for that receipt, including after navigation or refresh.

Legacy parity remains incomplete: normal processing uses consolidated Add Row verification
rather than individual verification/retraction, keeps its initial review page association,
and uses Excel section/row checks rather than the older read-only entity-placement proposal.
Legacy subtotal warnings remain visible; the normal Excel draft path blocks ambiguous
subtotals before insertion.

## T2e measurement

Measured on 2026-11-04 using `DesktopSmokeTests.TwentyReceiptWorkflowTiming`, Release,
the real bundled React/WebView2 UI and Excel adapter. Twenty generated synthetic PDFs
and an Excel-authored liquidation replica cover all 19 account columns; entries fill
rows 29 and 30, then insert above the subtotal. No real receipts/workbooks were opened.

The old workflow was measured before behavior changes and is reproducible at commit
`e250c2c`. The final benchmark defaults to the queue; `TSEK_TIMING_MODE=old` exercises
the retained single-receipt UI. Set `TSEK_TIMING_OUTPUT` to an absolute JSON output path,
then run:

```powershell
dotnet test tests/TsekSheet.Desktop.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~TwentyReceiptWorkflowTiming
```

Native file selections are injected; there is no measured human reading/typing or file
dialog decision time. Scripted input measures event dispatch and execution. Application
time is the remaining stage wall time, including asynchronous work, UI scheduling and
polling. The old workflow's step-navigation helper is included in application time.
Fixture authoring, Excel startup, initial app connection, screenshot capture and final
readback/cleanup are outside these stage measurements. Each table reports 20 samples;
retained setup stages have zero work after the first new receipt. Field confirmation
cost moves from the old field stage into the new Add Row stage. No timing threshold or
claim about human throughput is inferred from this scripted run.

### Old single-receipt workflow

Seconds; each stage has 20 samples.

| Stage | Scripted input total | Application total | Elapsed median | Elapsed slowest |
| --- | ---: | ---: | ---: | ---: |
| receipt navigation | 0.035 | 0.853 | 0.025 | 0.173 |
| field entry / verification | 0.278 | 1.654 | 0.092 | 0.127 |
| workbook opening | 0.020 | 2.238 | 0.103 | 0.275 |
| context selection | 0.045 | 0.002 | 0.002 | 0.006 |
| preview | 0.064 | 11.915 | 0.590 | 0.657 |
| Excel insertion | 0.042 | 24.674 | 1.223 | 1.465 |
| post-insertion navigation | 0.017 | 0.028 | 0.002 | 0.004 |

All stages: **0.502 s scripted input + 41.364 s application = 41.866 s**. Per-receipt elapsed median **2.063 s**; slowest **2.637 s**.

### New receipt queue

Seconds; each stage has 20 samples.

| Stage | Scripted input total | Application total | Elapsed median | Elapsed slowest |
| --- | ---: | ---: | ---: | ---: |
| receipt navigation | 0.005 | 0.037 | 0.000 | 0.033 |
| field entry / verification | 0.324 | 0.003 | 0.005 | 0.125 |
| workbook opening | 0.003 | 0.326 | 0.000 | 0.329 |
| context selection | 0.005 | 0.003 | 0.000 | 0.008 |
| preview | 0.018 | 17.562 | 0.878 | 0.994 |
| Excel insertion | 0.058 | 22.943 | 1.116 | 1.442 |
| post-insertion navigation | 0.000 | 0.054 | 0.001 | 0.021 |

All stages: **0.411 s scripted input + 40.928 s application = 41.339 s**. Per-receipt elapsed median **2.011 s**; slowest **2.852 s**.

## T2e-FIX preview diagnostics

The merged T2e code inferred uniqueness from the first required text field and from
vendor/invoice values despite layout profiles declaring no duplicate policy. It also
read the sheet a second time for that inferred check, after the mandatory Excel
preservation snapshot and layout validation had already run. The fix follows the
existing explicit duplicate policy and skips the additional read/scan when none is
configured. It does not cache, omit or weaken any preservation or prewrite checks.

`ExcelReceiptInsertion.PreviewTiming` and `ExcelHost.PreviewTiming` are optional local
callbacks reporting stage names and elapsed milliseconds, never file paths or receipt
contents. Nothing is logged automatically. To collect the 20-receipt measurements:

```powershell
$env:TSEK_PREVIEW_TIMING_OUTPUT = Join-Path (Get-Location) 'artifacts/preview-timing.json'
dotnet test tests/TsekSheet.Excel.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~OneDestinationSupportsTwentyConfirmedReceiptsAndPreservesAccountsFormulasAndNeighbors
```

The pre-fix measurement used merged main `7a62205` with instrumentation only. Both
versions use the same Excel-authored replica, 20 receipt values/account destinations,
and repeated inserts. Application total includes source checks, mapping, workbook
identity, adapter preview and duplicate policy work. Adapter capture/layout timings
are nested within adapter preview; they must not be added again to the total.
The expensive regression was the extra readback, not layout resolution. No layout
optimization or mutation change was warranted. These are application processing
measurements; they do not measure human entry speed.

Before: 20 previews on 2026-11-04; fixed: 20 previews in the final full run on
2026-11-05. Milliseconds; nested stages are not additive.

| Stage | Before median | Before slowest | Fixed median | Fixed slowest |
| --- | ---: | ---: | ---: | ---: |
| Queue wait | 0.002 | 0.100 | 0.002 | 0.004 |
| Source validation | 1.272 | 5.109 | 1.203 | 3.656 |
| Mapping and submission guards | 0.097 | 3.251 | 0.095 | 0.157 |
| Workbook identity | 14.867 | 23.828 | 14.111 | 19.497 |
| Excel adapter preview (includes next two rows) | 558.175 | 635.476 | 499.548 | 751.588 |
| Mandatory preservation capture | 540.081 | 609.837 | 482.890 | 733.194 |
| Adapter layout resolution | 0.396 | 2.168 | 0.357 | 0.737 |
| Additional duplicate UsedRange call | 13.316 | 19.246 | not run | not run |
| Additional duplicate readback | 274.920 | 328.777 | not run | not run |
| Additional duplicate layout resolution | 0.023 | 0.063 | not run | not run |
| Inferred duplicate scan | 0.975 | 1.358 | not run | not run |
| Total draft preview | 860.683 | 962.720 | 515.078 | 778.987 |

Across 20 previews, total processing was **17.256 s before** and
**10.847 s fixed**. Median preview time changed from
**0.861 s to 0.515 s**. The extra readback/scan is
not run; the mandatory capture and layout validation still run for every preview.

Verification note: the first full Excel run reported a table preservation mismatch.
The isolated three-mode rerun and final full run passed; the cause was not reproduced.
Preservation checks remain unchanged, with failure-only diagnostics added to the test.

## Product OCR (T4)

Provision the hash-checked local assets described in `ocr-setup.md`, then place a local
configuration at `%LOCALAPPDATA%\TsekSheet\ocr.json`. Asset paths resolve relative to that
configuration; use absolute paths when copying the example. Models and binaries are not
bundled or downloaded. The initial product engine is the existing PP-OCRv5 Mobile CPU worker
with no preprocessing. The existing Tesseract evaluator support remains available; the product
does not fall back or switch engines automatically. Missing or invalid local configuration/models
produce a recoverable OCR asset error: restore the configured assets and retry, or enter manually.
The shared configuration schema and hash checks are unchanged, including the retained Tesseract
assets; use the complete provisioned configuration described in `ocr-setup.md`.

Start a queued receipt and choose **Recognize page**. OCR fills only empty draft fields;
typed values and the account choice are retained. Inspect the word boxes, focus a field to
highlight matching source words, or click a matching word to focus its field. Drag a rectangle
on the page and choose **Recognize selected region** for another proposal. Clear a draft
value before requesting its replacement. Manually correct any missing or incorrect value.
OCR supports the existing extractor field keys (including amount, vendor, TIN, invoice and
date); custom profile keys remain manual. No OCR field is automatically verified.

**Cancel OCR**, source-page navigation and leaving a document cancel pending recognition;
late responses from a different source/page are ignored. Worker failure leaves manual entry
available. Recognition processes one page at a time at 300 DPI; preview sizing is unchanged.
Draft preview, explicit Add Row confirmation, backup, preservation checks and explicit Excel
save remain required. There is no automatic insertion or saving, vendor lookup or persistence.

Tests with provisioned assets use `TSEK_TEST_OCR_CONFIG` pointing to the local JSON config.
Without that variable, the two real-worker/hosted OCR tests report the missing assets as
unexecuted; model-free extraction and UI tests still run. Never use original owner documents
as synthetic test inputs.
