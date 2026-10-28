# Workbook library evaluation

Assessment date: 2026-10-28. Status: experimental evidence for Phase 0 and preparation for Phase 2. Neither phase is complete.

## Finding

Recommend **ClosedXML 0.105.1 for a bounded workbook editor, with Open XML SDK 3.5.1 for package inspection and validation**, subject to the additional acceptance gates below. ClosedXML already depends on Open XML SDK; this is one editing model with an independent validation boundary, not two competing mutation engines. Do not introduce these dependencies into production yet.

ClosedXML preserved the explicitly checked content in no-change, cell-edit, independent serialized-copy, and new-sheet experiments on both artificial fixtures. Three successive row insertions passed only after explicitly extending the declared data/table/summary ranges. Unassisted insertion, copied-sheet self-reference isolation, and delete/recreate replacement did not meet the required contract. No evaluated library is approved for unrestricted Excel editing.

This work follows [AGENTS.md](../../AGENTS.md), [GOALS.md](../../GOALS.md), [PLANS.md](../../PLANS.md), the [overview](../architecture/overview.md), and accepted [ADR-0001](../architecture/decisions/ADR-0001-desktop-framework.md), [ADR-0002](../architecture/decisions/ADR-0002-local-first.md), and [ADR-0003](../architecture/decisions/ADR-0003-generic-template-engine.md). Existing architecture, production code, solution membership, and dependency versions were not changed. No template mapping or OCR was implemented.

## Evidence and version selection

- **Source verified:** publisher documentation, license texts, NuGet package metadata, and the restored packages' `.nuspec` license declarations. A documented API is not evidence of preservation.
- **Experiment verified:** executed .NET 8 load/mutate/save/reopen probes, saved-package ZIP/XML comparisons, schema validation, and fixture hashes.
- **Recommended:** a proposed Phase 2 strategy, not an implemented or accepted compatibility guarantee.
- **Not executed:** explicit report entries for licensing gates and missing prototypes. These are not passes or proof that a library lacks the feature.

SDK: **8.0.427**, target **net8.0**, x64 Windows host. Stable package versions observed in official NuGet metadata were ClosedXML 0.105.1, EPPlus 8.7.2, NPOI 2.8.1, and DocumentFormat.OpenXml 3.5.1. NPOI runtime experiments intentionally use **2.7.6**, whose restored package declares Apache-2.0, rather than assuming the newer binary terms are suitable. No NPOI 2.8.x binary or EPPlus instance was executed.

The experiment's [project](../../tests/WorkbookLibrary.Evaluation/WorkbookLibrary.Evaluation.csproj) and [lock file](../../tests/WorkbookLibrary.Evaluation/packages.lock.json) pin the resolved graph. [Restored license inventory](evidence/restored-package-licenses.json) records package versions, declared licenses, and repository commits, including test infrastructure. The graph was restored together: ClosedXML used SixLabors.Fonts 1.0.1 as resolved with NPOI. A future production-only dependency graph must be checked separately.

## Library comparison

