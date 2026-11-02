# TsekSheet Implementation Roadmap

## Autonomous cycle 13 - live source-linked mapping preview

The [source-linked mapping plan](plans/phase-04-source-linked-mapping.md) connects
the privately owned live source-linked review to the existing deterministic
read-only column/section mapping. Fresh source observations bracket complete
worksheet analysis and typed projection; destination checks follow source I/O.
Lifetime, exact review/profile/source/destination versions and cancellation are
enforced through the serialized private boundary. Both synthetic profiles,
artificial native documents, independent sheets and dirty copies are tested.
Cached previews remain observations with CanApply=false. Exact executed checks
are in the [progress tracker](docs/development/AUTONOMOUS_PROGRESS.md). No OCR,
renderer, UI/bridge or workbook mutation is enabled; MVP remains incomplete.

Next bounded task: first record a focused plan to connect one live source-linked
review to the existing read-only single-record placement planner. Retain exact
live review/profile and explicit destination/session/sheet/revision/source
bindings, private review ownership and fresh source checks around analysis,
typed projection and planning. Recheck the destination after source I/O and
registration/lifetime/cancellation after final destination reads. Reuse the
existing pure planner's section/entity choices, duplicate warnings and conservative
worksheet analysis, preserving literal/typed/source provenance and CanApply=false.
Test both profiles, independent synthetic worksheets/dirty copies, corrections
requiring renewed verification, stale/foreign bindings, source changes/release/
disposal, cancellation and destination changes during reads, with unchanged
originals/cells/revisions. No batches, structural preflight, candidate preparation/
approval/adoption, OCR/tool choice, rendering, UI/bridge, persistence or workbook
writes in that slice. This is the documented Phase 3/4 live verified-data placement
prerequisite and needs no accounting-owner decision or routine milestone approval.

## Autonomous cycle 12 - live source-linked field review

The [source-linked review plan](plans/phase-04-source-linked-review.md) connects
exact registered local sources to a privately owned headless field review.
Fresh source checks bracket review commands and typed projection. Staged changes
commit only after final checks; cancellation rolls back and source failures
terminally invalidate the review. Corrections require renewed explicit field
verification. Caller-created evidence, cached snapshots and unwrapped reviews
cannot enter this live boundary. Both synthetic profiles and artificial native
files are tested. PDF ordinals remain associations, not parsed/rendered-page
proof. Exact checks are in the [progress tracker](docs/development/AUTONOMOUS_PROGRESS.md).
No OCR, UI/bridge, source-linked mapping, candidate adoption or workbook mutation
is enabled; Phase 2/3 and the MVP remain incomplete.

Next bounded task: first record a focused plan to connect this live source-linked
review to the existing headless read-only single-record mapping preview. Require
exact live review/profile and explicit destination/session/sheet/revision/source
bindings. Recheck the registered document authorities before and after worksheet
analysis and typed projection, and recheck the destination after source I/O so
changes during reads cannot return a current-looking preview. Keep ownership
private: do not expose or accept a substituted wrapped FieldReviewSession, cached
projection or caller-created verification proof. Reuse deterministic mapping and
the existing analysis/field constraints; preserve synthetic/proposal APIs and
every CanApply=false gate. Test both profiles, independent synthetic worksheets/
working copies, changed/released/disposed sources, corrections, cancellation and
destination changes during source reads, with unchanged originals/cells/revisions.
No placement batch, structural preflight, candidate approval/adoption, OCR tool
choice, rendering, UI/bridge, persistence or workbook mutation in this slice.
This is the documented Phase 3/4 live verified-data mapping prerequisite and
needs no accounting-owner decision or routine milestone approval.

## Autonomous cycle 11 - bounded local document source registration

The [local source registration plan](plans/phase-04-local-source-registration.md)
adds application-owned in-memory source associations for explicitly selected
PDF/PNG/JPEG files. Bounded read-only native filesystem observations retain
identity, metadata and SHA-256; fresh evidence issuance rejects missing, locked,
replaced or changed sources. Path-free immutable snapshots, exact versions,
terminal invalidation, cancellation and release/disposal are tested with
artificial local bytes. Signature triage is not parser/rendering/OCR validation.
Existing supplied SourceEvidence remains synthetic/proposal evidence in existing
review APIs; live source authority is not yet connected. Exact checks are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md); MVP remains incomplete.

Next bounded task: record a focused plan to connect live registered sources to
a headless application-owned field-review boundary. Bind drafts/corrections to
exact live registrations; obtain evidence through fresh source checks rather
than accepting arbitrary caller-created SourceEvidence as live proof. Revalidate
source authorities before and after explicit field verification and typed
projection, rejecting changed/replaced/released/disposed sources and stale or
foreign versions. Keep review ownership private so an unwrapped review cannot
bypass the live source checks. Corrections must invalidate verification; source
availability must never automatically verify a field. Test both synthetic
profiles and artificial local files with cancellation, missing/locked sources,
identity replacement and unchanged originals. Preserve existing synthetic/
proposal-only APIs and all mutation gates. Document PDF ordinal limitations;
do not claim parsed pages, rendered source inspection, human receipt acceptance
or OCR. No renderer/tool choice, UI/bridge, persistence, temporary document copies,
candidate adoption or workbook mutation in this slice. This is the documented
Phase 4 source-linked verification prerequisite, with no owner decision needed.

## Autonomous cycle 10 - live verified candidate metadata approval

The [verified candidate review lifecycle](plans/phase-03-verified-candidate-lifecycle.md)
owns live review/copy/parent authorities and the exact engine ticket, hashes and
ordered provenance. Explicit metadata-only approval rechecks those authorities
before and after the existing public engine command. Foreign/replayed commands,
duplicates, formatting gates, stale state and cancellation are tested; cleanup
distinguishes confirmed discard from reported invalidation/cleanup failure and
protects newer tickets. Public tickets stay permanently review-only, with no
adoption/integration/export/production Apply. Exact executed checks are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md); Phase 2/3 and MVP
remain incomplete.

Next bounded task: record a focused plan for the already-required local document
import/source association boundary (GOALS.md local document processing; Phase 4
source-linked verification). Add headless application-owned in-memory source
registration from explicitly selected local files, with bounded byte reads and
identity/content fingerprint/revision observations. Revalidate the source before
issuing evidence for review, reject missing/replaced/changed sources, and test
with artificial local image/PDF bytes and unchanged originals. Keep paths out of
field evidence and diagnostics. No PDF rendering/OCR tool choice, UI/bridge,
persistent store, temporary document copies, candidate adoption or workbook
mutation is authorized by this slice. Existing supplied SourceEvidence remains
synthetic/proposal evidence until live source registration is explicitly connected
to review; do not claim receipt verification or authenticate arbitrary caller
identities. Follow ADR-0002 lifetimes and document supported-input limits. This
independent dependency needs no accounting-owner decision or routine approval.

