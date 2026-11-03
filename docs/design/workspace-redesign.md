# Task-based desktop workspace

## Audit and plan — 2026-11-03

The current screen stacks connection diagnostics, workbook operations, receipt
review and synthetic planning in one long document. The receipt form pushes the
worksheet below the fold; full source hashes and repeated notices consume the
first viewport. Prototype welcome text and native picker demonstrations compete
with real work. Status is scattered; all controls use similar visual weight.
Keyboard focus exists on buttons but inputs, confirmations and task navigation
need consistent treatment. Fixed light colors prevent coherent dark appearance.

Reuse WorkbookViewer, ReceiptReview, SectionReview, PayablesProcessing, bridge
client and discard confirmation logic. Preserve their mounted state while changing
tasks: navigation must never dispose a review or lose a working copy/draft.
No backend service, source authority, verification rule or save restriction changes.

Direction: achromatic ledger, existing Segoe UI and TsekSheet mark. Accounting
objects provide identity: source information alongside checked fields and a
read-only destination proposal. No invented figures, sample receipts or charts.
The screen answers: what needs review before this receipt can be mapped?

1. Define centralized semantic surface/text/border/status/focus/spacing tokens,
   shared navigation/status/empty-state primitives and consistent native controls.
2. Replace the prototype page with a compact sidebar, contextual header, task
   workspace and persistent local/session status. Collapse navigation at narrow
   widths. Put diagnostics in their own task. Offer light/dark/system appearance
   without storing any workbook or receipt data.
3. Split receipt intake, workbook exploration and placement review into task views,
   retaining the live component instances. Keep workbook context/selectors visible
   where placement needs them. Summarize source and verification state, separate
   source inspector from fields, show unavailable rendering honestly, preserve
   correction/unsaved/duplicate/unresolved states. Improve mapped-value scanning.
4. Verify frontend and existing backend tests, actual hosted manual flow, theme,
   keyboard focus and window resizing; capture and inspect before/after views.

Production insertion/export remains disabled. Existing narrow cell-only workbook
operations keep their original restrictions. No new dependencies, network services,
commits or deployment. Physical native dialogs, real OCR and full MVP acceptance
are outside visual acceptance; do not imply they passed.

## Implemented design and acceptance

The shell has a 208px neutral sidebar, fixed contextual header and footer, and a
scrolling task canvas. Receipts is the starting task. Workbook and Placement share
live workbook/worksheet/copy context. Diagnostics is separate from accounting work.
Navigation hides presentation only; it retains the same React instances and live
bridge session. Unsaved receipt edits survive task changes. Correction and source
invalidation continue to remove preview validity through the existing logic.

`ui/Workspace.tsx` supplies Navigation, StatusBadge and EmptyState. Native form
controls, alert/confirmation patterns and existing specialized reviewers share
semantic tokens in styles.css. Light/dark palettes, success/warning/error surfaces,
focus rings, disabled controls, spacing and control dimensions are centralized.
Segoe UI and the existing local mark remain. Appearance is session-only; system
mode follows the OS color preference via CSS. No document data goes into storage.

At narrow widths the sidebar becomes a nonmodal menu with Escape/focus recovery.
Source name stays visible; detailed metadata/fingerprint is expandable. The source
preview explicitly says unavailable; the preview placeholder is omitted on narrow
windows to prioritize actual fields. Placement retains all duplicate/unresolved
warnings and displays verified literals/column proposals in a table. Existing
synthetic planning and read-only section review remain in the Workbook task.

Before/after evidence: artifacts/ui-redesign/before/screenshots and
artifacts/ui-redesign/after. Hosted screenshots cover 390/768/1100 placement views,
390/768/1440 light/dark receipt views, and a dark workbook view. Compared against
the captured pre-change screen: connection/demo panels no longer displace work,
source/field context is adjacent on desktop, task boundaries are explicit, and
fields remain reachable through the single scrolling canvas. Narrow source
metadata was compacted after visual review. No clipping observed in inspected views.

The automated browser inspector reports zero failures. Its initial 4.4:1 sidebar
caption contrast warning was corrected by darkening the shared muted-text token.
Two remaining whitespace warnings concern the disconnected browser empty state:
this is deliberately empty because there is no native host or real receipt data.
Hosted screenshots, rather than the disconnected preview, establish workflow QA.

Verification: full solution 1,061 tests passed; desktop regression rerun 16 passed;
final focused hosted receipt acceptance passed after keyboard/drawer/theme and
responsive refinements. Frontend suite: 75 tests. Typecheck and production builds
passed. The hosted test exercises actual browser Tab focus and Escape recovery,
source changes, duplicate warnings, section selection, corrections and unchanged
original hashes. More detail and development failures are in the progress tracker.

Remaining limits: no real PDF/image decoding, OCR, production receipt insertion or
export; no accounting-owner usability signoff or clean-machine installation claim.
Physical native-dialog acceptance and OS-theme switching while running were not
manually exercised. Native WPF title-bar appearance remains controlled by Windows.
The 390px layout is functional but requires vertical scrolling; desktop is primary.

## Superseded information architecture — Cycle 16

The styling foundation remains, but global Receipts / Workbook / Placement /
Diagnostics navigation is superseded by the workbook-centered workspace described
in [workbook-workspace.md](workbook-workspace.md). That report is authoritative for
current navigation, receipt steps, laptop sizing and verification evidence.
