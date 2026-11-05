# Offline OCR evaluation (T3a)

`tools/OcrEval` is an evaluation console, not product OCR. It never opens Excel or
changes review verification. Windows 10 22H2/Windows 11 x64 and the .NET 10 SDK are
required. Provision packages/models while connected; evaluation then uses local
files only. There is no runtime downloader or cloud endpoint. ONNX Runtime telemetry
is explicitly disabled. Network isolation by firewall/air gap was not part of the
synthetic measurement.

## Build and run

From the repository root, after provisioning the assets below:

```powershell
dotnet restore TsekSheet.sln --locked-mode
dotnet build tools/OcrEval/OcrEval.csproj -c Release --no-restore
dotnet test tests/TsekSheet.OcrEval.Tests -c Release --no-restore

# Creates 20 receipts, each in six variants, plus truth.csv and vendors.csv.
tools/OcrEval/bin/Release/net10.0-windows/OcrEval.exe --generate 20 --input artifacts/ocr-eval/synthetic-20

# Paths in this example resolve relative to tools/OcrEval/ocr.example.json.
tools/OcrEval/bin/Release/net10.0-windows/OcrEval.exe --input artifacts/ocr-eval/synthetic-20 --config tools/OcrEval/ocr.example.json --output artifacts/ocr-eval/my-run --report docs/ocr-bakeoff.md

# Later: same executable/config, different input folder; no code changes.
tools/OcrEval/bin/Release/net10.0-windows/OcrEval.exe --input fixtures/redacted/docs --config tools/OcrEval/ocr.example.json --output artifacts/ocr-eval/redacted-run --report docs/ocr-bakeoff.md
```

Generation requires a new or empty folder. Evaluation requires a new output folder;
previous runs are never replaced. Omit `--output` for a timestamped run directory.
`--report` defaults to `docs/ocr-bakeoff.md` and replaces that summary intentionally.
Exit codes: 0 complete without runtime errors; 1 invalid input or failed observations;
2 cancelled. Accuracy errors are measurements, not runtime failures. Ctrl+C cancels
owned workers and retains partial results. Model-free tests need the built renderer
but no Tesseract/models or Excel.

## Local assets and exact versions

