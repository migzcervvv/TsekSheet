# OCR bake-off — T3a synthetic evaluation

No engine decision. These are evaluation results, not verified product fields.

Run: `artifacts/ocr-eval/20261105T040021602Z-synthetic20`. 1440 engine/profile/page observations; 0 errors.

Field accuracy is macro exact match over vendor/TIN/invoice/date/amount after documented canonicalization. CER is the micro character error rate across those five fields, not the full receipt transcript. Seconds/page includes rendering, preprocessing, OCR and IPC; engine initialization is separate. Peak MB is sampled resident OCR-job memory (worker + Tesseract child), including preprocessing. See `run.json`, `machine.json`, `variant-summary.csv` and raw word JSON.

Machine (WMI): Intel(R) Core(TM) i3-14100; 15.7 GiB; Microsoft Windows 10 Pro 10.0.19045 build 19045.

| Engine | Preprocess | Pages | Errors | Field accuracy | CER | s/page | Peak MB |
|---|---|---:|---:|---:|---:|---:|---:|
| tesseract-psm3 | none | 120 | 0 | 63.5% | 27.79% | 0.953 | 151.3 |
| tesseract-psm6 | none | 120 | 0 | 66.3% | 23.86% | 0.968 | 151.1 |
| tesseract-psm11 | none | 120 | 0 | 62.8% | 28.45% | 0.971 | 151.6 |
| paddle-v5-mobile | none | 120 | 0 | 90.8% | 9.37% | 1.242 | 463.0 |
| tesseract-psm3 | deskew-adaptive | 120 | 0 | 65.2% | 26.29% | 1.005 | 151.7 |
| tesseract-psm6 | deskew-adaptive | 120 | 0 | 72.5% | 18.17% | 1.013 | 157.0 |
| tesseract-psm11 | deskew-adaptive | 120 | 0 | 66.2% | 25.59% | 0.978 | 146.5 |
| paddle-v5-mobile | deskew-adaptive | 120 | 0 | 94.5% | 6.71% | 1.235 | 482.0 |
| tesseract-psm3 | deskew-denoise-clahe | 120 | 0 | 70.2% | 24.22% | 1.988 | 136.9 |
| tesseract-psm6 | deskew-denoise-clahe | 120 | 0 | 73.3% | 20.28% | 2.020 | 143.7 |
| tesseract-psm11 | deskew-denoise-clahe | 120 | 0 | 70.3% | 24.45% | 2.024 | 143.2 |
| paddle-v5-mobile | deskew-denoise-clahe | 120 | 0 | 99.3% | 0.87% | 2.307 | 465.8 |

Synthetic-only evidence. Redacted-document evaluation remains T3b; T4 integration has not started. Failed rows remain in all denominators. Paddle word boxes are CTC-aligned estimates in preprocessed coordinates. Models and binaries are local, hash-checked and never committed.

## Real documents

No engine decision. Private packet data stays in memory; results contain metrics only. Tesseract PSM6 selected from T3a field accuracy. Handwritten annotations are separated, not recognized by a handwriting model.

| Engine | Preprocess | Printed exact | Handwritten exact | Field CER | Page pick | Corrected/pages | s/page | Peak MB | Errors |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| tesseract-psm6 | none | 20.3 % | 21.7 % | 61.19% | 17/20 | 4/141 | 8.384 | 303.5 | 0 |
| tesseract-psm6 | deskew-adaptive | 15.3 % | 17.4 % | 64.26% | 14/20 | 4/141 | 8.458 | 239.7 | 0 |
| tesseract-psm6 | deskew-denoise-clahe | 17.3 % | 21.7 % | 63.36% | 15/20 | 4/141 | 13.449 | 239.9 | 0 |
| paddle-v5-mobile | none | 24.3 % | 26.1 % | 55.74% | 14/20 | 4/141 | 8.080 | 634.3 | 0 |
| paddle-v5-mobile | deskew-adaptive | 22.8 % | 26.1 % | 57.91% | 16/20 | 4/141 | 8.462 | 637.5 | 0 |
| paddle-v5-mobile | deskew-denoise-clahe | 24.8 % | 26.1 % | 55.66% | 15/20 | 4/141 | 13.567 | 606.2 | 0 |

