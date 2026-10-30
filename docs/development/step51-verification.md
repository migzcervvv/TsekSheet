# Step 51 verification

Date: 2026-10-30 (Asia/Manila).

## Result and input gate

**Principal real-workbook acceptance goal: UNEXECUTED.** The only authorized input directory, `C:\Users\migzc\Desktop\TsekSheet\local-test-workbooks\`, was absent. No arbitrary drive, attachment or personal-directory workbook search was performed. Synthetic checks below are diagnostic readiness evidence only.

`git check-ignore -v` confirms `.gitignore:73` covers both `local-test-workbooks/` and `local-test-workbooks/copy.xlsx`; `.gitignore:18` covers `artifacts/step51/`. These checks establish ignore rules for prospective paths, not the presence or safety of a file. Individual-file identity, ancestor/reparse checks, private source hash before/after and private report generation are **UNEXECUTED** because no input exists. No private workbook was opened, copied, repaired, rewritten, uploaded or passed to Excel automation. No business data was introduced into tracked files, logs or screenshots.

All actual-workbook measurements remain unexecuted: file size, sheets/visibility, stored/populated/styled-only cells, dimensions, names/broken references, external links, embedded/binary parts, unsupported relationships, formulas/shared groups, styles/date/amount interpretation coverage and resource consumption. Header discovery, section bounds, heading/subtotal exclusion, repeated-vendor analysis, entire-sheet lookup coverage, subtotal impacts and workflow-profile fitness also remain unexecuted. No earlier user-described heading or row number was treated as an observed fact.

The [compatibility strategy](real-workbook-compatibility-strategy.md) separates every capability, existing code rule and conditional future architecture from unavailable private-file evidence. No real editability blocker or compatible production architecture can be selected from this run.

## Baseline and plan

Repository: `C:\Users\migzc\Desktop\TsekSheet`. Started on clean `main`, HEAD `de641d53eb87bb6553f2132a42463d04b1ca9796`. Origin fetch/push: `https://github.com/migzcervvv/TsekSheet.git`. No checkout, commit, push, reset or cleanup occurred.

Read AGENTS.md, GOALS.md, PLANS.md, README.md, architecture overview/three accepted ADRs and all ten requested development/workflow documents. Plan: check authorized input availability first; if absent, stop private qualification, run existing readiness checks, document exact constraints and supply procedure, and retain all mutation gates. No architectural implementation, new synthetic feature work, UI change, dependency upgrade or lockfile change was necessary.

Current local inventory:

| Item | Observed |
| --- | --- |
| Windows / architecture | 10.0.26200 / win-x64; Intel Core i5-12500H |
| Selected .NET SDK / MSBuild | 10.0.403 / 18.9.15 |
| Host/Core/WindowsDesktop runtime | 10.0.14 installed; older SDK/runtime families also remain installed |
| SDK selection | `global.json` pins 10.0.403, roll-forward disabled |
| Project configuration | `net10.0` / `net10.0-windows`, WPF x64, win-x64 restore graph; nine projects, evaluation/harness projects outside the solution |
| Node / npm / Git | 24.16.0 / 11.13.0 / 2.47.1.windows.1 |
| Frontend | npm lockfile, React 19.3.1, TypeScript 7.0.2, Vite 8.3.5, Vitest 5.0.5; toolchain accepts Node >=24.16.0 <25 and npm >=11.13.0 <12 |
| WebView2 | Installed application directory 156.0.4314.50; SDK package 1.0.4314.35; actual host tests passed |

## Verification results

Logs/TRX/probes are ignored under `artifacts/step51/`. Commands used locked restore graphs and existing user caches; no dependency versions changed.

| Check | Current result |
| --- | --- |
| Locked solution restore | Passed: `dotnet restore TsekSheet.sln --locked-mode --ignore-failed-sources` |
| Locked evaluation/native/managed restores | All passed with the same flags |
| Locked frontend installation | `npm ci --offline --no-audit --no-fund` passed with normal user cache access |
| Normal .NET 10 Release build | Passed, zero warnings/errors; runs npm ci, typecheck and production frontend build |
| Full solution tests | **357 passed, 1 failed, 1 skipped** of 359: Application 47 passed; Workbooks 297 passed/1 failed/1 skipped; Desktop 13 passed |
| WPF/WebView2 smoke | All 13 Desktop tests passed, including actual React/bridge workbook flows and inspection restrictions; native-selector seams, not manual dialogs |
| Frontend typecheck/build/tests | Passed; 35 tests |
| Toolchain tests | 3 passed |
| Evaluation safety tests | 13 passed; synthetic only |
| Native / managed commit harnesses | 600 / 390 passed, zero failed |
| Self-contained win-x64 publish | Passed to `artifacts/step51-win-x64`, runtime 10.0.14, `--no-restore` |
| Retained native display comparisons | Passed: Step 50 42 exact supported matches/24 explicit rejections; Step 49 26 matches/4 rejections, zero supported divergence |
| Expanded native formatting evidence comparison | Passed; retained exact row-height policy and discrepancies |
| Step 44 generated native reference comparison | Skipped; evidence not seeded into the current fingerprint-specific artifact directory |
| Source/fixture/logo/dependency fingerprints | 88 existing XLSX/SVG/lockfiles checked before/after; zero changed |
| New diagnostic tests | None added: no new diagnostic code or workbook mutation |
| Real-workbook tests/hash verification | **UNEXECUTED: no authorized input** |

