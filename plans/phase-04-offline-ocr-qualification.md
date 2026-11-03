# Offline OCR qualification — resumed 2026-11-03

Workbook opening/preservation verification precedes this resumed milestone. This
is dependency qualification, not an enabled OCR capability or production release.

## Executed checkpoint

- No `tesseract` command, standard Program Files Tesseract installation, or
  repository Tesseract/model payload was found in the targeted inventory. This is
  not an exhaustive search of the user's filesystem.
- `py --list-paths` locates Store-managed Python 3.12. Native execution through the
  launcher confirms **3.12.10, 64-bit**. The sandbox could not execute it; native
  read-only execution succeeded. A developer's Store installation is not a pinned
  redistributable application runtime.
- The upstream [installation guide](https://tesseract-ocr.github.io/tessdoc/Installation.html)
  separates engine and language data, identifies Apache 2.0 for the engine, and
  links Windows binary providers. Provider binaries/native dependencies need their
  own artifact and license inventory; an upstream link is not distribution proof.
- Upstream [tessdata_fast documentation](https://github.com/tesseract-ocr/tessdata_fast/blob/main/README.md)
  identifies integer LSTM models for Tesseract 4/5; legacy OEM 0/2 is incompatible.
  Its [license](https://github.com/tesseract-ocr/tessdata_fast/blob/main/LICENSE)
  is Apache 2.0. No moving branch URL will serve as the runtime/model pin.

## Next bounded implementation sequence

1. Select a reproducible Windows x64 Tesseract artifact or reproducible source
   build. Record exact version/source commit, acquisition URL, SHA-256, PE machine,
   complete native DLL dependencies and notices. Qualify an explicit English
   synthetic pilot model by immutable commit and hash; additional receipt languages
   require separate evidence. Inventory the Python redistribution/runtime payload.
2. Execute genuine recognition on generated receipt-like PNG/JPEG and rendered PDF
   pages with literal expected text/amount/date controls. Record errors and resource
   costs separately from recognition quality. No private workbook/receipt needed.
   An unavailable engine/model must produce unavailable status, never sample OCR.
3. Design the minimal Python process contract and bounded worker supervision before
   code changes. Reuse source authority/rendering contracts. Do not copy the
   renderer's one-process job limit blindly if Python launches a native child.
   Pin executable/model paths; no shell, PATH fallback, downloads or model updates
   during recognition. Bound input/output, pixels/pages, stderr, timeout and cleanup.
   OCR-resolution requirements need independent evidence; the current 1600px UI
   preview is not qualified as OCR input quality.
4. Bind recognition to freshly checked source registration/hash and exact page.
   Reject source/review changes and cancellation before adopting drafts. Populate
   only genuine **unverified** candidates; preserve manual corrections and renewed
   explicit field verification. No inferred confidence-based verification.
5. Exercise missing/corrupt/wrong-architecture runtimes/models, invalid output,
   process failures/timeouts/cancellation and source invalidation, then actual
   WPF/WebView2 recognition. Verify networking disabled and sources unchanged.
   Bundle only after complete notices/dependency and clean-machine evidence.

No engine/model was installed or downloaded in the resumed checkpoint. Genuine
recognition, runtime/model hashes, Python bundling and offline clean-machine
acceptance remain unexecuted. OCR stays unavailable in the application. Workbook
inspection, copy, mapping, insertion and output rights remain independent.
