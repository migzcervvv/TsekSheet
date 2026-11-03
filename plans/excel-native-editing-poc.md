# Excel-native editing proof of concept — 2026-11-03

## Scope and gate order

Experimental only. Target family is an artificial five-worksheet Payables-shaped
package; exact destination name `Unpaid RFPs - SEP`. No private workbook path/data,
production provider registration, WPF/React change, OOXML admission change, receipt
Apply/Save As integration, original save, commit, push or deployment. OCR and
worksheet-eligibility implementation remain paused.

Read project instructions/goals/roadmap/progress, the real editability investigation,
architecture, compatibility and structural/native-format preservation evidence.
Excel is a required user dependency; provider-specific COM stays experimental.

1. **Gate A first:** create a separate process/STA COM provider, bounded protocol,
   Windows message handling, exact Excel PID/start-time/HWND ownership proof and
   lifecycle reports. Never attach to an existing instance; never terminate an
   unverified Excel process. If activation/ownership/clean shutdown cannot be proved,
   stop native work. Existing Excel processes are observed only by PID/start time.
2. Generate small deterministic literal OOXML five-sheet fixtures in newly created
   experiment directories. Only artificial values; independent tests assert fixture
   contents, permissions/path checks, bounded inspection and deliberate corruption.
3. **Gate B:** lock/hash synthetic baseline, copy bytes to a new owned working path
   before Excel opens it, disable macros/events/link updates and automatic repair,
   serialize only a distinct candidate, close/reopen and independently inventory
   all package/XML/workbook-semantic differences. Retain originals and hashes.
   No blanket normalization exemption. Initially classify all deltas as unreviewed;
   only specific understood expected differences may be qualified. Preserve failures.
4. Only if A and B demonstrably pass, plan/implement **C**: one explicitly verified
   artificial record at a synthetic subtotal/template boundary; independently prove
   rows, formulas, styles, heights, merges, names, cross-sheet context and errors.
   Then small batch and repeated fresh candidates. No real-workbook row assumption.
5. Only while prerequisites remain qualified, expand **D** progressively: stored
   blanks, shared formulas/calcChain, merges/formatting, hidden/grouped rows, names,
   cross-sheet references, inert external metadata, broken references, comments and
   printer context. Bound file/expanded/XML/cell/part/time/process resources. Test
   cancellation/timeout/crash/stale-source/corrupt/permission failures without
   automatic retries or uncertain adoption.
6. Run appropriate new tests and existing safety regressions. Document actual gate
   outcomes, resources and unexplained deltas; update PLANS/progress. Recommendation
   is proceed/revise/reject for this experiment only. Real Payables remains unqualified.

## Initial implementation shape and limits

One unreferenced experimental executable with separate contracts, generator,
independent package oracle, process supervisor and Excel-specific provider modules;
an explicit worker mode runs in a child process. No production dependency changes
or Office PIA package: local COM activation behind a small provider interface.
Separate experiment tests do not require production feature permissions.

Only GUID-owned `artifacts/excel-native-poc/` runs and generated/hash-bound inputs
are accepted. Source opened read-only and locked; work/candidate paths never alias
source or overwrite prior files. No arbitrary workbook paths or public editing API.
Initial bounds: 4 MiB input, 16 MiB expanded, 128 parts, 10,000 stored cells,
five fixed tabs, depth 64, 200,000 XML elements, 2 MiB XML part, 16 KiB request,
256 KiB response, 45-second worker timeout and bounded graceful cleanup.

Process cleanup uses only the worker-created COM instance after renewed ownership
checks. No hard Excel kill is enabled. A blocked COM call can invalidate the result
and leave cleanup unconfirmed; report exact owned-process evidence and stop further
experiments. Managed cancellation is checked between calls, not claimed to interrupt
an in-flight COM operation. Never automatically replay an operation.
