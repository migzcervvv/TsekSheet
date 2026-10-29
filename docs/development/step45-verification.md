# Step 45 verification — 2026-10-29

## Starting state and environment

Started on dirty `main` at `8e8c299dbdf7d5e3f6708b31c5a069d2ef7fba9b`, containing the complete uncommitted Step 43/44 work, tests and retained native reference bundle. All modified/untracked files were preserved. No reset, clean, branch switch, dependency upgrade, commit or push occurred. PLANS.md records the implementation plan before architectural changes.

Observed tools: SDK **10.0.403**, runtime/publish **10.0.14**, Node **24.22.1**, local ignored npm override **11.13.0**. PATH npm was not upgraded. Existing ClosedXML 0.105.1, Open XML SDK 3.5.1, WebView2 1.0.4314.35 and frontend/test dependencies/lockfiles remain unchanged. Excel **16.0**, build **20326**, executable **16.0.20326.20232** was actually used. Test csproj changes add only synthetic fixture Content links.

## Row-height investigation and decision

Direct inspection established different saved OOXML, not merely a ClosedXML display difference:

- Source and TsekSheet: `<row ... ht="22" customHeight="1">`.
- Excel output: `<row ... ht="21.95" customHeight="1" x14ac:dyDescent="0.25">`.
- Both retain primary worksheet `defaultRowHeight="15"`; Excel adds descent metadata to worksheet defaults and rows.

Seventeen new controlled captures isolate no-op saving from insertion. In the **no-op** cases, Excel opens the input read-only, makes no row insertion and saves a separate output. Source file hashes remain identical. COM observations before saving and after reopening still report the requested height, while the saved XML differs for 20/22:

| Input height | Saved Excel `ht` | Opened/reopened COM height | Exact saved height? |
| --- | --- | --- | --- |
| 15 | 15 | 15 | Yes |
| 18 | 18 | 18 | Yes |
| 20 | **20.100000000000001** | 20 | **No** |
| 21 | 21 | 21 | Yes |
| 22 | **21.95** | 22 | **No** |
| 22.5 | 22.5 | 22.5 | Yes |
| 24 | 24 | 24 | Yes |
| 30 | 30 | 30 | Yes |

Custom-height flags remain `1` in these captures. Fixed 21-point insertion cases independently assert exact raw height/custom-height attributes. The interior-insertion capture's fixed row-16 COM probe can become an unrelated blank row after shifting; row-identity comparisons use the full resulting workbook, not that fixed probe.

