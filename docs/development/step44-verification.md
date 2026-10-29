# Step 44 verification — 2026-10-29

## Starting state and preserved work

Started on `main` at `8e8c299dbdf7d5e3f6708b31c5a069d2ef7fba9b`. The complete Step 43 implementation was uncommitted: twenty modified/untracked files, including the oracle/editor/tests and documentation. Those changes were preserved and extended; no reset, clean, branch switch, commit or push occurred. Git's aggregate diff therefore includes both milestones. The Step 44 implementation plan was recorded in PLANS.md before architectural changes.

Environment: .NET SDK **10.0.403**, runtime/publish **10.0.14**, Node **24.22.1**, verified local npm **11.13.0** through the existing Step 43 `NpmCliPath` override. PATH npm remains 10.9.8 and was not upgraded globally. ClosedXML **0.105.1**, Open XML SDK **3.5.1**, WebView2 **1.0.4314.35**, React **19.3.1**, TypeScript **7.0.2**, Vite **8.3.5** and all NuGet/npm lockfiles are unchanged. The workbook-test csproj adds only a corpus-specification Content item, not dependencies.

## Actual independent reference evidence

[Reference specification and procedure](excel-reference-corpus.md) distinguish hand-authored expectations, TsekSheet-produced candidates, actual Microsoft Excel outputs and pending native references. The retained bundle contains **19 synthetic XLSX files**: 11 inputs, 4 TsekSheet candidates, 4 Excel outputs; plus index, native evidence, comparison and README.

**Excel was actually used**: Microsoft Excel 16.0 build 20326, executable **16.0.20326.20232**, hidden COM automation on isolated generated inputs only. Macros/events/link updates were disabled; inputs opened read-only; outputs used new names; source hashes were checked after closing. No private workbook was opened. The controlled instance quit, and final inspection found zero Excel processes. This was native Excel automation, not manual Excel UI acceptance.

Four cases cover insertion within a genuine vendor group, after its final record, immediately before a section subtotal, and ordered batch insertion. Twenty literal expected formulas exercise SUM coverage, shifted section/summary addresses and absolute/mixed references. Excel recalculated section totals **225/250** and summaries **425/450** as independently expected. Raw formulas before explicit aggregation adjustment are retained: boundary insertion alone does not extend the original SUM to include new rows. The procedure explicitly expands the selected section SUM and preserves its original subtotal number format.

All four match the compared content/formula/style projection. However, Excel saves row heights **21.95** where TsekSheet retains **22**. The report preserves those differences; neither the production oracle nor the supported envelope was relaxed. Only two exact equivalent number-format encodings are normalized by the reference reader. Excel also changes package metadata/style/string/theme serialization and relationships and adds a calculation chain. Package inventories are recorded, not equated with semantic identity. Therefore these are **qualified comparisons, not full structural equivalence or general Excel compatibility**.

Seven unsupported controls—merged heading, defined name, Excel table, AutoFilter, cross-sheet formula, shared formula and unknown part—remain native-reference pending and must reject in TsekSheet. The immutable specification's initial pending labels are not rewritten after capture; later native evidence and comparison records establish actual subsequent provenance.

Retained evidence: `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel/{corpus.index.json,excel-evidence.json,semantic-comparison-v3.json}` with per-artifact and implementation/procedure/verifier fingerprints. The local final version is `artifacts/step44-reference/corpus-v1-EF9BB6C07FF1`. Earlier captures remain in ignored artifacts and are not counted as final accepted references. Tests successfully seeded matching retained evidence into a newly recreated artifact directory without launching Excel, then rebuilt fresh editor candidates for oracle/literal checks. Raw source-file fingerprint changes intentionally create a new version; new versions without native evidence remain pending.

## Oracle/editor independence and defects

The Step 43 oracle algorithm, grammar, exact row-height comparison and rejection envelope are unchanged. It still reads OOXML independently of the editor's row/formula mutation logic. Shared package utilities/envelope assumptions remain explicit. New tests feed plausible defective candidates: an absolute reference incorrectly kept at the old row, a missed lower-cell shift, an incomplete section SUM and a duplicated RFP. Every defect rejects. Existing Step 43 hand-authored golden expectations, malformed packages, style changes, shared/array/spill/unknown structures and eight transaction fault stages remain.