## Autonomous cycle 9 - live verified review-only candidate preparation

The [verified candidate preparation](plans/phase-03-verified-candidate-review.md)
connects 1-20 live reviews to the existing public review-only candidate generator
on an explicitly bound copy and parent destination. It retains ordered typed/
literal/source provenance, actual candidate/manifest/review hashes, oracle
findings and distinct lifecycle facts. Revalidation failures discard the exact
generated ticket; cleanup failures are explicitly reported. Synthetic tests
cover both profiles, narrow formatting, source/copy isolation, rejection,
cancellation and retries. No approval/adoption or structural integration/export
is enabled. Exact commands/results are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md); Phase 2/3 remain incomplete.

Next bounded task: record a focused plan for an application-owned verified
candidate review lifecycle that retains live authorities and exact engine ticket/
hash/provenance bindings. Add explicit **metadata-only review approval** through
the existing public review-only engine contract, requiring a deliberate command
for that exact ticket and explicit duplicate acknowledgment. Revalidate every
live review and copy/parent/base/profile/source binding before and after approval;
discard this ticket on stale review, destination change or cancellation, reporting
cleanup failures. Never accept caller-created snapshots as verification/approval
proof or substitute corrected entities without renewed verification. Test stale/
foreign/replayed approval, changes during approval, duplicates, formatting gates
and cleanup with both synthetic profiles and unchanged sources/cells/revisions.
Public tickets must remain permanently review-only; do not enable adoption,
structural integration/export, production Apply, UI/bridge, OCR, persistence or
broader compatibility. This follows Phase 3's verified preview/revision lifecycle
without authorizing active-workbook mutation or requiring routine approval.

## Autonomous cycle 8 - read-only verified structural preflight

The [verified structural preflight](plans/phase-03-verified-structural-preflight.md)
connects 1-20 live verified reviews and one explicit destination to the existing
read-only capability diagnostic. It shares Prepare's choice/duplicate/structure
precedence, retains ordered typed/literal/source provenance and separate lifecycle
facts, and rechecks all live authorities after preflight. Synthetic plain passes
and multi-boundary/merged/hidden/unsupported rejections preserve cells, revisions,
copy changes and source hashes. Every CanApply remains false; no candidate is
generated. Exact results and limits are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md). Phase 2/3 remain incomplete.

Next bounded task: first record a focused plan to connect live verified inputs
to the existing **review-only candidate preparation** on a selected working copy
and its explicit parent destination. Accept 1-20 live reviews/expected versions
and explicit copy/destination/profile/source bindings; revalidate every authority
after generation and discard the newly generated ticket on stale review,
destination change or cancellation. Retain per-record provenance and actual
candidate/manifest/review hashes and oracle/lifecycle facts. Test both synthetic
profiles, narrow supported formatting, source/copy isolation, rejection and
cleanup using the existing public review-only engine contract. Never accept cached
preflight as authority, synthesize human approval, call approval/adoption or enable
structural integration/export, UI/bridge, OCR, production Apply or broader
compatibility. This exercises the documented Phase 3 isolated-candidate/reopen/
validation requirement without enabling active-workbook mutation.

## Autonomous cycle 7 - bounded placement of live verified records

The [bounded verified placement extension](plans/phase-03-verified-bounded-placement.md)
accepts 1-20 live reviews with explicit expected versions and one selected
destination. It retains per-record typed/literal/source provenance, cross-record
duplicate warnings, unresolved choices and deterministic virtual row ordering.
Invalid/stale/foreign/repeated inputs reject the whole preview; every review and
the destination are rechecked. The older mapping path now also rejects review
changes/cancellation during its final destination read. All results remain
read-only with `CanApply=false`; Phase 2/3 and the MVP remain incomplete. Exact
checks and limitations are in the [progress tracker](docs/development/AUTONOMOUS_PROGRESS.md).

Next bounded task: record a focused plan to connect live verified placement to
the existing **read-only structural preflight**, following the established
`WorkflowPlanning.Prepare` precedence for unresolved choices, duplicate review
and supported/unsupported structure. Use live review sessions and expected
versions with one explicit destination; recheck all authorities after preflight.
Preserve per-record provenance and the distinct planning/preflight/generation/
oracle/approval/adoption/integration/export lifecycle facts. Test supported plain
synthetic fixtures and rejected multi-boundary/merged/hidden/unsupported cases on
independent worksheets/copies with unchanged cells, revisions and source hashes.
This is the existing Phase 3 capability diagnostic applied to verified inputs,
not candidate acceptance. Never accept cached observations as authority or enable
candidate generation/approval/adoption, mutations, UI/bridge, insertion, export,
OCR or production Apply. Retain all current compatibility and save gates.

## Autonomous cycle 6 - read-only placement of live verified fields

The [bounded verified placement preview](plans/phase-03-verified-placement-preview.md)
uses the existing pure section/entity/duplicate planner with live review and
workbook bindings. It retains typed/literal/source provenance, ambiguous section
and similar-name choices, duplicate warnings and independent worksheet/copy
coordinates. Entity correction requires renewed verification. All results remain
observations with `CanApply=false`; candidate generation and mutation gates are
unchanged. Exact checks and limitations are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md).

Next bounded task: record a focused plan to extend verified placement to the
existing planner's bounded 1-20-record input, using live review sessions and
explicit expected versions for every record with one selected destination.
Capture/recheck every live review and the destination; reject the whole preview
on invalid/stale/foreign/repeated review input. Preserve cross-record duplicate
warnings, unresolved choices, deterministic virtual row ordering and per-record
typed/literal/source provenance. Test both synthetic profiles and independent
worksheets/copies. This completes verified input coverage for the existing
bounded planner, not larger document-set processing. Never accept cached
observations as authority or enable candidate generation, mutation, UI/bridge,
insertion, export, OCR or production Apply. Phase 2/3 remain incomplete.

## Autonomous cycle 5 - read-only mapping of live verified fields

The [bounded verified mapping preview](plans/phase-03-verified-mapping-preview.md)
resolves configured columns and section bounds independently per selected
worksheet and captures typed/literal/source/review provenance from the live
review. Expected review/profile and destination session/source/sheet/revision
bindings reject stale or incompatible inputs. All results are read-only with
`CanApply=false`; raw-input proposal APIs and existing mutation gates remain
unchanged. Current checks and limitations are in the
[progress tracker](docs/development/AUTONOMOUS_PROGRESS.md).