| Engine | Preprocess | Group | Field | Scored | Exact | CER |
|---|---|---|---|---:|---:|---:|
| tesseract-psm6 | none | printed | vendor | 18 | 38.9 % | 48.40 % |
| tesseract-psm6 | none | printed | tin | 18 | 72.2 % | 18.70 % |
| tesseract-psm6 | none | printed | invoice_type | 15 | 26.7 % | 51.33 % |
| tesseract-psm6 | none | printed | invoice | 15 | 40.0 % | 46.88 % |
| tesseract-psm6 | none | printed | date | 18 | 38.9 % | 25.00 % |
| tesseract-psm6 | none | printed | vatable | 13 | 7.7 % | 86.46 % |
| tesseract-psm6 | none | printed | vat | 18 | 11.1 % | 84.17 % |
| tesseract-psm6 | none | printed | total | 18 | 0.0 % | 98.54 % |
| tesseract-psm6 | none | printed | wht | 15 | 0.0 % | 96.67 % |
| tesseract-psm6 | none | printed | amount_due | 18 | 5.6 % | 86.67 % |
| tesseract-psm6 | none | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | printed | vendor | 18 | 27.8 % | 43.59 % |
| tesseract-psm6 | deskew-adaptive | printed | tin | 18 | 66.7 % | 23.58 % |
| tesseract-psm6 | deskew-adaptive | printed | invoice_type | 15 | 20.0 % | 64.67 % |
| tesseract-psm6 | deskew-adaptive | printed | invoice | 15 | 26.7 % | 55.21 % |
| tesseract-psm6 | deskew-adaptive | printed | date | 18 | 27.8 % | 29.44 % |
| tesseract-psm6 | deskew-adaptive | printed | vatable | 13 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | printed | vat | 18 | 5.6 % | 94.17 % |
| tesseract-psm6 | deskew-adaptive | printed | total | 18 | 0.0 % | 98.54 % |
| tesseract-psm6 | deskew-adaptive | printed | wht | 15 | 0.0 % | 96.67 % |
| tesseract-psm6 | deskew-adaptive | printed | amount_due | 18 | 5.6 % | 89.63 % |
| tesseract-psm6 | deskew-adaptive | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | vendor | 18 | 33.3 % | 45.67 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | tin | 18 | 72.2 % | 18.29 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | invoice_type | 15 | 26.7 % | 51.33 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | invoice | 15 | 33.3 % | 57.29 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | date | 18 | 27.8 % | 30.56 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | vatable | 13 | 7.7 % | 92.71 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | vat | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | total | 18 | 0.0 % | 98.54 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | wht | 15 | 0.0 % | 96.67 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | amount_due | 18 | 5.6 % | 86.67 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | printed | vendor | 18 | 55.6 % | 35.58 % |
| paddle-v5-mobile | none | printed | tin | 18 | 83.3 % | 13.41 % |
| paddle-v5-mobile | none | printed | invoice_type | 15 | 40.0 % | 46.67 % |
| paddle-v5-mobile | none | printed | invoice | 15 | 53.3 % | 44.79 % |
| paddle-v5-mobile | none | printed | date | 18 | 22.2 % | 22.78 % |
| paddle-v5-mobile | none | printed | vatable | 13 | 38.5 % | 60.42 % |
| paddle-v5-mobile | none | printed | vat | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | printed | total | 18 | 0.0 % | 98.54 % |
| paddle-v5-mobile | none | printed | wht | 15 | 0.0 % | 92.22 % |
| paddle-v5-mobile | none | printed | amount_due | 18 | 5.6 % | 82.96 % |
| paddle-v5-mobile | none | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | printed | vendor | 18 | 50.0 % | 34.13 % |
| paddle-v5-mobile | deskew-adaptive | printed | tin | 18 | 77.8 % | 17.07 % |
| paddle-v5-mobile | deskew-adaptive | printed | invoice_type | 15 | 40.0 % | 49.33 % |
| paddle-v5-mobile | deskew-adaptive | printed | invoice | 15 | 60.0 % | 36.46 % |
| paddle-v5-mobile | deskew-adaptive | printed | date | 18 | 27.8 % | 21.67 % |
| paddle-v5-mobile | deskew-adaptive | printed | vatable | 13 | 23.1 % | 77.08 % |
| paddle-v5-mobile | deskew-adaptive | printed | vat | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | printed | total | 18 | 0.0 % | 98.54 % |
| paddle-v5-mobile | deskew-adaptive | printed | wht | 15 | 0.0 % | 92.22 % |
| paddle-v5-mobile | deskew-adaptive | printed | amount_due | 18 | 0.0 % | 98.52 % |
| paddle-v5-mobile | deskew-adaptive | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | vendor | 18 | 72.2 % | 30.29 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | tin | 18 | 83.3 % | 13.41 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | invoice_type | 15 | 33.3 % | 48.67 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | invoice | 15 | 46.7 % | 51.04 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | date | 18 | 27.8 % | 21.67 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | vatable | 13 | 30.8 % | 67.71 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | vat | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | total | 18 | 0.0 % | 98.54 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | wht | 15 | 0.0 % | 92.22 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | amount_due | 18 | 5.6 % | 80.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | rfp_no | 18 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | printed | voucher_no | 18 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | vendor | 2 | 0.0 % | 21.43 % |
| tesseract-psm6 | none | handwritten | tin | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | none | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | none | handwritten | invoice | 2 | 50.0 % | 40.00 % |
| tesseract-psm6 | none | handwritten | date | 2 | 0.0 % | 75.00 % |
| tesseract-psm6 | none | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | vat | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | total | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | wht | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | amount_due | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | none | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | vendor | 2 | 0.0 % | 52.86 % |
| tesseract-psm6 | deskew-adaptive | handwritten | tin | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | invoice | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | date | 2 | 0.0 % | 75.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | vat | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | total | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | wht | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | amount_due | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-adaptive | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | vendor | 2 | 0.0 % | 51.43 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | tin | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | invoice | 2 | 50.0 % | 40.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | date | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | vat | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | total | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | wht | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | amount_due | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| tesseract-psm6 | deskew-denoise-clahe | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | vendor | 2 | 50.0 % | 4.29 % |
| paddle-v5-mobile | none | handwritten | tin | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | none | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | none | handwritten | invoice | 2 | 50.0 % | 40.00 % |
| paddle-v5-mobile | none | handwritten | date | 2 | 0.0 % | 75.00 % |
| paddle-v5-mobile | none | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | vat | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | total | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | wht | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | amount_due | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | none | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | vendor | 2 | 50.0 % | 44.29 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | tin | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | invoice | 2 | 50.0 % | 40.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | date | 2 | 0.0 % | 45.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | vat | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | total | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | wht | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | amount_due | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-adaptive | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | vendor | 2 | 50.0 % | 47.14 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | tin | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | invoice_type | 2 | 100.0 % | 0.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | invoice | 2 | 50.0 % | 40.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | date | 2 | 0.0 % | 75.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | vatable | 1 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | vat | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | total | 2 | 0.0 % | 60.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | wht | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | amount_due | 2 | 0.0 % | 80.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | rfp_no | 2 | 0.0 % | 100.00 % |
| paddle-v5-mobile | deskew-denoise-clahe | handwritten | voucher_no | 2 | 0.0 % | 100.00 % |