| Criterion | ClosedXML 0.105.1 | EPPlus 8.7.2 | NPOI 2.7.6 / current 2.8.1 | Open XML SDK 3.5.1 |
| --- | --- | --- | --- | --- |
| .NET 8 | .NET Standard assets; compiled and executed on .NET 8 | Native net8.0 assets; metadata only | net8.0 assets; 2.7.6 executed, 2.8.1 metadata only | net8.0 assets; compiled and executed |
| Commercial redistribution | MIT; retain required notices, review dependencies | Commercial license required for the intended commercial context; PolyForm Noncommercial is not a commercial grant | 2.7.6 package declares Apache-2.0; NSax dependency declares LGPL-3.0-only. Current binaries have additional maintenance-fee terms | MIT; retain notices and review dependencies |
| `.xlsx` open/save | Verified on both fixtures | Documented; not run | Verified on both fixtures with XSSF 2.7.6 | Verified with SpreadsheetDocument |
| Formula text and cross-sheet references | Checked baseline formulas preserved; shifted direct summary references followed insertion | Documented support; unverified here | Checked baseline formulas preserved; shifted direct summary references followed insertion | Preserved in untouched parts; row address edits supply no automatic formula translation |
| Formatting, merges, hidden sheets | Explicit cell formats, bold/fill, widths, row heights, merges, hidden Lookup preserved in baseline checks | Unverified | Same checked subset preserved | Same checked subset preserved |
| Names, tables, metadata | Baseline named scopes/ranges, table extents, creator/title/custom property, date system and opaque probe preserved; replacement invalidated global names | Unverified | Baseline subset preserved; replacement lost local name; adds generator metadata | Baseline subset preserved; caller owns reference/relationship mechanics |
| Sheet copy | CopyTo executes; explicit self-qualified formula still targets source sheet | API documented generally; not run | CloneSheet executes with same self-reference issue; inline-string edit also failed | No high-level copy adapter implemented; relationship/style/name copying remains custom work |
| Replacement / creation | Add works; naive copy/delete/rename is unsafe for names | Unverified | Add works; naive replacement loses scoped name | Creation prototype works; replacement unexecuted |
| Row insertion | InsertRowsAbove shifts lower cells/merges and direct references; boundary-ended ranges do not expand automatically | Publisher documents reference adjustment; boundary behavior not tested | ShiftRows/CopyRows shifts lower cells/merges/direct references; same boundary expansion gap | DOM is a low-level format API; negative control demonstrates missing reference updates |
| Performance / memory | In-memory object model; serialized candidates add memory. Use bulk range changes where possible | No local measurement | In-memory XSSF; larger dependency graph; measured only 2.7.6 | DOM loads parts; streaming reading exists, but is not a general edit/copy/insertion engine |