Next bounded task: record a focused plan for a headless, single-record verified
placement preview that reuses the existing section/entity/duplicate planner.
Accept live review/workbook sessions and expected review/profile/destination
bindings, retain typed/literal/source provenance alongside proposed placements,
and recheck live revisions during capture. Preserve ambiguous-section choices
and duplicate warnings. Do not silently replace a verified entity with a
selected similar name: that change requires correction and fresh verification.
Use both synthetic profiles and independent worksheets/copies. Never accept a
cached mapping/projection as proof or enable edits, candidate generation,
UI/bridge, insertion, export, OCR or production Apply. This follows the existing
Phase 3 preview/mapping criteria; Phase 2/3 remain incomplete.

## Autonomous cycle 4 - typed projection of live verified fields

The [bounded typed projection](plans/phase-03-typed-review-projection.md) shares
the planner's established pure value validation and captures typed values from
the live review under its revision lock. It preserves literal text, normalized
values, decimal/date types and source/value/review/profile provenance. Draft,
stale, invalid-type and missing-identifier records reject without state changes.
No workbook mutation or production Apply capability is enabled. Cycle evidence
and remaining gates are in the [progress tracker](docs/development/AUTONOMOUS_PROGRESS.md).

Next bounded task: record a focused plan for a headless, read-only mapping preview
that accepts live review sessions and explicit expected review/profile and
worksheet revisions. Use the typed projection and current worksheet analysis to
map fields to configured columns, preserve literal/source provenance, and reject
stale/foreign versions and incompatible destinations. Exercise both synthetic
profiles on independent worksheets. Do not accept caller snapshots as proof or
enable workbook edits, UI/bridge, insertion, export, OCR or production Apply.
Existing raw-input proposal APIs remain proposal-only. Phase 2/3 remain incomplete.

## Autonomous cycle 3 - headless verified-field contract

The [bounded Phase 3 contract](plans/phase-03-verified-field-contract.md) now owns source-bound draft/corrected/explicitly verified state in memory. Every correction invalidates verification; stale/foreign/replayed commands and missing required/unverified supplied fields reject without changing state. Both synthetic workflow profiles and defensive snapshot/concurrency controls are tested. [Cycle 3 evidence](docs/development/AUTONOMOUS_PROGRESS.md) records 501 passing solution tests and 70 passing frontend tests. Source registration, typed mapping, OCR and actual human-review acceptance remain separate; all mutation gates and incomplete Phase 2/3 status remain unchanged.

Next bounded task: extract the existing planner's pure value-validation rules without changing their semantics, and add a headless typed-record projection that atomically consumes the live review at its expected version/profile. Reject stale/unverified/wrongly typed/invalid records and retain literal text plus source/version provenance. Record a focused plan first; do not accept snapshots as verification proof or enable workbook mutation, UI/bridge, insertion, export or OCR adapters.

## Autonomous cycle 2 - planner formula-context rejection

The older flat `WorksheetSectionAnalyzer.Record` now rejects named rows without identifiers when a configured context field contains a formula, including uncached and zero-cache formulas, inside either declared section. Both generic profiles and synthetic workbook/session/copy preservation controls are covered. Configured markers, entity-only notes, outside-section formula summaries and all mutation gates remain unchanged. [Cycle 2 evidence](docs/development/AUTONOMOUS_PROGRESS.md) records 464 passing .NET tests and 70 passing frontend tests; Phase 2/3 and the MVP remain incomplete.

Next bounded task: begin the already-required Phase 3 verified-data contract as a headless application-layer slice, consistent with ADR-0002/0003. First record a focused implementation plan, then model draft/corrected/explicitly verified field values bound to their source and value revision; corrections invalidate verification, and validation rejects draft/stale/missing required fields. Use artificial reimbursement and non-reimbursement inputs only. This independent core contract needs no accounting-owner decisions or OCR tool selection; it must not enable template application, insertion, export, persistence or bridge/UI commands. Mapping, extraction, packaging and actual accounting acceptance remain separate dependencies.

## Autonomous cycle 1 - unresolved financial context

The [autonomous progress tracker](docs/development/AUTONOMOUS_PROGRESS.md) records the current bounded repair and exact checks. Read-only analysis must retain an entity with formula-bearing or truncated financial context but no identifier as an unresolved record, while preserving explicitly configured subtotal/summary marker behavior. Synthetic domain and real-inspector tests cover hidden rows, cached/uncached formulas, review/retraction, and unchanged source bytes. Existing mutation gates remain unchanged.

Next bounded task: reproduce and repair the analogous missing-identifier/formula-context omission **inside declared data sections** in the older `WorksheetSectionAnalyzer.Record` planner. Preserve its configured markers and established outside-section summary behavior; do not enable Apply or expand its profile schema. Add rejection/control tests and run application, workbook and desktop regressions. This correctness repair does not depend on accounting-owner acceptance or a new architecture.

## Step 56 implementation plan

Result: [420 solution tests and 52 frontend tests pass](docs/development/step56-verification.md). Review loss requires default-preserving confirmation; bounded scroll/keyboard/A1 navigation and focused categories are implemented. Four actual tabs trim formatting-only tails to 20 columns; distant value/formula evidence keeps the third tab wide. Private source and all 126 fixture/logo/lock fingerprints are unchanged. Full safety harnesses and self-contained Windows publish pass. Accounting-owner acceptance remains outstanding; inspection mutation/export stays disabled and Phase 2 remains incomplete.

Preserve the dirty Step 51–55 baseline and every existing mutation restriction. Add a default-preserving confirmation for memory-only review loss on close, replacement, worksheet/workbook changes and native window close. Keep disposal unconditional for resource safety.

Use the existing bounded viewport bridge/cache for debounced, single-flight scrolling, direct A1 navigation and keyboard movement. Retain selection and invalidate obsolete responses without eagerly loading sheets. Extend the existing complete, budgeted worksheet scan with conservative meaningful-content bounds, retaining hidden data, formulas, distant values and internal blanks; offer the full stored range explicitly. These display bounds never affect analysis or editing admission.

Group Section Review into Overview, Sections & Subtotals, Missing Vendors, Potential Duplicates, Unassigned Rows and Review History. Keep decisions separate and retractable, preserve warnings and backend validation, and move technical detail into disclosures. Verify synthetic frontend/backend/native bridge paths, private source fingerprints and bounded qualification, established full checks and Windows publish. Record measurements, failures and limitations in step56-verification.md. No persistence, dependency changes, commit or push; Phase 2 remains incomplete.

## Step 55 implementation plan

Result: [416 solution tests pass](docs/development/step55-verification.md). The [memory-only Section Review panel](docs/development/payables-section-review.md) supports configured profiles, source-bound assertions, retraction and independent coverage. Private qualification retains 25 uncertain identities and 484 unassigned rows with zero fabricated human decisions; source and 126 fixture/logo/lock fingerprints are unchanged. Self-contained publish and existing safety suites pass. Step 56 should obtain actual accounting-owner review; insertion/export and Phase 2 remain unavailable/incomplete.

