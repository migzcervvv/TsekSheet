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