This demonstrates **Excel save serialization behavior in this environment**, independently of row insertion/template inheritance. It does not establish a universal mathematical conversion or prove equal rendering. Microsoft documents [Range.RowHeight as points](https://learn.microsoft.com/en-us/office/vba/api/excel.range.rowheight) and the [OOXML row height/custom-height attributes](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.row); those descriptions do not establish equivalence between the observed saved values. DPI/font/layout internals causing the exact conversion remain unproven. No guessed pixel/twip formula or broad tolerance was added.

**Policy:** preserve original OOXML heights, flags, defaults and styles exactly in the oracle/editor. Do not normalize 22 to 21.95 or 20 to 20.1. Public review approval is deliberately restricted to an explicitly fixed `ht="21" customHeight="1"` template, all other explicit workbook row heights also 21/custom, and the tested plain default-height configuration of 15. Other heights remain outside approval policy even if a particular native capture matched. This is a review qualification, **not production adoption eligibility**. All production Apply remains disabled.

## Expanded native reference evidence

Added 17 actual Excel outputs: eight no-op save cases and nine insertion/reference cases:

1. After a vendor's final row at fixed height 21.
2. Before an external subtotal at 21.
3. Three ordered records at 21.
4. Two sequential raw operations at 21, reopening/oracle-checking each TsekSheet candidate.
5. Subtotal insertion at 24.
6. Subtotal insertion at 30.
7. Blank-row style template at 21.
8. Non-default `"USD "#,##0.0000` currency format at 21.
9. Font size/bold, fill, border and alignment changes at 21.

Literal formulas independently cover absolute/mixed references, formulas below insertion, subtotal ends and downstream summaries. All nine insertion cases match the compared cell/formula/style/height projection; the 21-point cases also check exact raw custom-height state. No-op 20/22 differences remain explicit failures of exact height preservation, not ignored differences. New native captures call Excel recalculation, but no new general cached-value/recalculation capability is claimed; comparison focuses on formulas, values, formatting and direct XML. Step 44's four independently checked native totals remain separate evidence.

The blank-template and sequential raw references do **not** enable blank-template selection, repeated reviewed structural integration or production insertion. The internal lifecycle still supports one reviewed operation/one structural integration per main session. Unsupported controls for names, tables, merges, cross-sheet dependencies, filters and other advanced structures remain retained and rejected. Seven Step 44 controls still have no native output; none is counted as native success.

All new artifacts are retained under `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedFormatting`: 51 artificial XLSX files, immutable specification/index/capture evidence and fingerprinted comparison reports. Local capture path: `artifacts/step45-reference/formatting-v1`. Eight TsekSheet files are unchanged no-op baselines, not row-editor outputs. Combined evidence now includes 21 actual Excel outputs across Step 44/45, with explicit qualifications rather than blanket compatibility.

`scripts/Verify-ExcelFormattingReferences.ps1` accepts only owned generated corpus directories, checks provenance/hashes, refuses existing output/Excel processes, disables macros/events/link updates, opens inputs read-only, performs exact declared operations, saves new outputs, reopens for COM observations, checks source hashes, closes and quits its own instance. Final process inspection found zero remaining Excel processes. No private accounting workbook was opened. This was automated native Excel use, not manual Excel visual/dialog acceptance.

## Oracle strengthening

The formula grammar and strict unaffected-package/style comparison remain unchanged. New deliberate defects test row height, custom-height flags, worksheet defaults, wrong cell style/number-format ID, changed effective format definitions, borders/fills, duplicated vendor/RFP/invoice values and swapped batch records. These are independently specified corrupt candidates, not expected output generated by the mutator.

Step 44 references now compare fresh current-editor candidates against the retained historical Excel artifacts while retaining original capture tool fingerprints. Current editor/oracle/verifier hashes are recorded in separate v4 comparison reports; original index/captures are not rewritten. The former small-difference diagnostic bound is replaced with exact historical assertions (`22` versus `21.95`), preserving the known failure. Expanded references use exact positive matches and exact recorded failures; no numeric tolerance qualifies production operations.

Native Excel serialization still changes package parts and descent metadata. References are inspected independently and do not bypass the application's calculation-chain/advanced-content policies. Exact agreement of a limited projection is not full package/Excel compatibility certification.

## Real candidate-review and approval flow

The existing manual panel now connects to three trusted-origin, bounded bridge commands: `workbook.generateCandidate`, `workbook.approveCandidate`, `workbook.cancelCandidate`. There is **no production adopt/apply/integrate command**. Requests preserve the existing byte/depth/duplicate-key/unknown-field/path restrictions.

Select a clean isolated copy, resolve planner identity/section choices, preview, then generate. Candidate construction re-plans under original/copy locks, revalidates base/destination revisions and provenance, clones/serializes/reopens and runs the oracle. Accepted session state remains unchanged. The review shows workbook/worksheet/profile, vendor/section/field mappings, row count/location/shift extent, exact template/subtotal, formula impacts, style assumptions, oracle result, native rejection reasons, source/candidate/manifest/review fingerprints and independent intended copy/destination revisions.

Explicit approval requires a general acknowledgment and separate duplicate acknowledgment when warnings exist. The backend checks exact session/parent/source/worksheet, profile identity/hash, candidate ID, manifest/candidate/review hashes, oracle acceptance and current copy/destination/base revisions/bytes. The review fingerprint includes the oracle report. Returned DTOs are deep copies; no client field edits mutate the server candidate. Replay rejects. A destination edit before/after approval invalidates use of the old review; copy edits, regeneration, changed mappings/profile/section/identity/fingerprints likewise reject.

**Review approval records metadata only.** Public tickets are irreversibly marked `ReviewOnly`. Their adoption and structural integration explicitly reject even if internal experimental flags are changed. This is an invariant, not an experimental production switch. Existing internally generated Step 44 tickets retain their separately tested adoption/Merge/export paths; public tickets cannot enter them.

The UI distinguishes proposed/preflight from generated/oracle accepted/awaiting approval/user-approved review. It explicitly states that copy adoption, integration and export have not occurred. Nonqualified heights show a specific reason and disable approval. Final Apply is always disabled, including after approved review. Source data, accepted revision and dirty flags remain unchanged. Ordinary cell editing/Merge and separate new-file Save As retain their original semantics; an unmerged review cannot silently enter export.

Input, section/vendor choice, profile, view or copy/destination revision changes invalidate the displayed review and retract its server ticket. Cancellation discards review state; late generated/approval responses are ignored and retracted. Cancellation of an already-running read-only build is not guaranteed to preempt computation immediately; it cannot adopt workbook state. Requests wait for prior review cancellation before generating a replacement to avoid a cancel/generate race. Closing the page cancels lifetime work and releases candidates. No review is persisted in browser storage or a database.

Manual entry remains a single-record form. The bounded backend and generated-review table support ordered batches; service tests cover multiple rows and the independent native corpus covers three rows. No batch-entry editor, PDF OCR or profile import was added.

## Apply, structural Merge and export decision

**Production Apply is disabled for every operation.** Exact height evidence improved, but complete production adoption/integration/export UI, full native formatting semantics, release acceptance and real-workbook compatibility remain unproven. Approval of a review is not evidence that the operation can be applied.

Internal clean-copy structural adoption, one-operation conservative Merge and final-oracle/new-file export remain tested and separately gated. Old cell Merge cannot reinterpret structural rows as cell edits. Source/destination divergence, stale proof, failed oracle/reopen, cancellation and native faults retain prior accepted state. No automatic Merge/export/save follows approval. No existing file/source path can be overwritten; no Replace/Create New, autosave, database or online AI was introduced. Phase 2 remains incomplete.

## Final verification

| Check | Final result |
| --- | --- |
| Locked solution and three harness restores | Passed; lockfiles/dependency versions unchanged |
| Normal .NET 10 Release solution build | Passed, **0 warnings/errors**, normal frontend targets |
| Full solution | **266 passed**, 0 failed/skipped: Application 47, Workbooks 208, Desktop 11 |
| Existing Step 43/44 tests | Retained; 52 insertion/oracle and 34 internal lifecycle cases continue passing |
| New public review/oracle cases | **33 passed** |
| New expanded formatting corpus methods | **2 passed**, exercising 17 actual Excel outputs with exact qualified/blocked classifications |
| Frontend typecheck/tests/build | Passed; **31 tests**, including six new review/approval/cancellation/result-guard cases |
| Toolchain | **3 passed** |
| Independent evaluation safety | **13 passed** |
| Native / managed commit harnesses | **600 / 390 passed**, zero failures |
| Existing research matrix | **22 passed, 24 known failed, 26 unexecuted**; no execution errors; source hashes unchanged |
| Self-contained Windows x64 publish | Passed, locked runtime **10.0.14**, `artifacts/step45-win-x64` |
| Publish inspection | Ten required assets present; all three frontend files match dist hashes; Core/Desktop 10.0.14; zero XLSX fixtures shipped |
| Actual WPF/WebView2 | Eleven tests passed, including qualified 21-point review/approval and blocked 22-point review, duplicate acknowledgment, cancellation, no dirty/adoption state and 390/768/1100-width bounds |
| Source fingerprints | Retained originals and artificial input/output hashes verified |

Desktop tests run the actual WPF/WebView2/React/C# pipeline with native file-selector seams; native dialogs were not manually operated. Excel automation is reported separately above. No manual Excel visual-fidelity, clean-machine offline install, crash/power-loss or maximum-bound performance qualification is claimed. Published assets were inspected, not interactively smoke-tested.

Development failures were corrected and retained in the explanation: ambiguous XML SaveOptions qualification and a default-interface test call prevented initial compilation; a TypeScript callback narrowing issue was fixed. An initial literal assumption for saved height 20 was wrong—raw capture is `20.100000000000001`, projected as 20.1—and was replaced by exact observed assertions. No tolerance was introduced. Final suites have no unresolved regression failures. Five evaluation-only ImageSharp advisories (three high, two moderate) remain visible; none was suppressed or resolved by unrelated regression success.

Final logs/TRX: `artifacts/step45-tests`, full-suite timestamps `11_24_*`, plus `workbooks-final.trx`. Research matrix: `artifacts/workbook-evaluation/20261029-031843-1611ee68/results.json`. Native formatting evidence, original hashes and comparison records are retained in the source-tree fixture bundle; ignored capture artifacts are separate.

## Original source and real workbook boundary

| Retained original | Final SHA-256 |
| --- | --- |
| reimbursement-synthetic.xlsx | `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308` |
| laboratory-synthetic.xlsx | `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00` |

Both match the committed manifest. New captured inputs are hash-checked before/after Excel; retained output hashes bind actual native files. Public/internal lifecycle tests check generated sources through disposal and native staging cleanup. No original workbook was modified, and no workbook was automatically saved.

`copy.xlsx` was not found in the repository or attachments. It was not opened in Excel or claimed editable. Names, external links, unsupported parts and cross-sheet dependencies remain blocked; nothing was removed to force compatibility.

## Step 45 file inventory and reproduction

Created:

- `src/TsekSheet.Workbooks/CandidateReview.cs`: narrow formatting review policy and parent/copy-locked public read-only review operations.
- `tests/TsekSheet.Workbooks.Tests/CandidateReviewTests.cs`, `ExcelFormattingReferenceTests.cs`.
- `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/formatting-v1.json` and `VerifiedFormatting/` (51 synthetic XLSX, index/native evidence/comparison reports/README).
- `scripts/Verify-ExcelFormattingReferences.ps1`.
- `docs/development/step45-verification.md`.

Extended pre-existing work:

- Application `Workflows/WorkflowCandidateReview.cs`, `Workbooks/WorkbookContracts.cs`, `WorkbookWorkspace.cs`, `Foundation/BridgeSession.cs`.
- Workbooks `StructuralLifecycle.cs`, `StructuralRowCandidate.cs` (review-only construction scope; no formula grammar expansion).
- Frontend `PayablesProcessing.tsx`, its tests, `WorkbookViewer.tsx`, `bridge/{client.ts,workflows.ts}`, `styles.css`.
- `ReferenceCorpusTests.cs`: current-editor replay against immutable native captures, exact known height failure and separate verifier reports.
- Desktop smoke tests/csproj: actual review/approval cases and two synthetic fixture Content links.
- Requested development/workflow documents, PLANS.md and README.md.

All other Step 43/44 work is preserved. New binary fixtures are artificial, not private accounting data, and are excluded from production publish. No package/lockfile migration occurred.

Use the locked commands in README/Step 44 with output paths changed to `artifacts/step45-tests` and `artifacts/step45-win-x64`, retaining the existing verified NpmCliPath override. Run commands sequentially; frontend consumers must stop before build/publish npm ci. Formatting-only checks:

```powershell
dotnet test tests/TsekSheet.Workbooks.Tests -c Release --filter FullyQualifiedName~ExcelFormattingReferenceTests
dotnet test tests/TsekSheet.Workbooks.Tests -c Release --filter FullyQualifiedName~CandidateReviewTests
```

Matching retained formatting evidence can seed owned local artifacts without Excel. New capture versions must use new generated directories/indexes and the controlled procedure; never overwrite accepted references or run it on private workbooks. The mathematical height-conversion mechanism, broader source formatting, repeated reviewed integrations, source workbook compatibility and complete production mutation UI remain gates.

Step 46: establish an explicit native formatting policy beyond the narrow review subset, then design and independently verify production adoption and separately approved structural integration/export UI. Preserve the review-only invariant until a new reviewed capability replaces it; do not repurpose an internal flag as production authorization. Expand real native reference cases before enabling any mutation class. Phase 2 is incomplete.