Build an independent, in-memory read-only review service around one bounded inspection capture. Keep raw facts, configured profile, reviewer assertions and recomputed diagnostics separate. Bind every decision/retraction to the inspection/source/sheet/order/capture/profile/review revisions; validate source evidence and reanalyze the whole capture atomically. Never return insertion authority.

Expose native profile selection and strict review bridge commands; add a focused React panel with source navigation, section/header/boundary assertions, missing-identity and duplicate queues, retraction and independent before/after coverage. Release review state on cancellation, sheet/source switch and disposal. Preserve all existing editing/commit guards.

Validate with artificial domain, workspace, bridge, frontend and real WebView2 tests, then qualify the authorized ignored duplicate without manufacturing human approvals. Run established regressions/publish, compare source/assets/fixture/lock fingerprints, audit privacy and document measured limits and remaining accounting questions.

## Step 54 implementation plan

Result: [401 .NET tests pass](docs/development/step54-verification.md), with ten qualified exact native format-resolution controls and nine explicit rejections. Actual numeric qualification considers all 23,452 cells but certifies only 21 displays. A separate bounded capture and hierarchical/identity analyzer recognizes both headers and rejects incomplete real coverage without hiding the 25 missing vendors. Source fingerprints and all mutation gates are unchanged. [Step 55](docs/development/read-only-payables-section-analysis.md) should review the unresolved hierarchy/identities through a narrow interface and independently qualify useful additional display grammar; [separate reviewed output](docs/development/reviewed-payables-output-design.md) remains design-only.

1. Preserve all prior work; validate and privately fingerprint only the authorized duplicate and ignored diagnostics. Recheck the toolchain and immutable reference/fixture/branding/lock fingerprints.
2. Separate bounded stylesheet parsing from effective-format resolution. Keep globally malformed/budget-exceeded stylesheets raw; isolate duplicate, low-ID collision, missing and unresolved-inheritance cases. Preserve source/effective IDs, codes and precise reasons. Obtain independent literal/native synthetic controls before widening any accepted resolution.
3. Add a separate read-only region-capture contract and pure configurable hierarchical section/identity analyzer in the application layer. Bind session/sheet/revision/profile and explicit scan coverage; include hidden rows, cancellation and budgets. Require explicit boundary rules, reject overlap/incomplete coverage and never infer blank vendors or emit actionable insertion plans.
4. Expose conservative format details in the existing inspector and structured section diagnostics through the backend. Requalify the private workbook with an ignored profile/report; publish only technical aggregates. Document the proposed reviewed separate-output lifecycle without implementing export.
5. Run focused negative/safety tests, synthetic native evidence comparisons, full locked restores/build/regressions/harnesses/publish/desktop verification and final source/privacy fingerprints. Record all failures and unexecuted checks. Keep Phase 2 incomplete and every mutation gate unchanged.

## Step 53 actual workbook qualification

The authorized duplicate is supplied and [read-only qualification is measured](docs/development/step53-verification.md), with an unchanged private fingerprint. Five tabs browse; 439,321 stored cells, extensive names/external links, opaque parts and advanced structures block safe editing. Fields/external subtotal are visible, but missing identities and incomplete section bounds keep planning partial. No source repair, editing-limit increase or mutation occurred. All 359 solution tests and existing harnesses pass. [Current strategy](docs/development/real-workbook-compatibility-strategy.md) recommends Step 54: narrow read-only style interpretation and explicit layout/identity diagnostics, followed by a reviewed separate-output design before any direct package editing proof. Phase 2 remains incomplete.

## Step 52 portability repair and qualification gate

Plan: preserve Step 51 work; identify source/evidence byte conventions; add narrow Git attributes and explicit preflighted remediation; verify LF/CRLF and actual fresh checkout behavior; restore native comparison eligibility from retained evidence only; run full regression/publish; retain real-file qualification as unexecuted if input is absent. Result: [359 solution tests, five portability tests and four fresh-checkout reference tests pass](docs/development/step52-verification.md), zero solution failures/skips. No authorized workbook was supplied, no mutation envelope changed, and the conditional compatibility strategy remains unqualified. Step 53 should finish actual workbook qualification. Phase 2 is incomplete.

## Step 51 diagnostic readiness

The authorized `local-test-workbooks/` directory is absent: real-workbook qualification is **UNEXECUTED**. Run existing non-private readiness checks, preserve all mutation gates, and document exact supply instructions and conditional preservation alternatives without adding synthetic features. [Verification](docs/development/step51-verification.md) records 357 passing solution tests, one provenance failure and one native-reference skip; [strategy](docs/development/real-workbook-compatibility-strategy.md) separates code rules from unknown real-file findings. Step 52 must finish the real-input gate and resolve checkout-sensitive provenance before selecting an editing architecture. Phase 2 remains incomplete.

## Step 50 plan

Preserve the dirty 947ddea baseline and existing native evidence. Check only authorized ignored input paths. If absent, mark real qualification unexecuted. Capture an additional isolated synthetic precision corpus in native Excel, retain exact discrepancies, correct or explicitly reject proven display problems, then run the complete regression/publish and fingerprint checks. No mutation, cache, dependency or UI expansion.

## Step 49 plan

Preserve the dirty 947ddea baseline. Check only ignored authorized inputs; if absent keep qualification unexecuted. Capture isolated synthetic Excel Text/Value2/NumberFormat observations with source fingerprints and no links/macros/events. Compare literal expectations and inspector output, retain divergences, fix only proven issues, and run full regression/publish and bounded probes. Keep every mutation and private-data boundary unchanged.

Step 48 result: 17 new accounting-display cases and 357 solution tests pass. No private inputs supplied; bounded cache retained and production mutation gates unchanged. See [verification](docs/development/step48-verification.md).

## Step 48 plan

Preserve the dirty Step 46/47 tree. Inspect only explicitly supplied ignored local-test-workbooks inputs. Add an independently tested small accounting-number grammar, preserve raw/blank distinctions, retain conservative formulas and the bounded cache, run full regressions and document private-file availability and measured limits. No mutation gate, dependency or branding changes.

Step 47 implements bounded date/time and shared-formula interpretation plus measured recent-window caching. Real accounting workbooks remain unavailable for qualification; no ZIP row-location index is enabled. [Verification](docs/development/step47-verification.md) records synthetic evidence and limitations. Phase 2 remains incomplete and production Apply stays disabled.

## Step 47 implementation plan

