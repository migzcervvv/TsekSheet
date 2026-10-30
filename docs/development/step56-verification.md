# Step 56 — spreadsheet navigation and Section Review usability

Date: 2026-10-30 (Asia/Manila).

## Baseline and scope

Started on `main` at `de641d53eb87bb6553f2132a42463d04b1ca9796`, with **31 modified and 26 untracked Git path entries**. Preserved the complete Step 51–55 work, including private profile/qualification artifacts and native reference portability changes. The implementation plan was recorded in PLANS.md before changes. No commit, push, reset, clean, dependency upgrade, workbook rewrite or branding change occurred.

This milestone changes read-only browsing and review usability. It grants no editing, working-copy, insertion, Merge, export or production Apply authority. The editable 50,000-cell guard and Step 45 permanently review-only tickets are unchanged. No persistence, OCR, cloud service, database or private product review-file format was added. **Phase 2 remains incomplete.**

## Decision-loss protection

Closing Section Review or selecting a replacement profile with active decisions opens **Discard review decisions?**, with the number of in-memory decisions and the permanent loss explained. **Continue Reviewing** is focused by default; Escape also preserves state. Keyboard focus stays within the prompt. **Discard & Close** invokes the pending operation only after explicit choice. Zero-decision reviews close normally.

The viewer receives the active decision count and operation status, and applies the same guard before worksheet changes, either workbook-open path and workbook close. It does not initiate the destructive bridge request on cancellation. Review operations temporarily disable competing source changes. Native window closing reads the backend decision count and offers equivalent named actions with preservation as the default. Backend session disposal, process/browser failure and cancellation still release resources unconditionally; memory-only decisions cannot survive a process crash. This is a UI confirmation boundary, not a new backend authorization token or persistence protocol.

React tests cover zero/one/multiple decisions, cancellation, explicit discard, profile replacement and worksheet/workbook switches using the real BridgeClient transport. A decision response timeout or rejected response identity may leave a backend-only decision: the guard conservatively retains the possible count and labels the outcome unconfirmed. A structured backend validation rejection does not fabricate a pending decision. The stale-response test verifies that uncertain outcomes still require discard confirmation. The actual synthetic WPF/WebView2 test covers canceled profile/workbook/worksheet replacement, canceled and confirmed review close, native close refusal and confirmed worksheet-switch cleanup. Native picker and close-confirmation selector seams remain synthetic; manual native-dialog acceptance is not claimed.

## Navigation

Inspection uses a native scroll container with visible horizontal/vertical scrollbars and a bounded virtual spacer. Wheel, trackpad and scrollbar movement update source row/column windows. Go To Cell validates A1 coordinates through `XFD1048576`; arrows, Page Up/Down, Home/End and Ctrl+Home/End move selection and bring the target into view. Source labels and selected-cell details remain attached to source coordinates. Nearby viewport changes preserve selection. Paging and start-coordinate inputs remain accessible fallbacks. Review source navigation preserves the active review and decisions.

Movement is coalesced over **80 ms**, with at most one admitted viewport request from the viewer at a time. Queued obsolete movement is canceled; obsolete results cannot replace the current window/session. An admitted scan finishes under the existing backend budgets before the latest queued viewport runs. There is no new server-side per-scroll abort protocol. Every request remains **20 rows × 6 columns**, reusing the existing eight-window/2 MiB inspection cache. No eager workbook materialization, ZIP-offset index or disk cache is introduced.

Synthetic tests cover direct and maximum-coordinate navigation, keyboard movement, scroll events, rapid movement with delayed responses, single-flight behavior and selection retention. The actual native test exercises Go To Cell, keyboard selection, native scrollbar movement and both scroll-axis styles through React/C#. Physical mouse/trackpad hardware feel remains for manual acceptance.

## Display bounds and conservative limits

