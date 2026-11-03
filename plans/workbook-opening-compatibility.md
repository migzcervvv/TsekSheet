# Workbook opening and compatibility — 2026-11-03

OCR qualification is temporarily paused for this functional issue. Preserve the
existing uncommitted renderer/UI changes and all private workbook inputs.

## Investigation

Read AGENTS.md, GOALS.md, PLANS.md, the progress tracker (located at
docs/development/AUTONOMOUS_PROGRESS.md), workbook engine/edit/save/inspection
documentation, real-workbook strategy and Step 53 evidence. Open currently calls
WorkbookEngine.Open only; Inspect calls the separate WorkbookInspection boundary.
The native picker, serialized workspace and successful-session adoption are shared.
Open has full-package resource/security/schema/ClosedXML admission; Inspect has
independent metadata and selected-sheet XML budgets, with no mutable model.
Historical actual qualification stopped at 50,001 cells; other independently
observed blockers include external links, VML/comments/printer settings and shared
formulas. Do not attribute that first rejection to every user workbook.

## Implementation sequence

1. On known editable-admission rejection, independently attempt bounded inspection
   of the same native-selected path. Retain the editable rejection in project
   findings. Never retry location/format/access failures or cancellation as editing.
   If inspection fails, return structured reasons for both attempts and retain the
   previous project. Metadata success does not certify every worksheet.
2. Keep the real inspection session active, with EditingAllowed=false and its
   existing denied operations. Add an application copy gate before calling the
   engine; save/mapping gates remain independent. Existing narrow editable sessions
   continue to require explicit copy and new-output actions.
3. Surface inspection success, restrictions and expandable technical findings.
   Preserve error codes/details across the bridge. No success message on failed
   inspection, no suggested package stripping or implicit repair.
4. Add synthetic workbook/workspace/bridge/frontend and actual hosted WPF/WebView2
   regression coverage for large cells/sheet counts, links/binary/advanced/protected
   content, malformed packages, switching, rejected mutations and source hashes.
   Run focused then complete appropriate regressions without weakening gates.
5. Record exact outcomes and remaining compatibility proof in documentation. Resume
   the offline OCR dependency-qualification milestone after workbook verification.

## Editing compatibility decision

No editable-envelope expansion is justified by viewing evidence. Larger resource
limits need measured full-context copy/serialization costs. External links, names,
opaque objects, shared expressions, formatting and structural anchors need
independent semantic/package preservation proof for the proposed operation.
No strip/repair/rewrite/overwrite or receipt insertion/export is authorized here.

## Completion

Implemented and verified. The [compatibility report](../docs/development/workbook-project-opening.md)
records the final paths and feature requirements. The
[progress tracker](../docs/development/AUTONOMOUS_PROGRESS.md) records exact
application/workbook/frontend/hosted results and all failures/retries. OCR
dependency qualification has resumed under
[its bounded plan](phase-04-offline-ocr-qualification.md); recognition is not enabled.
