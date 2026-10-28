# Dependency security review and release readiness

Status: reviewed and partially remediated. **14 of 19 distinct advisories removed from the evaluation graph; 5 ImageSharp advisories remain explicitly unresolved.** Production/solution, file-commit harness and npm audits report no vulnerable packages on the queried feeds. **TsekSheet is not production-ready; Phase 2 remains incomplete.**

## Implementation plan

1. Preserve the current .NET 10 checkout and reproduce locked restore/audit results. Record advisory URLs, package versions, dependency origins and production/evaluation reachability; distinguish warning occurrences from distinct advisories.
2. Consult official NuGet, GitHub advisory and maintainer metadata for patched versions. Compare options without broad upgrades or changing framework targets. Preserve the original library matrix and known failures.
3. Apply the smallest compatible remediation, or explicitly isolate/document an unsafe experimental graph if a compatible fix cannot be demonstrated. Do not suppress audit warnings or weaken workbook guards.
4. Verify changed lockfiles and compare all matrix cases to retained baseline evidence. Run standard Release build, all solution/harness/frontend/native tests, publish/lifecycle and fixture SHA-256 checks.
5. Record local release-readiness evidence separately from manual native-dialog, clean-machine/offline and native Excel acceptance. Update only relevant documentation, retaining historical research provenance.

Baseline: SDK 10.0.403; branch main; commit c828394a580be35cb7065d5b82ac4557765e1688. All pre-existing uncommitted Merge, environment and migration work is retained. Initial sandboxed audit failed NuGet SSL authentication and is not counted as clean; the normal network/cache retry reproduced the warnings successfully.

## Baseline and evidence

Assessment date: 2026-10-28, Asia/Manila. The starting working tree contained 38 modified tracked files and 5 untracked files from earlier tasks. During the review, HEAD advanced **externally** to `a711216be151b5d6caefa9304cb10ccf546dfeb1` (Migration from .net 8 SDK to .net 10), recording the earlier work. This agent performed no reset, clean, branch change, commit or push and retained the external commit. The Step 41 security changes remain uncommitted. Frameworks remain net10.0/net10.0-windows and global.json still selects 10.0.403.

One successful evaluation locked restore initially emitted **19 warning occurrences**: **16 High (NU1903), 3 Moderate (NU1902)**. The JSON audit resolves these to **19 unique GHSA advisories and 19 unique CVEs across 3 packages**, not 19 vulnerable packages. Warnings repeat when restore/build/run is repeated; those repeated log lines are not new vulnerabilities. After remediation, a restore emits **5 occurrences**, corresponding to **5 remaining advisories/CVEs in 1 package: 3 High, 2 Moderate**.

The [retained normalized inventory](../research/evidence/dependency-security-review.json) records each advisory, CVE, exact installed version, affected range, first patched version, origin, scope, disposition and official source URL. Its source is NuGet's current advisory feed plus the [GitHub Advisory API](https://docs.github.com/en/rest/security-advisories/global-advisories). Raw before/after CLI reports, advisory responses and inspected nuspec/source files are in the ignored `artifacts/dependency-security-review` folder. This is a dated advisory-feed snapshot, not proof that a package has no unknown vulnerabilities.

## Dependency origin and exposure

| Graph | Relevant components | Findings / reachability |
| --- | --- | --- |
| Production Application/Workbooks/Desktop | ClosedXML 0.105.1, Open XML SDK/Framework 3.5.1, System.IO.Packaging 10.0.2 where not supplied by WindowsDesktop, WebView2 1.0.4314.35; runtime 10.0.14 | No vulnerable-package records reported. NPOI/ImageSharp and the old cryptography package versions are absent from the production publish. |
| Solution test projects | MSTest 4.5.2, Microsoft.NET.Test.Sdk 18.11.0 and their transitive graph | No reported advisories; test/development dependencies are not production processing dependencies. |
| Independent WorkbookLibrary.Evaluation | NPOI 2.7.6 → ImageSharp 2.1.11; NPOI → Cryptography.Xml 8.0.2 → Cryptography.Pkcs 8.0.1 before this change | All 19 initial findings originate here. The project is outside TsekSheet.sln and has no production project reference. After the patch only ImageSharp findings remain. |
| Independent native/managed file-commit harnesses | .NET runtime/native Windows APIs, no third-party PackageReferences | No reported advisories. Experiments use unique synthetic temporary files. |
| Frontend runtime and build/test graph | Locked React/Vite/Vitest/tooling graph | npm audit reports 0 vulnerabilities; no npm dependency was changed. |