1. Preserve the complete dirty Step 46 tree at 947ddea. Locate authorized real workbooks by filename only and retain synthetic qualification if absent. Keep source hashing, package budgets and all mutation gates intact.
2. Add bounded style/date interpretation with raw values retained, explicit 1900/1904 handling and unsupported-format warnings. Independently test literal expectations, including fictitious serial 60.
3. Add a read-only shared-formula parser for a narrow same-sheet grammar. Validate anchors/ranges/groups before reconstructing relative, absolute and mixed references; reject unsupported or inconsistent forms without evaluating caches.
4. Validate row structure and benchmark streaming navigation. Do not confuse decompressed XML positions with ZIP seek offsets; prefer a bounded recent-viewport cache if a safe useful index is not established.
5. Expose inspection cell details using existing components, run synthetic correctness/performance and desktop tests, full regression/harnesses/publish, then document measured capabilities, private-workbook availability and Step 48 blockers.

Step 46 result: separate bounded metadata inspection and a selected-sheet raw-value viewport are implemented. Full editable admission and every Step 45 review/adoption/Apply restriction remain unchanged. See [verification](docs/development/step46-verification.md). Phase 2 remains incomplete.

## Step 46 implementation plan (2026-10-29)

1. Preserve the dirty Step 45 and branding tree. Document the editable guard's binary, sheet and stored-cell limits without widening them.
2. Add a separate bounded ZIP/XML metadata inspector and read-only session. Retain a read-only source handle, validate package paths/relationships without resolving external targets, hash the source, and avoid ClosedXML. Read only selected worksheet data with cancellation, byte/element/time limits and a single viewport cache. Report unmeasured counts explicitly.
3. Expose a path-free inspection command and focused compatibility report in the existing viewer. Keep inspection sessions incapable of snapshots, copies, editing, Merge, structural review/adoption or export. Preserve production Apply and branding.
4. Exercise synthetic many-sheet, sparse, styled, shared-string, formula and adversarial packages; measure bounded performance and source hashes. Run existing regression, harnesses and publish checks. Record prototype limits and unexecuted real-workbook/manual acceptance honestly.

## Step 45 implementation plan (2026-10-29)

1. Preserve all Step 43/44 changes and immutable native reference artifacts. Inspect raw row/default/custom-height attributes and reproduce no-op saves, row insertion, explicit heights and repeated operations using new isolated Excel fixtures. Record pre-save, saved XML and reopened observations; infer no numeric equivalence from tolerance.
2. Retain exact OOXML preservation. Add an explicit native-format policy rejecting unproven heights for approval, expand independently specified Excel cases and negative controls, and strengthen oracle defect tests without widening formula grammar.
3. Expose bounded read-only candidate generation and explicit candidate-bound review approval through application/workspace/bridge/UI. Bind parent/source/copy/destination/profile/manifest/oracle and fingerprints. Mark these tickets review-only and unconditionally block their adoption/integration, independent of experimental flags. Keep production Apply disabled unless all production gates are proven.
4. Preserve separately gated internal adoption/Merge/export and normal cell operations. Add stale/replay/cancellation/tampering/formatting and actual WPF/WebView2 review tests. Run locked restores, full regression, independent harnesses, native reference verification and self-contained publish; report exact support and remaining blockers.

Step 45 result: [17 new actual Excel captures](docs/development/step45-verification.md) isolate save serialization from insertion: 22 -> 21.95 and 20 -> 20.100000000000001 occur on no-op saves despite unchanged COM observations. Exact 21-point insertion cases match the tested projection; no tolerance or automatic normalization was introduced. Real desktop candidate generation and explicit review-only approval are wired with complete fingerprints/revisions/oracle binding; all public tickets unconditionally reject adoption/integration. Production Apply stays disabled. Final checks: 266 .NET (47/208/11), 31 frontend, 13 evaluation safety, three toolchain, 600 native/390 managed checks, locked restores, normal Release build and self-contained publish pass. Source hashes and dependencies are unchanged; matrix remains 22/24/26, five advisories remain visible, real workbook untested and Phase 2 incomplete.

## Step 44 implementation plan (2026-10-29)

1. Preserve the complete dirty Step 43 tree. Define a versioned synthetic reference specification with literal expectations, negative controls and explicit hand-authored/TsekSheet/Excel/pending provenance. Generate only owned artificial artifacts; use the registered local Excel only through controlled isolated automation, with macros/events/links disabled and source hashes checked.
2. Separate candidate generation, oracle acceptance, exact candidate/review-hash approval and working-copy adoption. Bind each transition to session/source/profile/base revision and immutable package/manifest fingerprints. Preserve the eight Step 43 transaction fault stages.
3. Add an internal, separately gated structural integration transaction for one reviewed operation on a clean copy whose base package/revision exactly equals the destination. Reopen and run the oracle before atomic destination adoption; never replay structural rows as cell edits.
4. Retain a structural export proof on the integrated main session. Reopen and re-run semantic validation before the existing new-file-only NTFS commit, reject stale/diverged proof, and test the whole insertion/review/adoption/Merge/export chain and failures.
5. Expose explicit lifecycle review states in the manual Payables preview while keeping production Apply disabled. Add provenance, defective candidate, conflict, stale approval, cancellation/retry/export tests. Run all existing verification and document exact Excel evidence, limitations and unchanged research failures.

Experimental Merge/export requires separate internal capability and accepted reviewed bytes; no production bridge command enables it. Supported feature coverage does not expand beyond Step 43.

Step 44 result: [four actual Excel references and seven pending negative controls](docs/development/excel-reference-corpus.md), candidate-bound review/approval/adoption and a separately gated internal structural Merge/export chain are implemented. [Final verification](docs/development/step44-verification.md): 229 solution tests (47/173/9), including 34 new lifecycle cases and two corpus methods; 13 evaluation safety, 25 frontend, three toolchain, 600 native/390 managed checks, normal Release build and self-contained publish pass. Sources are unchanged; matrix remains 22/24/26 and five evaluation advisories remain visible. Native content/formula/compared styles agree with declared operations, but saved row heights differ (21.95 versus 22). Production Apply remains disabled, real copy.xlsx untested and Phase 2 incomplete.

## Step 43 implementation plan (2026-10-29)