The suite is **not fully passing**. `GenerateExpandedFormattingCorpusWithLiteralExpectations` fails its exact index provenance comparison: retained specification hash `2C148FB71178662CFFF4CEAE961B1EABD114626984AFED638C0C6C4AB42A86E7` versus current working-file hash `CB35C41CB3B1ED626A49929CABC349E86664B9F5FABC0562269115FB8342E89D`. `core.autocrlf=true` and `git ls-files --eol` report LF index/CRLF working files. Hashing an in-memory LF version of `formatting-v1.json` reproduces the retained hash exactly. This diagnoses checkout line-ending sensitivity; no reference hash, source file or accepted evidence was normalized or replaced to force a pass.

The Step 44 generator seeds retained evidence only if exact specification, fixture-generator and procedure hashes match. Its current artifact directory lacks `excel-evidence.json`, so `ExcelReferencesRequireActualProvenanceAndIndependentSemanticAgreement` is inconclusive/skipped. Retained evidence still exists under `ReferenceCorpus/VerifiedExcel`; no new native Excel capture was attempted. A separate provenance/checkout portability fix must preserve immutable evidence and restore exact verified inputs before this gate can pass. Never equate an LF diagnostic calculation with proof that all current inputs match.

Initial restricted `dotnet --info` could not enumerate Service Control Manager; initial sandboxed restore returned exit 1 with an empty log. The sandbox npm cache lacked `xmlchars` and offline install failed. Normal user-cache access resolved restore/install/build/testing without installing tools or upgrading packages. These initial failures and final results are retained. Final SDK inventory succeeded outside that restriction.

## Performance evidence — existing synthetic probes only

No actual-workbook performance claim is possible. Full-suite probes recorded:

| Existing synthetic case | Current observed measurements |
| --- | --- |
| 125,000-cell navigation, three runs | Metadata 1.9504–4.5128 ms; first view 234.008–618.9732 ms; distant view 164.773–523.4962 ms; recent cached view 0.0161–0.0246 ms; process-lifetime peak working set 132,493,312–135,512,064 bytes |
| 125,000 populated cells | Scan 165.2567–179.595 ms; source unchanged |
| 125,000 styled cells | Scan 123.3155–143.8116 ms; source unchanged |
| 60,000 formulas | Scan 103.8798–110.7908 ms; source unchanged |
| 200,000 shared strings | Selected scan 187.0661 ms |
| 512 tabs | Metadata 17.4815 ms; no eager cell loading |
| 100 sheets, ten selected scans | Metadata 4.4518 ms; selected scans 1.4467–2.7868 ms |

These are development-host/test-process measurements including other test activity, not scanner heap maxima, hard resource guarantees or real-workbook results. The existing eight-window/2 MiB accounted cache, sequential ZIP scans and admission limits remain unchanged.

## Supplying the missing input

1. From this repository, run `New-Item -ItemType Directory -Path .\local-test-workbooks -Force` if needed. Place an explicitly authorized **duplicate**, named `copy.xlsx`, directly in that directory using File Explorer. Keep the original elsewhere. Do not use a shortcut, symbolic link, junction or other redirected directory/file. Do not add the duplicate to Git or attach/upload it to chat.
2. Verify `git check-ignore -v -- local-test-workbooks/ local-test-workbooks/copy.xlsx` and `git ls-files -- local-test-workbooks` (the latter must be empty). Confirm that this duplicate is authorized for local read-only qualification.
3. Resume Step 51. Before opening, qualification must validate canonical containment in the designated directory and reject reparse points/links on every ancestor and the file; validate the private artifact path similarly. The ignore check alone does not establish path safety.
4. Record file length and SHA-256 only in a checked ignored private report. Keep a read-only source handle excluding writers/deletion, apply existing ZIP/XML budgets and use the existing analyzer/selected-sheet inspector. Never use Excel automation, follow external targets, resave, repair or create an editable session to bypass inspection restrictions.
5. Record actual technical counts, capabilities, interpretation coverage, section feasibility and timings privately. Retain raw values, classify cached formulas as potentially stale, and retain Step 50 over-precision rejection. Do not print names/records/amounts into tracked reports or tool logs. After closing, verify SHA-256 again. Publish only reviewed non-sensitive aggregates; no private hash/path or business records in tracked documentation.

## Changes, remaining risks and Step 52

Created this report and `real-workbook-compatibility-strategy.md`; updated PLANS.md, README.md and the compatibility/Payables documentation links. No production/test code, fixtures, branding or dependency files changed. No private diagnostic report was created because there is no private input.

The principal gate is still unexecuted and the solution has one failure/one skip. The research matrix remains **22 passes / 24 known failures / 26 unexecuted**, historical and not rerun. Five evaluation-only ImageSharp 2.1.11 advisories remain visible (three high/two moderate); evaluation was not used on external inputs. Manual native dialogs, clean-machine offline installation and private-file performance/layout/preservation remain unexecuted.

Recommended Step 52: first supply the authorized workbook and finish this qualification gate; address the narrowly diagnosed checkout/provenance portability issue while preserving retained evidence. Then select the smallest evidence-supported engineering change. Conditional choices are a measured editable-envelope extension for resource-only cases, targeted package changes within an independently validated transaction for provably isolated edits, or a separate reviewed output when dependencies remain unsafe. No actual-workbook strategy is approved yet.

Production Payables Apply stays disabled. Step 45 tickets remain permanently review-only. Inspection editing/copies/Merge/export, source/existing-file overwrite, autosave, PDF OCR, cloud services and databases remain unavailable. Approved Payables matching rules are unchanged. **Phase 2 is incomplete.**