Excel cross-checks use a separate read-only semantic projection; Excel package bytes are never treated as a production candidate or silently accepted by the normal safety policy. The actual Excel outputs contain a calculation chain that remains outside ordinary editable snapshot policy.

## Candidate review and approval contract

`WorkflowCandidateReview.cs` defines separate planning lifecycle facts, a generated `StructuralCandidateReview`, an explicit operation/template/subtotal specification, approval and structural-integration requests. The generated review contains original proposal/profile/source/session/worksheet identity, workbook/worksheet names, field values/vendor match/section choice/duplicates, row count/location, formula changes, style assumptions, rejection reasons, candidate/manifest/review SHA-256 and **independent intended copy and destination revisions**.

Generation serializes/reopens/runs the oracle but does not mutate accepted state. The server privately retains frozen candidate bytes, original bytes and serialized manifest. Returned DTOs are deep copies. Approval identifies exact candidate ID, review hash, candidate hash and expected copy revision, with explicit duplicate review when required. It cannot change mapped fields. Adoption is a separate call, reopens/revalidates the frozen candidate and checks cancellation before one atomic copy revision advance. Regeneration, altered tokens, copy edits, stale revision or profile invalidate use of the old approval. The review hash identifies the generated review snapshot and remains stable while lifecycle facts progress.

Approval is simulated explicitly by internal test calls; no production user-approval UI is connected. A candidate approval does not authorize overwriting a divergent destination: Merge independently rejects any destination/base divergence. Approved/copy-adopted/integrated/exported facts remain separate and queryable through the internal review view.

## Structural Merge eligibility

`StructuralLifecycle.cs` implements a separate internal structural integration operation. It does not extend ordinary cell Merge. It requires the internal insertion and integration capabilities, one clean-copy reviewed operation, exact original/parent/source/worksheet provenance, unchanged profile, matching approved candidate/manifest/review fingerprints, current independent copy/base/destination revisions, identical base/destination bytes and rediscovered section/subtotal/template boundaries. Post-review edits and any destination divergence reject; no rebase or implicit overwrite exists.

The approved entire workbook package is cloned, reopened and independently checked against the destination plus explicit manifest. Adoption is atomic and increments the destination once. Failed/cancelled integration retains main/copy state. Replays and a second structural integration into the same main session reject. A main-session cell edit predating copy creation is supported as part of its exact frozen base. Tests prove copy revision 0 -> 1 can correspond to destination revision 1 -> 2. Ordinary cell Merge behavior remains unchanged and still rejects structural copies.

This is a deliberately restricted internal experiment, not a production structural Merge API. Legacy repeated insertion tests remain passing, but repeated reviewed integration/rebase is unsupported.

## Structural export eligibility

Successful structural integration retains the pre-insertion bytes, exact manifest, candidate hash and integrated destination revision. Save As requires that exact accepted revision/bytes plus the internal integration capability. It performs normal full-workbook reopen/validation and reruns the oracle before the existing native commit. Post-integration edits, stale proof, disabled capability, changed candidate or failed validation block commitment.

Tests exercise the whole review -> approval -> copy adoption -> structural Merge -> new-file export chain. Reopened output passes the oracle; candidate/integrated/export hashes match. A separate earlier export excludes unmerged rows and remains unchanged. Direct copy export, source path, existing destination and stale revision remain rejected. Dirty/source-binding behavior is retained. Native staged/final SHA-256 and file identity checks establish that the committed bytes match the semantically validated candidate; no claim covers subsequent external changes after handle release.

Export remains new-file-only, local fixed NTFS, no overwrite/autosave and no original-source write. Structural working copies cannot use ordinary cell Merge/snapshot paths to escape these gates. Post-integration divergence requires a fresh workflow/session; there is no implicit proof repair.

## Desktop status