1. Freeze a narrow experimental envelope before mutation: whole-package safety validation, one visible selected sheet, bounded plain rows, uniform explicit record styles, no defined names/tables/merges/filters/hidden dimensions/advanced content, and only same-sheet SUM ranges or addition of A1 references. Reject all other formulas and dependencies.
2. Add a standalone OOXML preservation oracle with an explicit insertion manifest. Independently derive expected row/cell/reference changes; compare every package part, row attribute, cell and worksheet structure. Report categorized differences and reject unverifiable packages.
3. Add an internal, capability-gated candidate experiment on isolated working copies. Re-plan from the original request under the session lock, check provenance/revision/review, serialize and reopen, evaluate the oracle, then atomically adopt. Keep structural Merge and production Apply disabled.
4. Add independent literal expectations, adversarial candidate tampering, unsupported-structure fixtures, deterministic stage failures/cancellation and source hashes. Verify actual cells and repeated/batch insertion ordering.
5. Run locked restores, Release build/full tests, evaluation and commit harnesses, frontend/toolchain checks and win-x64 publish. Record exact evidence and limitations; retain known research failures.

Production Apply is intentionally outside this milestone's enabled scope. An internal experiment must not become an unreviewed route into cell-only Merge or Save As.

Step 43 result: [independent OOXML oracle and bounded internal candidate editor](docs/development/structural-preservation-oracle.md) implemented; production Apply and structural Merge/export remain disabled. [Verification](docs/development/step43-verification.md): 193 solution tests (47/137/9), including 52 structural tests, 13 evaluation safety tests, 25 frontend tests, 3 toolchain tests, 600 native/390 managed commit checks, normal Release build and self-contained win-x64 publish passed. Source hashes are unchanged. Research matrix remains 22 passes/24 known failures/26 unexecuted. Phase 2 remains incomplete; native Excel semantics and real workbook compatibility are unverified.

## Basis and Execution Rules

This roadmap follows [AGENTS.md](AGENTS.md), [GOALS.md](GOALS.md), the [architecture overview](docs/architecture/overview.md), and accepted decisions [ADR-0001](docs/architecture/decisions/ADR-0001-desktop-framework.md), [ADR-0002](docs/architecture/decisions/ADR-0002-local-first.md), and [ADR-0003](docs/architecture/decisions/ADR-0003-generic-template-engine.md).

Phase 0 is in progress. Phases 1 through 5 are planned. Existing architecture documents record decisions, not completed implementation or passed tests. Phase completion requires evidence against its acceptance criteria; no calendar estimates are assumed before dependency evaluation.

Current implementation checkpoint (2026-10-28, new Windows PC): the checkout includes the desktop shell, bounded workbook inspection/editing, isolated full-context copies, and new-file-only Save As. [Worksheet integration](docs/development/worksheet-integration.md) adds conservative recorded-cell Merge with frozen revisions, candidate validation, synthetic regression tests and desktop review/confirmation. Replace and Create New remain disabled because reference/resource preservation is not established. Phase 2 and Phase 3 are **incomplete**; no template engine, row insertion, OCR, overwrite saving or autosave is enabled. [Step 40 migration](docs/development/dotnet-10-migration.md) moves all nine projects to .NET 10 with SDK 10.0.403, preserving Step 39's PATH-based npm discovery and bounded frontend policy. Standard Release build, 98 solution tests, independent regression harnesses, native commit checks, frontend checks and self-contained win-x64 publish pass. Source fixture hashes remain unchanged. Evaluation-only dependency-audit warnings and clean-machine/manual-dialog acceptance remain separate follow-up work.

[Step 41 security review](docs/development/dependency-security-review.md) applies an evaluation-only Xml 8.0.5 / transitive Pkcs 8.0.2 servicing fix, removing 14 of 19 distinct advisories while preserving all 72 library-matrix outcomes. Five ImageSharp advisories remain unresolved in the isolated synthetic-only NPOI experiment; audit warnings are not suppressed. Production/solution and npm audits report no findings. Full regression, native commit and publish checks pass, but TsekSheet is **not production-ready**. Next acceptance work includes a reviewed legacy-evaluation dependency strategy, real native dialogs and independent offline installation.

[Step 42 workflow foundation](docs/development/workflow-profiles.md) (2026-10-29) adds approved local JSON profiles, bounded whole-sheet section analysis, conservative entity/alias matching, explicit multi-section choices, duplicate explanations and pure batch proposals. The [manual desktop Payables workflow](docs/workflows/payables-processing.md) previews destinations and mappings only. Structural insertion remains disabled: raw library insertion still fails formula/name/table expansion and there is no independent generic structural preservation oracle. [Verification and remaining gates](docs/development/safe-row-insertion.md) record 141 passing solution tests, 13 evaluation safety tests, 25 frontend tests, 3 toolchain tests, 600 native/390 managed commit checks and successful Release build/publish. The 72-case research matrix remains 22 passed/24 known failed/26 unexecuted. This is a bounded planning foundation, not completed Phase 2/3 or release acceptance. Next: prove structural reference/range preservation and clean row templates before connecting an Apply operation.

- Prioritize workbook preservation and automated regression tests before expanding UI or OCR capabilities. Never trade integrity for feature coverage.
- Operate offline after installation. Introduce no database, cloud services, paid AI APIs, hosted assets, or remote OCR fallback.
- Keep business logic independent of WPF and WebView2. Use plain domain types, application services, and narrow adapters for workbook access, OCR, and file commit.
- Keep originals unchanged until an explicit authorized Save or Save As. In-memory integration does not authorize disk writes.
- Build small vertical slices and use fixture-backed compatibility limits. Reject unsupported operations instead of silently losing content.
- Add tests alongside each implementation phase. Record failed and unexecuted checks with their impact; passing mocks alone cannot establish workbook compatibility.

The numbered phases describe delivery milestones. Phase 1 establishes only the shell and transport; production workbook commands remain unavailable until Phase 2 integrity gates pass. Workbook-library evaluation begins in Phase 0, and core workbook and template tests run independently of the desktop interface, preserving the overview's requirement to prove core behavior before connecting it to UI commands.

## Phase 0: Repository Foundation and Architecture

Status: In progress

### Objectives

- Establish a maintainable repository and test strategy around the accepted architecture.
- Resolve the highest-risk workbook and offline-distribution assumptions before broad implementation.
- Define a realistic MVP compatibility contract rather than claiming full Excel support.

### Deliverables

- Product goals, architecture overview, accepted ADRs, and this roadmap, reviewed together for consistency.
- Minimal proposed source/test layout and documented dependency directions for host, frontend, application/domain, workbook/template components, and OCR adapter.
- Reproducible build/test instructions, dependency version policy, and local development prerequisites.
- Synthetic or sanitized workbook and scanned-document fixture inventory with expected preservation invariants.
- A bounded workbook-library evaluation covering offline use, license/redistribution, round trips, deep copies, formulas, reference updates, row insertion, and sheet import/replacement.
- Initial supported-format/feature matrix, calculation policy, and risk register. Evaluate `.xlsx` first; explicitly decide how unsupported formats and advanced content are rejected.
- Initial offline runtime/installer feasibility assessment and a minimal test harness for later phases.