Only nonempty truth fields enter exact/CER denominators. Fields are scored on labelled pages independently of page selection. Invoice numbers compare digits only; invoice type separately. Service invoice maps to sales_invoice, generic invoice to invoice_official_receipt, other out-of-taxonomy types to other. Unlabelled pages are classified but classification accuracy is unavailable for them. Rotation counts are inferred corrections, not verified rotation accuracy: truth has no orientation labels. Orientation uses four Tesseract text/confidence probes before final OCR; time includes renderer, orientation probes and OCR/IPC. Peak is max sampled orientation/engine job memory, excluding parent/renderer. Report-only cross-checks and exact amount-label categories are in metrics CSV; no values/text/names are persisted. Empty cross-check operands are unavailable, never agreement evidence.
Summary exact match pools nonempty field instances, not unweighted field-type averages. All fields, including RFP/voucher references, are scored on their annotated page rather than combined across the packet; this limits interpretation of reference-field misses. Cross-check voucher numbers use the first voucher/document/CV-number label. Time excludes full-resolution parent bitmap rotation/re-encoding and engine initialization. These are extraction-pipeline measurements, not isolated OCR-transcription accuracy.

### Report-only cross-checks

Pooled across both engines and all three profiles; counts are comparisons, not unique packets. Unavailable operands are excluded from agreement denominators.

| Check | Available | Agree | Unavailable |
|---|---:|---:|---:|
| Invoice / RFP amount | 24 | 18 | 120 |
| Voucher document number | 46 | 1 | 94 |
| 2307 TIN | 65 | 14 | 37 |
| 2307 WHT | 0 | 0 | 102 |
