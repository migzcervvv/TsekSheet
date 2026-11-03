# Local source rendering — Cycle 17

The accepted workbook-centered interface is retained. Source registration now
connects to real PDF/PNG/JPEG viewing in the existing receipt inspector. Viewing
never verifies fields or changes the page association retained by a review.

## Implementation and limits

- Application-owned native capture uses the same validated handle to hash and copy
  bytes. Identity/metadata/hash must match the registration before decoding. Fresh
  source evidence and registration/lifetime checks follow decoding. Paths never
  enter React or the worker. No temporary source/render files are written.
- A separate one-shot `TsekSheet.Renderer.exe` uses Windows.Data.Pdf and Windows
  imaging. It receives bytes over stdin and returns PNG bytes/page metadata over
  stdout. It does not follow links, execute PDF actions, edit workbooks or save.
  The application launches without a shell, assigns a 512 MiB/one-process Windows
  job before sending source bytes, bounds pipe output and terminates on timeout,
  cancellation, page/source closure or page lifetime loss. Job controls are resource
  containment, not an AppContainer/security sandbox claim.
- Limits: 32 MiB input; 1–1000 PDF pages; page dimensions at most 14,400 API units;
  image headers at most 20 million pixels; preview longest side at most 1600 pixels;
  at most 12 MiB encoded PNG; 20-second job deadline. Unsupported, malformed or
  encrypted inputs fail closed with generic path-free errors. Manual review remains
  available when decoding is unsupported. Windows PDF sizes use DIPs, not PDF points.
- One render per bridge page. Explicit cancel waits for that job to finish cleanup.
  Cancelled/stale frontend generations cannot replace a later page. PNG blob URLs
  are revoked on replacement/unmount. CSP permits local image blobs, while remote
  requests, frames, objects and arbitrary filesystem access remain disallowed.
- Source pages can be navigated and enlarged in the current inspector/canvas.
  Enlargement keeps normal task scrolling and mounted field state. Cached images
  are observations, not durable source authority; verification/mapping still use
  the existing fresh application checks.

## Dependency and offline distribution review

Selected: Windows platform PDF/imaging APIs, rather than an additional PDF engine.
Microsoft documents stream loading and bounded destination rendering in
[PdfDocument.LoadFromStreamAsync](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfdocument.loadfromstreamasync)
and [PdfPageRenderOptions](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpagerenderoptions).
There is no renderer/model download or paid/cloud API in the application.

The worker targets `net10.0-windows10.0.19041.0`, pins Windows SDK targeting pack
`10.0.19041.34`, and uses the same .NET 10 prerequisite as the framework-dependent
development application. The targeting pack was restored from the existing local
NuGet cache. Build output includes the managed SDK projection/WinRT support DLLs;
Windows' native PDF/imaging DLLs are supplied and serviced by the OS, not copied
from the development machine. Copy/publish targets retain the worker payload.

The targeting pack's package metadata points to the
[Windows SDK license](https://learn.microsoft.com/en-us/legal/windows-sdk/license).
Its distribution conditions and the matching
[SDK redist list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) must be
included in final installer qualification. The C#/WinRT project's own source uses
the [MIT license](https://github.com/microsoft/CsWinRT/blob/master/LICENSE); this
does not replace the targeting pack's package terms. Development testing is
complete for the cases below; external redistribution/legal approval, a clean
machine and self-contained-worker packaging are not claimed complete. No publish
or deployment was executed.

## Verified behavior

Generated two-page PDFs render as different actual PNG pages. Generated PNG/JPEG
images render with original bytes unchanged. Worker tests cover malformed inputs,
missing workers, cancellation, timeout, request bounds and invalid page numbers.
Source tests reject changes/closure during rendering and keep all fields unverified.
The actual WPF/WebView2 pipeline displays the returned image, navigates pages and
enlarges it; this is not inferred from backend-only tests. Original source bytes
and workbook hashes are checked. Screenshots are in artifacts/rendering/screenshots.

No corpus-wide decoder compatibility/security qualification, encrypted-document
corpus, physical touchpad/device testing or offline clean-machine acceptance is
claimed. Preview resolution is bounded and may omit small source detail; it is not
an OCR-quality claim. Read the original externally when a preview is insufficient.

## Scrolling and hover

The main canvas now always allows overflow. Worksheet cell windows retain bounded
independent scrolling; expanded reports and editors participate in task flow.
Headers/footer do not shrink over task content. Main/sidebar are keyboard focusable.
No wheel interception or new JavaScript scroll implementation was added.

Hosted checks reach each task's last visible action at 1440×900, 1366×768, 1280×720,
1024×768, 390×768 and 1280×600, at 100% and 125% zoom, with details expanded. Native
browser PageDown, End, mouse-wheel and scrollbar drag checks pass. Trackpads use
the same browser wheel path; physical hardware was not exercised.

Open workbook hover formerly inherited the generic surface hover but retained
primary text color. The primary hover rule now explicitly retains its foreground,
background and border. Actual pointer-hover checks in light/dark verify at least
4.5:1 computed contrast; screenshots were inspected.

## Next functional milestone: genuine OCR

Windows OCR was evaluated but not enabled: Microsoft documents that
[Windows.Media.Ocr requires package identity](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr)
and [installed recognition language packs](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.availablerecognizerlanguages).
The current application/worker are unpackaged. Do not rely on unsupported calls or
silently register/install packages to bypass this requirement.

Next qualify a reproducible Windows x64 Tesseract runtime, all native dependencies,
Python/runtime choice and explicitly supported local language models.
[Tesseract's official installation guide](https://tesseract-ocr.github.io/tessdoc/Installation.html)
identifies its Apache 2.0 license and Windows binary sources; its
[model documentation](https://tesseract-ocr.github.io/tessdoc/Data-Files) describes
the model families. Pin runtime/model hashes and retain all relevant notices before
bundling; do not treat any random installed executable as a distributable runtime.
Reuse source capture and supervised jobs, derive genuine unverified candidates from
recognized content and bind them to exact source/page/review versions. Preserve
manual corrections, explicit verification and every receipt-output gate. This OCR
runtime/model qualification and extraction implementation remains the next task;
no OCR results or verified fields have been simulated in production.