Framework and package sources: [ClosedXML package](https://www.nuget.org/packages/ClosedXML/0.105.1), [EPPlus package](https://www.nuget.org/packages/EPPlus/8.7.2), [NPOI 2.7.6](https://www.nuget.org/packages/NPOI/2.7.6), [NPOI 2.8.1](https://www.nuget.org/packages/NPOI/2.8.1), [Open XML SDK package](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.5.1). ClosedXML's available “latest” documentation identifies itself as 0.102.0; version-specific compilation and experiments carry more weight than that label.

### Licensing and redistribution

[ClosedXML's MIT license](https://github.com/ClosedXML/ClosedXML/blob/develop/LICENSE) and [Open XML SDK's MIT license](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) provide a straightforward commercial baseline with notice requirements. The restored version-specific metadata also declares MIT. The proposed production combination does not need NPOI, NSax, or ImageSharp. Package upgrades still require a fresh transitive license review; a repository's current license is not evidence for every released binary.

[EPPlus licensing](https://epplussoftware.com/en/LicenseOverview/) distinguishes commercial licensing from noncommercial use and describes per-developer licensing with deployment terms. [EPPlus 8 setup](https://github.com/EPPlusSoftware/EPPlus/wiki/Getting-Started) requires license configuration. No entitlement was supplied or trial activated, and no noncommercial identity was asserted. All 18 runtime matrix entries remain unexecuted. A paid local library would not itself be a paid AI API, but adds cost and contract review. Deployment, offline activation/validation behavior, and the applicable EULA would need verification before selection. No licensing outcome or compatibility advantage is inferred from unexecuted cases.

NPOI needs particular care. Its [current repository](https://github.com/nissl-lab/npoi) distinguishes Apache-licensed source from binaries starting with 2.8.0, and links an [additional binary agreement](https://github.com/nissl-lab/npoi/blob/master/OSMFEULA.txt). That agreement describes maintenance fees for specified revenue-generating use, with revenue thresholds/exemptions. This report does not determine enforceability or TsekSheet's eligibility. Do not label current NPOI binaries unconditionally free for commercial redistribution. The 2.7.6 package inspected here declares Apache-2.0, includes its license, and identifies source commit `a0f50a01a845aa4dd793cee0ac5b776745966cb6`.

The actual 2.7.6 dependency graph also contains **NSax 1.0.2, LGPL-3.0-only** according to its restored publisher metadata ([publisher repository](https://github.com/antony-liu/NSax), [license text](https://licenses.nuget.org/LGPL-3.0-only)). That is not a blanket prohibition on commercial use, but redistribution must address applicable LGPL obligations. This assessment is not a completed compliance review. Fonts 1.0.1 and ImageSharp 2.1.11 declare Apache-2.0 in the exact restored packages; do not substitute assumptions about newer SixLabors licenses. These findings further weaken the case for adding NPOI as a second production editor.

## Synthetic fixtures and safety

The independent SDK [generator](../../tests/WorkbookLibrary.Evaluation/FixtureFactory.cs) creates two artificial workbooks with no real financial or personal data:

| Fixture | Main worksheet / summary | Alternate worksheet / summary | Purpose |
| --- | --- | --- | --- |
| [reimbursement-synthetic.xlsx](../../tests/WorkbookLibrary.Evaluation/Fixtures/reimbursement-synthetic.xlsx) | Expense Draft / 10 | Second Batch / 14 | Reimbursement-like input, calculated amounts, date column and totals |
| [laboratory-synthetic.xlsx](../../tests/WorkbookLibrary.Evaluation/Fixtures/laboratory-synthetic.xlsx) | Lab Runs / 16 | Instrument B / 22 | Generic measurement log using the same structural pattern |

Each also has a hidden Lookup sheet and an Overview sheet. Data starts at row 5, followed by a summary, a merged marker three rows below it, and dependent content five rows below it. Both data sheets have a real table. Coverage includes numeric/date formats, bold/fill, explicit widths and heights, title/footer merges, global and worksheet-scoped names, cross-sheet formulas, an explicit self-reference with absolute/mixed anchors, workbook date-system metadata, core/custom properties, and an opaque custom XML payload. Artificial labels and the custom property identify the synthetic provenance.

Summary coordinates and names are fixture specifications passed to the probes, not production constants. Only the selected main sheet is changed; the differently sized alternate sheet is checked for preservation. No template JSON or mapping engine exists. Applying an insertion to both sheets in a single transaction was not tested.

Creation uses `FileMode.CreateNew`; generation refuses to overwrite an existing fixture. [SHA-256 manifest](../../tests/WorkbookLibrary.Evaluation/Fixtures/sha256.json) identifies the retained inputs. Each probe loads source bytes into its own memory streams, writes to a new GUID-scoped directory under `artifacts/workbook-evaluation`, and checks the source file hash afterward. No library save API receives a source fixture filename. All 72 matrix entries report unchanged sources.

## Experiment method and results

The isolated [evaluation project](../../tests/WorkbookLibrary.Evaluation/README.md) is deliberately outside `TsekSheet.sln` and has no production project references. Its methods are prototypes of library calls, not a workbook engine. An independent ZIP/XML inspector normalizes shared versus inline strings, simple sheet-name quoting, numeric representations and the tested style properties. It compares explicit cell content, formula text, styles, names, table extents, selected dimensions, sheet order/state, merges and metadata against an operation-specific oracle. It also runs OpenXmlValidator and reopens outputs with the candidate library.

Recorded additions of previously implicit column widths and nonconflicting generator custom properties are observations, not integrity failures. Existing explicit widths/properties are checked. ClosedXML materializes default widths of 9.140625 on sheets with no explicit columns. Their pixel-level appearance was not verified. NPOI adds Generator/Generator Version custom properties. These transformations remain visible in [raw results](evidence/compatibility-results.json); no byte-identical round-trip claim is made.

The final matrix has **72 entries: 22 passed, 24 failed, 26 not executed, 0 execution errors**. Forty-six candidate outputs were saved, reopened, and checked. Every executed output had zero SDK schema errors, including semantically failing outputs. Failure denotes a violation of this experiment's stated contract, not necessarily an implementation bug in a library or proof no safer adapter can exist.

| Operation (two fixtures each) | ClosedXML | NPOI 2.7.6 | Open XML SDK | EPPlus |
| --- | --- | --- | --- | --- |
| Open/save without intended edits | 2 pass | 2 pass | 2 pass | 2 unexecuted |
| Edit A5 and reopen | 2 pass | 2 fail | 2 pass | 2 unexecuted |
| Independent serialized deep copy | 2 pass | 2 fail on saved text | 2 pass | 2 unexecuted |
| Copy selected sheet, edit copy, rebind self-reference | 2 fail | 2 fail | 2 unexecuted | 2 unexecuted |
| Create new worksheet | 2 pass | 2 pass | 2 pass | 2 unexecuted |
| Copy/delete/rename replacement | 2 fail | 2 fail | 2 unexecuted | 2 unexecuted |
| Insert one row before summary | 2 fail | 2 fail | 2 fail, negative control | 2 unexecuted |
| Three insertions, save/reopen between each | 2 fail | 2 fail | 2 fail, negative control | 2 unexecuted |
| Three insertions with explicit declared-range repair | 2 pass | 2 unexecuted | 2 unexecuted | 2 unexecuted |
| Total | 10 pass / 8 fail | 4 pass / 12 fail / 2 unexecuted | 8 pass / 4 fail / 6 unexecuted | 18 unexecuted |

### Observed failures and boundaries

1. **Boundary expansion is application intent.** Inserting above Expense Draft row 10 moves its summary to 11, footer marker to 14, and lower dependent content to 16. Both high-level libraries adjusted the direct Overview reference to C11 and the lower formula to C11+1. They kept `SUM(B5:B9)`, the equivalent C-column sum, Overview's range sum, DataValues, and table A4:D9 ending at row 9. After three insertions these five boundaries were still unchanged. The generic fixture reproduced the same five discrepancies at a different row and sheet name. This is a required expansion policy absent from the raw call, not proof that standard Excel insertion should infer the intended data region.
2. **A bounded repair passed.** The ClosedXML prototype inserts a whole row, copies A:D from the preceding row with formula translation and height, writes only synthetic inputs, then explicitly reconstructs the two known summary formulas, the declared Overview range formula, the named data range, and the table extent. Three serialize/reopen cycles passed on each fixture, including moved footer merges, lower content, unaffected sheets and metadata. This is a fixture-scoped policy experiment. It is not a generic formula parser, complete template engine, or permission to use string replacement on arbitrary formulas.
3. **Copy isolation needs a reference policy.** CopyTo and CloneSheet produced separate cells/styles, but E5 in Working Copy still explicitly referenced Expense Draft or Lab Runs. The contract expected that explicit self-reference to bind to Working Copy. A library may intentionally preserve such a formula; TsekSheet must define and verify the intended identity transformation. Cross-sheet dependencies on other sheets must not be rebound indiscriminately. Copy alone is not the independent-working-copy integration contract.
4. **Delete/recreate is unsafe replacement.** ClosedXML turned the global DataValues and SummaryCell names into `#REF!`; recreating the original sheet name did not recover them. NPOI lost the sheet-scoped LocalInput name. NPOI also retained stale inline text. No replacement candidate was accepted. Inbound cell references, names, table identities and local scopes require explicit preservation/rebinding tests before replacement is enabled.
5. **NPOI inline-string setter failed this input form.** Its A5 setter returned the requested value in memory, but saved `<c t="inlineStr"><v>Edited synthetic value</v><is><t>Sample-1</t></is></c>`. The inspector reads the inline string selected by the cell type, so the old text remains. Style mutation in the deep-copy probe did persist and the original in-memory workbook stayed isolated, but the complete saved-edit contract failed. A recreated/shared-string-cell workaround was not tested. This finding is specific to 2.7.6 and the tested setter/input form; it is not extrapolated to all NPOI releases or cells.
6. **Schema-valid is insufficient.** The SDK negative control changes row/cell addresses only. It intentionally omits Excel reference mechanics and new-row population, while leaving names/formulas/merges/table extents wrong. The outputs pass OpenXmlValidator but fail 18 semantic checks after one insertion and 36 after three. Those four failures are expected negative controls, not a failed SDK insertion API. Custom SDK sheet copy/replacement and range expansion were not implemented.

The small opaque XML probe survived all executed outputs, including high-level saves. This does not prove preservation of arbitrary extensions, VBA, drawings, pivots, connections, signatures, or other unknown parts. Removed part names during replacement are recorded separately; a changed part URI alone is not data loss if relationships and semantics remain valid.

## Bounded performance samples

Three fresh-process samples used 10,000 main-sheet data rows, a small alternate sheet, formulas, tables and the same preservation probes. Input generation and independent inspection were excluded from timing. Timing includes candidate load, save to memory, and candidate-library reopen. [Raw samples](evidence/performance-samples.json) retain exact byte counts.

| Library | Elapsed milliseconds | Total managed allocation | Managed heap after operation | Process working set after operation |
| --- | ---: | ---: | ---: | ---: |
| ClosedXML | 2,439 | 242.75 MiB | 28.24 MiB | 123.30 MiB |
| NPOI | 1,378 | 145.94 MiB | 48.00 MiB | 126.42 MiB |
| Open XML SDK | 716 | 42.02 MiB | 37.80 MiB | 85.07 MiB |

All three preserved the checked round-trip content, validated, reopened, and left their generated source unchanged. These are single samples, not peak memory measurements or a fair speed ranking: SDK has less editing machinery, caches/JIT differ, fixture generation already used SDK, and the NPOI/SDK samples ran concurrently. They establish execution at this bounded size only. Full workbook bytes, active objects, serialized working copies and candidates coexist; Phase 2 needs limits and measured peak memory on realistic bounded sizes. Large/repeated structural edits and many concurrent copies were not benchmarked.

The [ClosedXML performance guide](https://github.com/ClosedXML/ClosedXML/wiki/Performance-Guide) explains the cost of repeated individual structural shifts and favors range operations. The [SDK large-document guide](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/how-to-parse-and-read-a-large-spreadsheet) distinguishes DOM memory from streaming reads. Neither establishes acceptable TsekSheet performance or supplies complete editing semantics. EPPlus performance was not measured.

## Recommended Phase 2 foundation

Keep one workbook-library adapter behind a plain C# boundary independent of WPF/WebView2. Begin with ClosedXML and the supported subset exercised here. Use SDK/package inspection for feature inventory, semantic preservation checks and serialization validation. Do not add NPOI as a silent fallback or splice arbitrary unknown XML back into a high-level save.

1. Open original bytes read-only, identify workbook structure/capabilities and record source identity/hash. Keep an immutable session baseline. Never give an editor the original save path.
2. Build working copies from independent serialized session snapshots, retaining dependency context and logical sheet identity. Independent serialization is the tested isolation mechanism; sharing live sheet/style objects is not approved. Worksheet-only copies and merge conflict/revision semantics remain Phase 2 work.
3. For cell edits, whitelist supported value/formula changes and compare the serialized candidate's affected and unaffected content. Treat strings as text unless an explicit formula operation is authorized.
4. For structural edits, accept a typed, validated insertion plan carrying current boundaries, row patterns and declared aggregation expansion. Use library-aware translation, rebuild only declared formula/range intent, validate all dependencies, serialize/reopen, and adopt only a fully passing candidate. Resolve boundaries again after every insertion. The experimental coordinates must not enter production.
5. Gate working-copy integration separately: merge supported cell changes against the expected revision; preserve worksheet identity for replacement where feasible; create-new requires an explicit self/cross-sheet reference policy, unique names and table/name collision handling. None of these integration implementations was proven here. Reject them until dedicated fixtures pass.
6. Explicit Save/Save As serializes a candidate to an application-owned destination-side temporary file, validates and reopens it, checks external modifications, and commits using the separately tested safe-save policy in ADR-0002. Cancellation before adoption/commit discards the candidate. Locked files, disk exhaustion, races, crash recovery and atomic commit behavior were not exercised by this in-memory library evaluation.

Reject unsupported operations with a specific reason before adopting a candidate. Rejection must preserve the prior in-memory state and original bytes. A recognized `.xlsx` extension alone is not a capability check.

### Features requiring rejection or further investigation

Until independently tested and explicitly supported, block affected edits/structural changes for unknown package features; malformed or ambiguous names/regions; ambiguous formula translations; unsupported external/3D references, INDIRECT/OFFSET-dependent addresses, structured-reference transformations, shared/array/spill formulas; formulas intersecting merged regions without a defined policy; table/name conflicts; protected or encrypted content; and worksheet bounds violations. Never refresh external links or execute macros. A supported workbook may be readable while a proposed structural operation is rejected.

The two fixtures do not cover `.xls`, `.xlsb`, macro-enabled formats, encryption, charts/images/drawings, rich text, comments, conditional formatting, validations, pivots/caches, slicers, connections, digital signatures, print areas/titles, very hidden sheets, localization, nondefault date systems, Unicode/apostrophe sheet names, errors, circular formulas, or ZIP-bomb/resource-limit handling. Their preservation, safe rejection and metadata impacts need dedicated cases. Only a subset of styles and metadata is normalized; fonts/alignment/borders/theme fidelity and page rendering remain unverified.

Formula preservation is distinct from calculation. Formula text and selected dependencies were checked; cached values and Excel-equivalent calculation were not. Mixed/absolute anchors exist in a stationary self-reference, but movement/copy of every anchor pattern is not covered. No native Excel open/repair/recalculation or visual comparison was performed. The performance fixtures do not replace adversarial or user-approved representative workbooks.

EPPlus remains an alternative only after an appropriate license and actual compatibility experiments. NPOI 2.8.x behavior remains untested. Vulnerability advisory review and a production dependency/license manifest remain release requirements. The retained .NET 8 baseline ends support November 10, 2026; a maintained release beyond that date requires the already documented upgrade.

## Reproduction and verification

Run from the repository root in Windows CMD. Package acquisition requires an approved NuGet source/cache during development; the experimental runtime itself uses no cloud service, network data, Excel installation, or database. Do not point the generator or probes at user workbooks.

```cmd
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set TESTINGPLATFORM_TELEMETRY_OPTOUT=1
dotnet --version
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet build tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --no-restore
dotnet run --project tests\WorkbookLibrary.Evaluation --no-build --no-restore
dotnet test tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --no-restore --logger trx
dotnet run --project tests\WorkbookLibrary.Evaluation --no-build --no-restore -- --benchmark ClosedXML
dotnet run --project tests\WorkbookLibrary.Evaluation --no-build --no-restore -- --benchmark NPOI
dotnet run --project tests\WorkbookLibrary.Evaluation --no-build --no-restore -- --benchmark OpenXmlSdk
dotnet build TsekSheet.sln -c Release --no-restore
dotnet test TsekSheet.sln -c Release --no-build --no-restore --logger trx
cd src\TsekSheet.Frontend
node "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js" run test -- --run
```

Committed fixtures are reused by default. `dotnet run --project tests\WorkbookLibrary.Evaluation -- --generate` is only for initial generation into a fixture directory where neither source file exists; rerunning against the retained fixtures deliberately throws. Do not delete them merely to run evaluation. Performance inputs are independently generated in fresh artifact directories.

The matrix runner returns nonzero for execution errors or source mutation. Known compatibility failures remain explicit `Failed` records even when the runner exits zero; its exit code is not a compatibility acceptance gate. Inspect totals and every difference. Unexecuted cases carry their reasons. Raw candidate outputs and TRX files remain under ignored artifact/TestResults directories; the dated JSON evidence is retained here for review. Early inspector-development runs were superseded after correcting number-format scoping and overlapping key relocation; no final library claim uses those runs.

Observed verification:

| Check | Result |
| --- | --- |
| Isolated experiment build | Success, 0 warnings / 0 errors |
| Compatibility matrix | 22 passed / 24 failed / 26 not executed / 0 errors |
| Fixture schema and source preservation | Both fixtures valid; all 72 source-hash checks unchanged |
| Harness regression tests | 13 passed / 0 failed / 0 skipped |
| Production solution Release build | Success, 0 warnings / 0 errors; frontend typecheck and production build included |
| Existing Application tests | 11 passed / 0 failed / 0 skipped |
| Existing Desktop tests | 6 passed / 0 failed / 0 skipped, including real WPF/WebView2 React loading, bridge progress and cancellation |
| Existing frontend tests | 8 passed / 0 failed / 0 skipped |
| Native Excel compatibility, visual fidelity and recalculation | Not executed |
| Production workbook integration, merge and safe-save fault injection | Not implemented or tested in this evaluation |

## Files added

- `docs/research/workbook-library-evaluation.md` and three evidence JSON files (compatibility matrix, performance samples, restored-package licenses).
- `tests/WorkbookLibrary.Evaluation/`: project and lock file; fixture generator; library probes; ZIP/XML inspector; integrity oracle; console runner/benchmark; 13 harness cases; README; two `.xlsx` sources and their SHA-256 manifest.

No existing source or architecture files were modified. Generated build/test artifacts are outside the deliverable source changes. Phase 0 and Phase 2 remain incomplete.