No binaries, model weights, generated receipts or private configuration are committed.
`artifacts/` is ignored. Copy the example config under `artifacts/` for local changes
and adjust paths (relative paths resolve against the config's directory). All five
`sha256` entries are mandatory and checked before inference. Empty `renderer` uses
the renderer copied beside the console; empty `vendorList` uses input `vendors.csv`
if present. `threads` controls CPU/OpenCV/Tesseract threads, default 2. Processing is
serial; no GPU provider is selected.

Tesseract tested version: **5.5.1, conda-forge Windows x64 build hfa586c3_2**.
Install into an isolated prefix with its DLL dependencies, using
[conda-forge's recipe](https://github.com/conda-forge/tesseract-feedstock):

```powershell
micromamba create -p artifacts/ocr-assets/tesseract-env -c conda-forge tesseract=5.5.1=hfa586c3_2 -y
artifacts/ocr-assets/tesseract-env/Library/bin/tesseract.exe --version
```

The tested provisioner was [micromamba 2.10.0](https://github.com/mamba-org/micromamba-releases)
(BSD-3-Clause), obtained from the official Windows x64 API. Provisioning is separate
from evaluation. The receipt language file is **tessdata_best 4.1.0 eng**, not the
smaller default data installed by conda. Place it directly in `artifacts/ocr-assets/`.
Download the official **PP-OCRv5 mobile Paddle3.0.0 ONNX inference** archives,
extract them under that same directory, and place the **PaddleOCR v3.3.2** dictionary
there as `ppocrv5_dict.txt`. Verify these SHA-256 values with `Get-FileHash -Algorithm SHA256`:

| Asset / exact source | SHA-256 |
|---|---|
| [Tesseract 5.5.1 hfa586c3_2 conda package](https://conda.anaconda.org/conda-forge/win-64/tesseract-5.5.1-hfa586c3_2.conda) | `394A3DC0D9AB9EC0B930ECFF19A8D27DF99C82F321805FCB7A95B732FB024BF7` |
| Installed `Library/bin/tesseract.exe` from that package | `EA22B4ADAA35BA9F449AAFF9F111C97550ECD2220510CDCE60C1503465A38357` |
| [tessdata_best 4.1.0 eng.traineddata](https://raw.githubusercontent.com/tesseract-ocr/tessdata_best/4.1.0/eng.traineddata) | `8280AED0782FE27257A68EA10FE7EF324CA0F8D85BD2FD145D1C2B560BCB66BA` |
| [PP-OCRv5_mobile_det_onnx_infer.tar](https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv5_mobile_det_onnx_infer.tar) | `781056046C9ED77A15C94681605DB6A0F62317C2E9CCE6931C71DA2478D4BC30` |
| Extracted detection `inference.onnx` | `A431985659DC921974177A95ADCFBB90FD9E51989A5E04D70D0B75F597B6E61D` |
| [PP-OCRv5_mobile_rec_onnx_infer.tar](https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv5_mobile_rec_onnx_infer.tar) | `F7E792BC836F36E7EF895AD47C426D75B0B75B1650CAA6D63FE9418441FFBA8C` |
| Extracted recognition `inference.onnx` | `DA72DC72CA4DC220DF0DFDE68C1DEDC31C58D3E76A25871122E5056227D50092` |
| [PaddleOCR v3.3.2 ppocrv5_dict.txt](https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/v3.3.2/ppocr/utils/dict/ppocrv5_dict.txt) | `D1979E9F794C464C0D2E0B70A7FE14DD978E9DC644C0E71F14158CDF8342AF1B` |

### Implementation choice and licenses

No third-party Paddle OCR wrapper is used. The harness implements DB contour
scoring, rotated-box expansion/unclip, perspective crop and CTC decoding directly
with OpenCV and ONNX Runtime. DB expansion uses the minimum rectangle of the
expanded detection rectangle; it is not an arbitrary-polygon Clipper implementation.
The model preprocessing and dictionary conventions follow the upstream
[detection](https://github.com/PaddlePaddle/PaddleX/blob/release/3.3/paddlex/inference/models/text_detection/processors.py)
and [recognition](https://github.com/PaddlePaddle/PaddleX/blob/release/3.3/paddlex/inference/models/text_recognition/processors.py)
code and the downloaded model `inference.yml` files. BGR input, detector ImageNet
normalization, DB thresholds 0.3/0.6, unclip 1.5 and recognition height 48 are used.
CTC blank/repeat removal uses the supplied dictionary plus space. Paddle word boxes
are estimates derived from CTC timestep spans, not separately trained word detection.

| Dependency / data | Version | License / source |
|---|---|---|
| Tesseract / tessdata_best eng | 5.5.1 / 4.1.0 | [Apache-2.0](https://github.com/tesseract-ocr/tesseract/blob/5.5.1/LICENSE), [data Apache-2.0](https://github.com/tesseract-ocr/tessdata_best/blob/4.1.0/LICENSE) |
| PP-OCRv5 mobile models / dictionary | Paddle3.0.0 ONNX / v3.3.2 | [PaddleOCR Apache-2.0](https://github.com/PaddlePaddle/PaddleOCR/blob/v3.3.2/LICENSE) |
| Microsoft.ML.OnnxRuntime | 1.31.1 | [MIT](https://github.com/microsoft/onnxruntime/blob/main/LICENSE) |
| OpenCvSharp4 / runtime.win | 4.13.0.20260627 | [Apache-2.0](https://github.com/shimat/opencvsharp/blob/main/LICENSE); native OpenCV Apache-2.0 |
| System.Management | 10.0.14 | [.NET MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| PdfPig (renderer metadata only) | 0.1.16 | [Apache-2.0](https://github.com/UglyToad/PdfPig/blob/master/LICENSE) |

NuGet dependencies and transitive versions are pinned in project lockfiles. Keep
the licenses/notices of native dependencies when redistributing; this ticket does
not redistribute a Tesseract environment or models. Leptonica in the tested
Tesseract environment is 1.86.0 (BSD-2-Clause).

## Input format and field rules

UTF-8 `truth.csv`, one receipt per file (all pages of a PDF are one receipt):

```csv
file,vendor,tin,invoice,date,amount
receipt-01.pdf,SYNTHETIC BAYAN STORE,101-000-001-00000,TS-00001,2026-01-01,187.25
```

Files may be relative subpaths inside the input folder; accepted extensions are
`.pdf`, `.png`, `.jpg`, `.jpeg`. Escaping/reparse-point inputs, duplicate file
rows and network paths are rejected. Sources are read only and their SHA-256 is
checked again after evaluation. Keep the dataset unchanged during a run. No original
document bytes are copied to output. OCR text, extracted values and filenames **are**
sensitive on real data: keep redacted runs local under ignored `artifacts/`.

Optional UTF-8 `vendors.csv` has columns `name,tin`. A unique exact normalized TIN
match selects the vendor; otherwise the best unique normalized name match among
the first five text lines (similarity >= 0.65) is used, else the first text line.
The vendor list is supplied reference information, not training. The generator
writes a fake list; results with it measure extraction with that reference available.

Shared rules for both engines: TIN regex accepts nine digits with an optional
3–5 digit branch; invoice labels SI/OR/INV/INVOICE with #/No./number; ISO,
month-first numeric or English-month dates; amount on TOTAL/GRAND TOTAL/AMOUNT DUE
lines. Multiple different eligible totals yield an empty amount, not a guess.
No VAT conversion is performed. Canonical truth uses ISO dates and decimal amounts
without grouping. Comparison normalizes Unicode/case/whitespace, TIN punctuation,
dates and amount formatting; invoice separator whitespace is removed during
extraction. Missing fields count as incorrect. Numeric ambiguous dates mean MM/DD.

## Rendering, profiles and supervision

The existing renderer path gains an opt-in DPI argument. PDFs render at 300 DPI,
raised to the highest embedded-image effective native DPI when higher. Images keep
native resolution. Existing preview size/limits remain unchanged. OCR rendering
rejects resolutions above 1200 DPI, sides above 16384 pixels, more than 64 million
pixels, input above 128 MiB or oversized encoded responses; failures are reported.
The model-free tests cover 300-DPI vector PDF rendering, a higher-resolution embedded
image and unchanged preview dimensions.

Preprocessing profiles: `none`; `deskew-adaptive` (Hough-line deskew and Gaussian
adaptive threshold); `deskew-denoise-clahe` (same deskew, non-local-means denoise and
CLAHE). The synthetic generator uses a fixed seed, OpenCV text and fake Philippine
SI#/OR#, TIN, VATable sales, VAT and TOTAL lines. Variants: clean, Gaussian blur,
Gaussian noise, +/-3–8-degree skew, JPEG quality 20, low contrast. These fonts and
degradations are not a substitute for real scans.

`IOcrEngine` returns text, word boxes and confidence in [0,1]. Boxes refer to the
preprocessed image. Each engine configuration runs in an owned out-of-process
worker with a Windows Job Object, memory cap, timeout and cancellation; Tesseract
uses `ProcessStartInfo.ArgumentList`, stdin/stdout and no shell. Four long-lived
workers are initialized lazily; only one processes a page at a time. No runtime
code fetches assets or sends documents over a network.

## Metrics and output

- `results.csv`: one row per document/engine/preprocess; per-field expected/actual
  and exact match, variant, page count, CER, stage timing, peak MB and errors.
- `variant-summary.csv`: engine x preprocess x variant, including each field's accuracy.
- `words/*.json`: full OCR text, word boxes/confidence and errors for each page/configuration.
- `machine.json`: WMI CPU/logical processors, installed RAM and Windows version/build.
- `run.json`: runtime, asset hashes, configuration and metric definitions;
  `initialization.json`: initialization seconds per engine configuration.
- `docs/ocr-bakeoff.md`: generated single summary table; no engine selection.

**CER is field CER**, because the required truth schema has no full-page transcript:
sum Unicode-code-point Levenshtein edits over the five canonical fields divided by
their total reference characters. Insertions can make CER exceed 100%. Field
accuracy is the average of five per-field exact-match rates. Failed observations
remain in accuracy/error denominators; their partial text is retained.

Seconds/page includes renderer, preprocessing, OCR and IPC, excluding initial model
load and field/report writing. Rendering is performed once per page but charged to
each configuration for a comparable standalone estimate. Multi-page document time
is divided by pages; summary time is the page-weighted mean. Peak MB is the maximum resident working set sampled every
20 ms for the active OCR worker job, including preprocessing and the Tesseract
child, excluding the parent, renderer and idle workers. It is sampled, not an
allocation high-water mark or total application footprint. Initialization is separate;
memory on failed requests may be unavailable (zero). CSV cells that begin with
formula-control characters are escaped for safe spreadsheet viewing; raw JSON
retains exact OCR strings.

Synthetic-only results do not decide the engine. T3b redacted evaluation and T4
product integration remain separate tickets.

## Real-document packets (T3b)

The console detects this exact truth header and uses the metrics-only packet path:

```csv
file,page,doc_type,vendor,tin,invoice_type,invoice,date,vatable,vat,total,wht,amount_due,rfp_no,voucher_no,notes
```

UTF-8 BOM and RFC4180 quoted names/fields are supported. Empty trailing records are
ignored. Each nonempty row labels one primary source page, numbered from 1; multiple
labelled pages per packet are supported. Every PDF page is rendered/classified, including
pages without truth. Ground-truth values never influence orientation, OCR, extraction or
page selection. Notes containing `handwritten` select a notes-flag group, not a measured
handwriting subset or a claim about every field's ink. Classifications are scored only
where truth exists. Non-invoice policy/payment/collection sources have explicit classes
and do not enter invoice-page-pick or invoice-identifier/type denominators.

Use the same run command with `--input fixtures/redacted/docs`; keep output under ignored
`artifacts/ocr-eval/`. The schema selects packet mode automatically. No OCR text, source
names, field values, images or crops are persisted by this mode. `results.csv` contains
anonymous packet ordinals, page indices, fixed label/classification categories and numeric
metrics only. `machine.json` contains hardware metrics. Never commit private inputs or
raw derived content. The generated Real documents section preserves the synthetic report.

Set `ownTin` in ignored local configuration; the committed zero-filled example is a
placeholder and packet mode rejects it. All TINs with that nine-digit taxpayer root are
excluded, including zero-padded branch variants. Amount categories are separate, with
spatial labels and no VAT conversion; `total_amount_due`, `amount_due` and `net_amount_due`
are distinct label matches. Conflicting labels/values produce an ambiguous empty value.
Identifiers retain letters, separators and leading zeros; SI/OR/INV type aliases
canonicalize independently. Voucher number and document reference are separate fields.
Rows/column headers associate amounts using word coordinates, including amount-before-label
and multiline header layouts. Wide column gaps and exempt/zero-rated labels are boundaries.
Multiple candidate values without a unique structural association remain ambiguous;
the first/largest amount is never a fallback.

Cardinal orientation uses four reduced-resolution Tesseract PSM6 text/confidence probes
at 0/90/180/270, then rotates the full bitmap before final OCR for both engines. The score
uses English document keywords and confidence-weighted text length. This is a heuristic,
not a trained orientation classifier. Correction counts are inferred, not verified rotation
success: the supplied truth schema has no orientation labels. Probe time/memory is charged
to every engine/profile; initialization stays separate, and peak excludes renderer/parent.

Page selection ranks invoice classifications first, then invoice/receipt keywords and
extracted invoice/total presence, choosing the earliest tie. Service invoices map to
sales_invoice; generic invoices to invoice_official_receipt; out-of-taxonomy truth types
map to other. Report-only cross-check rows compare invoice vs RFP gross totals without
net fallback, invoice identifier vs voucher document reference (not voucher number),
and invoice vs 2307 TIN/WHT. Identifiers are not reduced to digits for cross-checks.
Missing operands are unavailable. These checks do not authorize writes or select an engine.

Summary exact match pools scorable nonempty field instances. Page-local fields use their
annotated source page unless private review records an ambiguity or key/source mismatch.
RFP, voucher, WHT and net-payment expectations require independent source verification;
otherwise they are unscored, even if an OCR string happens to match. Truth values remain
unchanged. Timing includes full-resolution parent bitmap decode/rotation/re-encoding.
Anonymous packet ordinals follow first-file appearance in truth.csv. Only applicable rows
enter accuracy/agreement denominators; page rows carry timing/classification metadata, and
their exact flag has no scoring meaning.

Private `field-sources.csv` beside truth.csv has columns `file,field,page,status,sha256`.
Statuses: `verified`, `source_ambiguity`, `answer_key_mismatch`, `unverified_source`.
Use the original PDF's SHA-256; changed sources become `stale_source` and unscored.
The sidecar assigns provenance only and cannot override truth values. Verified packet
fields are evaluated on those actual source pages independently of OCR classification.
Metrics report expected/scored counts and exclusion reasons per field/profile/group.

For explicit local inspection, use `--inspect 1,2,3` with the normal input/config flags
and a new `--output fixtures/redacted/ocr-review-local`. Exactly three ordinals are
required. This separate mode exports private previews and recognized text/word boxes
only under this checkout's ignored `fixtures/redacted/`, outside the input docs folder;
linked output folders are rejected. Never force-add these files. Default evaluation
continues to write metrics only. Optional private `inspection-counts.csv` contains only
`category,affected_packets` for the six fixed anonymous review categories, each 0–3.
Counts are reviewer-labelled overlapping affected packets, not an exhaustive error tally.
Original T3b tables remain marked superseded; corrected runs replace only the corrected
section. Model/engine settings are unchanged; no engine decision follows automatically.
