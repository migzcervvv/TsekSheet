# T2 explicit-instance experiment — stopped, not qualified

**Recommendation: reject both activation approaches for T2 lifecycle acceptance pending further evidence.**
Do not switch the production adapter. Draft PR #4 remains Draft. This experiment stopped on
an unexplained process-identity rejection, as requested; it did not finish thirty repetitions
per approach. The completed `/x` trials also reproduced the original shutdown delay.

## Mechanism and safety boundary

`Run.ps1` validates the fully qualified installed Office `EXCEL.EXE`, its valid Microsoft
Authenticode signature and SHA-256, and inventories startup folders/options without writes.
The experiment uses `ProcessStartInfo(executable)`, `UseShellExecute=false`, and separate
`ArgumentList` arguments `/x` and an admitted synthetic bootstrap path. No shell, ROT,
`GetActiveObject`, title-based selection or arbitrary-instance fallback is used.

It retains the launched process handle, PID, UTC creation time and Windows session. Only
that PID's `XLMAIN`/`EXCEL7` document pane is eligible. More than one candidate is rejected.
`AccessibleObjectFromWindow(EXCEL7, OBJID_NATIVEOM, IID_IDispatch)` returns a typed
`Microsoft.Office.Interop.Excel.Window`; its `Application` must map back to the launched PID
and document root. Executable, creation time, session and application HWND/PID are rechecked
before writes, Calculate, Close and Quit. Workbook IUnknown identities, exact working-copy
paths and the complete open-book inventory must match. Unexpected workbooks prevent Quit.
All COM calls and releases run on one pumped, serialized STA per owned session; no COM DTO
crosses that boundary. The existing bounded busy filter and readback/preservation code are reused.

The only seed is the retained Excel-authored value-only workbook (SHA-256
`41515AFF89A6E7F1F12EA40B3E75728F55B05A55CAC6D2A66AAD6EF03097F116`).
Every opened file is a new hash-verified synthetic working copy under `fixtures/excel/t1-activation-*`.
The existing `PackageGuard` rejects macros including XLM, external resources, protection,
invalid packages and admission-limit violations. Compatibility findings are retained, not
treated as security certification. This modern Excel file has an `extLst` compatibility finding;
this experiment measures its Excel readback, and does not enable the superseded writer.
The bootstrap opens before automation security can be set, so this is **not** a general factory
for user files. Later opens set ForceDisable immediately before `Workbooks.Open(UpdateLinks:0)`.
Backups are hash verified before writes, formulas are explicit synthetic formula targets,
all changes are discarded, and originals are never saved. Office settings were not changed.

## Fixed run and results

Environment: Windows session 18, Excel x64 **16.0.20527.20112**, .NET 10.0.14.
Five repetitions of six scenarios per approach were scheduled (60 attempts), interleaved
by scenario with approach order reversed on even repetitions. A separate visible synthetic
session with an open workbook ran throughout, on its own STA. A thirty-second startup
acquisition bound and unchanged **ten-second Quit deadline** were used; late exit was
observed independently up to ninety seconds, with native stacks captured only after failure.

The run stopped at order 21: **20 complete lifecycles, one rejected launch, 39 scheduled
attempts not executed**. No failed attempt was replaced or excluded.

| Scenario | COM ten-second failures / completed | `/x` ten-second failures / completed |
|---|---:|---:|
| Launch / Quit | 1/2 | 0/2 |
| Open / close / Quit | 2/2 | 0/2 |
| Bulk values | 2/2 | 1/2 |
| Formula / Calculate | 2/2 | 1/2 |
| Ten write/recalculate batches in one session | 0/1 | 0/1 |
| Two workbooks, preserving the first while editing the second | 0/1 | 0/1 |
| Total complete lifecycles | **7/10 failures** | **2/10 failures** |