The evaluation runner loads the two retained synthetic fixtures, verifies their manifest hashes, and writes candidates into unique artifact directories. Benchmarks generate their own artificial input. Neither the production UI nor an application project invokes this runner. Do **not** use it as an arbitrary-workbook or image converter. This project boundary is dependency isolation, not an OS sandbox or a guarantee against malicious local input substitution.

### Reachability categories used below

- **X** — EncryptedXml, SignedXml, canonicalization or KeyInfoX509Data processing. The current probes do not explicitly invoke XML signing, encryption/decryption or signature-validation APIs. Package loading can contain other internal paths; absence of an explicit call is not a full proof of unreachability. Both affected cryptography packages are patched regardless.
- **C1/C2** — SignedCms.CheckSignature / EnvelopedCms.Decode respectively. No CMS signature/envelope operations are requested by the probes or production application. The transitive package is patched regardless.
- **I1/I5** — ICC parsing / BigTIFF decoding. NPOI's pinned [ImageUtils source](https://raw.githubusercontent.com/nissl-lab/npoi/a0f50a01a845aa4dd793cee0ac5b776745966cb6/main/SS/Util/ImageUtils.cs) calls Image.Load for image-dimension helpers. These paths are potentially reachable if image inputs/features are introduced; the retained fixtures have no drawing/image parts and the probes do not call image helpers. No claim of complete library-level unreachability is made.
- **I2/I3/I4** — HistogramEqualization / TIFF T4 encoder / TIFF T6 encoder. The evaluation probes do not request these processors or encoders. They are still vulnerable installed code and must not be treated as fixed merely because a cell-only matrix passes.

The production package guard rejects detected drawing/advanced/external/signed content where required by its existing compatibility rules. That guard does not wrap the independent NPOI experiment, and is not used to dismiss its dependency findings. No malicious images, exploit payloads or real user workbooks were used in this review.

## Distinct vulnerability inventory

Ranges below are the branch relevant to the installed version. The official advisory links and JSON evidence also contain other supported branches. **All rows affect WorkbookLibrary.Evaluation only**, through the origins above; none is a reported production/solution-test advisory.

