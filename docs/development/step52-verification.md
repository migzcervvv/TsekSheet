# Step 52 verification

Date: 2026-10-30 (Asia/Manila).

## Outcome

The Git portability repair restores exact reference provenance without changing accepted hashes or disabling verification. **All 359 solution tests pass, with zero failures/skips.** Both the previously failing formatting provenance test and previously skipped native semantic agreement test pass. An isolated local Windows checkout with `core.autocrlf=true` independently passes all four reference/provenance tests without remediation.

**Real Payables qualification remains UNEXECUTED.** The authorized `local-test-workbooks/` directory is absent. No unrelated folders were searched. No private workbook was opened, uploaded, repaired, saved, or passed to Excel automation. Private before/after hashing and a private diagnostic report are therefore unexecuted, not passed.

## Baseline, scope and plan

Repository `C:\Users\migzc\Desktop\TsekSheet`; branch `main`, HEAD `de641d53eb87bb6553f2132a42463d04b1ca9796`; origin fetch/push `https://github.com/migzcervvv/TsekSheet.git`. The six Step 51 documentation changes were present and preserved. Starting status is retained under ignored `artifacts/step52/starting-status.txt`. No project checkout/reset/clean, staging of user work, commit or push occurred. Git index operations described below affected isolated test repositories only.

Reviewed AGENTS.md, GOALS.md, PLANS.md, README.md, architecture overview/ADRs, Step 50/51 evidence and the requested reference, display, compatibility, structural-safety and Payables documents. Plan: distinguish historical byte conventions, apply narrow attributes, preflight and back up only unchanged allowlisted inputs, prove both checkout conditions and native eligibility, run regressions/publish, then document the absent-input gate. No mutation architecture or new financial feature was implemented.

Verified current SDK **10.0.403**, Core/WindowsDesktop runtime **10.0.14**, Node **24.16.0**, npm **11.13.0**, Git **2.47.1.windows.1**. Project targets/dependency graphs remain .NET 10/net10.0-windows, WPF x64 and the existing locked win-x64 graph. `global.json`, NuGet/npm locks, React/TypeScript/Vite/WebView2 versions and branding are unchanged. The existing Windows/WebView2 environment executes all 13 desktop tests.

## Root cause and exact repair

`core.autocrlf=true` converted LF Git blobs into CRLF working files. The generator compares **raw** source hashes to historical indexes, so equivalent text was not equivalent provenance. This also prevented native-reference seeding: the specification, fixture-generator and capture-procedure hashes failed the exact admission predicate, leaving the new fingerprint-specific artifact directory without native evidence.

There are two historical conventions, and a global LF rule would be wrong:

| Material | Required working bytes | Evidence |
| --- | --- | --- |
| Specifications, fixture generator and capture procedures | LF | Historical source SHA-256 matches the committed LF blob. Formatting specification is exactly `2C148FB71178662CFFF4CEAE961B1EABD114626984AFED638C0C6C4AB42A86E7`. |
| Replay editor/oracle and verifier identities | LF | These source hashes identify current replay independently of historical capture. |
| Retained capture/index/comparison JSON | CRLF | Native evidence binds exact index bytes: Step 44 `078CA5B5053A0A00BEEF5F2BF3696E647A9B119FF74CB83CADC1BAEC68275371`, Step 45 `D11DE99CFED0AA3BAF183F9C0AE2786502CF7641CCAA1E2FFFC366F463CA7427`. Their normalized LF blobs have different hashes. |
| XLSX packages | Binary | Never subject to text conversion. |

The new `.gitattributes` declares LF for nine explicit provenance/replay source paths, CRLF for JSON in the four retained corpus subdirectories, and binary treatment for reference-corpus XLSX packages. There is no repository-wide text rule; unrelated source files keep their existing checkout behavior. This is deterministic checkout policy, **not verifier-side normalization**. Exact-hash comparisons and native observations remain unchanged.

`scripts/Repair-ReferenceCheckout.mjs` provides a read-only preview by default and an explicit `--apply` procedure for an existing checkout. It preflights all nine inputs before writing: staged bytes must match HEAD, working bytes must equal the LF Git blob or its exact CRLF checkout form, and the relevant original source hashes and native index bindings must verify. User changes, mixed endings, BOM changes, unknown history, redirected paths and altered native indexes reject. It never normalizes arbitrary evidence.

