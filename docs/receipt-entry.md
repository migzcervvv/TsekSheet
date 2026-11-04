# Receipt queue

In **Receipts**, choose **Receipt queue**, then **Add PDFs or images**. Select up to
100 synthetic or owner-approved local receipts. Choose a profile and start the queue.
The source files stay in their original locations; the app keeps only in-memory
references and review state. Closing the app discards the queue and drafts.

Open a workbook in the app-owned visible Excel instance. Choose the workbook identity,
matching sheet and section, then explicitly use that destination for this receipt and
new receipts. Previously visited receipts keep their own destination. To change the
profile, choose it and explicitly apply the change; matching typed fields become
unverified drafts and a fresh destination selection is required.

Read the source beside the fields, enter the required values and choose an account.
New receipts inherit the active account as a draft. **Preview row** validates the
drafts and shows the actual Excel row, columns and literal values. It does not verify
or write them. **Add Row — confirm values and destination** is the explicit human
confirmation of the displayed values and destination. Only then does the app verify
the fields and submit the existing Excel insert operation. Amounts are written as
entered; no VAT conversion is performed.

Success identifies the highlighted Excel row and advances to the next pending receipt.
Previous/Next retain drafts. Skip leaves a receipt reviewable in the Review selector;
automatic advance omits skipped and inserted receipts. Inserted receipts are read-only.
Review highlighted changes and save explicitly in Excel (including Excel's Save As).

Missing/invalid fields, ambiguous subtotal formulas, changed sources or targets and
duplicates block Add Row. Duplicate detection covers equal source hashes in the queue,
the profile's identifiers (the first required text field for layout profiles), and
equal invoice references with equal or missing vendor identity in the selected section.
After a rejected or uncertain write, inspect Excel and use **Refresh queue** before
requesting another preview. A receipt with an uncertain submitted outcome cannot be
submitted again. The existing backup, formula protection and Excel preservation checks
remain in force. VENDOR is never written by the receipt queue.

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