The Payables panel explicitly presents planner/preflight, generation, oracle, approval, working-copy adoption, integration and export separately. Desktop previews are always planning-only: candidate not generated, oracle not run, approval not requested and copy not adopted. Package preflight may permit an experimental construction attempt, never imply completed insertion. Frontend validation rejects fabricated generated/oracle-accepted facts in a planning response.

**Production Apply remains disabled for all operations.** There is no new mutation bridge command. Internal lifecycle success alone, four qualified native references and measured formatting discrepancies do not justify connecting production Apply. Explicit vendor/section choices, duplicate warnings, whole-sheet search and manual entry remain functional. The original profile `insertionPolicy` remains disabled; no generic rows, years, worksheet/vendor names were introduced.

## Final verification

| Check | Result |
| --- | --- |
| Locked solution and evaluation/native/managed restores | Passed; dependency locks unchanged |
| Normal .NET 10 Release solution build | Passed, **0 warnings/errors**; normal locked npm ci/typecheck/build targets, no bypass |
| Complete solution | **229 passed**, 0 failed/skipped: Application 47, Workbooks 173, Desktop 9 |
| Existing Step 43 structural coverage | **52 passed**, all eight original stages retained |
| New lifecycle coverage | **34 passed**, including each of eight new lifecycle stages with both failure/cancellation and successful retry |
| Reference corpus test methods | **2 passed**, exercising 11 classifications and four actual Excel comparisons; no pending native control counted as native success |
| Independent evaluation safety | **13 passed**, 0 failed/skipped |
| Library research matrix | **22 passed, 24 known failed, 26 unexecuted**, no execution errors; all source hashes retained |
| Frontend typecheck/build/tests | Passed; **25 tests** |
| Toolchain tests | **3 passed** |
| Native commit harness | **600 passed, 0 failed**, offsets 8/16/20 |
| Managed commit harness | **390 passed, 0 failed** |
| Self-contained Release win-x64 publish | Passed, runtime **10.0.14**, locked dependencies, `artifacts/step44-win-x64` |
| Publish inspection | Ten required app/runtime assets present, all three frontend files hash-identical; Core/Desktop runtimeconfig 10.0.14; zero XLSX test/reference artifacts shipped |
| Actual WPF/WebView2 | All nine tests passed; new lifecycle honesty assertion, manual Payables flow and existing width/source/dirty-state checks |
| Source SHA-256 | Both retained baseline fixtures match committed manifest; generated inputs checked throughout capture/lifecycle |

Fault coverage includes planning, creation/partial mapping/formula translation/serialization, oracle, approval, copy adoption, Merge construction/oracle/adoption, export construction/semantic validation and native commitment. Actual corrupt and reopenable semantically tampered candidates reject. Failed native export/cancellation leaves no destination or owned staging file and supports retry. Stage failures never advance accepted revisions or export metadata.

The five evaluation-only ImageSharp advisories remain visible: three high, two moderate. No package versions or audit warnings were suppressed. The 24 known matrix failures and 26 unexecuted cases remain limitations, not regression-suite passes.

Actual desktop tests use WPF/WebView2/React/C# with injected native selector/confirmation inputs. They are not manual native-dialog testing. Microsoft Excel was separately used through controlled hidden COM automation, not manually operated for visual fidelity. Published assets were structurally checked, not interactively smoke-tested. Clean-machine offline installation, maximum-size performance, arbitrary Excel fidelity, crash/power-loss and manual native-dialog acceptance remain unverified.

Intermediate findings were corrected without deleting negative cases: hashing an Excel output before closing found its file lock; the procedure now hashes after close. Initial comparison revealed built-in/escaped format encodings, subtotal autoformatting and measured saved row-height differences. Equivalent encodings are narrowly recognized, the declared subtotal operation preserves its original format, and heights stay reported. A short-lived Excel-process guard rejection was retried only after inspection showed Excel had exited; no process was killed. Corrupt Merge bytes initially leaked InvalidDataException; they now return a structural-validation rejection while retaining state. Final suites have no unresolved regression failures.