The existing complete, budgeted selected-sheet scan now additionally reports `MeaningfulLastRow` and `MeaningfulLastColumn`. It includes every serialized value and every formula element, even an empty/cacheless/shared follower, across hidden rows and distant columns. Formatting-only and empty stored cells do not extend these bounds. The rectangle always starts at A1: internal blank rows, blank fields within records, sparse gaps, subtotal and summary coordinates are never collapsed or renumbered.

Shared-string indices are conservatively included even when they resolve to empty strings; determining bounds does not retain the complete shared-string table. Whitespace or unsupported stored values may conservatively extend the range. Objects, merges, drawings and spill regions without stored content are outside this cell-content rectangle. A successful scan measures all stored cell evidence within existing limits; a budget/parse failure does not produce certified partial bounds. Declared Excel dimensions remain untrusted and never allocate or certify content.

**Inspect full stored range** restores the maximum stored cell/row-element extent. Explicit cell/start-coordinate navigation can additionally inspect the requested area outside that extent. Display bounds do not feed section capture coverage, analyzer completeness, editing eligibility or mutation checks. Synthetic tests cover internal blanks, styled tails in both axes, empty worksheets, hidden/distant values, uncached formulas, conservative empty shared-string references, full-range coordinates and unchanged source fingerprints.

## Section Review organization

Six category buttons expose **Overview**, **Sections & Subtotals**, **Missing Vendors**, **Potential Duplicates**, **Unassigned Rows** and **Review History**. Overview shows six compact before/after accounting counts; full coverage/timing is in Technical capture details. Decision controls appear alongside the relevant review category. Headers and diagnostics use disclosures; all warnings and paged diagnostics remain accessible. Existing styling and branding are retained.

Common decision labels now include **Confirm section ending**, **Mark as subtotal / non-record**, **Acknowledge duplicate warning** and **Requires investigation**. After confirming an ending on an originally record-shaped row, a guided prompt offers a separate non-record classification. It requires another explicit reason and confirmation. The backend decision types and validations are unchanged; the decisions have separate IDs and retraction controls. No blank vendor is automatically classified, and boundary review alone does not resolve an identity warning. Original/recomputed duplicate warnings remain retained. Existing stale binding, replay, source evidence, dependency and no-insertion tests remain authoritative.

Synthetic native layout checks cover widths **390, 768 and 1100** without document overflow. Artificial navigation/review screenshots are retained with the Desktop test outputs; no private accounting screenshots were created.

## Private read-only requalification

Used only the authorized ignored `local-test-workbooks/copy.xlsx`. Revalidated its canonical ordinary-file path and every source/profile/output ancestor against reparse points, Git ignore/index exclusion, size and SHA-256 agreement with the historical private fingerprint. The ordinary inspection engine held read-only handles; no Excel automation, external-link resolution, source repair/save/normalization or workbook mutation occurred. Fingerprints and records remain under ignored `artifacts/step56-private/`.

All **five tabs** still browse and the total remains **439,321 stored cells**. Four tabs have **16,378 stored columns but only 20 content-evidence columns**. The third has **16,384 stored/content-evidence columns**, which remain visible. A separate bounded read-only capture of its last six columns over 1,605 rows confirmed **one resolved nonempty value and one formula**, with no truncation. This extra capture is independent of the single retained section-review role capture; no private contents were emitted in tracked files.

Observed meaningful row bounds remain **1,608 / 1,596 / 1,605 / 1,619 / 1,634** in tab order. No actual trailing row trim is justified by this scan. Target review still retains **20,904 role coordinates, 1,519 hidden rows, 807 original record-shaped rows, 25 uncertain identities, 484 unassigned meaningful rows and 21 duplicate groups**. Qualification creates **zero human decisions**; it does not replay the user's manual accounting decisions or certify section/identity/preservation completeness.

## Measurements

Same-source development-host runs; these are observations, not capacity guarantees or isolated causal benchmarks.

