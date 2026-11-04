# Bounded T2 Excel lifecycle comparison

Diagnostic results on Excel x64 **16.0.20527.20112**, .NET 10, 2026-11-03.
The standalone executable has no TsekSheet implementation or test-framework references.
It uses typed embedded interop, its own pumped STA and deterministic releases. Each trial
records owned PID/start time, build, visible window state, COM releases, Quit status and exit evidence.

## Results

Failures against the unchanged ten-second deadline, three repetitions per populated cell:

| Scenario | COM activation | Explicit `/x` | Explicit `/x /safe` |
|---|---:|---:|---:|
| Activate / Quit, no workbook | 0/3 | — | — |
| Open / close / Quit | 0/3 | 0/3 | 0/3 |
| Assign `Value2` / close / Quit | 2/3 | 0/3 | 0/3 |
| Formula / calculate / close / Quit | 3/3 | 0/3 | 0/3 |

The original T2 five-insert test failed **3/3 during fixture cleanup**, before insertion.
All eight failed owned processes subsequently disappeared without intervention. Four OS
exit timestamps are valid (60.10–60.14 seconds after Quit); four raw timestamps were zero
FILETIMEs (1601) and are rejected. Those four reported exited at 60.09–60.14 seconds, but
their exact OS exit times are unavailable. Raw evidence is retained, not overwritten.
The final comparison is 25 passed / 5 failed / 0 skipped; original wrappers 0/3/0;
affected non-Excel unit tests 4/0/0. Excel process census before/after the matrix: 0/0.

The failures reproduce without TsekSheet. Their visible main windows disappear; the owning
STAs terminate. Native main-thread stacks wait in `mso20win32client!Ordinal53733`, called
through Office UI frames. Recorded zero RCW counts do **not** prove complete COM release.
Normal COM inventory found all COM add-ins disconnected and no Excel add-ins enabled (`Installed=false`);
startup folders were empty and the alternate startup path blank. Add-in registrations and
LoadBehavior values were unchanged. Normal `/x` also passed, so Safe Mode is not required
for the observed difference. A clean Windows account and another Office build were not tested.
Excel's registered COM server uses `/automation`; that flag, activation mechanism and startup
timing were not separated. Attribution: **COM-activation-sensitive Excel/interop shutdown delay**,
not proven to be a specific vendor/runtime defect. No product cleanup fix is implemented.

The smallest supported corrective experiment is an activation-only factory change using an
explicit owned `/x` process and PID-validated typed access. Production verification would still
need macros disabled before user-file opening and all existing lifecycle/preservation tests.
The three-run sample is not proof that this candidate fixes production reliability.

## Reproduction and evidence

Build this project in Release after a locked restore. Author a fresh synthetic workbook using
`ExcelLifecycle.Diagnostics.exe com author 0 <absolute fixtures/excel/t1-*/synthetic.xlsx> <output> [stack-tool]`.
Run `Run-Matrix.ps1 -SyntheticPath <file> -OutputDirectory <fresh-output> -StackTool <native-probe>`.
Build `TsekSheet.Excel.Tests` in Release, then run `Run-Original.ps1 -Repository <repo> -OutputDirectory <fresh-output>`.
Runs are fixed at three repetitions. The observer retains the ten-second verdict, observes
for 30 seconds in the controller and concurrently up to 90 seconds externally. Native snapshots
may briefly suspend only the identified test-owned process after its deadline has already failed.
`NativeStack.cpp` builds against the Microsoft debugger SDK; no debugger binaries are redistributed.
The committed observer waits for the native process handle and rejects invalid OS exit times.

Local records: `artifacts/lifecycle-matrix/final/`, `original/`, `final-summary.json`, and
`tests/TsekSheet.Excel.Tests/TestResults/t2-original-matrix-*.trx`. Preliminary diagnostic failures
(Safe Mode add-in inventory unavailable; an overly restrictive backup hash reader) are retained
separately and excluded from the corrected comparison. No Office settings were deliberately changed.
No original test was removed/skipped or preservation assertion weakened. T2 remains incomplete.

References: [Safe Mode/startup behavior](https://learn.microsoft.com/en-us/troubleshoot/microsoft-365-apps/excel/files-open-automatically),
[own-window native object model](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromwindow),
[OS process times](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes).