In this checkout, nine clean source inputs needed CRLF-to-LF remediation. The applied bytes are the original Git blobs; there are no source logic/test expectation changes. Original bytes and before/after SHA-256 were retained under ignored `artifacts/reference-checkout-8a9de6e6-e9e3-451a-9f78-b4fcc358a1be/`; the plan/result are also logged under `artifacts/step52/`. Existing manifests and XLSX outputs were never rewritten. A second preview reports no remaining changes. No destructive Git restore/reset or broad normalization was used.

## Native evidence restoration

The corrected checkout reproduces the exact Step 44 specification, fixture-generator and procedure fingerprints. Existing generator code then seeds the **existing verified retained bundle** into its new ignored fingerprint-specific directory. No manifest was invented and no Excel automation/capture was needed.

Verification checks both native index hashes, source procedure bindings, original input/candidate hashes for every indexed case, all **four Step 44 and 17 Step 45 actual Excel output hashes**, and original spec hashes. The independent .NET test builds current-editor candidates and compares their semantic projection to those native outputs. Content/formula/compared styles agree within the existing explicit projection; historical row-height discrepancies remain exactly asserted. This is not full package equivalence or production insertion certification.

The historical editor fingerprint is intentionally different from the current replay editor. That existing distinction is preserved: original metadata stays unchanged, while fresh candidates go through the current oracle and literal expectations and the comparison report records current implementation fingerprints. No current implementation was relabeled as the capture tool. The oracle's historical source hash and original generator/procedure hashes were verified; the original editor was not regenerated or presented as current code.

## Automated verification

All final logs/TRX are under ignored `artifacts/step52/`.

| Check | Result |
| --- | --- |
| Locked solution/evaluation/native/managed NuGet restores | Passed, `--locked-mode --ignore-failed-sources`; existing user caches |
| .NET 10 Release build | Passed, zero warnings/errors; normal frontend npm ci/typecheck/build targets executed |
| Full solution tests | **359 passed / 0 failed / 0 skipped**: Application 47, Workbooks 299, Desktop 13 |
| Focused reference/provenance tests | **4 passed / 0 failed / 0 skipped**, including both Step 51 problem tests |
| New checkout/remediation/evidence tests | **5 passed** using `node --test scripts/reference-portability.node-tests.mjs` |
| Independent fresh local Windows checkout | **4 reference/provenance tests passed**, zero skipped; Windows autocrlf enabled, no repair step |
| Frontend typecheck / production build | Passed |
| Frontend / toolchain tests | **35 / 3 passed** |
| Evaluation safety tests | **13 passed**, synthetic only |
| Native / managed commit harnesses | **600 / 390 passed**, zero failed |
| Self-contained win-x64 publish | Passed to `artifacts/step52-win-x64`, runtime 10.0.14 |
| Actual WPF/WebView2 smoke | All **13 Desktop tests** passed; native picker/confirmation seams, not manual dialogs |
| Existing fixture/logo/lockfile SHA-256 checks | **88 checked, zero changed** |
| Retained corpus material excluding two remediated specs | **86 files checked, zero changed**, including capture/index/comparison JSON and XLSX evidence |
| Real-file source hash and qualification | **UNEXECUTED**, no authorized workbook |

The five new tests use literal independently specified LF/CRLF/binary byte expectations, verify that unrelated text still follows ordinary Git settings, replay all actual corpus files through isolated indexes under autocrlf true/false, verify native bindings/outputs, and prove that staged/unstaged user modifications prevent all remediation writes. They require Node and Git, with no added packages. Isolated repositories are retained in temporary directories for diagnosis and make no commits.

For the additional actual local clone, `git clone --no-hardlinks --no-checkout` created a new ignored directory; its index was loaded from HEAD. Since Step 52 is uncommitted, only the proposed `.gitattributes` was copied/staged **inside that clone** before `checkout-index`. Autocrlf was true. Locked restore and all four reference tests passed from freshly materialized project files. Neither a source repair nor a copied artifact/evidence directory was used: the unchanged tests seeded the retained tracked bundle themselves. This verifies the proposed policy before requiring a user commit.

Development attempts are retained honestly: the first sandboxed Git tests could not create temporary `.git/config`; normal user access resolved that environment issue. One new test initially attempted to check out an already-existing unrelated control file; restricting the second checkout to the intended paths fixed the test setup. The first no-checkout clone restore failed because its index was initially empty; loading HEAD into that isolated index fixed setup. All final checks above passed; none of these attempts changed accepted evidence or verification expectations. Python's Windows app-execution alias was unavailable during inventory; no Python tool was needed for this repair.