| Workload | Step 55 baseline | Step 56 |
| --- | ---: | ---: |
| Metadata/open | 486.3 ms | 611.6 ms |
| Single section capture | 214.2 ms | 134.7 ms |
| Initial pure review analysis | 175.9 ms | 166.2 ms |
| Five further pure analyses | 71.9–100.1 ms | 69.6–103.9 ms |
| 34 source-navigation viewports | 74.1–207.4 ms each | 77.1–241.5 ms each |
| Five-tab/review/navigation qualification | 6,002.6 ms | 6,422.5 ms |
| Cumulative managed allocations | 2,579,075,544 bytes | 2,574,815,448 bytes |
| Process peak working set | 101,183,488 bytes | 97,972,224 bytes |

The additional distant-column probe is excluded from that matched workload. Display bounds add integer comparisons during the existing scan, with no additional retained sheet model. The synthetic rapid-scroll test sends 41 burst movements while one request is delayed: the initial view plus at most two subsequent requests are admitted, with peak concurrency one. Scroll debounce adds up to 80 ms before an uncached request; streaming scans still dominate distant navigation. There is no claim that XML scanning became faster.

## Final verification

Final full-suite and publish results follow. Development logs preserve restricted-host cancellation/stall, a build/test dependency-file lock race, three initial frontend failures (range/caption expectations and intentionally retained selection), two candidate desktop timing failures and an initial private-runner invocation missing its required root argument. Those are development failures, not silently omitted results. Candidate tests now wait for enabled controls before clicking. Final suites have no failures or skips.

| Check | Result |
| --- | --- |
| Four locked restores | Passed; no lock/dependency changes |
| Frontend typecheck | Passed |
| Frontend React/bridge tests | 52 passed, zero failed |
| Toolchain tests | 3 passed, zero failed |
| Checkout portability | 5 passed, zero failed |
| Evaluation safety | 13 passed, zero failed/skipped |
| Native / managed file-commit harnesses | 600 / 390 passed, zero failed |
| Focused actual native navigation/review/candidate tests | 3 passed, zero failed |
| Normal .NET Release build | Passed, zero warnings/errors |
| Complete .NET solution tests | **420 passed**, zero failed/skipped: Application 72, Workbooks 333, Desktop 15 |
| Self-contained Windows x64 publish | `artifacts/step56-win-x64`; runtime 10.0.14, x64 PE and runtime/native dependencies checked, all five frontend files match the final build |
| Fixture/logo/lock fingerprints | **126 checked**, zero changed before/after |
| Private source fingerprint | Historical, starting, qualification, distant-probe and final values agree privately |
| Scoped privacy audit | **76 text artifacts checked**, zero matches |

Evaluation-only ImageSharp advisories remain unchanged (three high/two moderate). Existing native formatting/reference/provenance comparisons are exercised by the complete Workbooks suite; no new Excel capture or unqualified format grammar was introduced. The privacy audit checks new/untracked text, added diff lines and ordinary Step 56 logs against private source/worksheet/entity/identifier terms; it supplements review rather than proving absence of every sensitive substring. The historical research matrix remains 22 passes / 24 known failures / 26 unexecuted and is not rerun. Clean-machine offline installation, manual physical scrolling/native dialogs, actual accounting-owner review and complete Excel calculation/visual/preservation fidelity remain unexecuted or unproven.

## Changed files and follow-up

Product changes: application workbook contracts/workspace and bridge pending-count exposure; inspection scanner bounds; native window-close handling; frontend viewer, SectionReview, bridge scan validation and styles; new `useReviewDiscard.tsx` and `worksheetNavigation.ts`. Verification changes: shared artificial frontend review fixture, new navigation and display-bounds tests, updated review/viewer/native integration/smoke tests. Documentation: README, PLANS, the Section Review guide and this report. Prior dirty files remain preserved.

Next acceptance is accounting-owner use of natural scrolling and the focused review queues against the private workbook. Remaining limits are conservative shared-string bounds, unsupported non-cell visual areas, full-sheet rescans on uncached windows, completion of unresolved accounting review and absence of persistence. Source preservation and editing restrictions remain intact; Phase 2 is incomplete.
