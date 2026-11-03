# Desktop source review integration

Priority: usable desktop vertical slices supersede further isolated headless work.

Audit: WPF hosts bundled React through the trusted-origin BridgeSession. Existing
WorkbookViewer exposes native workbook open/inspection, navigation, isolated cell
edits, narrow merge/Save As, SectionReview and synthetic PayablesProcessing.
Bridge commands already cover these capabilities and workflow.profiles. Registered
local documents, SourceLinkedFieldReview, verified mapping/placement and verified
candidate lifecycle are headless. Reuse them; do not duplicate their rules.

Implementation sequence:
1. Native PDF/PNG/JPEG picker registers a bounded read-only source. Page-owned
   application coordinator retains source/review authority; only path-free DTOs
   cross the bridge. Cancel leaves existing work intact. Explicit release and page
   disposal clean up memory. No browser storage or document copies.
2. Render source metadata, profile choice and genuine manual entry. Save edits
   separately from explicit human verification; corrections invalidate verification.
   Display unavailable OCR/rendering and unvalidated PDF page associations clearly.
   Profiles remain labelled synthetic compatibility configurations.
3. Reuse source-linked mapping with explicit workbook/session/sheet/revision/hash
   binding. Extend the same checked boundary to the existing pure placement planner.
   Show section choices, duplicate warnings and proposed rows; CanApply stays false.
4. Verify application/bridge contracts, frontend interactions and actual hosted
   React/WebView2 flow using native selector seams and generated source/workbook
   files. Compare hashes, run full solution/frontend tests and inspect screenshots.

OCR remains blocked by decoder, process supervision and redistribution work.
Rendering is unavailable. Safe receipt output remains blocked by structural
preservation/adoption and authorization gates. Existing narrow workbook capabilities
retain their current restrictions. No original workbook writes, commits or deployment.

UI direction: achromatic ledger, retaining existing brand palette/type. The main
question is which manually entered fields have been explicitly checked. Source
metadata and field status remain adjacent; fields come from the selected profile,
never sample receipt values. Empty means empty. Destination changes discard cached
preview display. Fresh commands always check live review/source/destination state.

Implemented in Cycle 14: milestones 1–4 are available in the actual hosted manual
single-record workflow. Application rules remain independent of the host/UI.
Solution verification: 1,061 .NET tests passed; final frontend suite: 74 passed;
final hosted receipt rerun: one passed. Detailed failures, evidence and remaining
safety gates are recorded in docs/development/AUTONOMOUS_PROGRESS.md.