## Reproducible Windows checkout procedure

Once the user chooses to commit/distribute this policy, test a separate clone of that revision. Do not run a broad checkout/reset against an existing working tree. Example PowerShell (replace the source and destination placeholders with a local repository containing Step 52 and a new empty path):

```powershell
git clone -c core.autocrlf=true -- '<local repository containing Step 52>' '<new test clone>'
Set-Location '<new test clone>'
git config --get core.autocrlf
git ls-files --eol -- tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/corpus-v1.json tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel/corpus.index.json
node scripts/Repair-ReferenceCheckout.mjs
node --test scripts/reference-portability.node-tests.mjs
dotnet restore tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj --locked-mode --ignore-failed-sources
dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ReferenceCorpusTests|FullyQualifiedName~ExcelFormattingReferenceTests'
```

Expected: specification `w/lf`, retained index `w/crlf`, preview has nine `changed: false` entries, five Node tests pass, four .NET tests pass with no skips. Repeat in another new clone with `core.autocrlf=false`: explicitly attributed files must have the same bytes. No Excel installation or automation is required for these retained-evidence comparisons.

For an existing unchanged Windows checkout needing the same scoped repair:

```powershell
node scripts/Repair-ReferenceCheckout.mjs
# Review the nine-file plan; --apply refuses staged/content modifications.
node scripts/Repair-ReferenceCheckout.mjs --apply
```

The helper repairs only its explicit LF source allowlist. It deliberately refuses altered retained index bytes. If an existing checkout has LF native JSON because it predates the CRLF attributes, do not normalize it during verification: preserve modifications, compare Git/history and the native index binding, then materialize those exact attributed files separately. Prefer the new-clone procedure for qualification. No accepted hash should be edited to accommodate a checkout.

## Real workbook, restrictions and next step

`git check-ignore -v` confirms the designated directory/file and private artifacts would be ignored, but no source file exists to validate. Actual sheet counts/visibility, cells, dimensions, names/broken references, external/embedded parts, relationships, formula patterns, display coverage, headers, section bounds, vendor matches and subtotal dependencies are all **UNEXECUTED**. Each real-file capability—metadata, viewing, display interpretation, section/vendor analysis, editable sessions, copies, ordinary edits, insertion, structural Merge and export—remains unexecuted. Inspection mutation and production insertion remain blocked by existing policy regardless of prospective viewing success.

To supply the input, create `local-test-workbooks/` in this repository and place an authorized regular duplicate named `copy.xlsx` there using File Explorer. Keep the original elsewhere; do not upload/add the duplicate to Git or use linked/redirected paths. Follow the [Step 51 supply and path/hash procedure](step51-verification.md#supplying-the-missing-input). Explicit source/ancestor containment, ignore checks, bounded read-only inspection and private before/after hashing are still required at qualification time. No private data or private fingerprint is in tracked documentation.

The [strategy](real-workbook-compatibility-strategy.md) remains conditional: measure before extending resource limits; consider targeted OOXML edits inside an independently validated transaction only for proven dependency scope; consider a separate reviewed output when preservation cannot be established. No approach is selected or certified without actual workbook evidence.

Added `.gitattributes`, the explicit remediation script, five portability/safety tests and this report. Updated the reference-corpus, compatibility strategy/analyzer, Payables, PLANS and README documentation. Nine existing source/spec working files changed byte endings only and still match their HEAD blobs; no production/test logic, historical manifests, binary fixtures, locks or SVG content changed. All previous Step 51 work remains present.

Remaining limitations: real-file qualification, manual native dialogs and clean-machine offline installation are unexecuted. The historical research matrix stays **22 passes / 24 known failures / 26 unexecuted** and was not rerun. Evaluation-only ImageSharp 2.1.11 still emits **five advisories (three high/two moderate)**; no dependency upgrade or warning suppression occurred. Native row-height mismatches, financial over-precision rejection and unsupported formula/format limitations are preserved.

Recommended **Step 53**: supply the authorized workbook and finish the read-only qualification gate, then choose the smallest evidence-supported compatibility change. Keep every approved Payables matching rule unchanged. Production Apply remains disabled, Step 45 approval tickets remain permanently review-only, inspection editing/Merge/export and source overwrite remain disabled, and **Phase 2 remains incomplete**.