COM acquisition succeeded 10/10; `/x` succeeded 10/11, with one pre-acquisition process
identity rejection. Among acquired applications: zero observed PID/window mismatches,
COM/STA failures or unexpected sampled modal dialogs. All 32 write batches passed value,
formula and preservation checks (28 included formula/recalculation checks). All completed
STAs stopped and Quit returned. All nine delayed processes exited at **60.099–60.159 s**
after Quit with valid OS timestamps, without termination or manual intervention. Native main
threads wait in the same `mso20win32client!Ordinal53733` / Office UI path as the retained
failure. Zero tracked RCWs does not establish release of every possible COM reference.

Every completed trial preserved the concurrent session's exact Excel readback snapshot.
Sentinel PID **37536**, start **2026-11-03T15:37:52.6264501Z**, remained open until the matrix
stopped and its creator cleaned it up (exit 2.334 s after Quit). There were no pre-existing
actual user Excel processes: the concurrent-user test is synthetic, not evidence about real
user workbooks. Process census: **0 before; 1 after the rejected acquisition; 0 after cleanup**.

Rejected launch: order 21, `/x` repeated scenario, PID **2884**, start
**2026-11-03T15:40:28.2586091Z**. The guard failed before native acquisition; it did not modify
or close the bootstrap. The original trace does not identify which comparison failed, so
the cause remains inconclusive. A separate cleanup-only command subsequently revalidated
that exact launch, session, executable, native pane, HWND/PID and sole unchanged synthetic
workbook before Close/Quit (exit 2.196 s). This does **not** convert the rejected attempt to a pass.

## Evidence, limitations and next action

Raw records remain in `artifacts/activation-experiment/qualification-2/`: `environment.json`,
`launch-preflight.json`, `order-and-results.json`, `result-*.json`, per-PID native stacks,
sentinel/baseline records and `cleanup-revalidation-*/`. Original `summary.json` calls all
21 attempts "Completed"; use `reviewed-summary.json` for the explicit attempt/completion
distinction. Older lifecycle evidence and the earlier three-run sample remain unchanged.

`qualification-1/` records a pre-launch compatibility-gate rejection (zero Excel launches).
The first cleanup attempt's sharing violation is retained in `rejected-launch-cleanup.json`;
a later cleanup used a read-sharing admission stream. No Excel process was killed.
The supervisor initially waited for inherited stdout EOF after its controller had exited:
that is a **harness wait**, distinct from the nine timed Excel shutdown failures. The committed
script bounds stream draining; the guard now logs each compared identity field. These
diagnostic hardenings compile, but the sixty-attempt matrix was not restarted after the stop.

Background Application, unit, frontend and desktop regressions overlapped part of the matrix;
the desktop focus smoke test failed then (23/1/0), but passed after Excel cleanup (24/0/0).
This is an environmental limitation, not proof of the rejection's cause. Application: 629/0/0;
Excel non-Excel-category tests: 4/0/0; frontend: 83/0/0; toolchain: 3/0/0. No production
preservation test was changed. Two full Excel suites and redacted-workbook acceptance were
not rerun in this stopped experiment; T2 remains incomplete.

Unlike the earlier small sample, this sequence closes the command-line bootstrap and opens
the working workbook through COM. `/x` therefore does not by itself remove the delay for
the required operation sequence. The exact Office/interop dependency remains unknown.
Smallest supported action: keep production activation unchanged, retain the failing evidence,
and let the planner decide whether to authorize another bounded run with detailed identity
traces and a startup-workbook-retention control. No production rewrite is supported.

Reproduction: locked restore/build this isolated project in Release, then use PowerShell 7
`Run.ps1 -ExcelExecutable <absolute installed EXCEL.EXE> -SyntheticSeed <retained synthetic.xlsx>
-OutputDirectory <fresh directory> -StackTool <local native stack probe>`.
It is outside the solution/CI and does not change the application write path.

References: Microsoft documents [`/x`](https://support.microsoft.com/en-us/office/lifecycle/command-line-switches-for-microsoft-office-products)
and [`EXCEL7` native object-model access](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromwindow).
[`AutomationSecurity`](https://learn.microsoft.com/en-us/office/vba/api/excel.application.automationsecurity)
does not disable XLM macros, which is why package admission remains necessary;
[`Workbooks.Open`](https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open) documents link-update control.
