# Experimental Excel-native provider

**Not a production workbook provider. Gate A passed on the tested host; Gate B
does not qualify preservation. Insertion and complexity expansion are stopped.**

Read [the plan](../../plans/excel-native-editing-poc.md) and
[architecture, results and safety limits](../../docs/development/excel-native-editing-poc.md).
The [retained review](evidence/initial-noop-v1.json) identifies the exact synthetic
capture, hashes, native observations and unapproved differences. Full raw reports
and generated files remain under ignored `artifacts/excel-native-poc/`.

The [theme-control revision evidence](evidence/gate-b-theme-controls-v1.json) retains
the complete-theme/literal-font/negative matrix. Calibri is preserved by both
controlled baselines. B remains blocked by layout/package findings and native
cleanup/timeouts in other runs. The unqualified diagnostic COM reader was removed.
No row insertion is implemented. Original failed evidence remains unchanged.

## Boundaries

- This executable is not referenced by the production solution/application or UI.
- Only deterministic generated five-sheet inputs enter native code. The worker
  requires the exact fixture-version SHA-256, not merely a marker or sheet name.
- Native calls are in ExcelProvider; contracts and independent ZIP/XML evidence
  contain no COM objects. An explicit child-process mode owns the STA/message pump.
- No worksheet insertion API/command, generic workbook-open API, production Apply,
  Save As registration, existing-file output or Google Sheets integration exists.
- All work is restricted to new GUID directories under `artifacts/excel-native-poc`.
  Source is locked/read-only; `working.xlsx` is an exact-byte copy made before Excel
  opens it; `candidate.xlsx` is distinct. Existing outputs are never overwritten.
- Never use real workbook bytes/data to construct the fixture. No real workbook
  path is accepted. No native normalization exemption has been granted.

## Reproducible commands

Windows x64, installed Excel, pinned SDK from global.json, Windows Desktop runtime.
No Office PIA/NuGet package is needed by the provider. Tests reuse the existing
MSTest 4.5.2/Test SDK 18.11.0 versions in a separate experimental test project.
Only the tests reference Open XML SDK 3.5.1 for independent read-only schema validation.

```powershell
dotnet restore experiments/TsekSheet.ExcelNative.Poc.Tests --locked-mode
dotnet test experiments/TsekSheet.ExcelNative.Poc.Tests -c Release --no-restore
dotnet run --project experiments/TsekSheet.ExcelNative.Poc -c Release --no-restore -- gate-a
```

Gate A prints its newly generated run root. Only an actual passing Gate A receipt
with owned-process exit and restored instance settings permits the normal harness
to start B:

```powershell
dotnet run --project experiments/TsekSheet.ExcelNative.Poc -c Release --no-build --no-restore -- gate-b '<passing Gate A root>'
```

The optional third argument selects `explicit-theme-v1`, `literal-font-v1`, or the
original default `incomplete-theme-v1`. Every selection generates a fresh isolated
source; source admission still requires its exact generator hash. Current results
remain failures and do not authorize further native work or insertion.

Exit code 2 and `Passed=false` are the **expected preserved failure** for the
initial no-op fixture on the measured host. Native operations may complete and
clean up while preservation fails. Do not treat `Code=COMPLETED` as a gate pass.
`InsertionAuthorized` remains false in every report.

`analyze <owned run root>` can generate missing semantic evidence for an existing
synthetic capture without starting Excel. Evidence creation uses create-new and
refuses to overwrite a prior report. The raw and semantic comparators do not adopt
a workbook or confer edit permission.

`review <owned run root>` performs independent bounded offline analysis without
starting Excel and creates comparison-v3.json, semantics-v3.json and
preservation-review-v3.json. It refuses collisions and requires an exact generated
source hash. It does not promote invalidated/unknown native outcomes. Complete raw
differences remain even when a narrow encoding interpretation is accepted.

## Current test coverage

54 tests cover deterministic fixture provenance/content, positive unchanged
comparison without insertion rights, deliberate value/formula/cache/height/font/
style/identity defects, unknown part retention, active/external/dangling/DTD input,
file/part/entry/depth/report budgets, calcChain versus worksheet counts, path and
protocol rejection, invalid gate progression, prelaunch cancellation and pre-COM
source/output/cancellation failures. These are synthetic safety/oracle tests,
not substitutes for actual Excel evidence or Gate D failure qualification.
Revision controls also cover valid complete-theme/literal sources, effective font
resolution, missing-theme failure, font/theme substitution, calculation flags/mode,
worksheet targets, descent/explicit height, encoding equivalence and unrelated
XML/namespace/relationship/part retention.