| Package at baseline | Advisory / CVE | Severity | Relevant affected range → first patched | Reachability | Disposition |
| --- | --- | --- | --- | --- | --- |
| ImageSharp 2.1.11 | [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44), CVE-2026-106114 | Moderate | >=1.0.0-beta0001, <3.2.0 → 3.2.0 | I1: unvalidated ICC allocation | Unresolved |
| ImageSharp 2.1.11 | [GHSA-j3p4-wp97-rph4](https://github.com/advisories/GHSA-j3p4-wp97-rph4), CVE-2026-106113 | High | >=2.0.0, <3.2.0 → 3.2.0 | I2: histogram index | Unresolved |
| ImageSharp 2.1.11 | [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q), CVE-2026-106110 | High | >=2.0.0, <3.2.0 → 3.2.0 | I3: T4 buffer writes | Unresolved |
| ImageSharp 2.1.11 | [GHSA-jjfr-hcj7-qf5w](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w), CVE-2026-106115 | High | >=2.1.0, <3.2.0 → 3.2.0 | I4: T6 buffer writes | Unresolved |
| ImageSharp 2.1.11 | [GHSA-wmxv-xphr-5c9g](https://github.com/advisories/GHSA-wmxv-xphr-5c9g), CVE-2026-106116 | Moderate | >=2.0.0, <3.2.0 → 3.2.0 | I5: decoder non-progress | Unresolved |
| Cryptography.Pkcs 8.0.1 | [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx), CVE-2026-84008 | High | >=8.0.0, <=8.0.1 → 8.0.2 | C1: repeated certificate parsing | Patched to 8.0.2 |
| Cryptography.Pkcs 8.0.1 | [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), CVE-2026-70162 | Moderate | >=8.0.0, <=8.0.1 → 8.0.2 | C2: CMS attribute CPU use | Patched to 8.0.2 |
| Cryptography.Xml 8.0.2 | [GHSA-37gx-xxp4-5rgx](https://github.com/advisories/GHSA-37gx-xxp4-5rgx), CVE-2026-33116 | High | >=8.0.0, <=8.0.2 → 8.0.3 | X: EncryptedXml loop | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-w3x6-4m5h-cxqf](https://github.com/advisories/GHSA-w3x6-4m5h-cxqf), CVE-2026-26171 | High | >=8.0.0, <=8.0.2 → 8.0.3 | X: resource consumption | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-6588-8gv4-xfgh](https://github.com/advisories/GHSA-6588-8gv4-xfgh), CVE-2026-32203 | High | >=8.0.0, <=8.0.2 → 8.0.3 | X: buffer/resource handling | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-cvvh-rhrc-wg4q](https://github.com/advisories/GHSA-cvvh-rhrc-wg4q), CVE-2026-47302 | High | >=8.0.0, <=8.0.3 → 8.0.4 | X: XML encryption parsing | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-g8r8-53c2-pm3f](https://github.com/advisories/GHSA-g8r8-53c2-pm3f), CVE-2026-47304 | High | >=8.0.0, <=8.0.3 → 8.0.4 | X: encryption protection bypass | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-23rf-6693-g89p](https://github.com/advisories/GHSA-23rf-6693-g89p), CVE-2026-50648 | High | >=8.0.0, <=8.0.3 → 8.0.4 | X: resource consumption | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-8q5v-6pqq-x66h](https://github.com/advisories/GHSA-8q5v-6pqq-x66h), CVE-2026-50525 | High | >=8.0.0, <=8.0.3 → 8.0.4 | X: resource consumption | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-mmjf-rqrv-855v](https://github.com/advisories/GHSA-mmjf-rqrv-855v), CVE-2026-50527 | High | >=8.0.0, <=8.0.3 → 8.0.4 | X: encrypted-data validation | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-r955-8q5j-3h83](https://github.com/advisories/GHSA-r955-8q5j-3h83), CVE-2026-96958 | High | >=8.0.0, <=8.0.4 → 8.0.5 | X: signed-XML limits | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-7hqp-7c5m-4vq6](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6), CVE-2026-96960 | High | >=8.0.0, <=8.0.4 → 8.0.5 | X: signed-XML limits | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-c399-9853-6jmj](https://github.com/advisories/GHSA-c399-9853-6jmj), CVE-2026-96959 | High | >=8.0.0, <=8.0.4 → 8.0.5 | X: signed-XML limits | Patched to 8.0.5 |
| Cryptography.Xml 8.0.2 | [GHSA-vrv5-2jg9-4mmr](https://github.com/advisories/GHSA-vrv5-2jg9-4mmr), CVE-2026-96222 | High | >=8.0.0, <=8.0.4 → 8.0.5 | X: certificate-format spoofing | Patched to 8.0.5 |

## Remediation decisions and compatibility

Added one evaluation-only direct reference: **System.Security.Cryptography.Xml 8.0.5**. The [official package metadata](https://www.nuget.org/packages/System.Security.Cryptography.Xml/8.0.5) requires **Cryptography.Pkcs 8.0.2**, so the latter updates transitively. This stays within the previous major version and removes all 14 reported cryptography advisories: 13 High, 1 Moderate. It does not change ClosedXML, Open XML SDK, System.IO.Packaging, NPOI, .NET targets, production references or npm dependencies. Only this evaluation project's lockfile changed for the security remediation. Both ordinary and win-x64 cryptography entries are patched.

After regeneration with `--force-evaluate`, all solution and independent harness restores pass in **locked mode**. Evaluation output continues showing the five ImageSharp warnings. No NuGetAudit disabling, NoWarn, audit exclusion, severity downgrading or compatibility/test relaxation was added.

### Why ImageSharp remains unresolved

The advisory metadata identifies 3.2.0 as the nearest patched branch for all five findings (4.1.2 also patches the newer 4.x branch). There is no listed patched 2.x version. The alternatives considered were:

| Option | Decision |
| --- | --- |
| Override ImageSharp with 3.2.0 | Not adopted as a drop-in fix. It crosses a major version outside NPOI 2.7.6's published graph and changes from the retained 2.x package terms to the [Six Labors Split License](https://raw.githubusercontent.com/SixLabors/ImageSharp/v3.2.0/LICENSE). License eligibility/distribution and image-feature compatibility have not been established for TsekSheet. No specific binary failure is claimed; passing current cell-only fixtures would not qualify image handling. |
| Upgrade NPOI to 2.8.1 | Not adopted. Official nuspec replaces ImageSharp with SkiaSharp/native assets and changes the cryptography branch; the [maintainer's binary-release EULA policy](https://github.com/nissl-lab/npoi) changes starting with 2.8.0. This is a new evaluation/library/distribution qualification task, not a small servicing fix. |
| Remove or hide NPOI to get a clean default audit | Not done. It would prevent reproducing its 18 matrix entries or conceal the remaining installed risk. Historical results, NPOI's exact version, the matrix and all known failures remain intact and visible. |
| Retain explicit findings within the existing isolated synthetic-only project | Adopted for this step. Remaining installed code is vulnerable; this is a documented unresolved risk, not a clean bill of health or permanent acceptance. Do not use it on external workbooks/images. |

The production app has no NPOI dependency. Published deps.json and files were checked: no NPOI, no ImageSharp, and no Cryptography.Xml/Pkcs **8.x package entries**. WindowsDesktop can supply newer cryptography assemblies; their mere presence is not evidence of shipping the vulnerable evaluation versions. No general claim of production security follows from the absence of these particular findings.

## Verification and preserved research

| Check | Final result |
| --- | --- |
| Locked NuGet restores | Solution and all 3 independent harnesses pass; evaluation retains 5 explicit warnings |
| Standard Release solution build | Pass, **0 warnings / 0 errors**, SDK 10.0.403, normal frontend build |
| Full solution tests | **98 passed / 0 failed / 0 skipped**: Application 16, Workbooks 74, Desktop 8 |
| Workbook evaluation harness tests | **13 passed / 0 failed / 0 skipped** |
| Workbook library matrix | **22 passed, 24 known failures, 26 unexecuted, 0 execution errors** |
| Native Windows commit harness | **600 passed / 0 failed**, offsets 8/16/20; includes 200 expected unsupported-relative-mode rejections |
| Managed Windows commit harness | **390 passed / 0 failed** |
| Frontend typecheck/tests/build | Pass, **19 tests passed** |
| Toolchain tests | **3 passed**, no failures/skips |
| Self-contained win-x64 publish | Pass, locked restore/runtime 10.0.14; `artifacts/step41-win-x64` |
| Publish inventory | Runtimeconfig net10.0/Core+Desktop 10.0.14, required assets and frontend hashes checked; vulnerable evaluation graph absent |
| Published process lifecycle | Input-idle, responsive TsekSheet window, normal close, exit code 0 |
| Final NuGet audit | Solution and both commit harnesses: 0 findings; evaluation: **5 advisories, 1 package** |
| npm audit | 0 vulnerabilities; dependency graph unchanged |
| Retained source fixtures | Both match committed SHA-256 manifest |

The new matrix is `artifacts/workbook-evaluation/20261028-155047-a6254b30/results.json`. All **72** Status, Differences and SchemaErrors fields match the pre-remediation Step 40 report `20261028-153128-c999a240/results.json`; all 72 SourceUnchanged flags are true. Neither the oracles nor expected failures were adjusted. Original retained research evidence under docs/research/evidence remains unchanged; the security inventory is an additional evidence file. Evaluation still runs NPOI 2.7.6, now with the explicit servicing override documented here.

TRX results end in `2026-10-28_23_51_45_net10.0` (Application), `_23_51_47_net10.0` (Workbooks), `_23_51_51_net10.0` (Desktop), and `_23_50_44_net10.0` (evaluation harness). Test execution identifies .NETCoreApp v10.0. No new failing test was converted into a passing expectation. The initial sandbox network failure is an environmental failure, not a successful audit.

Original hashes after tests and publishing:

- Reimbursement: `876A0A0F9E9BE0D5832B371F0F9197CC2C7AE2F3DA139986251051F717AA8308`.
- Laboratory: `D51D1C148B6D1AA26C219B6ECEA49108B0DDB8BFB799CAD37A922BEE91BB6D00`.

## Release-readiness checklist

The current-PC checks below use the existing synthetic fixtures only. They do not constitute independent-machine acceptance.

| Acceptance area | Evidence / remaining gap |
| --- | --- |
| Standard .NET 10 build/test/publish | Passed locally, without diagnostic build overrides |
| WPF/WebView2 startup and bridge | Real controls/browser exercised by 8 passing desktop tests |
| Workbook open/navigation and hidden sheets | Passed in the real desktop suite using native selection seams |
| Working-copy edits and formula protection | Passed, including independent source/copy views and errors |
| Merge preview/cancellation/confirmation | Passed; original session view refreshes; original file and earlier export stay unchanged |
| New-file Save As / existing-name rejection / cancelled selection | Passed with real native commit and injected destination selector; filesystem races and failure/cancellation tests pass |
| Unsaved-work confirmation | In-page confirmation exercised; native close decision uses an injected confirmation seam |
| Published executable | Responsive startup and normal close verified separately; this process-only check does not assert its browser handshake |
| Actual native Open/Save dialog, keyboard/focus, shutdown with dialog open | **Not manually exercised**. Real WPF tests use selection/confirmation seams. |
| Clean-machine offline installation/first run | **Not executed on an independent environment**; current package acquisition used approved network/cache access |
| Native Excel repair/recalculation/visual fidelity and representative workbooks | **Not executed**; only synthetic preservation scope is qualified |
| Dependency/distribution security and licenses | 5 evaluation-only advisories remain; complete redistribution/license, runtime servicing and installer review still open |
| Crash/power loss, real disk exhaustion and unsupported filesystems | Earlier fault tests/policy limits remain; these physical acceptance cases are unexecuted |
| Phase 2/product release | **Incomplete / not production-ready** |

The existing source-byte baseline, package/candidate validation, typed edits, formula/reference guards, working-copy isolation, strict Merge conflicts/adoption, Save As exclusion of pending copies, dirty states and no-overwrite/cancellation guarantees are unchanged. Replace, Create New, structural insertion, templates, OCR, existing-file replacement, autosave, database/cloud functionality remain disabled/out of scope.

## Reproduction and changed files

Run the normal build/regression/publish commands in [the migration report](dotnet-10-migration.md). Security checks, from the repository root in Windows CMD, are:

```cmd
dotnet restore TsekSheet.sln --locked-mode
dotnet restore tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj --locked-mode
dotnet list TsekSheet.sln package --vulnerable --include-transitive --format json --no-restore
dotnet list tests\WorkbookLibrary.Evaluation\WorkbookLibrary.Evaluation.csproj package --vulnerable --include-transitive --format json --no-restore
dotnet list tests\WindowsFileCommit.Evaluation\WindowsFileCommit.Evaluation.csproj package --vulnerable --include-transitive --format json --no-restore
dotnet list tests\WindowsFileCommit.NativeEvaluation\WindowsFileCommit.NativeEvaluation.csproj package --vulnerable --include-transitive --format json --no-restore
pushd src\TsekSheet.Frontend
call npm.cmd audit --json
popd
```

Inspect command exit codes and errors before interpreting an empty findings list. Do not disable audit, use `--ignore-failed-sources`, or run automatic broad fixes to obtain a clean-looking result. Expect the five explicitly unresolved ImageSharp advisories in the evaluation graph. Re-audit after future dependency or advisory-feed changes.

New: this document and `docs/research/evidence/dependency-security-review.json`.

Modified for Step 41: `tests/WorkbookLibrary.Evaluation/WorkbookLibrary.Evaluation.csproj`, its `packages.lock.json` and README; root README/PLANS; current toolchain and migration documentation. The project change is one security override; the lock changes only Xml/Pkcs types/versions/dependency metadata/hashes, not other library versions or targets. The prior Merge/environment/migration code is preserved in the externally advanced HEAD. No original research evidence, source fixture, safety implementation, test logic, npm file, SDK pin or production dependency changed.

Recommended next milestone: separately retire or requalify/isolate the legacy image-dependent NPOI research graph with a reviewed license and image-feature policy, then perform real native-dialog and genuinely independent offline installation acceptance. Do not expand spreadsheet functionality or declare Phase 2 complete on the strength of this audit.