### Acceptance Criteria

- Architecture boundaries and save invariants are documented; unresolved choices have explicit evaluation criteria.
- Workbook evaluation has reproducible fixture results, including unchanged formulas, formatting, hidden sheets, names, and cross-sheet references for the proposed supported scope.
- A viable library and license path is identified before Phase 2 implementation depends on it. Preservation failures narrow support or trigger another evaluation; they are not accepted as silent data loss.
- Test commands and environment requirements are reproducible. Unavailable tooling and unexecuted checks are reported.
- No architecture document is treated as implementation completion. Phase 0 remains open until its repository, evaluation, and validation deliverables are satisfied.

### Dependencies

- Authoritative requirements and accepted ADRs.
- Access to development tooling, candidate dependencies and their redistribution terms, and representative safe test fixtures.

### Major Technical Risks

- A workbook library may round-trip simple cells while damaging advanced or unrelated workbook content.
- Replacement, cross-sheet copy, or formula adjustment may exceed candidate-library capabilities.
- Offline runtime packaging or dependency licensing may constrain distribution.
- Overly broad format support or excessive project splitting may expand scope before core safety is proven.

## Phase 1: WPF + WebView2 + React Desktop Foundation

Status: Planned

### Objectives

- Establish a Windows shell with locally hosted React + TypeScript and a narrow C# command boundary.
- Keep UI plumbing separate from workbook and workflow logic.

### Deliverables

- WPF lifecycle, WebView2 initialization, local frontend assets, and native dialog adapters.
- Versioned JSON requests, responses, errors, and progress events with request IDs and session/revision fields where applicable.
- Origin and command allowlists, payload validation, controlled navigation, and no unrestricted .NET object or filesystem exposure.
- Minimal interface states for loading, errors, cancellation, and unsaved-work status, initially backed by test services.
- Bridge contract tests and a small desktop startup/shutdown smoke test.

### Acceptance Criteria

- The installed development build launches its bundled interface without network access or a local HTTP server.
- Valid test requests reach application entry points; malformed, oversized, unknown, and unauthorized-origin messages are rejected predictably.
- A long-running test operation does not block the UI, reports progress, and supports cancellation and clean shutdown.
- Application/domain tests run without WPF or WebView2. Production workbook editing/saving is not enabled before Phase 2 acceptance.

### Dependencies

- Phase 0 component boundaries, toolchain baseline, and runtime feasibility findings.
- A supported WebView2 runtime installation path; no first-run download assumption.

### Major Technical Risks

- Runtime availability, UI-thread dispatch, and host shutdown can cause startup or lifecycle failures.
- Contract drift or excessive bridge payloads can create fragile UI behavior.
- Business rules can leak into event handlers unless application entry points remain authoritative.

## Phase 2: Excel Workbook Engine and Safe Editing

Status: Planned

### Objectives

- Prove supported workbook preservation before exposing real editing through the bridge.
- Establish in-memory sessions, isolated working copies, and an explicit, safe persistence boundary.

### Deliverables

- Workbook compatibility inspection, read-only source loading, original-byte baseline/fingerprint, stable worksheet IDs, session revisions, and dirty state.
- Worksheet navigation and viewport reads that retain all unselected and hidden content.
- Typed cell-value, formula, and supported-format edits with validation and formula/protected-cell safeguards.
- Independent selected-sheet working copies backed by verified deep clones of supported workbook context, with base snapshots and change records.
- Candidate validation/adoption primitives for multi-step mutations and engine-level probes for sheet integration and row insertion needed by Phase 3.
- Explicit Save and Save As with destination authorization, candidate serialization/reopening, content-based external-change detection, safe commit/backup behavior, and no direct serialization into originals.
- Defined Save As rebinding, calculation/stale-result behavior, memory/copy limits, cancellation boundaries, and failure reporting.
- Automated workbook fixtures and file-commit failure tests; minimal real-workbook UI integration after core checks pass.

### Acceptance Criteria

- Opening, navigating, editing, copying, cancelling, and closing without saving leave original bytes unchanged.
- Working-copy mutations share no mutable cell/style objects with session sheets or other copies; supported cross-sheet context remains intact.
- Reopened saved fixtures preserve formulas, formatting, names, merged ranges, references, and unaffected workbook content within the declared support contract. Unknown preservation behavior blocks the affected operation or save.
- Save writes only to the authorized destination; Save As to a separate path preserves the source. Pending copies are never silently integrated.
- External changes, including same-timestamp content changes and destinations created during Save As, prevent silent overwrites. Test coordination near commit, not only the initial fingerprint check.
- Locked files, permission/disk errors, failed serialization/validation, and pre-commit cancellation retain unsaved state and original integrity. Commit-boundary cancellation reports the actual outcome and preserves required recovery artifacts.
- Core workbook regression tests pass before real editing commands are enabled in the desktop UI. Tests distinguish formula preservation from recalculation.

### Dependencies

- Phase 0 library evaluation, supported scope, and fixtures.
- Phase 1 bridge for UI delivery; core workbook development and testing do not depend on the host.

### Major Technical Risks

- Serialization may drop unsupported workbook parts or alter cached calculations.
- Full-context clones can exhaust memory on large workbooks.
- External writers and filesystem replacement behavior can undermine overwrite checks; unsupported safe-commit environments must fail safely.
- Excessive editing scope can turn the MVP into an attempted Excel replacement.

## Phase 3: Generic Template Engine and Worksheet Integration

Status: Planned

### Objectives

- Map verified records through reusable JSON definitions without accounting-specific engine branches.
- Deliver merge, replace, create-new, and template-driven row insertion with fixture-backed preservation guarantees.

### Deliverables

- A minimal versioned JSON format, parser, validation, field typing, deterministic mappings, and actionable diagnostics, evolving from ADR-0003 rather than an upfront universal schema.
- Separate structural selectors/bindings and simple workflow constraints; template identity/revision distinct from format version.
- Independent bindings for multiple worksheets using one template, with region boundaries, summary markers, and template row patterns resolved per worksheet.
- Reimbursement example configuration and at least one non-reimbursement fixture.
- Preview/apply flows tied to template, working-copy, and destination revisions, with per-worksheet operation results.
- Three-way merge of supported changed properties against the base snapshot, conflict rejection, safe replacement with dependency inspection, and create-new import with unique names and resource/reference resolution.
- Structural insertion plans that insert full rows before summary sections, copy formatting and applicable formulas, expand declared aggregates, and shift summary/lower content without hardcoded row numbers or worksheet names.
- Automated integration and repeated-insertion regression suites plus focused UI controls after engine tests pass.

### Acceptance Criteria

