# Workflow profiles

Step 45 connects the original portable planner to public **review-only** generation/approval/cancellation. The profile schema/insertion policy is unchanged. Generation re-plans under copy/destination locks; explicit approval binds the original/selected-copy identities, source hash, profile hash, section/mappings, exact manifest/candidate/review hashes, oracle report and independent revisions. All public tickets permanently reject adoption/integration. Only a narrow native-height policy permits review approval; no profile can override it. See [Step 45 verification](step45-verification.md).

Step 44 adds `WorkflowPlan.Review` with distinct planning-only lifecycle facts. `StructuralCandidateReview` is a separate generated-candidate contract containing exact profile/proposal/source identity, workbook/worksheet, manifest/candidate/review fingerprints, section/row/template specification, field values, duplicate warnings, formula/style impacts, rejection reasons and independent intended copy/destination revisions. Candidate generation leaves accepted state unchanged; explicit approval and adoption are separate internal calls. Profile policies remain disabled and unchanged. See [lifecycle evidence](step44-verification.md).

Step 43 adds a read-only `StructuralStatus` to session-backed plans: ambiguous destination, duplicate review, unsupported structure, stale proposal, experimental envelope only, or production disabled. Preflight does not certify a candidate; `CanApply` and profile `insertionPolicy` remain disabled. An internal experiment re-plans from the original request and compares the complete reviewed plan under the session lock before mutation. See [oracle contract](structural-preservation-oracle.md) and [verification](step43-verification.md). The Step 42 implementation details below remain applicable to pure planning.

Step 42 implements a generic, plain-.NET profile/analysis/planning layer in `TsekSheet.Application/Workflows`. It has no WPF, WebView2, ClosedXML, database, cloud or OCR dependency. `WorksheetAnalysisReader` uses the existing bounded session projection; the pure analyzer and planner also accept synthetic domain data. `WorkbookWorkspace` serializes production planning with existing edit/Merge operations.

## Versioned local JSON

The approved examples are embedded from `src/TsekSheet.Application/Workflows/Profiles/*.workflow.json`: `synthetic-payables` and `synthetic-laboratory`. The latter changes all field roles and headings and is tested against the same engine. Profile parsing is public/testable, but arbitrary frontend JSON and user paths are not accepted. Profile import and a richer template editor are planned.

Schema 1 contains:

| Members | Supported meaning |
| --- | --- |
| schemaVersion, id, revision, displayName | Schema 1, stable ASCII ID, positive profile revision, review label |
| worksheetRule | `selected-with-unique-headers`: explicit worksheet selection; exactly one complete header row |
| maxRows, maxColumns | Complete-sheet capture limits; at most 2,000 rows, 32 columns and 40,000 cells |
| fields | 3–16 unique keys/header labels; text, decimal or ISO date, required flag and length 1–256 |
| entityField, identifierFields, contextFields | Required text entity, 1–4 literal text identifier roles, up to 8 explanatory context roles |
| sections | 1–16 stable IDs/labels with exact normalized heading and subtotal markers |
| aliases, foldCase, collapseWhitespace | Up to 64 one-hop aliases; Unicode Form C, trim, optional whitespace folding and invariant case folding |
| fallbackSectionId | Approved missing-entity section; cannot name an unknown section |
| existingDestination, missingDestination | Only `after-last-match` and `before-subtotal` |
| duplicatePolicy | Only `entity-and-any-identifier`; context explains but does not suppress warnings |
| insertionPolicy, stylePolicy | Only `disabled` and `explicit-template-required` |
| requireReview, requireDuplicateReview | Both must be true |

JSON is case sensitive, bounded to 64 Ki characters/depth 12, and rejects unknown members, duplicate JSON keys, unsupported schema/policies, repeated keys/normalized headers/headings, heading/subtotal collisions, invalid aliases, alias chains/cycles and invalid role mappings. Aliases are explicit equivalences approved by the profile author; similar spelling does not create one. Generic code contains no workbook names, years, vendor names or payables section markers.

Manual values reject unknown keys, invalid XML characters, overlength/required fields and missing identifiers. Decimal values use invariant decimal-point syntax, no thousands separators, nonnegative values up to one trillion and at most four decimal places. Dates use `YYYY-MM-DD`. Input is literal planning data, never formula code or commands. This bounded policy does not implement a complete accounting schema.

## Proposal contract

`WorkflowPlanRequest` freezes session/worksheet IDs, expected session revision, approved profile ID and SHA-256 of canonical typed profile JSON. The reader captures current source SHA-256 and checks revision again after reading; workspace serialization prevents production concurrent mutation. Missing/stale identities or profile hashes fail. Plans are ephemeral and contain base coordinates, virtual final coordinates, mappings, all section matches, suggestions, duplicate explanations, confirmations and integrity concerns.

The pure planner accepts 1–20 uniquely identified records. It orders hypothetical insertions by ascending base boundary then input order and adds preceding insertion offsets. A batch with unresolved choices has no final row positions. Up to 8 similar-name suggestions and 50 warnings per record are allowed; too many duplicates rejects the request rather than silently truncating warnings. Duplicate warnings do not mean the plan can be applied. `ReadyForReview` indicates resolved choices only; `CanApply` is always false.

The bridge adds only `workflow.profiles` and `workbook.planRecords`, after handshake, through the existing trusted-origin/depth/16 KiB request policy. Strict request parsing rejects unknown/duplicate nested members. Larger batches may exceed the transport bound even though the pure engine allows 20; no bound was raised. There is **no apply command**. The default session insertion contract checks identity/revision/profile/review and then always rejects `ROW_INSERTION_DISABLED`, including a forged `CanApply=true` plan. It is a rejection gate, not an execution or candidate-validation implementation.

## Lifecycle and verification

Planning is read-only and does not change original sessions, copies, revisions, dirty flags or source bytes. Failed/cancelled analysis leaves them unchanged. A successful ordinary cell Merge makes old proposals stale; the existing copy/base conflict rules still apply. There is no shifted-coordinate replay through Merge. Save As exports the main session only, never a proposed or unintegrated working-copy record.

Coverage includes strict profile rejection, both generic profiles, fields/aliases/similarity, all-section matches, explicit choices, duplicates and batches, stale/cancelled analysis and blocked mutation. See [verification results](safe-row-insertion.md). Profile format versioning does not establish Phase 3 template-engine completion.