## Fingerprints and real workbook

| Retained original | SHA-256 |
| --- | --- |
| reimbursement-synthetic.xlsx | `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308` |
| laboratory-synthetic.xlsx | `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00` |

All 19 retained artificial corpus files have per-file hashes in the index/native evidence; the comparison binds native evidence and verifier fingerprints. Original corpus captures were preserved when creating the retained replay bundle. Generated temporary sources are checked at disposal, and native staging cleanup is asserted.

`copy.xlsx` was not found in the repository or attachments. Its actual contents/blockers were not inspected, and no real accounting workbook was tested or added. Defined names, external links and unsafe package structures remain rejected; no simplification or open-policy bypass was introduced.

## Evidence and changed files

Final solution TRX: `artifacts/step44-tests/final/*10_39_*.trx`; final build/test/publish/evaluation/native/managed logs are under `artifacts/step44-tests`. Research matrix: `artifacts/workbook-evaluation/20261029-023138-f0bef9bf/results.json`. Actual WPF capture remains `tests/TsekSheet.Desktop.Tests/bin/Release/net10.0-windows/payables-planning.png`. Native reference artifacts are retained under the source-tree fixture path described above, separate from production publish.

Step 44 created:

- `src/TsekSheet.Application/Workflows/WorkflowCandidateReview.cs`
- `src/TsekSheet.Workbooks/StructuralLifecycle.cs`
- `scripts/Verify-ExcelReferenceCorpus.ps1`
- `tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs`
- `tests/TsekSheet.Workbooks.Tests/ReferenceCorpusTests.cs`
- `tests/TsekSheet.Workbooks.Tests/StructuralLifecycleTests.cs`
- `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/corpus-v1.json`
- `tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel/`: 19 artificial XLSX files, index, native evidence, comparison and README.
- `docs/development/excel-reference-corpus.md`, `docs/development/step44-verification.md`.

Step 44 extended existing/uncommitted files:

- `src/TsekSheet.Application/Workflows/WorkflowPlanning.cs`: distinct planning lifecycle facts.
- `src/TsekSheet.Workbooks/{StructuralRowCandidate.cs,WorkbookEngine.cs,WorkbookSession.cs}`: preparation-only branch, internal capability/fault seams, export proof validation and disposal.
- `src/TsekSheet.Frontend/src/{PayablesProcessing.tsx,PayablesProcessing.test.tsx,bridge/workflows.ts}`: honest review display and strict planning-result checks.
- `tests/TsekSheet.Desktop.Tests/DesktopSmokeTests.cs`, `tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj`: actual desktop assertion and corpus Content item.
- `PLANS.md`, `README.md`, `docs/development/{structural-preservation-oracle.md,safe-row-insertion.md,worksheet-integration.md,workbook-safe-saving.md,workflow-profiles.md}`, `docs/workflows/payables-processing.md`.

Other Step 43 files remain as inherited uncommitted work. No dependency/lockfile migration, retained original modification, automatic commit/push, database, online AI service, OCR, Replace/Create New, overwrite or autosave was introduced.

## Reproduce and remaining gates

Use the locked build/test/publish commands in README and Step 43 verification, substituting `artifacts/step44-tests` and `artifacts/step44-win-x64` for output paths. Use the existing verified `NpmCliPath` override while PATH npm remains below the required range. Stop on any failure; do not run frontend consumers during build/publish npm ci. Reference-only capture/replay commands are in [excel-reference-corpus.md](excel-reference-corpus.md). No Excel install is needed to recheck the matching retained bundle; new corpus versions require fresh reference evidence or remain explicitly pending.

Step 45: establish an explicit preservation decision for the measured native row-height differences, expand independent Excel cases and adversarial manifests, then implement a separately reviewed production candidate/approval/integration UI with lifecycle cancellation and outcome handling. Keep production Apply disabled until those gates and release/real-workbook compatibility are adequately proven. The current experiment allows only one clean-copy operation and one structural integration per main session; it is not general conflict resolution or arbitrary Excel editing. Phase 2 remains incomplete.