- Unknown template versions, ambiguous/missing/overlapping boundaries, invalid mappings, unsupported directives, and incompatible worksheets are rejected without mutations.
- The same template works on worksheets with different names, lengths, and summary positions; bindings and mutable workbook state remain independent.
- Merge touches only intended supported properties and rejects competing changes. Replacement preserves valid inbound dependencies or is blocked. Create-new resolves supported names/references without importing unintended context sheets.
- Repeated insertion, including after serialization/reopening, moves summaries by the cumulative row count and preserves all lower content, formatting, merges, names, tables, and affected references within the supported scope.
- Copied formulas retain relative, absolute, and mixed-reference semantics; summaries include new entries. No formula string replacement or assumption of automatic range expansion is used.
- Structural edits run on isolated candidates that are validated, serialized, and reopened before adoption. Failed validation, unsupported merges/references, stale plans, and cancellation leave prior state and original files unchanged.
- Shifted coordinates are not treated as ordinary independent merge edits. Structural plans integrate only against compatible revisions.
- Templates cannot bypass verification, formula-cell protection, or explicit saving. Use synthetic explicitly verified records before Phase 4; do not introduce an OCR-verification bypass.

### Dependencies

- Phase 2 sessions, working-copy isolation, workbook capability checks, candidate validation, and safe-save tests.
- Phase 1 transport and interface for user-facing delivery; template and integration tests remain headless.

### Major Technical Risks

- Boundary resolution and insertion across merged ranges or complex formulas can be ambiguous.
- Replacement/import can invalidate names, tables, or cross-sheet dependencies.
- Structural merges against divergent revisions can corrupt intent if treated as cell diffs.
- Template scripting, broad migration support, or multi-sheet transactions can add unnecessary complexity; retain a bounded declarative vocabulary and per-sheet scope.

## Phase 4: Offline OCR and Human Verification

Status: Planned

### Objectives

- Extract draft information from scanned receipts and PDFs locally and require human review before worksheet mapping.
- Contain OCR failures and sensitive temporary data without affecting workbook integrity.

### Deliverables

- Selected local PDF renderer/OCR tooling with redistribution review, supported languages/layouts, and packaged Python/runtime models.
- Supervised child-process adapter using controlled arguments without a shell and a versioned structured request/result contract.
- Source-linked extraction candidates with page/region information and confidence where available.
- Review UI with source pages, corrections, explicit field verification, and verification invalidation after changes.
- Job progress, limits, timeout/cancellation, malformed-result handling, and temporary-file ownership/cleanup.
- Contract tests, process-failure tests, representative recognition evaluation, and verification-gate application tests.

### Acceptance Criteria

- Receipt and scanned-PDF extraction works with networking disabled and no model downloads or remote fallback.
- Draft or invalidated OCR values cannot reach template application through either UI or direct application commands. Confidence does not count as verification.
- Python has no workbook-editing responsibility; OCR failure/cancellation leaves active workbook state and originals unchanged.
- Source evidence remains available for review. Intermediates are removed after consumers finish; rendered pages are removed when review/session use ends.
- Success, failure, cancellation, deletion failure, and abandoned jobs exercise bounded cleanup without deleting originals, active jobs, unrelated files, or save recovery backups.
- Recognition quality is measured separately from contract correctness; limitations are documented rather than hidden by automatic posting.

### Dependencies

- Phase 3 field/mapping contract and application verification policy.
- Phase 1 review interface and progress transport; Phase 2 session safety.
- Local OCR/PDF tools, representative scans, and offline redistribution rights.

### Major Technical Risks

- Scan quality and layout variation may produce incorrect or incomplete candidates.
- OCR/model distribution can increase installation size and resource use.
- Child-process cleanup, retained source pages, and cancelled jobs can leak local temporary data.
- A visually complete review screen can mask missing application-level verification enforcement.

## Phase 5: End-to-End Integration, Testing, and Packaging

Status: Planned

### Objectives

- Deliver a supported Windows installation that completes the verified-document-to-workbook workflow offline.
- Establish release evidence for integrity, safe failure, usability, and operational limits.

### Deliverables

- Complete flow: open workbook, select worksheets, create working copies, extract/review documents, map verified data, insert rows, preview merge/replace/create-new, integrate, and explicitly save.
- Offline installer with required .NET, WebView2, Python, frontend assets, OCR models, and dependency/license notices supplied through supported distribution mechanisms.
- Automated core/fixture regression runs, focused desktop end-to-end tests, and real-filesystem save/recovery fault tests.
- Resource and responsiveness measurements on representative workloads, with documented workbook/job/copy limits.
- User guidance for supported features, stale formula results, pending copies, unsaved-session loss, overwrite conflicts, recovery artifacts, and temporary-file handling.
- Release checklist and results that distinguish passed, failed, and unexecuted checks; offline installation and upgrade verification on clean Windows environments.

### Acceptance Criteria

- A clean supported Windows environment installs through the offline path and completes first launch and the full workflow without network requests or downloads.
- Reimbursement and non-reimbursement examples use the same generic engine. One template binds correctly to multiple distinct worksheets without state leakage.
- End-to-end saved workbooks reopen and satisfy preservation invariants; source fingerprints remain unchanged until authorized saving. Unsupported features fail with actionable diagnostics.
- All mandatory workbook, repeated-insertion, integration, verification, and save-safety regressions pass. Any failure affecting integrity or original-file protection blocks release; unexecuted mandatory checks do not count as acceptance.
- Exercise external modification, near-commit races, locked destinations, disk exhaustion, interrupted OCR, shutdown, cancellation, and process interruption around safe-save boundaries. Validate actual outcomes and recovery artifacts.
- Logs, frontend storage, and temporary directories show no unintended document retention. Cleanup handles abandoned jobs without removing active or unrelated data.
- Measured performance fits documented limits, UI remains responsive, and errors preserve unsaved state wherever the process remains available.

### Dependencies

- Accepted Phase 1–4 deliverables and passing integrity suites.
- Supported Windows/filesystem test environments, offline runtime distributions, and representative complete fixtures.

### Major Technical Risks

- Dependency/runtime gaps may appear only on clean offline machines.
- End-to-end flows can expose stale revisions, verification loss, or save-state mismatches absent from isolated tests.
- Filesystem behavior and process interruption may reveal recovery gaps.
- Memory and OCR costs can make larger workflows impractical; retain measured limits rather than adding hidden persistence or services.

## Deferred Scope

Defer full Excel parity, advanced interactive conflict resolution, large multi-workbook batches, multi-sheet transactions, rich template editors or scripting, persistent crash recovery, extensive template migration, real-time collaboration, and non-Windows hosts until concrete requirements justify them. No deferred feature authorizes databases, cloud services, unverified OCR posting, or background overwrites of originals.
