# Workbook-centered workspace — implementation plan

2026-11-03. Supersedes the global task model in workspace-redesign.md.

Audit: WorkbookWorkspace owns one active workbook plus at most two independent
copies. WorkbookViewer already owns their React state and all pending-change /
section-review discard guards. ReceiptReview is a page-owned live review, not a
separate project. Keep both mounted. Do not create recent-project persistence or
concurrent workbook sessions. Current global Placement and Diagnostics destinations
misrepresent this model. Repeated prose and large rendering placeholders hide the
fields/grid; technical inspection reports compete with normal worksheet controls.

1. Workbook navigation: sidebar shows a native Open/switch action, active workbook
   filename, and Overview/Receipts/Worksheets context. Settings contains appearance
   and Troubleshooting. Publish path-free display context from the existing viewer;
   route sidebar selection through its existing guards. Navigation never disposes
   a session. An active receipt must be explicitly closed before replacing/closing
   its workbook so it cannot silently migrate to a different project.
2. Semantic presentation: shared information/success/warning/error/unavailable
   notices with text and symbols; technical details are expandable. Preserve the
   approved SVG file, inspect its viewBox and improve the displayed crop/size only.
3. Receipt workflow: Source → Fields → Destination → Review in the same mounted
   review. Destinations reuse active workbook and worksheet selectors (one viewer).
   Keep full source hashes behind disclosure. No fake rendering/OCR or auto verify.
4. Worksheets: compact top context, main viewport, bounded cell navigation and
   secondary details. Keep inspection-only labels and all existing restrictions.
   Use remaining laptop height for the grid; avoid competing canvas/grid scrollbars.
5. Capture active-workbook before/after views at 1366×768, 1280×720, 1440×900,
   1024×768 and narrow fallback; test theme, keyboard, long names, state retention,
   duplicate/stale-source paths, pending-change protection and existing regressions.

No backend changes, dependencies, original writes, production receipt insertion /
export, commits or deployment. Existing narrow cell-only save behavior is retained.

## Implemented and verified

App retains one WorkbookViewer. The viewer publishes path-free name/worksheet/copy/
pending/inspection context to the shell. Sidebar selection invokes the same open
and discard guards as its native workbook actions. No list of recent workbook
paths is invented. Overview summarizes actual live context. Worksheets also remains
available before selection so users can choose either validated open or inspection.
Receipts is unavailable until a workbook is active. An active receipt blocks UI
workbook replacement/close until explicitly closed; this is a presentation safeguard,
not a new backend verification or authorization policy.

ReceiptReview owns the four step positions and never remounts on step/task changes.
The Destination step shows the same viewer's current workbook/copy/worksheet
selectors; the Review step shows mapping/proposed rows, duplicates and required
choices. Existing typed verification, source/destination invalidation and proposal
restrictions are unchanged. Unsaved edits show a warning and cannot be previewed.
Source identity is visible, while full hashes/configuration/page-association details
are disclosed on demand. Rendering/OCR and receipt output are expressly unavailable.

The shared Notice pattern distinguishes information, success, warning, error and
unavailable states using symbols, labels and semantic tokens. The existing status
badges and native controls are reused. Duplicate warnings use the same warning
presentation. Settings has Appearance and expandable Troubleshooting; the diagnostic
commands still run through the actual host. Technical worksheet reports and section/planning tools are secondary expandable content; explicit cell/range controls remain available.

The approved logo SVG has a 1500×1500 viewBox, including a transformed 1448×1086
embedded image and substantial surrounding canvas. The file is unchanged. CSS crops
the visible mark into a larger 40px brand area and pairs it with a 20–22px wordmark.
Long project filenames are clamped in the sidebar with full title text, and retained
in workbook context; they do not force a huge navigation column or horizontal page
scroll. No new raster/logo asset or UI framework was introduced.

The worksheet task uses a bounded flex canvas with the grid owning ordinary cell
scrolling. Compact selector/action rows, ellipsized repeated filenames and collapsed
technical disclosures reserve the remaining height for the viewport. Expanded
technical reports can scroll their secondary body; this is intentional. The grid
still reads exactly the same bounded rows/columns and preserves cell semantics.
Native inspection remains explicitly noneditable, with its existing backend gates.

## Evidence and limits

- Before: actual hosted receipt test passed with pre-change binary; captures copied
  to artifacts/workbook-ux/before/screenshots (390/768/1440 widths).
- After: artifacts/workbook-ux/after contains actual active-workbook receipt captures
  in light/dark at 1366×768, 1280×720, 1440×900, 1024×768 and 390×768. Worksheets are
  captured at the same sizes and at 125% WebView2 zoom. Long source/workbook names
  are generated artificial test inputs. Multiple desktop/laptop/scaled captures were
  visually inspected; source/sidebar/toolbars were compacted after review.
- Final solution run: application 599 + workbook 446 + desktop 16 = 1,061 passing,
  zero failed/skipped. Final focused hosted run after the small-text correction:
  one passed. Builds include frontend typecheck/production build and WPF host.
- Frontend: 76 passing tests, including four-step unsaved-draft retention. Existing
  section-review, workbook-copy/edit/save and bridge tests remain. Hosted tests cover
  live step navigation, workbook selection, project-switch blocking with a live
  receipt, source changes, correction/reverification, duplicate warnings, worksheet
  navigation, keyboard focus, menu Escape and light/dark layouts.
- Final source lint and browser inspector: zero failures/warnings. Inspector concerns
  initially found small secondary text; labels/footer are now at least 11px. Actual
  hosted views, rather than the disconnected browser view, establish workflow QA.
- Original hashes, selected generated-workbook hash, tracked workbook/media/lock
  inputs and git diff --check pass. No originals were edited or exports enabled.

Observed limits: higher zoom shows fewer worksheet rows; the grid remains bounded
and scrollable. The 390px fallback is functional but desktop is primary. Physical
Windows monitor DPI transitions were not changed/tested; 125% content zoom is not a
claim of physical multi-monitor scaling acceptance. Physical native-dialog and
accounting-owner usability signoff, real OCR/rendering, clean-machine installation
and full MVP acceptance remain unexecuted. No production receipt insertion/export.
