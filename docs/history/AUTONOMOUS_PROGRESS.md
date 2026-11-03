# Autonomous MVP progress

## Cycle 20 — 2026-11-03 — Gate B theme controls; preservation remains blocked

Followed plans/excel-native-gate-b-revision.md before experimental changes. Preserved
all existing uncommitted work, both original failed captures and initial-noop-v1.json.
No private workbook was opened/copied/read/modified in this revision. No product
source, UI, admission, preservation oracle or production dependency changed.
OCR, worksheet-eligibility UI and new production editing remain paused.

### Delivered and independent checks

- Preserved the exact original generator hash CE86AA36...9D5AB as the unresolved
  minor-theme negative control. Added complete linked Calibri-theme and literal
  Calibri/FF000000/no-scheme sources. The theme seed uses only the retained
  synthetic candidate's complete DrawingML definitions, explicitly replacing its
  major/minor Latin fonts with Calibri. No independent Excel-authored full-workbook
  baseline is claimed. All three source packages pass read-only Open XML SDK 3.5.1
  Microsoft365 schema validation. SDK reference is experimental-test-only.
- Worker admits exactly these three generated hashes; private/foreign files still
  cannot enter through matching marker/sheet names. Only probe/no-op remain.
- Added independent font/scheme/effective-Latin/color analysis, resolved dependency
  edge and worksheet-target preservation, calculation-setting visibility and a
  versioned classification review. Plain string/XML encoding equivalences have
  explicit tests; descent/unknown raw differences require review; font/value/
  formula/cache/style/name/identity/target/explicit-layout/calculation-mode changes
  and lost relationships/parts/annotations fail. Complete raw reports remain.
  Production oracle and strict experimental raw qualification were not relaxed.
- Added create-new lifecycle journals and a provider-returned result before outer
  worker-loop exit so failures retain evidence. A supporting bounded COM font/
  geometry reader failed cleanup qualification and was removed; only DTOs for its
  invalidated captures remain. No UI or WPF/business-logic integration was added.

### Actual native results; failures remain failures

Fresh normal Gate A passed: e3ff9c335393433588c9bc46a9e8e2e1. Excel 16.0/build20326.

| Control/run | Font | Source/copy/reopen | Preservation/lifecycle outcome |
| --- | --- | --- | --- |
| Complete theme d6063112eaa54e52b5c10d79c288efbc | Calibri retained | Confirmed; settings restored and owned exit confirmed | B blocked: 12 changed/4 added parts, 224 raw facts, 15 v3 semantic differences |
| Literal e3c6c096e6574c39afe2e4f20dffdd84 | Calibri retained | Provider confirms; independent hashes match | EXCEL_CLEANUP_UNCONFIRMED; TIMEOUT_OUTCOME_UNKNOWN; 709 facts, 15 semantic differences |
| Negative e30ef468986b4a8cb609442cd52eba81 | Aptos Narrow substitution reproduced | Provider confirms; independent hashes match | Cleanup unconfirmed/timeout unknown; preservation also fails; 710 facts, 33 semantic differences |

All controls preserve 18 interpreted cells, artificial values, two formulas/caches,
five worksheet identities/targets, ranges, explicit heights, print margins and
empty merges/names. The negative changes font/style. Other controls have nine row
and five default-row dyDescent additions plus omission of explicit false calculation
flags. These are not approved by matching formula caches. Microsoft documents a
custom-height side effect for dyDescent; no independent rendering or future
auto-height behavior was qualified. Existing dependency edges remain, but added
metadata/default-style/chain relationships and other raw facts still need a finite
reviewed contract. Context-sheet raw XML changes also remain unapproved.

Complete-theme addition prevents the rewrite with the original styles on this
host; literal font selection also prevents it. This supports missing-theme/minor-
scheme fallback as the observed condition. It does not prove universal behavior,
the exact Open-versus-Save transition or rendered appearance across environments.
Calibri remains the expected identity; Aptos Narrow is not treated as equivalent.

Additional diagnostic runs dc125706e71643679e05613a0458097e,
e6c6b9e3d23e4654bace1937b2ba5236, b93f4d61eed0451a9444da96d61a086a and
749c8b0a1bdd463c811c6deb05328ded remain invalidated/unknown. One late-bound diagnostic
failed RuntimeBinderException. Property-readback approaches failed cleanup; the
reader was removed. Literal/negative failures also occurred without it, so its
sole causality is unproven. Journals show Quit returned, owned exit was not
confirmed within five seconds, and some worker outer loops failed to exit even
after persisting an invalidated provider result. No timeouts were widened; no Excel
process was killed/attached to. The supervisor stopped only its own timed-out child.
Later process inventory shows no Excel remaining, without retroactive gate approval.

**B remains blocked; C was not implemented/entered; D was not entered.** No native
insertion, real template-boundary assumption, output adoption or production editing
permission. Stop on the remaining preservation/lifecycle findings as requested.

### Verification and retained evidence

- Final **54 experimental tests passed**, zero failed/skipped; interim 46/51/53 green
  runs retained. Tests include schema and precise positive/negative classification
  controls, not substitute native-complexity qualification.
- Existing workbook **457** and application **608** tests passed unchanged, zero
  failed/skipped. No frontend/desktop UI rerun because no product/UI change.
- 321 protected production/test/initial-failure-evidence hashes unchanged. All
  generated baseline/working hashes remain exact. New reports use v3 filenames;
  initial comparison files and initial review JSON were not overwritten.
- artifacts/excel-native-gate-b-revision/ holds TRX, protected hashes and audit;
  artifacts/excel-native-poc/<run>/ holds source/working/candidate/raw/semantic/
  lifecycle records. Versioned experiments/TsekSheet.ExcelNative.Poc/evidence/
  gate-b-theme-controls-v1.json records hashes, status, measurements and findings.
- Updated experimental report, README and PLANS. No commit, push or deployment.

Remaining prerequisites: investigate RCW/STA-loop cleanup with exact ownership;
independent visual/descent/auto-height proof; finite package/calculation contract;
only then repeat B and consider the one artificial C insertion. Independent visual
rendering, other Office versions/locales, native D failure/complexity scenarios,
real workbook editing and production integration remain unexecuted/unqualified.

## Cycle 19 — 2026-11-03 — gated Excel-native synthetic proof of concept

Read the user's pasted experiment request in full, AGENTS/GOALS/current plans and
progress, the actual editability investigation, architecture/compatibility/structural
and native-format evidence. Recorded plans/excel-native-editing-poc.md before code.
Preserved all prior uncommitted work. Excel is now a required user dependency;
the adapter remains isolated and experimental. OCR and worksheet-eligibility UI
implementation remain paused.

### Delivered

- New unreferenced experiments/TsekSheet.ExcelNative.Poc executable: plain provider
  contracts, deterministic literal five-sheet fixture generator, strict source
  provenance/hash/path admission, fixed-runtime child-process supervisor and STA
  OLE/message-pump Excel provider. No Office PIA package, production solution/project
  reference, business-logic dependency, normal editing permission or UI integration.
- Ownership uses newly created Excel HWND/PID/start time/session and an initially
  empty workbook set; rechecks identity and expected paths before operations/Quit.
  No attachment to existing Excel or Excel kill path. Final cleanup restores six
  instance properties and confirms the exact owned process exited. COM rejected
  calls are not automatically replayed. Timeout/cancel invalidates the result;
  only the owned child worker can be stopped, with Excel cleanup left unconfirmed
  if necessary. Native hang/crash recovery is not claimed qualified.
- Worker accepts only probe/no-op and the exact known synthetic source bytes, not
  an arbitrary workbook matching a marker or tab name. It locks/hashes baseline,
  makes an exact-byte new working copy before opening it, saves only a distinct
  candidate and reopens read-only. Macros/events/link updates are disabled on the
  owned instance, normal load has no repair fallback, and no explicit calculation
  or insertion call is implemented. Existing output collisions reject.
- Independent PackageEvidence/SemanticEvidence modules retain package hashes,
  complete bounded positional XML deltas and a separate values/formulas/caches/
  styles/layout/identity projection. No blanket native-normalization exemption.
  Every report denies insertion authority. Production OOXML policies stay unchanged.
- New experiments/TsekSheet.ExcelNative.Poc.Tests: fixture, preservation-defect,
  parser/budget/path/protocol/gate and early failure tests. New lockfiles are scoped
  to these experimental projects, using the repository's existing test versions.
- README, docs/development/excel-native-editing-poc.md and the versioned
  experiments/TsekSheet.ExcelNative.Poc/evidence/initial-noop-v1.json retain the
  architecture, evidence paths, exact synthetic hashes, reviewed observations,
  limits and revise recommendation. PLANS updated. No actual accounting file
  was read/modified or used to generate fixtures in this cycle.

### Actual gates

**A passed on the measured normal lifecycle.** Final evidence:
artifacts/excel-native-poc/0b9f7822c0b94e0292181a6025802c3b/. A newly created instance
was owned and exited; all six instance settings were restored/read back. Excel
16.0/build 20326, x64 host. No existing Excel processes were present; concurrent
user-document/add-in scenarios remain untested and are not implied by the pass.

**B not qualified.** Final evidence:
artifacts/excel-native-poc/0ef13cb9836b47f3bef060a20edfd599/; first completed no-op
capture b007fcb80dfb48e0b9dade7248843d52/ reproduces the same findings. Exact working
copy/source hash/reopen/cleanup checks pass. The final native worker also restores
its instance settings. No editing call occurs. Baseline 4,294 bytes; candidate
12,389 bytes; 11 changed and five added parts; 710 raw positional XML facts differ.
All 18 stored cells, decoded values, two formula expressions/caches and five
worksheet identities remain in the inspected projection. However, Calibri changes
to Aptos Narrow in every cell style. A theme/sharedStrings/calcChain/core/app parts
are added; nine row and five default-row records acquire dyDescent=0.25. Explicit
21-point/custom heights remain. Semantic projection has 32 differences (18 style,
9 row, 5 default-row). The missing source theme/minor-font scheme is a plausible
contributor, not a proven harmless conversion. No normalization allowlist adopted.

**C/D not entered.** No insertion, batch/repeated insertion, realistic native
complexity expansion, native COM timeout/crash/permission fault or real-workbook
qualification. The user explicitly required stopping before insertion when B
cannot qualify. Positive narrow value/cache observations do not overrule the failed
font/layout/package evidence. No production editing or receipt output is enabled.

### Checks and measurements

| Check | Actual result |
| --- | --- |
| Final experimental build and tests | 27 passed, zero failed/skipped; artifacts/excel-native-poc-tests/final-verified/ |
| Existing full workbook regressions, unchanged --no-build --no-restore | 457 passed, zero failed/skipped; existing-workbooks/ |
| Existing full application regressions, unchanged --no-build --no-restore | 608 passed, zero failed/skipped; existing-application/ |
| Native Gate A final | Passed owned-instance/settings-restoration/exit checks; no workbook opened |
| Native Gate B final | Worker transport/lifecycle completed; exact copy/source/reopen/cleanup passed; independent preservation gate intentionally failed |
| Final B resource sample | Activation/ownership 1,627 ms; open 520 ms; serialization 55 ms; reopen 51 ms; worker 5,067 ms; supervisor 5,396 ms; Excel peak 221,798,400 bytes (211.52 MiB); worker peak 57,806,848 bytes (55.13 MiB) |

These samples are not hard process-memory limits, maximum-workbook qualification or
simultaneous additive peaks. Existing source/fixture/production dependency hashes
are preserved; no UI changes require a fresh frontend/hosted UI acceptance run.

Development failures retained: the first supervisor raised InvalidOperationException
after worker exit while collecting final metrics; its ownership journal remained
and subsequent inspection showed no Excel process. Metrics now sample while the
worker is alive, and native responses persist before supervisor result assembly.
dotnet test rejected a restore-only switch; restore/test were separated. Sandboxed
restore used CodexSandboxOffline's empty cache and failed NU1102; native restore
reused pinned packages. Early vulnerability-metadata access emitted NU1900; no
audit suppression or production dependency change. A patch-tool failure left a
new experimental source incomplete; it was reconstructed and verified through final
native A/B and test builds. An added 10,000-entry calcChain parser control passed
the worksheet budget but correctly hit the existing comparison-report limit: its
initial test had 26 passes/one assertion error; the corrected assertion requires
that exact rejection. Final 27 tests pass without raising any bound. Gate B remains
an actual preservation failure, not a test harness failure to be hidden.

### Handoff

Recommendation **revise**: qualify a fully specified synthetic font/theme baseline
and a specific independently tested native-difference policy; retain the failed
capture. Repeat B before any C insertion. Only then may shared/calcChain/merge/
hidden/group/name/cross-sheet/external-metadata/broken-ref/comment/printer and native
failure cases progress. Real accounting editing remains disabled until separate
preservation/operation-bound authority/explicit user approval/accounting acceptance.
No automatic commit, push or deployment; no OCR or eligibility UI resumption.

## Cycle 18 — 2026-11-03 — real workbook opening and compatibility

Temporarily paused OCR for the reported Open-versus-Inspect failure. Read project
instructions/goals/plans, this tracker, workbook architecture/engine/inspection/
editing/save documentation, real-workbook strategy and retained actual Step 53
evidence. Preserved all pre-existing uncommitted work. Recorded
plans/workbook-opening-compatibility.md before code changes. Root
AUTONOMOUS_PROGRESS.md does not exist; this is the repository's tracker.

### Finding and changes

Open only called full editable admission; Inspect used the independent bounded
XML reader. The restrictive full-package gates protect preservation; preventing
otherwise safe inspection from becoming a project was the functional defect.
Historical actual qualification stopped at 50,001 stored cells, LIMIT_EXCEEDED,
against a 50,000 limit, with other independently observed link/name/VML/comment/
printer/shared-expression blockers. No claim is made that every newly reported
workbook has that first rejection. No private workbook was inspected or modified
during this cycle.

- WorkbookWorkspace.cs independently attempts inspection only after known
  editable-admission rejections. The actual inspection session becomes active,
  without flag conversion or mutable model. Returned findings retain the exact
  first code/message. Failed/cancelled attempts preserve the previous project;
  format/location/access/busy errors and cancellation do not trigger fallback.
  Copy creation now also denies noneditable sessions at the application boundary.
- WorkbookContracts.cs / BridgeSession.cs / bridge/client.ts preserve bounded
  structured code/message Details for both failed attempts. Existing origins,
  payloads, revisions, native path privacy and operation allowlists are unchanged.
- PackageGuard.cs identifies the detected external link/active/binary/VML/printer/
  image/protection feature and schema error ID. Advanced findings accumulate the
  exact detected feature kinds. No admission count, parser or preservation check
  was relaxed. The old EDIT_SCOPE text now accurately describes narrow existing
  operations and separately authorized new-file Save As.
- WorkbookInspection.cs distinguishes metadata XML/compression/file failures.
  Existing independent admission and per-sheet budgets, caches and denied mutable
  methods remain. Metadata success does not claim every tab is browsable.
- WorkbookViewer.tsx / styles.css display inspection-only project status and
  collapsed technical Details, distinguish automatic rejection from explicitly
  unevaluated Inspect, retain failed-opening codes/details, identify noneditable
  projects in the sidebar, and wrap long codes at narrow widths. Copy/Save As and
  receipt mapping/production insertion/output gates remain. Removed stale UI advice
  to repair/simplify a workbook as an opening workaround.
- New artificial AccountingCompatibilityFixture.cs, WorkbookProjectOpeningTests.cs
  and WorkbookOpeningSmokeTests.cs reproduce cells/tabs/links/link parts/VML/media/
  printer/protection/advanced and malformed cases. WorkbookWorkspaceTests.cs adds
  non-retry/cancellation tests. Existing suites and native-selector test seam remain.
  DesktopSmokeTests becomes partial solely to keep the new hosted test separate.
  New fixture compile links added to workbook/desktop test projects; no dependency
  version/lockfile changed.
- docs/development/workbook-project-opening.md contains the opening-path comparison,
  error/capability matrix and missing accounting-editing requirements. Analyzer docs
  and PLANS link the current behavior. Working copies, edits, Merge, new-file Save
  As, receipt insertion, source/existing-file overwrite and unknown content retain
  their independent restrictions. No original content was stripped or simplified.

### Executed verification

Evidence: artifacts/workbook-opening/; TRX counters were inspected, not inferred
from build exit status. Screenshots are under screenshots/ and desktop test output;
desktop and narrow inspection notices/Details were visually inspected. No tests
were removed, disabled or skipped; existing preservation tests remain authoritative.

| Check | Actual result |
| --- | --- |
| Initial focused workbook-opening suite | 11 passed, zero failed/skipped; focused/ |
| Initial native complete solution after fallback integration | Application 603 + workbooks 457 + desktop 24 = 1,084 passed, zero failed/skipped; solution-native/ |
| Final application and workbook suites after non-retry tests/diagnostic changes | 608 + 457 = 1,065 passed, zero failed/skipped; final/ |
| Final desktop suite after narrow-width fix | 23 passed, one existing receipt drawer Escape/focus timeout, zero skipped; desktop-final/; new actual workbook opening/navigation/switching/authorization/Details regression passed |
| Unchanged failed receipt test, isolated --no-build rerun | 1 passed, zero failed/skipped; receipt-recheck/; all 24 desktop cases have passing executions on final code |
| Final frontend suite | 81 tests across ten files passed, zero failed; frontend-final.txt |
| Final frontend typecheck/production assets and WPF/test builds | Passed through normal desktop test build; no validation bypass or test-only product capability |
| Source/fixture preservation and protected-input diff | Synthetic hashes unchanged across open/read/switch/copy denial/save denial/close; tracked workbook/media/lock inputs unchanged |
| git diff --check | Passed, with existing LF/CRLF normalization notices only |

Development failures retained: first frontend run had two new selector assertions
fail because diagnostics also appear in the existing report and code/message text
is split by markup. Assertions now scope to the specific Details element, retaining
the same expected content. Initial desktop compile used the wrong positional
selector parameter; named saveSelector fixed it. Sandboxed workbook full run had
45 native directory-handle access-denied failures (412 passes); sandboxed WebView2
crashed before startup. Authorized native execution passes those checks. A later
hosted opening assertion raced the asynchronous sidebar effect; it now waits for
the exact new project name as well as the matching rejection and enabled worksheet.
Visual review found clipped long codes at 390px despite no page overflow; flex child
min-width/wrapping and a notice-content bounds assertion fix that defect. Final
desktop full run had the unrelated existing drawer-focus timeout listed above;
the unchanged isolated rerun passes. These failed runs remain in the evidence.
No test expectation or preservation gate was weakened to obtain admission.

Unexecuted: physical native-file-dialog interaction, private-workbook mutation or
production edit qualification, accounting-owner acceptance, native Excel full
appearance/calculation proof, hostile corpus exhaustiveness and clean-machine
offline deployment. The latest complete desktop run is not represented as a
zero-failure run; its passing isolated retry is recorded separately.

### Resumed offline OCR milestone

After the workbook checks, resumed dependency qualification under
plans/phase-04-offline-ocr-qualification.md. Targeted PATH/default-install/repository
inventory found no Tesseract or traineddata payload. py --list-paths finds Store
Python; native read-only execution confirms **Python 3.12.10 x64**. Sandbox launch
of python/py was denied; native launcher execution succeeded. This developer
installation is not a bundled reproducible runtime.

Rechecked primary upstream engine/model documentation and licenses. The resumed
plan requires immutable Windows runtime/native DLL/Python/model hashes/notices,
genuine synthetic recognition and source-bound unverified drafts before enabling
OCR. No runtime/model was downloaded, installed or recognized text fabricated.
Recognition, packaging and clean-machine qualification remain incomplete. OCR is
still unavailable, but the temporary workbook-fix pause is lifted. Production
receipt insertion/export and complex-accounting editing remain gated. No commit,
push or deployment occurred.

## Cycle 17 — 2026-11-03 — scrolling, hover and genuine source rendering

Read AGENTS.md, GOALS.md, current PLANS/progress, accepted UI design/session/bridge
contracts and relevant tests. Preserved all dirty Cycle 14–16 work. Recorded
plans/phase-04-rendering-and-scrolling.md before the architecture extension. No
broad UI redesign, original workbook write, commit/push/deployment or receipt-output
policy change. Implemented scrolling first, verified it, then connected native
PDF/image rendering. The user's additional Open workbook hover defect is fixed.

### Delivered and changed files

- styles.css / App.tsx: every task canvas permits vertical overflow; bounded grid
  retains intentional cell scrolling. Expanded details/editors remain in canvas
  flow. Header/footer do not shrink across last actions. Main/sidebar accept native
  keyboard focus. No wheel handlers or new JavaScript scrolling implementation.
  Primary hover explicitly retains foreground/background/border so the generic
  hover rule cannot make Open workbook text disappear.
- New src/TsekSheet.Renderer project (solution/build/test asset wiring included):
  one-shot Windows.Data.Pdf/Windows imaging worker receives bytes over pipes, returns
  real bounded PNG/page metadata, and never receives original paths or writes files.
  SDK targeting pack 10.0.19041.34 is pinned; native decoders are OS-supplied. Actual
  generated PDF/PNG/JPEG fixtures render. Parsed PDF page navigation and enlargement
  are available in the existing Source inspector, independently of review association.
- New Application/Documents/DocumentRendering.cs: path-free contract and supervised
  process. At most one worker per bridge, a 512 MiB/one-process job assigned before
  sending bytes, bounded stdin/stdout/stderr, 20-second timeout, cancellation, process
  cleanup and PNG signature/dimension checks. No shell/network/model download.
- WindowsDocumentReader.cs / LocalDocumentSources.cs: capture bytes and identity/hash
  from the same validated native handle, rejecting unavailable/changed registrations.
  DesktopReceiptReview.cs checks exact registration before/after render and retains
  review/value/source versions and all existing verification rules. Render output
  cannot authorize mapping or workbook changes.
- BridgeSession.cs / MainWindow.xaml.cs: native renderer composition, sourceRendering
  capability, strict receipt.render/cancelRender commands; explicit cancel waits for
  worker cleanup, close/lifetime cancel in-flight work. Existing command restrictions
  and original/source privacy checks remain.
- New SourcePreview.tsx / SourcePreview.test.tsx, receipt/client bridge types and
  App/WorkbookViewer/ReceiptReview props: real source image, page navigation, render
  errors/cancel/retry, enlargement, generation/version checks, stale image/page-count
  removal and blob revocation. CSP permits image blobs only; remote/frame/object
  restrictions remain. Rendering never seeds sample values or verifies a field.
- Tests: SourceRenderingTests.cs (new), DocumentRenderingTests.cs (new), hosted
  overflow/native rendering/hover tests and frontend source-view tests. Existing
  rapid-scroll test now synchronizes on admitted requests instead of a fixed delay,
  and uses the same 5-second async observation limit as other workbook tests.
- PLANS.md and docs/development/local-document-rendering.md: functional priority,
  implementation, bounds, licensing/offline installation review and OCR handoff.

### Actual checks

Evidence: artifacts/rendering/; screenshots under screenshots/ and desktop test
output. Native source/image and hover screenshots were visually inspected. No
backend-only test is treated as proof of desktop integration.

| Check | Actual result |
| --- | --- |
| Hosted scroll milestone, before enabling renderer | 1 passed (scroll-corrected). Last visible actions reached for Overview/Receipts/Worksheets/Settings at 1440×900, 1366×768, 1280×720, 1024×768, 390×768 and 1280×600 at 100%/125%, with details expanded. Native PageDown and wheel passed. |
| Final extended hosted scroll/native UI checks | 2 passed (hosted-final). Added actual scrollbar drag and End-key bottom reachability. Real PDF page viewing/navigation/enlargement and Open workbook pointer-hover contrast >=4.5:1 pass in light/dark. |
| Source rendering authority checks | 4 passed (source-final): unchanged/changed/closed/cancelled source scenarios; fields remain draft and original bytes unchanged unless deliberately modified by the synthetic test. |
| First complete integration-boundary solution run | 1,071 passed: application 603, workbooks 446, desktop 22; zero failed/skipped. TRX counters inspected. Full frontend install/typecheck/build and solution build passed. |
| Final focused native suite after cleanup/registration hardening and adding request-bounds test | 7 passed (focused-final): five worker cases including PNG/JPEG, PDF pages, malformed/missing/cancel/timeout/request-limit cases, plus scroll and hosted source/hover tests. No failures/skips. |
| Final hosted rerun after clearing stale preview page metadata | 1 passed (preview-final), rebuilding final app/frontend. |
| Final frontend suite | 79 tests across ten files passed, zero failed; frontend-final.txt retained. Typecheck passed. Focused scrolling/source UI checks also passed (11 tests). No test overlapped npm clean install. |
| Protected input diff / git diff --check | Existing tracked workbook/media/branding/lock inputs unchanged; diff check passed with LF/CRLF notices only. New renderer project lockfile is separate. |

Development failures: the first scroll harness clicked Start before selecting the
expected synthetic profile/readiness; selecting explicitly fixed the fixture. A
sandbox restore could not reach NuGet and used a different global cache; restoring
the pinned SDK from the existing local cache under the build user's context fixed
it without downloading a runtime. Worker project reference initially rejected the
higher Windows target/runtime-pack resolution; it now builds as a separate executable
reference without API assembly linkage, with matching restored SDK assets. First
worker PDF test incorrectly expected PDF points rather than Windows DIPs (three
passes/one assertion failure); expected pixel dimensions were corrected, not product
limits. Two later full frontend runs had one timing failure each in the existing
rapid-scroll test (78 passed): fixed-time admission and one-second initial async
observation were unreliable under load. Causal request synchronization and the
existing workbook-test timeout convention resolved this; final full suite passes.
No test removed, disabled or skipped. Failed TRX evidence is retained.

### Bounds, dependency evaluation and handoff

Limits: 32 MiB input; up to 1000 PDF pages; bounded page geometry; up to 20M source
image pixels; preview longest side 1600px; PNG output 12 MiB; 20-second job. Input,
output and page versions are checked. Resource job containment is not claimed to be
an AppContainer sandbox. Images remain observations; all later verification/mapping
commands still freshly check source authority. No temporary document/image files.

Windows SDK/license/redist and C#/WinRT sources are linked in the rendering report.
Development build uses local targeting pack/projection assemblies and OS-native
code. External redistribution/legal approval, complete installer notices, SDK
servicing/vulnerability audit, self-contained worker packaging and clean-machine
offline installation are not claimed complete. No publish/deployment executed.
Physical trackpad/monitor-DPI transitions and a broad encrypted/hostile PDF corpus
were not exercised; synthesized wheel, scrollbar and keyboard paths were tested.

This execution ends with scrolling/hover and a genuine rendered-source vertical
slice implemented and verified, not MVP completion. OCR remains unavailable.
Microsoft documents Windows.Media.Ocr package-identity/language-pack requirements;
this app is unpackaged, so it was not enabled through unsupported calls. Next is
pinned distributable Tesseract/native/Python/runtime/model qualification and actual
recognition feeding unverified source-linked drafts. Preserve manual corrections,
renewed verification and all candidate/preservation/explicit-approval gates. Do not
resume cosmetic redesign or isolated backend abstractions as the default priority.

## Cycle 16 — 2026-11-03 — workbook-centered workspace

Implemented the corrected product model: a selected workbook is the project.
Read the pasted workbook-centered request, project instructions/goals/plans/progress,
prior design report, relevant session/bridge/frontend architecture and source.
Preserved the complete dirty Cycle 14/15 tree. Recorded focused implementation plan
in docs/design/workbook-workspace.md before code edits. Backend services and
contracts, accounting rules, authorization and workbook/source preservation policies
were not changed in this cycle.

### Changed presentation and navigation

- App.tsx / ui/Workspace.tsx: single-workbook project picker and active project name,
  Overview/Receipts/Worksheets context, Settings → Appearance/Troubleshooting. Native
  workbook selection uses the existing viewer's guards. No recent-path persistence,
  additional workbook sessions or duplicated workbook viewer. Live review/copy state
  stays in the same mounted instances. Long names are clamped with full title text.
- WorkbookViewer.tsx: publishes path-free display context and accepts sidebar open
  intent; retains existing pending-copy/section-review guards. A live receipt blocks
  UI workbook replacement/close until the receipt is explicitly closed, retaining
  its drafts. This is a presentation safeguard, not new server-side authorization.
  Overview reflects live workbook context; Worksheets remains available before
  selection for validated open or read-only inspection. Existing independent-copy,
  edit/merge/separate-output functions retain their original restrictions.
- ReceiptReview.tsx: one Source → Fields → Destination → Review flow. Step changes
  retain the same live review and unsaved field state. Destination uses the same
  active workbook/copy/worksheet selectors; Review retains mapped literals, proposed
  rows, duplicate warnings and required choices. Source/rendering status is compact;
  full hashes/page/configuration details are expandable. Typed verification and all
  source/destination invalidation remain in existing application services.
- Shared Notice pattern: labelled symbols and semantic information/success/warning/
  error/unavailable styles. Color alone never establishes state. Unavailable OCR,
  rendering and receipt output are explicit. Unsaved preview attempts and duplicates
  have warning hierarchy; operation/source failures have error hierarchy.
- styles.css: compact workbook toolbar/selectors, main grid with bounded scroll
  ownership, collapsed package/worksheet/section/planning diagnostics, larger visible
  branding crop from the unmodified approved SVG, laptop layouts and legible secondary
  text. No new framework/dependency. The SVG's 1500-square canvas and transformed
  embedded image explain its previously tiny visible mark; only CSS framing changed.
- App.test.tsx, Connection.test.tsx, ReceiptReview.test.tsx and DesktopSmokeTests.cs:
  contextual navigation, settings diagnostics, four-step draft retention, native
  project selection, source guards, visible keyboard focus, long filenames, explicit
  step readiness, responsive grid bounds and WebView2 zoom. Existing tests retained.
- PLANS.md, docs/design/workbook-workspace.md and workspace-redesign.md: current
  priority, audit, implementation report, before/after review and remaining limits.

No automatic commits/push/deployment, original workbook changes, fake extraction,
new persistence or enabled production receipt insertion/export. Existing local
sources, working copies and verification authorities are preserved across navigation.

### Actual verification and evidence

Evidence: artifacts/workbook-ux/. Baseline actual hosted test ran before source
changes with --no-build; before/screenshots retains the prior active-workbook
captures. after/ contains actual hosted receipt images in both themes at
1366×768, 1280×720, 1440×900, 1024×768 and 390×768, worksheet images at those sizes
and a 125% WebView2 zoom image. Multiple laptop/desktop/scaled views were inspected.
Source placeholders/sidebar/toolbar were compacted after visual review. Cell scroll
stays in the bounded grid; opening secondary technical reports may scroll that
secondary body. Higher zoom shows fewer rows, accurately, without claiming more data.

| Check | Executed result |
| --- | --- |
| Baseline hosted manual receipt test, Release --no-build --no-restore | 1 passed; before TRX retained. |
| Final dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/workbook-ux/solution-final | 1,061 passed: application 599, workbooks 446, desktop 16; zero failed/skipped. All counters inspected. Frontend install/typecheck/production build and WPF build succeeded. |
| Corrected full desktop regression run | 16 passed; desktop-corrected TRX retained. |
| Final focused hosted acceptance after typography/initial overview cleanup | 1 passed; acceptance-final TRX retained. Rebuilt final executable/frontend. |
| Frontend typecheck and final npm.cmd run test -- --run | Passed; 76 tests across nine files passed. frontend-final.txt retained. No test overlapped dependency installation. |
| Actual hosted manual workflow | Passed workbook selection, source registration, field save/verification/correction, step navigation/draft retention, destination context, proposed rows/duplicates, source-change rejection and source release. Switching workbook with an active receipt is blocked and keeps its draft. |
| Laptop/theme/keyboard/zoom acceptance | Passed page-overflow and visible-grid/pane bounds at requested sizes, explicit light/dark themes, browser Tab focus, drawer Escape/focus recovery, long source/workbook names, and no-page-overflow at 125% WebView2 zoom. Captures retained. |
| Source design lint / browser inspector | Final zero failures/warnings. Inspector reports 11px minimum secondary text; actual hosted views establish workflow acceptance. |
| Protected input diff and git diff --check | Tracked workbook/media/SVG/lock files unchanged. Original and generated selected-workbook/source hashes passed in hosted tests, except the deliberate artificial source-change scenario. Diff check passes with LF/CRLF notices only. |

Development failures: one leftover legacy Activity string failed TypeScript checking
and was corrected. First focused destination assertion ran before the parent step
update; later a disabled step click during in-flight field verification was ignored.
The harness now waits for enabled controls and the exact current step before use.
First full solution run had application/workbooks passing and 13 desktop passes/
three failures: a grid border-box measurement, an obsolete validated-open assumption
for an inspection fixture, and timing/visibility of secondary planning tools.
Helpers now choose the appropriate entry point, disclose tools and measure the
visible grid/pane bounds; final desktop suites pass. One new frontend test queried
an adjacent number/label with the wrong accessible name (75 passes/one failure);
step controls now have explicit labels and all 76 pass. Design lint initially
flagged a check glyph; it was replaced by a plain status symbol. Inspector initially
flagged 10px secondary text; it is now at least 11px with sentence-case labels.
No checks or tests were disabled. No failures remain in final executed suites.

Unexecuted: physical monitor-DPI transitions/multi-monitor acceptance (content zoom
is not that test), physical native-file-picker usability, accounting-owner signoff,
real receipt OCR/rendering and clean-machine installation/full MVP acceptance.
The native WPF title bar remains Windows-controlled. No production receipt output.

### Handoff

The first verified workbook-centered implementation milestone is complete and the
remaining dependency-ready presentation milestones are connected. Continue from
observed workbook/receipt usability issues, with one active project and no invented
recent data. Real renderer/OCR qualification and accounting-owner compatibility/
preservation acceptance remain open. Do not return to global Placement/Diagnostics
navigation or isolated backend abstraction work as the default priority. MVP is
not claimed complete.

## Cycle 15 — 2026-11-03 — task-based desktop UI/UX

Implemented the first redesigned workspace under the user's new UI/UX priority.
Read the pasted design request, project instructions/goals/current plans/progress,
architecture, existing host/frontend and tests. Preserved all uncommitted Cycle 14
work. No backend services, contracts, authorization, source verification or workbook
preservation policies changed during this redesign. Audit and implementation plan
were recorded before changes in docs/design/workspace-redesign.md.

### Delivered

- App.tsx: compact task sidebar, contextual header, dedicated Receipts/Workbook/
  Placement/Diagnostics tasks, persistent connection status, session-only appearance
  selection and responsive nonmodal menu. Navigation resets canvas scroll and keeps
  live component instances mounted. Escape restores menu-trigger focus.
- ui/Workspace.tsx (new): reusable accessible Navigation, StatusBadge and EmptyState.
  Semantic tokens and shared native control/alert/confirmation styling in styles.css
  replace scattered light-only colors. Existing specialist components are reused.
- WorkbookViewer.tsx: separates workbook exploration from receipt intake; placement
  retains destination workbook/sheet/copy controls without the large cell grid.
  Original admission, working-copy, merge, Save As and discard behavior is retained.
- ReceiptReview.tsx: source inspector beside fields, visible manual verification and
  unsaved status, expandable source metadata/fingerprint, clear unavailable rendering,
  links between tasks, mapped-value table and retained duplicate/unresolved warnings.
  Narrow inspector omits the unavailable-image block and keeps the source name/details.
  Existing correction/source/destination invalidation logic remains unchanged.
- App.test.tsx, Connection.test.tsx and DesktopSmokeTests.cs: update obsolete welcome/
  diagnostics-location assumptions and exercise task navigation/appearance, actual
  browser Tab focus, menu Escape recovery, draft retention, light/dark resizing,
  source invalidation and complete manual receipt placement. Existing tests retained.
- PLANS.md and docs/design/workspace-redesign.md: UI-first priority, design decisions,
  audit, before/after review, acceptance and remaining limitations.

No new dependencies, mock extraction, original workbook writes, commits, pushes or
deployment. Production receipt insertion/export stays disabled. Existing synthetic
profiles remain explicitly labelled. No receipt/workbook data or theme preference
is persisted in browser storage.

### Executed checks

Evidence directory: artifacts/ui-redesign/. Before screenshots were captured from
an actual --no-build WPF/WebView2 receipt test, before changing the frontend. After
captures are in after/: placement at 390/768/1100, receipts at 390/768/1440 in both
light/dark, and workbook in dark. Desktop, narrow and medium captures were visually
inspected and compared; narrow source information was compacted after that review.

| Check | Actual result |
| --- | --- |
| Baseline hosted receipt test, Release --no-build --no-restore | 1 passed; before TRX and screenshots retained. |
| dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/ui-redesign/solution | 1,061 passed: application 599, workbooks 446, desktop 16; zero failures/skips. TRX counters inspected. Included successful frontend install/typecheck/build and host build. |
| Full desktop rerun after focus/theme/draft tests, Release --no-restore | 16 passed, zero failed/skipped; desktop-final TRX. |
| Final focused hosted receipt test after final responsive source-inspector change | 1 passed, zero failed/skipped; acceptance-responsive TRX. Final bundled frontend/host rebuilt successfully. |
| npm.cmd run typecheck and final npm.cmd run test -- --run | Typecheck passed; 75 tests across nine files passed. frontend-final.txt retained. No test overlapped dependency installation. |
| Design lint | Zero failures/warnings in App, ReceiptReview and shared workspace primitives. |
| Browser inspector, local bundled build | Zero failures. Initial sidebar caption contrast warning fixed through shared muted token. Final two whitespace warnings are intentional disconnected-host empty space; no fake receipt data added to fill it. Report/screenshots under .taste-skill/inspect. |
| Native keyboard/responsive acceptance | Tab focuses a field action with visible outline; narrow menu opens, Escape closes/restores trigger focus; task changes retain unsaved draft; light/dark color schemes and no-page-overflow checks pass. Actual receipt/source-change flow and source/workbook hashes pass. |
| Protected inputs and git diff --check | Tracked workbook/media/lock inputs unchanged; diff check passes with LF/CRLF notices only. |

Development failures: initial script-generated JSX had syntax errors, repaired before
execution. First frontend run: four obsolete welcome/diagnostics-location assertions
failed (70 passed). Next run: four navigation-name queries failed due to adjacent
caption text (71 passed); navigation now has explicit accessible labels and tests
use those names. Final frontend run passes all 75. Additional drawer acceptance
first failed because programmatic click did not focus the menu trigger; harness
now models actual trigger focus and waits for React state before asserting Escape
recovery. acceptance-confirmed and acceptance-responsive pass. No tests disabled.

Unexecuted: physical file-picker/manual accessibility signoff, accounting-owner
usability review, live Windows OS-theme switching, clean-machine installer and OCR/
rendering/full-MVP acceptance. Native WPF title bar remains Windows-controlled.
System theme uses CSS preference support; explicit light/dark modes were tested.

### Handoff

UI-first priority is current. The first complete redesigned desktop workspace is
built and verified. Next work should address observed workflow usability, real
source renderer/OCR qualification and owner-validated accounting mappings; do not
resume isolated backend abstractions by default. Backend/output safety gates from
Cycle 14 remain open. No claim of full MVP completion or accounting acceptance.

## Cycle 14 — 2026-11-03 — desktop integration first

Completed the first usable desktop vertical slice and continued through the
source-linked mapping/placement milestones. Starting HEAD `688b3c0`; starting
working tree was clean. SDK 10.0.403; Node v24.22.1. Read the project instructions,
goals, roadmap, architecture/ADRs and existing application, host, frontend and tests.
Recorded `plans/phase-04-desktop-review-integration.md` before implementation and
made integration-first work the current priority in PLANS.md. Historical cycle
entries below are retained; their next-headless-task ordering is superseded.

### Audit and scope

| Capability at start | Desktop access / integration decision |
| --- | --- |
| WPF/WebView2 trusted-origin bridge and bundled React | Working infrastructure reused; origin, envelope, size, replay and command restrictions retained. |
| Workbook viewer, native open/inspection, navigation, copies, narrow cell editing/merge/Save As | Already exposed; retained existing admission and save restrictions. |
| Read-only SectionReview and synthetic PayablesProcessing | Already exposed; retained. Existing workflow.profiles, workbook.planRecords and candidate metadata commands were reused/left intact. |
| LocalDocumentSources and SourceLinkedFieldReview | Headless at start; now connected through page-owned application authority, native PDF/PNG/JPEG selection and manual review UI. |
| Verified mapping and placement | Headless at start; now exposed for one live source-linked record and the explicitly selected validated workbook/sheet or independent working copy. |
| Bounded batches, structural preflight and verified candidate lifecycle | Remain headless. Receipt Apply/output is unsafe until adoption/preservation/authorization gates are satisfied; no disconnected metadata approval UI was added. |
| OCR, PDF/image decoder/rendering and process supervision | No completed production adapter/runtime/model distribution to connect. UI explicitly reports unavailable OCR/rendering and supports genuine manual entry. |

### Implemented behavior and changed files

- `src/TsekSheet.Application/Documents/DesktopReceiptReview.cs` (new): page-owned,
  single-source/manual-review coordinator using the existing native registry and
  private SourceLinkedFieldReview. Paths enter only through the native selector.
  Keeps exact review/value versions and source/page associations; edits, explicit
  verification and retraction use server-retained field/source bindings. No caller
  verification flags or replacement sources are accepted. Close releases even an
  invalidated registration; disposal releases all state. Returned state is an
  observation, not source or mutation authority.
- `src/TsekSheet.Application/Foundation/BridgeSession.cs`: adds receipt capability
  advertisement and strict receipt.select/state/start/correct/verify/retract/close/
  preview commands, path-free results and structured source/review errors. Empty
  selection cancels without creating a receipt. Existing commands remain intact.
- `src/TsekSheet.Desktop/MainWindow.xaml.cs`: native PDF/PNG/JPEG picker with the
  existing native-dialog exclusion; an injected native selector seam supports
  hosted tests. Composes receipt selection into the existing page bridge.
- `src/TsekSheet.Application/Verification/SourceLinkedFieldReview.cs`: adds
  PreviewPlacement inside the existing serialized source-checking boundary. Reuses
  typed projection, deterministic mapping and the pure planner; source checks
  bracket analysis/planning and final destination checks follow source I/O.
- `src/TsekSheet.Application/Workbooks/WorkbookWorkspace.cs`: serialized read-only
  receipt preview against a page-owned exact session. Inspection sessions reject.
- `src/TsekSheet.Frontend/src/ReceiptReview.tsx` and `bridge/receipts.ts` (new),
  `bridge/client.ts`, `App.tsx`, `WorkbookViewer.tsx`, `styles.css`: source name,
  format/size/hash, explicitly synthetic profile selection, PDF page association,
  manual field entry/save/verify/retract, source-invalidated and unknown-outcome
  recovery, mapped columns, section choices, similar entity information, duplicate
  warnings and proposed rows. Corrections remove verification; unsaved edits hide
  previews and cannot be verified. Saving one field preserves other unsaved drafts.
  An optional field cleared after entry can be explicitly verified as absent.
  Preview display is invalidated by destination/choice/value changes. No Apply or
  receipt output command/control exists. No browser persistence or sample receipts.
- `tests/TsekSheet.Application.Tests/DesktopReceiptBridgeTests.cs` (new),
  `SourceLinkedMappingTests.cs`, `tests/TsekSheet.Workbooks.Tests/VerifiedMappingWorkbookTests.cs`,
  `tests/TsekSheet.Desktop.Tests/DesktopSmokeTests.cs`, and frontend
  `ReceiptReview.test.tsx` (new): bridge/security/lifetime/correction/source-change
  tests, source-linked placement for both profiles, unchanged workbook cells,
  formulas/formatting, clean/dirty independent copies and source hashes; actual
  WPF/WebView2/React receipt flow and responsive screenshots.

Native source registration still proves bounded identity/content observations and
signature triage only. Tests use artificial signature-bearing document bytes and
generated XLSX files. PDF ordinals are associations, not parsed-page proof. Manual
verification is explicitly human verification of entered data, never OCR proof.
Profiles remain synthetic compatibility configurations, not owner-approved real
accounting mappings. Reviews are memory-only; closing/reloading loses them, as
stated in the UI. No original/accounting workbook was opened or written. No
dependencies, lockfiles, mutation gates, candidate adoption or save policy changed.

### Executed verification

Evidence is under ignored `artifacts/desktop-integration/`; desktop screenshots are
under `tests/TsekSheet.Desktop.Tests/bin/Release/net10.0-windows/receipt-review-*.png`.

| Check | Actual result |
| --- | --- |
| Application build, Release --no-restore | Passed, zero warnings/errors. |
| Focused application/bridge tests, outside sandbox | 5 passed, zero failed/skipped; focused-native TRX retained. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/desktop-integration/solution-final` | 1,061 passed: application 599, workbooks 446, desktop 16; zero failed/skipped. All three TRX counter sets inspected. Solution build includes clean frontend dependency install, typecheck, production build and WPF build. |
| Final focused hosted desktop rerun after optional-field UI fix, same project/Release/--no-restore with filter FullyQualifiedName~ReceiptManualReview | 1 passed, zero failed/skipped; desktop-confirmed TRX retained. Rebuilt frontend and host successfully. |
| Final frontend typecheck and `npm.cmd run test -- --run` | Typecheck passed; 74 tests across nine files passed, zero failed. frontend-final.txt retained. No test overlapped npm clean install. |
| Actual WPF/WebView2 receipt acceptance | Passed native selector seam → registration → manual fields → explicit verification → workbook selection → mapping/duplicate warning → section choice/proposed row → correction/reverification → changed-source rejection → refresh/close. Source/workbook hashes unchanged before the deliberate artificial document edit; workbook hash unchanged afterward. |
| Hosted viewport checks at 390, 768 and 1100 window widths | Passed no page overflow; screenshots captured. Desktop and narrow screenshots visually inspected. |
| Design source lint | 0 failures/warnings for ReceiptReview.tsx. Standalone browser inspector failed with CDP Page.enable timeout; hosted viewport/screenshot evidence above completed. |
| `git diff --exit-code -- '*.xlsx' '*.xlsm' '*.pdf' '*.png' '*.svg' '*lock*'` | Passed, 126 tracked protected workbook/media/lock files unchanged relative to HEAD. Generated-file hash/cell/revision checks also passed in tests. Historical cycle13 ignored baselines are absent in this checkout, so no claim of rerunning that baseline. |
| `git diff --check` | Passed; LF-to-CRLF notices only. |

Development failures retained/reported: the first focused application run inside
the sandbox had four SOURCE_UNAVAILABLE failures and one pass because native
parent-directory handle observations were denied; the same tests passed outside
the sandbox without weakening checks. The first new frontend test run had two
failures from unsupported jest-dom matchers (71 passed); assertions now use normal
DOM properties. A later frontend run had one ambiguous text query after adding
multiple-unsaved-field coverage (73 passed); it now asserts both statuses. The
first two hosted receipt runs failed because automation typed/asserted before
asynchronous field commands finished; explicit enabled/state waits corrected the
harness. Final hosted and frontend runs pass. No test was disabled or skipped.

Not executed: physical native-dialog acceptance, real receipt decoding/rendering,
OCR/process-failure/offline model acceptance, accounting-owner validation,
clean-machine offline installation, standalone file-commit/evaluation harnesses,
Windows publish and new native Excel preservation captures. No MVP/release claim.

### Handoff and remaining gates

Milestones 1–4 are connected in the desktop for the supported **manual** single-
record workflow. Stop at this execution boundary with a verified usable slice.
MVP remains incomplete. No commit, push, deployment or original overwrite occurred.

Next integration-first work: qualify a bounded local source renderer and OCR
runtime/decoder with redistribution/license evidence and supervised cancellation,
timeout and malformed-result tests, then connect genuine draft extraction to this
same manual review interface without changing explicit verification. Until those
requirements are satisfied, keep unavailable labels and manual entry. Production
receipt output requires the existing structural preservation/adoption gates and
explicit destination/approval checks first; never route a preview through ordinary
cell edits, enable CanApply, or imply that metadata approval authorizes export.
Real accounting profiles/owner acceptance and clean-machine full-flow acceptance
remain open. Do not resume the former chain of isolated headless abstractions as
the default priority.

## Cycle 13 - 2026-11-02

Completed Cycle 12's documented next task: headless live source-linked single-
record mapping preview. Read AGENTS.md, GOALS.md, PLANS.md, progress, architecture/
ADRs, relevant review/mapping/source plans and current code/tests. Preserved the
starting dirty/untracked tree, supervisor files and `adas`. Recorded
`plans/phase-04-source-linked-mapping.md` before implementation. No private or
accounting workbook was opened or written. Workbook tests generate synthetic
XLSX files; source tests generate signature-bearing artificial bytes. Deliberate
external-change tests modify only those generated artificial document files.

Starting evidence: `artifacts/autonomous-cycle13/starting-tracked.diff` and
`starting-status.txt`. All 141 protected baseline files matched at start.
SDK 10.0.403; Node v24.16.0.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/SourceLinkedFieldReview.cs`:
  `PreviewMapping` accepts the exact live review version, workbook session and
  destination/session/sheet/revision/source/profile binding. It keeps the wrapped
  field review private and uses the existing serialized staged-command boundary.
  Fresh source checks bracket complete worksheet analysis and typed projection.
  Destination checks run before capture, after initial source I/O and after final
  source I/O. Final registration/lifetime/cancellation checks reject changes during
  the last destination read. Reentrant commands reject; concurrent corrections
  serialize. Existing deterministic mapping and conservative analysis rules are
  reused. The immutable result retains literal/typed/source/value/review/profile/
  destination provenance, independent columns/section bounds and CanApply=false.
  Source authority failures terminally invalidate the review; cancellation and
  incompatible analysis do not change fields or workbook state. Existing public
  synthetic/proposal APIs and other review commands retain their behavior.
- `tests/TsekSheet.Application.Tests/SourceLinkedMappingTests.cs` (new): 64 cases
  cover both profiles, PDF/PNG/JPEG artificial native inputs, independently shifted
  and permuted sheets, typed decimal/date values, literal formula-looking text,
  optional absence, immutable observations and path-free results. Reject wrong
  destination/profile/review bindings, corrected/retracted/unverified/invalid
  values and incompatible worksheets. Cover timestamp-preserving content edits,
  identical-byte replacement, missing/locked/released/disposed sources, source
  changes during worksheet reads, destination changes during source reads, late
  cancellation/release/disposal and earlier-source release during a later read.
  Test renewed verification after correction, concurrent/reentrant commands,
  unchanged cells/revisions and closed native handles. Cached observations cannot
  replace the privately owned review or revive invalidated authority.
- `tests/TsekSheet.Workbooks.Tests/VerifiedMappingWorkbookTests.cs`: six new live
  source-linked cases cover both profiles on generated XLSX files. Independently
  resolve sheets and clean/dirty working copies, reject stale/foreign copy bindings
  and incompatible headers/boundaries, and reject source release. Compare every
  projected cell, including formulas/formatting and hidden context, copy changes,
  dirty state, revisions, review state and workbook/document source hashes.
- `plans/phase-04-source-linked-mapping.md` (new): implementation plan, implemented
  contract, supported-input/observation limits and next bounded task.
- `PLANS.md`: Cycle 13 outcome and next source-aware single-record placement task;
  all earlier roadmap content retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; all earlier
  cycles retained below.
- `automation/agent-status.txt`: final supervisor decision, written only after the
  progress report and final checks. Other automation files unchanged.

No dependencies, foundational architecture, profile schema, native reader, OCR,
renderer, persistence, temporary document copies, UI/bridge, placement batch,
structural preflight, candidate lifecycle, adoption/integration/export, production
Apply or save policy changed. No workbook manipulation was added to production.

### Commands and actual results

Commands ran from the repository root except the frontend test. .NET commands
used Release and --no-restore. Evidence is retained under the ignored
`artifacts/autonomous-cycle13/` directory. No checks were removed or skipped.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| First `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SourceLinkedMappingTests --logger 'trx;LogFileName=source-mapping.trx' --results-directory artifacts/autonomous-cycle13/focused` | Test compilation failed with CS0411: missing generic exception type on the new assertion helper. Fixed to `Assert.Throws<Exception>`. No tests ran on that invocation. |
| Same focused command after compilation repair | 59 passed, one failed, zero skipped. The new missing-required fixture attempted to verify a blank required value; the existing verification policy correctly rejected it. Failed TRX retained under focused/. Fixed the fixture to assert that rejection and leave it unverified. |
| Focused command with `--results-directory artifacts/autonomous-cycle13/focused-final` after fixture repair and additional race cases | **64 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingWorkbookTests --logger 'trx;LogFileName=mapping-workbooks.trx' --results-directory artifacts/autonomous-cycle13/workbook-focused` | **16 passed**, including six new source-linked cases; zero failed/skipped. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle13/solution-final` | **1,055 passed**: application 594, workbook 446, desktop 15; zero failed/skipped. |
| Same solution command with `--results-directory artifacts/autonomous-cycle13/solution-final-checked` after making the draft test use an actual unverified draft | **1,055 passed** again: application 594, workbook 446, desktop 15; zero failed/skipped. All three final TRX counter sets inspected; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed within the solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after solution completion | **70 tests / eight files passed**, zero failed. Did not overlap dependency installation. |
| Accidental frontend repeat from repository root | Failed with ENOENT because root has no package.json; no tests executed. Corrected to src/TsekSheet.Frontend after final solution completion: **70 tests / eight files passed** again. |
| SHA-256 comparison with artifacts/autonomous-cycle13/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final check; protected-start.json and protected-final.json retained. |
| `git diff --check`; trailing-whitespace scan of this cycle's code/tests/plan and new roadmap/tracker entries | Passed; informational LF-to-CRLF notices only. Existing historical whitespace preserved. |
| Comparison of starting tracked diffs outside PLANS.md | Passed exactly. Initial comparison-script attempts misread UTF-8 and stripped trailing diff context; correcting decoding and preserving context resolved the false mismatches. retained-starting-changes.diff retained; no source edits were needed. |

Bounded repair corrected two new test-code mistakes described above; no product
policy or existing coverage was weakened. Final review also strengthened the draft
case to use a newly created unverified draft rather than a retracted value and
reran the complete solution. No failed tests remain in the final executed suites.

Not executed this cycle: standalone evaluation/file-commit harnesses, standalone
Node toolchain tests, locked restore, Windows publish, native Excel capture,
private-workbook qualification/privacy audit, accounting-owner review, physical/
native-dialog acceptance, clean-machine offline installation, PDF/image decoder
qualification, source-aware downstream placement/candidates and end-to-end OCR.
Historical executions are not current evidence. No release/MVP completion claim.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **70 new .NET cases**, **1,055 total .NET tests**
and **70 frontend tests** pass. The bounded live source-linked mapping task is
complete; useful independent documented MVP work remains.

Next task: first record a focused plan to connect one live source-linked review
to the existing read-only single-record placement planner. Retain private review
ownership, exact live review/profile and destination/session/sheet/revision/source
bindings, source checks around analysis/projection/planning, destination checks
after source I/O and final registration/lifetime/cancellation checks. Reuse the
existing pure planner's explicit section/entity choices, duplicate warnings and
conservative worksheet analysis, with literal/typed/source provenance and every
CanApply=false. Test both profiles, independent synthetic sheets/dirty copies,
renewed verification after correction, stale/foreign bindings, source changes/
release/disposal, cancellation and destination changes during source reads; retain
unchanged originals/cells/revisions. No batches, structural preflight, candidate
preparation/approval/adoption, OCR/tool choice, rendering, UI/bridge, persistence or
workbook writes in that next slice. This is the documented Phase 3/4 placement
prerequisite and needs no accounting-owner decision or routine milestone approval.

Source checks observe checked instants without permanent file locks; changes
restored entirely between observations remain undetectable. PDF ordinals are
requested associations, not parsed/rendered page proof. Cached previews and
snapshots remain historical observations with no live verification, placement
or mutation authority. Artificial Verify commands test policy enforcement, not
actual human receipt inspection or recognition acceptance. Read-only mapping
does not establish writable-target/formula protection or structural preservation.

Later MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 source-linked placement/rendering/local OCR/tool
redistribution/process supervision and Phase 5 clean-machine offline/full-flow
acceptance. Phase 2/3 and MVP remain incomplete. Later manual gates do not block
the next bounded source-aware placement task.

## Cycle 12 - 2026-11-02

Completed Cycle 11's documented next task: headless live source-linked field
review. Read AGENTS.md, GOALS.md, PLANS.md, progress, architecture/ADRs, relevant
source/review/mapping plans and current source, review, mapping and test code.
Preserved the starting dirty/untracked tree, supervisor files and `adas`.
Recorded `plans/phase-04-source-linked-review.md` before implementation.
No private/accounting workbook was opened or written. New file tests use only
generated artificial signature-bearing bytes, without claiming decoder validity.

Starting evidence: `artifacts/autonomous-cycle12/starting-tracked.diff` and
`starting-status.txt`. All 141 protected baseline files matched at start.
SDK 10.0.403; Node v24.16.0.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/SourceLinkedFieldReview.cs` (new):
  privately owns a field review bound to one live registry. Draft/correction
  commands accept exact registered versions and page associations, never raw
  caller evidence or an unwrapped review. Fresh reads bracket creation, explicit
  verification, correction, retraction, validation and typed projection. Commands
  stage changes privately and commit only after final source checks. Cancellation
  rolls back and permits retry; source failures terminally invalidate the review.
  Final registration checks detect an earlier source's release during later reads.
  Wrong commands/new foreign associations do not invalidate unrelated current
  associations. Corrections, including identical values or new source bindings,
  require renewed verification. Commands serialize and reject reentrancy;
  disposal during reads cannot return results. Disposal forgets field state and
  leaves the shared registry to its owner. Immutable display/typed outputs remain
  observations, with no live mapping or mutation authority.
- `src/TsekSheet.Application/Verification/FieldReviewSession.cs`: internal
  staging fork retains exact review/profile identity and revisions, while owning
  independent state/configuration containers. Existing public synthetic/proposal
  review semantics and all mapping APIs remain unchanged.
- `src/TsekSheet.Application/Documents/LocalDocumentSources.cs`: internal
  locked availability check for source-linked reviews, including empty drafts;
  updates the ownership comment. No public registry or native reader behavior
  changed.
- `tests/TsekSheet.Application.Tests/SourceLinkedFieldReviewTests.cs` (new):
  44 cases cover both profiles, PDF/PNG/JPEG artificial inputs, explicit
  verification, absent versus supplied blank fields, literal formula-looking
  text, typed decimal/date/source provenance and immutable observations. Reject
  stale/foreign/forged commands and registrations, invalid pages/types/values,
  timestamp-preserving edits, identical-byte replacement, missing/locked/released/
  disposed sources, and late cancellation/release/disposal. Verify staged rollback,
  terminal invalidation despite restoration/re-registration, multiple sources,
  final-read registration checks, serialized/reentrant commands, shared-registry
  ownership, unchanged source bytes and closed handles. Sources can be observed
  without automatically verifying any field.
- `plans/phase-04-source-linked-review.md` (new): implementation plan recorded
  before code, implemented contract, supported-input limits and next bounded task.
- `PLANS.md`: Cycle 12 outcome and next live source-aware read-only mapping task;
  all historical plans preserved.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; earlier
  cycles retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  the progress tracker and final checks. Other automation files are unchanged.

No dependency, foundational architecture, profile schema, native reader, OCR,
renderer, persistence, temporary document copy, UI/bridge, source-linked mapping,
candidate approval/adoption, structural integration/export, production Apply or
save policy changed. No workbook manipulation was added in this slice.

### Commands and actual results

Commands ran from the repository root except the frontend test. .NET commands
used Release and --no-restore. Evidence is retained under the ignored
`artifacts/autonomous-cycle12/` directory; no checks were removed or skipped.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| `dotnet build src/TsekSheet.Application/TsekSheet.Application.csproj -c Release --no-restore` | Passed, zero warnings/errors. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SourceLinkedFieldReviewTests --logger 'trx;LogFileName=linked-review.trx' --results-directory artifacts/autonomous-cycle12/focused` | **44 passed**, zero failed/skipped, on the first run. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle12/solution-final` | **985 passed**: application 530, workbook 440, desktop 15; zero failed/skipped. All three TRX counter sets inspected; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed within the solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after the solution completed | **70 tests / eight files passed**, zero failed. Did not overlap dependency installation. |
| SHA-256 comparison against artifacts/autonomous-cycle12/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final check; protected-start.json and protected-final.json retained. |
| `git diff --check`; trailing-whitespace scan of this cycle's code/tests/plan and new roadmap/tracker entries | Passed; informational LF-to-CRLF notices only. Existing historical whitespace preserved. |

No development build/test failures occurred, and no bounded repair was needed.

Not executed this cycle: standalone evaluation/file-commit harnesses, standalone
Node toolchain tests, locked restore, Windows publish, native Excel capture,
private-workbook qualification/privacy audit, accounting-owner review, physical/
native-dialog acceptance, clean-machine offline installation, PDF/image decoder
qualification, source-aware downstream mapping and end-to-end OCR. Historical
executions are not current evidence. No release or MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **44 new focused tests**, **985 total .NET
tests** and **70 frontend tests** pass. This bounded live source-linked review
task is complete; useful independent documented MVP work remains.

Next task: first record a focused plan to connect this live source-linked review
to the existing headless read-only single-record mapping preview. Require exact
live review/profile and explicit destination/session/sheet/revision/source
bindings. Recheck registered documents before and after worksheet analysis and
typed projection, then recheck the destination after source I/O. Keep review
ownership private; do not expose/substitute its FieldReviewSession or accept
cached projections as live verification. Reuse deterministic mapping, field
constraints and conservative worksheet analysis. Test both profiles, independent
synthetic sheets/working copies, stale/corrected reviews, source changes/release/
disposal, cancellation and destination changes during reads, with unchanged
originals/cells/revisions. Preserve existing synthetic APIs and CanApply=false.
No placement batches, structural preflight, candidate approval/adoption, OCR tool
choice, rendering, UI/bridge, persistence or workbook writes in this next slice.
This Phase 3/4 mapping prerequisite needs no routine approval or owner decision.

Source checks remain observations at checked instants, with no permanent locks;
changes restored entirely between observations cannot be detected. PDF ordinals
are requested associations (1-10000), not parsed pages or rendered inspection;
images allow page one only. Cached snapshots, including Active status, perform
no I/O and are not current-source authority. A terminal Invalidated review may
display historical verified field statuses but rejects all live commands and
projection. Existing supplied SourceEvidence review APIs remain synthetic/
proposal-only. Artificial Verify commands establish application enforcement,
not actual human receipt inspection, OCR accuracy or end-to-end acceptance.

Later open MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 source-linked mapping/rendering/local OCR/tool
redistribution/process supervision and Phase 5 clean-machine offline/full-flow
acceptance. Phase 2/3 and the MVP remain incomplete. Later manual gates do not
prevent the next bounded source-aware mapping task.

## Cycle 11 - 2026-11-02

Completed Cycle 10's documented next task: bounded headless local document
registration/source association. Read AGENTS.md, GOALS.md, PLANS.md, current
progress, architecture and ADRs, relevant verification plans and existing review,
filesystem and test contracts. Preserved the existing dirty/untracked tree and
supervisor files. Recorded `plans/phase-04-local-source-registration.md` before
implementation. No original/private accounting workbook was opened or written;
source tests generated artificial local PDF/image signature-bearing bytes.

Starting evidence: `artifacts/autonomous-cycle11/starting-tracked.diff` and
`starting-status.txt`. The copied protected baseline matched all 141 files at
start. SDK 10.0.403; Node v24.16.0.

### Completed work and exact changed files

- `src/TsekSheet.Application/Documents/LocalDocumentSources.cs` (new): owns up to
  20 in-memory associations from explicit host file selection. Immutable path-free
  snapshots expose application IDs, exact revision/hash versions, kind, length
  and status. Every evidence issuance reopens and checks all bytes and identity.
  Changed/replaced/missing/locked sources invalidate permanently at a new revision;
  restoring bytes cannot revive old authority. Release at the current version
  frees capacity, including invalidated entries. Cancellation leaves retryable
  state. Session commands are serialized and disposal forgets paths. No document
  bytes, rendered pages or persistent state are retained; no files are deleted.
- `src/TsekSheet.Application/Documents/WindowsDocumentReader.cs` (new): separate
  narrow Windows x64/fixed NTFS adapter. Absolute regular local paths only;
  network/device/stream/alias/reparse paths reject. Native file identity, canonical
  handle paths and creation/write metadata complement SHA-256 and length checks.
  File/ancestor handles exclude writes/deletes only during observation and close
  on success, failure or cancellation. Streaming uses a 64 KiB buffer with a hard
  32 MiB byte ceiling; limits may be lowered. PDF/PNG/JPEG signature triage matches
  extensions; parsing/rendering validity is explicitly unclaimed. Platform errors
  become path-free diagnostics without retaining path-bearing inner exceptions.
- `src/TsekSheet.Application/TsekSheet.Application.csproj`: grants the application
  test assembly internal access for deterministic filesystem/lifecycle seams;
  no dependency or framework changes.
- `tests/TsekSheet.Application.Tests/LocalDocumentSourcesTests.cs` (new): 49 cases
  cover formats, unchanged originals, exact byte/registration bounds, ownership,
  forged/stale versions, content edits with restored timestamps, identical-byte/
  identical-timestamp replacement, missing/locked/unsupported files, metadata and
  signature changes, unsafe paths and a real NTFS junction. Verify terminal
  invalidation, explicit release, disposal, mid-read/late cancellation, final-read
  release/disposal and handle cleanup. Real filesystem tests prove file writes,
  file renames and ancestor renames reject during a read and succeed in obtaining
  exclusive file access afterward. Generated fixtures remain artificial input
  triage tests, not a claim of valid PDF/image decoding or receipt acceptance.
- `plans/phase-04-local-source-registration.md` (new): implementation plan,
  implemented behavior, supported-input limits and consistency boundaries.
- `PLANS.md`: Cycle 11 evidence and the next source-linked field-review task.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; all prior
  cycles retained below.
- `automation/agent-status.txt`: final decision only, written after this tracker
  and final checks. Other automation files and `adas` are preserved.

No foundational architecture, dependencies, profile schema, OCR, rendering,
source-to-review integration, persistence, UI/bridge, candidate adoption,
structural integration/export, production Apply or save policy changed.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET commands used
Release and --no-restore. TRX/protection/summary evidence is retained under the
ignored `artifacts/autonomous-cycle11/` directory.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~LocalDocumentSourcesTests --logger 'trx;LogFileName=sources.trx' --results-directory artifacts/autonomous-cycle11/focused` | **46 passed**, zero failed/skipped. |
| Same focused command with `LogFileName=sources-final.trx` and directory `artifacts/autonomous-cycle11/focused-final` | After adding junction, mid-revalidation cancellation and invalidated-capacity tests: **49 passed**, zero failed/skipped. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle11/solution-final` | **941 passed**: application 486, workbook 440, desktop 15; zero failed/skipped. Inspected all three TRX counters; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed within the solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after solution completion | **70 tests / eight files passed**, zero failed. Did not overlap dependency installation. |
| SHA-256 comparison against artifacts/autonomous-cycle11/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final check; protected-start.json and protected-final.json retained. |
| `git diff --check`; whitespace scan of new cycle code/tests/plan and new roadmap/tracker entries | Passed; informational LF-to-CRLF notices only. Existing historical tracker whitespace retained. |

No failed build/tests or bounded repair was needed; no test was removed, disabled
or skipped.

Not executed this cycle: standalone evaluation/file-commit harnesses, standalone
Node toolchain tests, locked restore, Windows publish, native Excel capture,
private-workbook qualification/privacy audit, accounting-owner review, physical/
native-dialog acceptance, clean-machine offline installation, PDF/image decoder
qualification and end-to-end OCR. Historical executions are not current evidence.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **49 new tests**, **941 total .NET tests** and
**70 frontend tests** pass. This bounded source-registration task is complete;
useful independent documented MVP work remains.

Next task: first record a focused plan for a headless application-owned field-
review boundary connected to live registered sources (GOALS.md and Phase 4).
Bind drafts/corrections to exact live registrations and obtain evidence through
fresh source reads. Do not accept caller-created SourceEvidence as live proof.
Revalidate before and after explicit verification and typed projection; reject
changed/replaced/released/disposed sources, stale/foreign versions and cancellation.
Keep review ownership private, require renewed verification after correction,
and never infer verification from source availability. Test both synthetic
profiles and artificial local files with unchanged originals. Preserve existing
synthetic/proposal-only APIs and all mutation gates. No PDF/OCR tool choice,
rendering, UI/bridge, persistence, temporary copies, candidate adoption or workbook
mutation in this slice. This dependency needs no routine milestone approval.

Source snapshots/evidence are observations at checked instants; no lock survives
a read. Changes restored entirely between observations cannot be detected.
PDF page ordinals are requested associations (1-10000), not parsed page count or
rendered-page proof; images permit page one only. Synchronous local reads offer
cooperative cancellation, not a hard I/O deadline. Existing FieldReviewSession
still accepts supplied synthetic/proposal evidence: source registration does not
authenticate those callers, connect review, or establish human receipt acceptance.

Later open MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 source-linked review/local OCR/tool redistribution/
process supervision and Phase 5 clean-machine offline/full-workflow acceptance.
Phase 2/3 and the MVP remain incomplete. Later manual gates do not prevent the
next bounded source-linked review task.

## Cycle 10 - 2026-11-02

Completed Cycle 9's documented next task: an application-owned live verified
candidate review lifecycle with explicit metadata-only approval. Read AGENTS.md,
GOALS.md, the roadmap/Phase 3 acceptance, architecture/ADRs, relevant verification
plans, oracle/formatting restrictions and current application/workbook contracts
and tests. Preserved existing dirty/untracked work and supervisor files. Recorded
`plans/phase-03-verified-candidate-lifecycle.md` before implementation.

Starting evidence: `artifacts/autonomous-cycle10/starting-tracked.diff` and
`starting-status.txt`. All 141 files in the copied protected baseline matched at
start. SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was
opened or written; workbook writes used newly generated artificial XLSX fixtures.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedCandidateReviewSession.cs`
  (new): constructs only from fresh live candidate preparation, retaining every
  live review/version, explicit copy/parent/base/source/profile binding, ordered
  typed/literal/source provenance and a private deep copy of the engine ticket.
  Approve requires the exact full CandidateReviewApproval command. Foreign
  commands cannot approve or discard this ticket; duplicates need deliberate
  acknowledgment, formatting gates remain, and successful replays reject.
  Live authorities are checked before and after the public metadata-only engine
  operation. Only the approval lifecycle fact may change in its response.
  Stale/cancelled/failed operations discard the exact ticket at the current copy
  revision. Cleanup errors are reported alongside the original error and mark
  the session Invalidated; confirmed cleanup marks it Discarded. Neither state
  permits approval. Explicit Discard also reports cleanup failures. Snapshots
  deep-copy candidate collections; valid engine approval metadata remains visible
  if subsequent validation fails, separately from session acceptance/discard.
  Commands are serialized per session and every CanApply stays false.
- `src/TsekSheet.Application/Verification/VerifiedCandidatePreparation.cs`:
  exposes its existing live authority recheck internally for lifecycle reuse;
  generation behavior and cleanup policy are unchanged.
- `tests/TsekSheet.Application.Tests/VerifiedCandidateReviewSessionTests.cs`
  (new): 59 cases cover both profiles and 1/20 bounds, ordered provenance,
  immutable ownership, every command binding, duplicate/formatting gates,
  foreign/superseded/replayed commands, concurrent approval serialization, every
  review's correction/reverification/retraction before/during approval, copy/
  parent/base/source changes, final-read changes/cancellation, explicit discard,
  response tampering, cleanup failures and replacement-ticket protection.
- `tests/TsekSheet.Application.Tests/VerifiedCandidatePreparationTests.cs`:
  makes the existing context/session reusable internally; extends the fake with
  approval observation/failure/response seams. Existing preparation tests remain
  intact and fake mutation/save operations still fail immediately.
- `tests/TsekSheet.Workbooks.Tests/VerifiedCandidateReviewWorkbookTests.cs`
  (new): 44 cases use both artificial profiles, independent shifted/permuted
  bindings and 1/20 records with the actual public review-only engine contract.
  They prove metadata-only approval with exact hashes/oracle/provenance; existing
  and batch duplicate acknowledgment; rejection of heights 20, 22, 21.95 and 24;
  before/during/after approval changes and cancellation; fresh retry, explicit
  discard, cleanup failure reporting and protection of real replacement tickets.
  Compare cells/formulas/formats, source hashes, revisions, review snapshots,
  sibling isolation and copy changes; deliberately injected edits remain intact.
  Public tickets reject adoption even with experimental flags, and production
  insertion remains disabled.
- `tests/TsekSheet.Workbooks.Tests/VerifiedCandidateWorkbookTests.cs`: makes
  existing synthetic fixture/input/binding/hash/viewport helpers reusable
  internally, without changing their behavior or removing any tests.
- `plans/phase-03-verified-candidate-lifecycle.md` (new): focused implementation
  plan, implemented contract, lifecycle/cleanup distinctions and consistency limits.
- `PLANS.md`: Cycle 10 evidence and next dependency-ready source-registration
  task tied to existing local import/source-linked verification requirements.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; all prior
  cycle entries retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker and final checks. Other automation files and `adas` are preserved.

No foundational architecture, dependencies, profile schema, OCR, source
registration, persistence, UI/bridge, editable admission, candidate adoption,
structural integration/export, production Apply or save policy changed. Engine
approval changes review metadata only; public tickets remain permanently review-only.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET commands used
Release and --no-restore. Ignored TRX/protection/summary evidence is retained in
`artifacts/autonomous-cycle10/`.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedCandidateReviewSessionTests --logger 'trx;LogFileName=lifecycle.trx' --results-directory artifacts/autonomous-cycle10/focused-application` | First invocation failed compilation (CS0246/CS1503), no tests ran. Corrected test type/constructor arguments; identical command then **55 passed**, zero failed/skipped. |
| Same application command with `LogFileName=lifecycle-final.trx` and directory `artifacts/autonomous-cycle10/focused-application-final` | After adding explicit cleanup/status/replay cases: **59 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedCandidateReviewWorkbookTests --logger 'trx;LogFileName=lifecycle-workbooks.trx' --results-directory artifacts/autonomous-cycle10/focused-workbooks` | First invocation failed compilation (CS1061), no tests ran. Corrected CellInfo assertion from Text to Value; identical command then **44 passed**, zero failed/skipped. |
| Same workbook command with `LogFileName=lifecycle-workbooks-final.trx` and directory `artifacts/autonomous-cycle10/focused-workbooks-final` | After distinguishing cleanup failure from confirmed discard and retaining returned approval facts: **44 passed**, zero failed/skipped. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle10/solution-final` | **892 passed**: application 437, workbook 440, desktop 15; zero failed/skipped. All three TRX counters inspected; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed during solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after solution completion | **70 tests / eight files passed**, zero failed. Did not overlap dependency installation. |
| SHA-256 comparison against artifacts/autonomous-cycle10/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final check; protected-start.json and protected-final.json retained. |
| `git diff --check`; trailing-whitespace scan of new/modified lifecycle code, tests and plan | Passed; informational LF-to-CRLF notices only. |

Bounded repairs: initial application test code named a nonexistent record type
and used the wrong DuplicateWarning argument order; corrected to the existing
RecordInsertionProposal/DuplicateWarning contract. Initial workbook test asserted
a nonexistent CellInfo.Text; corrected to Value. Product code was unaffected by
these compilation errors. Review refinement distinguished failed cleanup from
confirmed discard and retained actual returned approval facts after invalidation.
No test was removed, disabled or skipped. Final suites have no failures.

An expanded final whitespace scan including the whole historical tracker found
one whitespace-only line at its historical tail. The final action stopped before
writing status. Retained that existing content, checked the new Cycle 10 entry
separately, and kept the code/test/plan scan and git diff check unchanged.

Not executed this cycle: standalone evaluation/file-commit harnesses, standalone
Node toolchain tests, locked restore, Windows publish, new native Excel capture,
private-workbook qualification/privacy audit, accounting-owner review, physical/
native-dialog acceptance, clean-machine offline installation and end-to-end OCR.
Historical results are not current executions. No release/MVP completion claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **103 new tests**, **892 total .NET tests** and
**70 frontend tests** pass. This bounded metadata-approval task is complete;
useful independent documented MVP work remains.

Next task: first record a focused plan for the headless local document import/
source association boundary required by GOALS.md and Phase 4 source-linked
verification. Add application-owned in-memory source registration from explicitly
selected local files, bounded byte reads and identity/content fingerprint/revision
observations. Revalidate source content before issuing evidence for review;
reject missing/replaced/changed sources. Test artificial generated local image/
PDF files, cancellation/limits and original-byte stability. Keep paths out of
field evidence and diagnostics, follow ADR-0002 lifetimes and document input
limits. No PDF rendering/OCR choice, UI/bridge, persistence, temporary document
copies, candidate adoption or workbook mutation in this slice. Existing supplied
SourceEvidence is still synthetic/proposal evidence until live registration is
explicitly connected to review; never authenticate arbitrary caller identities
or claim actual receipt acceptance. This independent prerequisite needs no
accounting-owner decision or routine milestone approval.

Current review snapshots, including Approved, are observations at checked
versions, not durable write authority or active source checks. There is no lock
across field-review/copy/parent sessions. Later consumers must revalidate live
state; source identities remain supplied evidence rather than authenticated
document bytes. If cleanup fails, the engine may still retain the exact ticket;
the error and Invalidated state explicitly expose that uncertainty. Synthetic
verification does not establish actual human source review or general Excel fidelity.

Later open MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 local OCR/tool redistribution/source-linked review/
process supervision and Phase 5 clean-machine offline/full-workflow acceptance.
Phase 2/3 and the MVP remain incomplete; those later manual gates do not prevent
the next bounded source-registration task.

## Cycle 9 - 2026-11-02

Completed Cycle 8's documented next task: headless review-only candidate
preparation from bounded live verified records. Read AGENTS.md, GOALS.md, the
roadmap/Phase 3 acceptance, architecture/ADRs, relevant verification plans,
structural oracle/compatibility limits, and current application/workbook
contracts and tests. Preserved all prior dirty/untracked work and supervisor
files. Recorded `plans/phase-03-verified-candidate-review.md` before implementation.

Starting evidence: `artifacts/autonomous-cycle9/starting-tracked.diff` and
`starting-status.txt`; the copied protected baseline matched all 141 files at
start. SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was
opened or written; new workbook writes used artificial generated fixtures only.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedCandidatePreparation.cs` (new):
  `Prepare` consumes 1-20 live review/version/choice inputs and explicit copy/
  parent/profile/source/sheet/base bindings. It freezes caller inputs, obtains
  fresh typed placement and calls the existing public review-only engine
  generator. It rechecks every authority before and after generation. Stale
  reviews, changed destinations/copies and cancellation discard the exact new
  ticket at the current copy revision. Cleanup failures, including STALE_REVIEW,
  are reported with the original error in AggregateException; a different ticket
  is never cancelled. Results retain ordered typed/literal/source/value-version
  mappings and actual candidate/manifest/review hashes, oracle findings and
  lifecycle facts. CanApply stays false; no approval/adoption method is called.
- `src/TsekSheet.Application/Verification/VerifiedMappingPreview.cs`: adds an
  internal WorkbookInfo overload for the existing destination validation, so
  candidate binding checks validate their captured identities/revisions directly.
  Existing mapping/placement behavior is retained.
- `tests/TsekSheet.Application.Tests/VerifiedCandidatePreparationTests.cs` (new):
  50 cases cover both profiles, 1/20 bounds, ordered provenance, defensive caller
  input/result ownership, all explicit binding failures, invalid/draft/stale/
  foreign/repeated inputs, every review's correction/retraction during generation,
  copy/parent/base/source changes, final-read changes/cancellation, exact ticket
  cleanup at current revision, replacement-ticket protection and explicit cleanup
  failure reporting. The fake rejects unexpected workbook mutation/save calls.
- `tests/TsekSheet.Workbooks.Tests/VerifiedCandidateWorkbookTests.cs` (new):
  32 cases use artificial XLSX inputs and the public review-only engine contract.
  Both profiles, 1/20 records and independent shifted/permuted sheet bindings
  actually generate/reopen/pass the oracle. Tests retain narrow fixed-21/default-
  15 formatting findings and duplicates; reject dirty copies, divergent parent
  bases, multi-boundary/merged/hidden/unsupported structures; inject oracle
  tampering and cancellation; and prove cleanup/retry. A delegating observation
  seam changes real copy/parent/review state or cancels immediately after the
  real generator returns, proving exact ticket cancellation at current revision.
  Projected cells/formulas/formats, revisions, copy changes, review snapshots,
  sibling isolation and generated-source hashes are compared. Deliberately
  injected edits remain intact; production InsertWorkflowRows still rejects.
- `plans/phase-03-verified-candidate-review.md` (new): focused implementation plan,
  implemented contract, cleanup semantics and consistency/compatibility limits.
- `PLANS.md`: Cycle 9 result and next dependency-ready verified candidate review
  lifecycle task. Historical roadmap and incomplete phase status are retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; prior
  cycles retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker and final checks. Other automation files and `adas` are preserved.

No dependency, foundational architecture, profile schema, source registration,
OCR, persistence, UI/bridge, editable admission, approval/adoption, insertion,
structural integration/export, production Apply or save policy changed. The
existing engine's public tickets remain permanently review-only.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and --no-restore. Ignored TRX/protection/summary evidence is retained
under `artifacts/autonomous-cycle9/`.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedCandidatePreparationTests --logger 'trx;LogFileName=candidate.trx' --results-directory artifacts/autonomous-cycle9/focused-application` | First invocation: compilation failed (CS1061); no tests ran. After correcting the assertion, identical command: **50 passed**, zero failed/skipped. |
| Same application command with `LogFileName=candidate-final.trx` and directory `artifacts/autonomous-cycle9/focused-application-final` | Final focused run after cleanup-reporting refinement: **50 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedCandidateWorkbookTests --logger 'trx;LogFileName=candidate-workbooks.trx' --results-directory artifacts/autonomous-cycle9/focused-workbooks` | Initial real-workbook coverage: **24 passed**, zero failed/skipped. |
| Same workbook command with `LogFileName=candidate-workbooks-final.trx` and directory `artifacts/autonomous-cycle9/focused-workbooks-final` | After adding real post-generation changes/cancellation: **32 passed**, zero failed/skipped. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle9/solution-final` | **789 passed**: application 378, workbook 396, desktop 15; zero failed/skipped. All three TRX counters inspected; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed during solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after desktop completion | **70 tests / eight files passed**, zero failed. No overlap with dependency installation. |
| SHA-256 comparison with artifacts/autonomous-cycle9/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final check; protected-final.json retained. |
| `git diff --check`; trailing-whitespace scan of new plan/code/tests and mapping API | Passed; informational LF-to-CRLF notices only. |

Bounded repair: the first application compilation referred to a nonexistent
SourceEvidence.Region property. Corrected the test assertion to PageNumber;
product code was unaffected. Cleanup review also removed an initial interpretation
of STALE_REVIEW as successful cleanup: it now explicitly reports that failure
with the original error, because the public API cannot distinguish replacement
from another revision race. Replacement protection and both reported errors are
tested. No test was removed, disabled or skipped. Final suites have no failures.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
or privacy audit, accounting-owner review, physical/native-dialog acceptance,
clean-machine offline installation and end-to-end OCR. Historical results are not
current executions. No release or MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **82 new tests**, **789 total .NET tests** and
**70 frontend tests** pass. The selected candidate-preparation task is complete;
useful independent documented Phase 3 work remains.

Next task: first record a focused plan for an application-owned verified
candidate review lifecycle retaining live authorities and exact ticket/hash/
provenance bindings. Add explicit metadata-only review approval using the
existing public review-only engine contract: require a deliberate command for
that exact ticket and explicit duplicate acknowledgment. Revalidate every live
review and copy/parent/base/profile/source binding before and after approval.
Discard this ticket on stale review, destination change or cancellation and
report cleanup failures. Never accept caller-created snapshots as proof or
substitute corrected entities without renewed verification. Test stale/foreign/
replayed approval, changes during approval, duplicates, formatting gates and
cleanup for both synthetic profiles with unchanged source/cells/revisions.
Public tickets must stay permanently review-only. Do not enable adoption,
structural integration/export, production Apply, UI/bridge, OCR, persistence or
broader compatibility. This follows the existing Phase 3 verified preview and
revision lifecycle, without authorizing active-workbook mutation.

Current observations have no cross-session lock or durable write/approval
authority. Any later consuming operation must revalidate live state. Source
identities remain supplied evidence, not authenticated/registered document bytes.
New successful generation retains the existing engine replacement semantics for
older pending reviews; this adapter does not restore replaced tickets. Synthetic
verification does not establish actual receipt review or general Excel fidelity.

Later open MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 local OCR/tool redistribution/source-linked review/
process supervision and Phase 5 clean-machine offline/full-workflow acceptance.
Phase 2/3 and the MVP remain incomplete; later manual gates do not block the next
bounded headless review-lifecycle task.

## Cycle 8 - 2026-11-02

Completed Cycle 7's documented next task: read-only structural preflight of
bounded live verified records. Read the goals, roadmap/Phase 3 acceptance,
architecture/ADRs, prior verification/placement plans, compatibility and oracle
limits, and relevant application/workbook code/tests. Preserved prior dirty/
untracked work and user/supervisor files. Recorded
`plans/phase-03-verified-structural-preflight.md` before implementation.

Starting evidence: `artifacts/autonomous-cycle8/starting-tracked.diff` and
`starting-status.txt`. The copied protected baseline matched all 141 files at
start. SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was
opened or written. Workbook writes used newly generated artificial fixtures only.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedPlacementPreview.cs`:
  `VerifiedPlacement.PreflightMany` accepts 1-20 live review/version/choice inputs
  and one explicit destination. It shares the bounded capture/typed projection/
  worksheet planning path with PreviewMany, adds only read-only structural
  inspection, and rechecks destination identity/source/sheet/revision, every live
  review and cancellation after all diagnostic branches. Ordered mappings retain
  literal/normalized/typed/source/value-version provenance. All CanApply flags
  remain false; existing Preview and PreviewMany remain planning-only.
- `src/TsekSheet.Application/Workflows/WorkflowPlanning.cs`: shares Prepare's
  existing choices/duplicates/structural-inspection precedence in one internal
  helper. Unresolved choices and duplicate warnings never reach inspection.
  Existing raw-input behavior, status meanings and lifecycle gates are retained.
- `tests/TsekSheet.Application.Tests/VerifiedPlacementPreviewTests.cs`: extends
  the existing fake with an optional read-only inspection callback/count. It
  still fails by default on inspection and on all mutation/candidate operations.
- `tests/TsekSheet.Application.Tests/VerifiedStructuralPreflightTests.cs` (new):
  36 cases cover both profiles, 1/20 bounds, ordering/provenance/types/independent
  bindings, lifecycle facts, choice/duplicate precedence, bidirectional batch
  warnings, defensive inputs/results, invalid input before inspection, every
  review's correction/retraction during inspection, destination changes and
  final-read review changes/cancellation across all diagnostic branches.
- `tests/TsekSheet.Workbooks.Tests/VerifiedStructuralPreflightWorkbookTests.cs`
  (new): 14 cases cover both profiles on independent shifted/permuted selected
  sheets, clean/dirty copies, 1/20-record plain passes and rejected multi-boundary,
  merged, hidden-row, unsupported-formula and unselected-sheet-formula fixtures.
  Compare every projected cell, revisions, dirty/copy changes, review snapshots
  and generated-source hashes. Forged executable plans still reject through the
  production InsertWorkflowRows guard. No candidate is generated.
- `plans/phase-03-verified-structural-preflight.md` (new): focused implementation
  plan, implemented contract and scope/consistency/envelope limits.
- `PLANS.md`: Cycle 8 result and next dependency-ready Phase 3 review-only
  candidate-preparation task; prior roadmap and incomplete phase status retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff; all prior
  cycles retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker. Other automation files, `adas` and prior work remain preserved.

No dependency, profile schema, foundational architecture, source registration,
OCR, persistence, UI/bridge, editable admission, candidate generation/approval/
adoption, insertion, integration, export, production Apply or save policy changed.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and --no-restore. Development TRX, protection and summary evidence is
retained under ignored `artifacts/autonomous-cycle8/`.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | 10.0.403; v24.16.0. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedStructuralPreflightTests --logger 'trx;LogFileName=preflight.trx' --results-directory artifacts/autonomous-cycle8/focused-application` | First run: **four failed**, 32 passed, zero skipped. Incorrect fixture expectation explained below. |
| Same application command with `LogFileName=preflight-final.trx` and directory `artifacts/autonomous-cycle8/focused-application-final` | Final: **36 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedStructuralPreflightWorkbookTests --logger 'trx;LogFileName=preflight-workbooks.trx' --results-directory artifacts/autonomous-cycle8/focused-workbooks` | First invocation: compilation failed with CS1061, no tests ran. After correcting the test's method name, identical command: **14 passed**, zero failed/skipped. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle8/solution-final` | **707 passed**: application 328, workbook 364, desktop 15; zero failed/skipped. All three TRX counters inspected; solution-summary.json retained. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed during the solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in src/TsekSheet.Frontend after desktop completion | **70 tests / eight files passed**, zero failed. No overlap with dependency installation. |
| SHA-256 comparison with artifacts/autonomous-cycle8/protected-baseline.json | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final comparison; protected-final.json retained. |
| `git diff --check`; trailing-whitespace scan of new plan/application/workbook test files and placement API | Passed; informational LF-to-CRLF notices only. |

Bounded repairs: the first application run incorrectly expected the reused
fixture's first section to shift by 20 rows; that fixture shifts only its second
section. The corrected assertion retains the first-section row and separately
asserts the second-section shifted subtotal. The first workbook compilation used
the nonexistent ApplyStructuralInsertion method; corrected the test to the
existing InsertWorkflowRows interface guard. No product behavior was relaxed.
Initial failing application TRX is retained. Final suites have no failures/skips;
no tests were removed or disabled.

The existing envelope rejects formulas on unselected sheets. Positive independent
bindings therefore use separate generated workbooks, each with its own shifted/
permuted selected sheet and constant-only context. The negative fixture retains
unselected-sheet formulas and proves rejection. No package content was stripped
to force admission; a plain preflight pass is not preservation/Excel acceptance.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
or privacy audit, actual accounting-owner review, physical/native-dialog
acceptance, clean-machine offline installation and end-to-end OCR. Historical
results are not current executions. No release or MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: 50 new tests, **707 total .NET tests** and
**70 frontend tests** pass. The selected read-only verified preflight task is
complete; useful independent documented Phase 3 work remains.

Next task: first record a focused plan to connect live verified inputs to the
existing review-only candidate preparation on a selected working copy and its
explicit parent destination. Accept 1-20 live reviews/expected versions and
explicit copy/destination/profile/source bindings. Revalidate every authority
after generation and discard the newly generated ticket on stale review,
destination change or cancellation. Retain ordered typed/literal/source provenance
and actual candidate/manifest/review hashes, oracle findings and distinct
lifecycle facts. Test both synthetic profiles, narrow supported formatting,
copy/source isolation and rejected/cancelled cleanup through the existing public
review-only engine contract. Never accept cached preflight as authority or
synthesize approval; do not call approval/adoption, enable structural integration/
export, UI/bridge, OCR, production Apply or broaden compatibility. This exercises
the existing Phase 3 isolated-candidate/reopen/validation requirement without
enabling active-workbook mutation or requiring routine human milestone approval.

Current captures have no cross-session lock or durable write authority. Monotonic
versions bound consistency; later operations must revalidate live state. Source
evidence is supplied identity, not authenticated/registered document bytes.
Synthetic verification does not establish actual human receipt review.

Later open MVP gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 local OCR/tool redistribution/source-linked review/
process supervision and Phase 5 clean-machine offline/full-workflow acceptance.
Phase 2/3 and the MVP remain incomplete; those later gates do not block the next
bounded headless Phase 3 task.

## Cycle 7 - 2026-11-02

Completed Cycle 6's documented next task: bounded verified placement for the
existing planner's 1-20-record input. Read the goals, roadmap/Phase 3 acceptance,
architecture/ADRs, relevant verification/mapping/placement plans, structural
preservation and compatibility limits, and application/workbook code/tests.
Preserved all prior dirty/untracked work and user/supervisor files. Recorded
`plans/phase-03-verified-bounded-placement.md` before implementation; added a
related consistency-repair plan before reproducing/fixing the older mapping gap.

Starting evidence: `artifacts/autonomous-cycle7/starting-tracked.diff` and
`starting-status.txt`. The copied protected baseline matched all 141 files at
start. SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was
opened or written; workbook write tests generated artificial fixtures only.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedPlacementPreview.cs`:
  `VerifiedPlacement.PreviewMany` accepts 1-20 live review/version/choice inputs
  and one explicit destination. It copies caller inputs, projects live typed
  values, rejects repeated validated review identities, analyzes one worksheet
  and invokes only the existing pure planner. Its defensive ordered mappings
  preserve typed/literal/normalized/source/value-revision provenance and link
  each review to its proposal record. Cross-record duplicates and unresolved
  choices remain visible; no record is silently discarded or renamed. All live
  reviews and the destination are rechecked after planning, then cancellation.
  Every CanApply remains false. Single-record placement reuses field conversion.
- `src/TsekSheet.Application/Verification/VerifiedMappingPreview.cs`: extracts
  shared profile/review binding checks without changing their error semantics.
  The existing mapping preview now rechecks the live review and cancellation
  after its final destination read, rejecting corrections/retractions or
  cancellation occurring at that read.
- `tests/TsekSheet.Application.Tests/VerifiedPlacementBatchTests.cs` (new):
  38 cases cover both approved profiles, 1/20 bounds, deterministic input/virtual
  row order, independent shifted/permuted destinations, source and typed/literal
  provenance, existing and bidirectional cross-record duplicates, unresolved and
  invalid choices, correction/renewed verification, defensive observations,
  caller-list changes, missing/repeated/foreign/forged/stale/draft/retracted/
  wrong-profile/invalid-type inputs, invalid destinations, cancellation and
  correction of first/middle/last reviews during capture/final destination reads.
  The fake fails if mutation/candidate/preflight methods are called. Even forged
  executable proposals still reject through the engine interface contract.
- `tests/TsekSheet.Application.Tests/VerifiedPlacementPreviewTests.cs`: exposes
  the existing mutation-rejecting fake internally for reuse by the batch tests;
  all existing single-record tests are retained.
- `tests/TsekSheet.Application.Tests/VerifiedMappingPreviewTests.cs`: three
  regressions reproduce and verify final-read correction/retraction/cancellation
  rejection in the older mapping preview.
- `tests/TsekSheet.Workbooks.Tests/VerifiedMappingWorkbookTests.cs`: two new
  synthetic XLSX cases cover both profiles on independently shifted/reversed
  worksheets and clean/dirty working copies with 3 and 20 records. They retain
  duplicate links, choices, per-record mapping provenance and virtual rows;
  reject stale/foreign/repeated/invalid inputs and forged Apply. Every projected
  cell, copy change list, revision, dirty state and source hash is unchanged.
  The fixtures retain formulas/formatting, merged lower footers, hidden rows and
  a hidden context sheet. Four existing incompatible-sheet cases also exercise
  bounded-preview rejection on main and copy.
- `plans/phase-03-verified-bounded-placement.md` (new): implementation plan,
  contract, test evidence and observation/consistency boundaries.
- `PLANS.md`: Cycle 7 outcome and dependency-ready read-only verified structural
  preflight task; all historical plans and incomplete phase status retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and handoff, with
  all prior cycles retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker. Other automation files, `adas` and prior work remain preserved.

No dependency, schema, foundational architecture, source registration, OCR,
persistence, UI/bridge, editable admission, structural preflight, candidate
generation/approval/adoption, insertion, integration, export, production Apply
or save-authorization policy changed. Raw-input APIs remain proposal-only.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and `--no-restore`. TRX and protection/summary evidence is retained under
ignored `artifacts/autonomous-cycle7/`, solely for development diagnostics.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0`. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~VerifiedPlacementBatchTests\|FullyQualifiedName~VerifiedPlacementPreviewTests\|FullyQualifiedName~VerifiedMappingPreviewTests' --logger 'trx;LogFileName=bounded-placement.trx' --results-directory artifacts/autonomous-cycle7/focused-application` | **99 passed**, zero failed/skipped. Before adding the three mapping consistency regressions. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingWorkbookTests --logger 'trx;LogFileName=bounded-workbooks.trx' --results-directory artifacts/autonomous-cycle7/focused-workbooks` | First run: **two failed**, eight passed, zero skipped; fixture error explained below. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingWorkbookTests --logger 'trx;LogFileName=bounded-workbooks-final.trx' --results-directory artifacts/autonomous-cycle7/focused-workbooks-final` | After the fixture correction: **10 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReviewAndCancellationAreRecheckedAfterFinalDestinationRead --logger 'trx;LogFileName=mapping-before-fix.trx' --results-directory artifacts/autonomous-cycle7/red-mapping` | Before the mapping repair: **three expected regression failures**, zero passed/skipped. The old path returned despite final-read correction/retraction/cancellation. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle7/solution-final` | Final: **657 passed**: application 292, workbook 350, desktop 15; zero failed/skipped. All three TRX counters inspected; `solution-summary.json` retained. Includes all mapping repairs and final workbook/application tests. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed during the solution run; lockfiles unchanged. |
| `npm.cmd run test -- --run` in `src/TsekSheet.Frontend`, after desktop checks completed | **70 tests / eight files passed**, zero failed. No overlap with dependency installation. |
| SHA-256 comparison with `artifacts/autonomous-cycle7/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final comparison. Summary in `protected-final.json`. |
| `git diff --check` | Passed, including final documentation updates; informational LF-to-CRLF notices only. |

Bounded repairs: the first new workbook test used `Artificial new entity` as an
unambiguous fallback, but it shares the word `entity` with an existing entity.
The planner correctly kept the similar-name choice unresolved and withheld every
virtual row. Changing only that synthetic input to `Novel Organization` made the
intended fallback case valid; similar-name choice/reverification tests remain.
The related mapping gap was independently reproduced by the three red tests,
then fixed with final live-review and token checks. All initial TRX results are
retained; final suites have no failures/skips. No test was removed or disabled.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
or privacy audit, actual accounting-owner review, physical/native-dialog
acceptance, clean-machine offline installation and end-to-end OCR. Historical
results are not current executions. No release/MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verifiable progress: **43 new tests**, **657 total .NET tests** and
**70 frontend tests** pass. The bounded verified-input task and related mapping
consistency repair are complete.

Next task: record a focused plan, then connect live verified placement to the
existing read-only structural preflight. Follow `WorkflowPlanning.Prepare`'s
precedence for unresolved choices, duplicate review and structural diagnostics.
Accept live reviews with explicit expected versions and one destination; recheck
all authorities after preflight. Retain typed/literal/source provenance and the
separate planning/preflight/generation/oracle/approval/adoption/integration/export
facts. Test supported plain synthetic packages and existing unsupported merged,
hidden and multi-boundary/section cases on independent worksheets/copies, proving
unchanged cells, revisions, dirty state and source hashes. This existing Phase 3
capability diagnostic needs no accounting-owner decision. Do not generate or
approve/adopt candidates, accept cached observations as authority, or enable
mutation, UI/bridge, insertion, integration, export, OCR or production Apply.

The previews remain observations at captured versions, without a global lock or
durable authorization. Destination consistency relies on monotonic session
revisions; future operations must revalidate live state. Source evidence is
supplied identity, not authenticated/registered documents. Synthetic Verify
calls do not establish actual human review of a receipt.

Later open gates remain: accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 local OCR/redistribution/source-linked review and
process supervision, and Phase 5 offline installation and complete-workflow
acceptance. Phase 2/3 and the MVP remain incomplete. Those external/manual gates
do not block the next independent preflight task.

## Cycle 6 - 2026-11-02

Completed Cycle 5's documented next task: headless single-record verified
placement preview. Read AGENTS.md, GOALS.md, roadmap and Phase 3 acceptance,
architecture overview/ADRs, focused review/projection/mapping plans, integration
and structural preservation limits, and current application/workbook code/tests.
Preserved all prior dirty/untracked user and supervisor work. Recorded the
focused implementation plan before code in
`plans/phase-03-verified-placement-preview.md`.

Starting evidence: `artifacts/autonomous-cycle6/starting-tracked.diff` and
`starting-status.txt`; the copied 141-file protected baseline matched at start.
SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was opened
or written. XLSX writes used newly generated artificial fixtures only.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedMappingPreview.cs`: extracts
  shared internal live validation/capture and mapping helpers; the public mapping
  API and its guard behavior remain unchanged and are regression-tested.
- `src/TsekSheet.Application/Verification/VerifiedPlacementPreview.cs` (new):
  `VerifiedPlacement.Preview` accepts live review/workbook sessions and explicit
  expected review/profile/destination bindings. It feeds one typed verified
  record into the existing pure planner, retaining the mapping/provenance and
  placement proposal. Section choices, similar-name suggestions and duplicate
  warnings remain explicit. Choices cannot supply replacement field values;
  changing the entity requires correction and fresh verification. Configured
  aliases preserve verified text. Destination, live review and cancellation are
  checked after planning, including correction/cancellation during the final
  destination read. All `CanApply` properties remain false; no structural
  inspection or candidate/mutation API is called.
- `tests/TsekSheet.Application.Tests/VerifiedPlacementPreviewTests.cs` (new):
  25 cases cover both approved profiles, independently shifted/permuted sheets,
  exact/alias/fallback/similar entities, ambiguous and invalid section choices,
  duplicates, deterministic/defensive observations, typed/literal/source/value-
  revision provenance, correction and renewed verification, draft/retracted/
  stale/foreign reviews, invalid destination/type/boundary inputs, cancellation
  and changes during capture. Fake mutation/preflight methods fail if called;
  forged `CanApply=true` proposals still reject through the engine contract.
- `tests/TsekSheet.Workbooks.Tests/VerifiedMappingWorkbookTests.cs`: two new
  synthetic XLSX placement tests cover both profiles on independent worksheets
  and clean/dirty copies, reversed columns, shifted bounds, unresolved choices,
  duplicate retention, foreign/stale copy bindings, invalid choices and forged
  Apply rejection. Every projected cell, formula/format, copy change list,
  dirty/revision state and source hash is preserved. Existing four incompatible
  worksheet cases now also exercise placement rejection on main and copy.
  Fixtures retain merged lower footers, hidden rows and a hidden context sheet.
- `plans/phase-03-verified-placement-preview.md` (new): preimplementation plan,
  final contract, consistency limits, rejection behavior and remaining gates.
- `PLANS.md`: Cycle 6 outcome and next bounded verified-input task; previous
  plans and incomplete phase status are retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this evidence and cycle handoff;
  all previous entries retained below.
- `automation/agent-status.txt`: final supervisor decision, written only after
  this tracker. Other automation files, `adas` and prior work are preserved.

No dependency, schema, architecture, OCR, source registration, persistence,
UI/bridge, editable admission, insertion, candidate generation/approval, export,
integration, production Apply or save-authorization policy changed. Existing
raw-input proposal APIs remain proposal-only.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and `--no-restore`. TRX and baseline/summary files are retained under
ignored `artifacts/autonomous-cycle6/`, solely as development evidence.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0`. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~VerifiedPlacementPreviewTests\|FullyQualifiedName~VerifiedMappingPreviewTests' --logger 'trx;LogFileName=placement.trx' --results-directory artifacts/autonomous-cycle6/focused-application` | Initial compilation failed with `CS1061`; after the test-only correction below, **59 passed**, zero failed/skipped. This focused run preceded the two additional final-capture controls. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingWorkbookTests --logger 'trx;LogFileName=placement-workbooks.trx' --results-directory artifacts/autonomous-cycle6/focused-workbooks` | **Eight passed**, zero failed/skipped, on first run. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle6/solution` | **612 passed**: application 249, workbook 348, desktop 15; zero failed/skipped. Precedes final recheck hardening. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle6/solution-final` | Final: **614 passed**: application 251, workbook 348, desktop 15; zero failed/skipped. All three TRX counters inspected and retained in `solution-summary.json`. |
| Desktop builds' normal npm clean install, frontend typecheck and production build | Passed in both solution runs; lockfiles unchanged. |
| `npm.cmd run test -- --run` in `src/TsekSheet.Frontend`, after the final solution run | **70 tests / eight files passed**, zero failed. No overlap with desktop dependency installation. |
| SHA-256 comparison with `artifacts/autonomous-cycle6/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final comparison. Summary retained in `protected-final.json`. |
| `git diff --check` | Passed, including after final documentation updates; informational LF-to-CRLF notices only. |

Bounded repair: the first application test build called the default
`IWorkbookSession.InsertWorkflowRows` method on the concrete fake type, producing
`CS1061`. Casting the fake to its interface fixed the test; product gates were
unchanged. No executing tests failed. During final review, moved the live review
check after the final destination read and added a final cancellation check,
with two new race controls. The complete solution was rerun after that change.
No tests were removed, disabled or skipped.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
or privacy audit, actual accounting-owner review, physical/native-dialog
acceptance, clean-machine offline installation and end-to-end OCR. Historical
results do not count as current executions. No release/MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. Verified progress: **27 new tests**, **614 total .NET tests** and
**70 frontend tests** pass. The single-record placement task is complete; the
next documented independent task needs no routine milestone approval.

Next task: record a focused plan, then extend verified placement to the existing
planner's bounded 1-20-record input. Use live review sessions with expected
versions for every record and one explicit selected destination. Capture/recheck
every review and the destination; reject the entire observation on invalid,
stale, foreign or repeated review input. Preserve cross-record duplicate
warnings, unresolved entity/section choices, deterministic virtual row ordering
and each record's typed/literal/source provenance. Exercise both artificial
profiles on independently bound worksheets/copies and serialize frontend checks
after desktop builds. This covers the existing bounded planner; larger document-
set processing remains outside the MVP. Do not accept cached observations as
authority or enable candidate generation, edits, UI/bridge, insertion, export,
OCR or production Apply.

These previews are observations at captured versions; they are not durable
authorization, target-cell safety checks or structural preservation proof. The
destination guard relies on monotonic session revisions, and subsequent actions
must revalidate live state. Source evidence remains supplied identity rather
than authenticated/registered documents. Synthetic verification does not prove
actual human review of a scanned source.

Later gates remain: actual accounting-owner hierarchy/identity review, complex-
workbook preservation and approved editing lifecycle, full Phase 3 template/
integration delivery, Phase 4 local OCR/redistribution/source-linked review and
process supervision, and Phase 5 offline installation and complete-workflow
acceptance. These do not block the next read-only bounded input task. Phase 2/3
and the MVP remain incomplete.

## Cycle 5 - 2026-11-02

Implemented the documented headless read-only mapping preview from Cycle 4.
Read goals, roadmap, architecture/ADRs, focused contract/projection plans and
current review, profile, analysis, planning and session/test contracts. All prior
dirty/untracked user and supervisor work is preserved. The focused plan was
recorded before code in `plans/phase-03-verified-mapping-preview.md`.

Starting evidence: `artifacts/autonomous-cycle5/starting-tracked.diff` and
`starting-status.txt`; the copied 141-file protected baseline matched at start.
SDK 10.0.403; Node v24.16.0. No original/private accounting workbook was opened
or written. All generated workbook inputs are artificial test fixtures.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/VerifiedMappingPreview.cs` (new):
  single-record `VerifiedMapping.Preview` takes live review/workbook sessions
  and explicit expected review/profile/destination bindings. The existing
  bounded complete-sheet reader resolves headers and section bounds. Live typed
  projection occurs after analysis; a final destination session/source/sheet/
  revision check rejects changes during capture. Defensive field/section lists
  retain header, resolved column, literal/normalized text, decimal/date values,
  source evidence, value revision and full review/profile/destination identity.
  Optional absence remains empty/null; formula-looking text remains literal.
  There is no target data row, insertion proposal or mutation authority, and
  `CanApply` is always false.
- `tests/TsekSheet.Application.Tests/VerifiedMappingPreviewTests.cs` (new):
  36 cases cover both profiles on independently named, shifted and permuted
  worksheets; typed/literal/source provenance; optional absence; defensive and
  deterministic observations; invalid destination/profile/source/revision;
  draft/corrected/retracted/stale/foreign/forged review versions; invalid types;
  incompatible worksheets; review/destination changes during analysis; final
  destination rechecking; cancellation and renewed verification after correction.
  Mutation methods on the fake session fail immediately if invoked.
- `tests/TsekSheet.Workbooks.Tests/VerifiedMappingWorkbookTests.cs` (new):
  six cases generate synthetic XLSX files for both profiles. Two independently
  configured worksheets have reversed columns and different summary positions;
  successful main/copy previews compare every projected cell, including
  formulas/formatting, merged footers and hidden context sheets. A deliberately
  edited copy retains its dirty state/change list and makes old bindings stale;
  the main session remains unchanged. Incompatible missing-subtotal/duplicate-
  header inputs reject on main and copy. Source hashes match after disposal.
- `plans/phase-03-verified-mapping-preview.md` (new): preimplementation plan,
  implemented contract, error behavior, consistency/lifetime limits and evidence.
- `PLANS.md`: Cycle 5 outcome and next bounded verified-placement preview task;
  all earlier plans and incomplete phase status are retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this report and next-cycle handoff;
  previous cycle evidence retained below.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker. Other automation files, `adas` and prior work are preserved.

No dependency, schema, foundational architecture, source registration, OCR,
persistence, UI/bridge, editable admission, workbook mutation, production Apply,
insertion, candidate generation/approval, export, integration or save policy
changed. Existing raw-input planning APIs remain proposal-only.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and `--no-restore`. TRX files and summaries are under ignored
`artifacts/autonomous-cycle5/`, solely as development evidence.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0`. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingPreviewTests --logger 'trx;LogFileName=mapping.trx' --results-directory artifacts/autonomous-cycle5/focused-application` | **36 passed**, zero failed/skipped on first run. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~VerifiedMappingWorkbookTests --logger 'trx;LogFileName=mapping-workbooks.trx' --results-directory artifacts/autonomous-cycle5/focused-workbooks` | Initial: **two failed, four passed**, zero skipped; fixture error described below. |
| Same focused workbook command with `LogFileName=mapping-workbooks-final.trx` | Final: **six passed**, zero failed/skipped after the fixture repair. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle5/solution` | **587 passed**: application 226, workbook 346, desktop 15; zero failed/skipped. All projects built. All three TRX result counters checked and retained in `solution-summary.json`. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed within the solution run; dependency lockfiles unchanged. |
| `npm.cmd run test -- --run` in `src/TsekSheet.Frontend`, after the solution run | **70 tests / eight files passed**, zero failed. No overlap with the desktop dependency install. |
| SHA-256 comparison with `artifacts/autonomous-cycle5/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed**, at start and final comparison; `protected-final.json` retains the summary. |
| `git diff --check` | Final passed. A documentation-stage check first reported an extra blank line at `PLANS.md` EOF; removing only that extra line repaired it. Informational LF-to-CRLF notices remain. |

Bounded test repair: the initial workbook controls attempted a deliberate
in-memory copy edit in an empty cell. The existing engine correctly rejected
`Only existing populated cells can be edited in this milestone.` The fixture
now seeds that artificial note cell, retaining the same independence/stale-copy
coverage without relaxing product admission. Both failing results remain in
`focused-workbooks/mapping-workbooks.trx`; the repaired focused run and complete
solution run pass. No tests were removed, disabled or skipped.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
or privacy audit, actual accounting-owner review, physical/native-dialog
acceptance, clean-machine offline installation and end-to-end OCR. Historical
results do not count as current checks. No release/MVP completion is claimed.

### Remaining work and next cycle decision

**CONTINUE**. This cycle made verified progress: **42 new tests**, **587 total
.NET tests** and **70 frontend tests** pass. No routine approval is needed for
the next documented independent application-layer task.

Next task: record a focused plan, then add a headless single-record verified
placement preview using the existing section/entity/duplicate planner. Accept
live review/workbook sessions and explicit expected review/profile/destination
bindings; retain typed/literal/source provenance alongside proposed placements.
Recheck live versions, preserve unresolved section choices and duplicate
warnings, and require correction plus renewed verification instead of silently
substituting a selected similar name for a verified entity. Test both synthetic
profiles and independent worksheets/copies. Do not accept cached projections or
mappings as authority or enable edits, candidate generation, UI/bridge, insertion,
export, OCR or production Apply. Keep raw-input proposal APIs proposal-only and
serialize frontend tests after desktop dependency builds.

The current mapping is an observation at captured revisions, never durable
authorization. The destination bracketing relies on the established monotonic
session revision contract. Later actions must revalidate live state again;
correction may invalidate the observation after capture. Source hashes remain
supplied identities, not authenticated/registered documents. Synthetic explicit
verification is not actual human review of a receipt.

Remaining later gates are unchanged: actual accounting-owner hierarchy/identity
review, complex-workbook preservation and approved editing lifecycle, full
Phase 3 integration/template delivery, Phase 4 local OCR/redistribution/source-
linked review/process supervision, and Phase 5 offline installation and complete
workflow acceptance. These external/manual gates do not block the next bounded
read-only preview task; Phase 2/3 and the MVP remain incomplete.

## Cycle 4 - 2026-11-02

Preserved all prior tracked/untracked Cycle 1-3 and user/supervisor changes.
Read the goals, roadmap, architecture overview/ADRs, Phase 3 contract plan and
current review/profile/planning code and tests. Selected the dependency-ready
typed projection task from the Cycle 3 handoff. The focused implementation plan
was written before code in `plans/phase-03-typed-review-projection.md`.

Starting evidence: `artifacts/autonomous-cycle4/starting-tracked.diff` retains
the prior tracked changes; `original-validation.cs.txt` retains the planner's
original value-validation method. The copied protected baseline checks the same
141 reference/fixture/branding/lock files; all matched at start. SDK 10.0.403;
Node v24.16.0. No original/private accounting workbook was opened or written.

### Completed work and exact changed files

- `src/TsekSheet.Application/Workflows/WorkflowValueValidation.cs` (new): pure
  shared validation/normalization with parsed decimal/date results. Preserves
  configured-key, raw-length-before-trim, XML text, required-field, invariant
  decimal/ISO date and identifier rules and existing error messages. Validation
  does not establish human verification.
- `src/TsekSheet.Application/Workflows/WorkflowPlanning.cs`: delegates its
  existing value validation to that helper and retains normalized string values
  and proposal-only behavior. Existing Apply restrictions remain unchanged.
- `src/TsekSheet.Application/Verification/FieldReviewSession.cs`: adds
  `Project(expectedVersion)` and snapshots identifier configuration defensively.
  Checks the exact live review/profile version, completeness and explicit
  verification, then validates/types/captures fields under the correction lock.
  Stale, draft, wrongly typed and missing-identifier records reject without
  changing state. Existing `Validate` still checks only review completeness.
- `src/TsekSheet.Application/Verification/TypedReviewRecord.cs` (new): defensive
  read-only projection retaining exact literal and normalized text, nullable
  decimal/date values, source identity/page, field value revision and full
  review/profile version. Optional blank/absent typed values remain null.
- `tests/TsekSheet.Application.Tests/TypedReviewRecordTests.cs` (new): 44 cases
  covering both profiles, planner parity, numeric/date boundaries, culture
  independence, literal formula-looking text, provenance, optional absence/blank,
  immutable captures, mutable caller configuration, forged observations,
  stale/foreign/profile versions, corrections/retraction and concurrent
  projection/correction. The concurrency test exercises 20 races. Raw planner
  malformed-key/null/length/XML rejections remain covered. No tests were removed,
  disabled or skipped; this slice performs no workbook manipulation.
- `plans/phase-03-typed-review-projection.md` (new): preimplementation plan,
  implemented API, errors, evidence and trust/lifetime limits.
- `plans/phase-03-verified-field-contract.md`: follow-through link from the
  completed Cycle 3 next task; prior contract and policy limits retained.
- `PLANS.md`: Cycle 4 outcome and next bounded read-only mapping task, retaining
  all previous plans and incomplete phase status.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this report and handoff; prior
  cycles retained.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this tracker. Other automation files, `adas` and preexisting changes preserved.

No dependency, template schema, foundational architecture, source registration,
OCR adapter, persistence, UI/bridge, editable admission, workbook mutation,
production Apply, insertion, export, integration or save authorization changed.

### Commands and actual results

Commands ran from the repository root except frontend tests. .NET checks used
Release and `--no-restore`. TRX results and summaries are retained under ignored
`artifacts/autonomous-cycle4/`; these are development evidence, not product data.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0`. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TypedReviewRecordTests\|FullyQualifiedName~WorkflowPlanningTests\|FullyQualifiedName~FieldReviewSessionTests' --logger 'trx;LogFileName=projection.trx' --results-directory artifacts/autonomous-cycle4/focused` | **122 passed**, zero failed/skipped on first run. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger trx --results-directory artifacts/autonomous-cycle4/solution` | **545 passed**: application 190, workbooks 340, desktop 15; zero failed/skipped. All projects built. Three distinct TRX files retained and checked in `solution-summary.json`. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed as part of the solution command; lockfiles unchanged. |
| Initial `npm.cmd run test -- --run` in `src/TsekSheet.Frontend` | **Failed before running tests**: eight worker startup errors (`MODULE_NOT_FOUND` for `@asamuzakjp/dom-selector`) while the desktop build concurrently ran `npm ci`. See failure/repair note below. |
| Same frontend command, rerun after the solution command finished | **70 tests / eight files passed**, zero failed. |
| SHA-256 comparison with `artifacts/autonomous-cycle4/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed** at start and final comparison; `protected-final.json` retains the summary. |
| `git diff --check` | Passed; informational LF-to-CRLF notices only. |

Failure and bounded repair: frontend tests were incorrectly scheduled alongside
the desktop build's dependency mutation. The initial run executed no tests and
is not counted as passing. The build completed its normal clean install;
rerunning the unchanged frontend test command afterward passed all 70 tests.
Future cycles must serialize frontend tests after any desktop build that runs
`npm ci`. No product compilation or executing-test failure occurred.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook
qualification/privacy audit, actual accounting-owner review, physical/native
dialog acceptance, clean-machine offline installation and end-to-end OCR.
Historical results do not count as current checks. No release/MVP completion is
claimed.

### Remaining work and next cycle decision

**CONTINUE**. This cycle made verified progress: the bounded typed projection
is implemented, 44 new tests pass, and final regressions pass **545 .NET tests
and 70 frontend tests** with no failures/skips. No routine approval is required
for the next documented independent task.

Next task: record a focused plan, then implement a headless read-only mapping
preview accepting live review sessions and explicit expected review/profile and
worksheet revisions. Use typed projection plus current worksheet analysis to
map logical fields to configured columns while retaining literal text and
source/version provenance. Reject stale/foreign reviews, stale destinations and
incompatible profiles/worksheets; test both synthetic profiles on independent
worksheets. Never accept snapshots or cached validation/projection objects as
verification authority. Keep raw-input proposal APIs proposal-only and all
workbook editing, UI/bridge, insertion, export, OCR and production Apply gates
unchanged. Serialize frontend tests after desktop builds.

The current projection is an immutable observation at one review version, not
durable authorization. Later mapping/apply must validate live review and
destination versions again; correction can invalidate a captured result at any
time. Source hashes remain supplied identities; document registration,
authentication, availability and document-wide invalidation remain future
adapter responsibilities. Synthetic explicit verification is not actual human
review of a receipt.

Later MVP work remains: actual accounting-owner hierarchy/identity review,
complex-workbook preservation proof and approved editing lifecycle, full Phase 3
mapping/integration, Phase 4 OCR/tool redistribution/source-linked human review
and process supervision, and Phase 5 clean-machine offline/full-flow acceptance.
Phase 2/3 remain incomplete. Those later manual/external gates do not block the
next read-only headless mapping task.

## Cycle 3 - 2026-11-02

Preserve all preexisting tracked/untracked Cycle 1/2 and user/supervisor changes.
The next dependency-ready task is the Phase 3 headless verified-field contract
selected in the Cycle 2 handoff. Read the goals, roadmap, architecture/ADRs,
existing workflow profiles/planning, and application test conventions.

Implementation plan recorded before code: `plans/phase-03-verified-field-contract.md`.
Own in-memory draft/corrected/verified state, bind commands to review/profile/
field/source revisions, invalidate on every correction, reject stale commands
and missing required/unverified supplied fields. Use only artificial inputs;
no mapping, OCR adapter, UI/bridge, persistence or workbook mutation is enabled.

Starting evidence: `artifacts/autonomous-cycle3/starting-tracked.diff` preserves
the prior tracked diff. The copied protected baseline checks 141 reference,
fixture, branding and lock files, all unchanged at cycle start. SDK 10.0.403;
Node v24.16.0.

### Completed work and exact changed files

- `src/TsekSheet.Application/Verification/FieldReviewSession.cs` (new): a headless,
  bounded, in-memory record review service using existing profile field
  definitions. Draft intake retains exact text and document identity/hash/
  revision/page evidence. Missing fields remain visible. Every correction,
  including same-text/source-only corrections, clears verification and advances
  the field value revision. Explicit verification and retraction advance the
  record revision. All commands bind review/profile/field/source versions and
  run atomically; rejected commands leave state unchanged. Validation rejects
  missing required values and every supplied unverified field. Snapshots are
  defensive observations, never verification authority.
- `tests/TsekSheet.Application.Tests/FieldReviewSessionTests.cs` (new): 37
  synthetic cases covering both generic profiles, explicit verification,
  corrections/retraction/replays, all source identity components, old-value
  restoration, stale/foreign/altered bindings, competing commands, missing and
  optional fields, defensive collections/snapshots and malformed/bounded inputs.
  Tests also preserve literal formula-looking text and ensure diagnostics do not
  echo supplied values. No workbook manipulation was added by this slice.
- `plans/phase-03-verified-field-contract.md` (new): the plan recorded before
  implementation, implemented API/behavior, evidence limits and next bounded
  task. Its source and verification boundaries remain explicit.
- `PLANS.md`: Cycle 3 outcome and the next dependency-ready headless typed-record
  task. All previous plans remain intact.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this cycle's plan/results/handoff;
  all previous cycles retained.
- `automation/agent-status.txt`: final supervisor decision only, written after
  this report. All other automation files, `adas`, and prior dirty work remain.

No private/accounting workbook was opened or written. No dependencies,
foundational decisions, profile schema, bridge/UI commands, editable admission,
Apply/adoption, inspection mutation/export or save authorization changed.

### Commands and actual results

Commands ran from the repository root except the frontend test. All .NET tests
used Release and `--no-restore`; no test was removed, disabled or skipped.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0`. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FieldReviewSessionTests --logger 'trx;LogFileName=field-review.trx' --results-directory artifacts/autonomous-cycle3/focused` | **37 passed**, zero failed/skipped on the first run. |
| `dotnet test TsekSheet.sln -c Release --no-restore --logger 'trx;LogFileName=cycle3.trx' --results-directory artifacts/autonomous-cycle3/solution` | **501 passed**: application 146, workbooks 340, desktop 15; zero failed/skipped. All projects built successfully. |
| Desktop build's normal npm clean install, frontend typecheck and production build | Passed as part of the solution command; lockfiles unchanged. |
| `npm.cmd run test -- --run` in `src/TsekSheet.Frontend` | **70 tests / eight files passed**, zero failed. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --no-build --logger 'trx;LogFileName=workbooks-final.trx' --results-directory artifacts/autonomous-cycle3/workbooks-final` | **340 passed**, zero failed/skipped. Evidence-only rerun explained below; the unchanged build from the successful solution command was used. |
| SHA-256 comparison with `artifacts/autonomous-cycle3/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed** at start and final comparison; summary in `protected-final.json`. |
| `git diff --check` | Passed; informational LF-to-CRLF notices only. |

No development compilation/test failures occurred. Diagnostic issue: the solution
logger used one fixed TRX filename, so the workbook and desktop runs reported
overwriting that file. The application result was copied to
`solution/first-result.trx` before overwrite; the final desktop result is retained
as `solution/latest-result.trx` and `solution/cycle3.trx`. The successful workbook
suite was rerun with a unique output directory/file to retain its complete
machine-readable evidence. This did not change tests or their outcomes.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked
restore, Windows publish, new native Excel capture, private-workbook qualification
and privacy audit, accounting-owner review, physical/native-dialog manual
acceptance, clean-machine offline installation and end-to-end OCR. Historical
results are not current executions. No release or MVP acceptance is claimed.

### Limits, remaining work and next cycle decision

**CONTINUE**. This cycle delivered and verified the selected contract: **501
solution tests and 70 frontend tests pass**, with no failures/skips. The next
independent approved task is available; human approval is not needed to resume.

Next task: first record a focused plan, then extract the existing proposal
planner's pure field-value validation without changing its accepted semantics.
Add a headless typed-record projection consuming the live review atomically at
an expected review/profile version. Reject draft/stale/wrongly typed or
workflow-invalid records, retain exact literal text and source/version provenance,
and test both synthetic profiles. A validation result or caller-created snapshot
must not become proof of verification, and a correction must not race a
validate-then-read sequence. This is a documented Phase 3 field/mapping
prerequisite; do not enable workbook edits, UI/bridge, insertion, export,
persistence or OCR adapters in that bounded slice.

Current review validation establishes only verification/completeness, not decimal,
date, identifier or mapping validity. Source hashes are supplied identities;
source registration/authentication, source availability, document-wide invalidation
and evidence lifetime remain future adapter responsibilities. Profile changes
require a new review with fresh verification. Synthetic `Verify` calls establish
application contract correctness, not actual human verification of a receipt.

Later open MVP work remains: actual accounting-owner hierarchy/identity review,
complex-workbook preservation proof and approved editing lifecycle, complete
Phase 3 typed mapping/integration, Phase 4 OCR/tool redistribution/source-linked
human review/process supervision, and Phase 5 clean-machine offline and complete
workflow acceptance. Phase 2/3 remain incomplete; those later external/manual
gates do not block the next headless contract task.

## Cycle 2 - 2026-11-02

Preserve the complete Cycle 1 working tree, including its uncommitted application/tests/docs and user/supervisor files. Read the authoritative goals, roadmap, architecture/ADRs, detector/profile documentation and relevant code/tests. The next approved bounded task is the older planner's missing-identifier/formula-context omission inside declared data sections.

Plan: reproduce cached and uncached contextual-formula omissions in the pure analyzer; reject these rows as `AMBIGUOUS_RECORD` without evaluating caches. Keep entity-only notes, configured heading/subtotal markers and outside-section formula summaries unchanged. Add synthetic workbook/session/copy preservation tests, then run application, workbook and desktop regressions. Record exact commands and results here before setting the supervisor status. No architecture/schema/admission or mutation capability changes are planned.

Starting diagnostic evidence: `artifacts/autonomous-cycle2/starting-tracked.diff` captures existing tracked changes; `protected-baseline.json` fingerprints the same 141 reference/fixture/branding/lock files used in Cycle 1. SDK `10.0.403`, Node `v24.16.0`.

### Completed work and exact changed files

- `src/TsekSheet.Application/Workflows/WorksheetAnalysis.cs`: stored contextual formulas now count as evidence of a possible named record without an identifier inside declared data sections. The analyzer rejects with `AMBIGUOUS_RECORD`; it never evaluates or approves financial values.
- `tests/TsekSheet.Application.Tests/WorkflowPlanningTests.cs`: eight regression cases cover both approved generic profiles, both configured context roles, empty/nonzero/zero formula caches and both declared sections. Two controls preserve entity-only notes, unnamed formula summaries, configured heading/subtotal markers, outside-section summaries and the unverified-context placeholder on identified records. Input captures remain unchanged on rejection.
- `tests/TsekSheet.Workbooks.Tests/WorksheetAnalysisContextTests.cs` (new): four synthetic workbook cases cover both sections, hidden/visible rows and cached/uncached formulas. Both main sessions and independent working copies reject ambiguous analysis without changing revisions, dirty state, projected cells or copy changes. A fifth case retains outside-section formula-summary planning with `CanApply=false`. Literal OOXML caches avoid relying on calculation; all test writes target newly generated artificial inputs, and baseline/generated-source hashes are unchanged after disposal.
- `docs/development/worksheet-section-detection.md`: documents conservative formula-context rejection and its existing marker/summary allowances.
- `PLANS.md`: adds this cycle's outcome and the dependency-ready Phase 3 headless verified-data task. The preexisting Cycle 1 plan and historical plans are retained.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this cycle's plan, evidence, limitations and next step; previous cycle evidence retained.
- `automation/agent-status.txt`: final supervisor decision only. Other automation files, `adas` and the prior dirty code/tests/docs are preserved.

No original/accounting workbook was opened or written. No dependency, architecture, profile schema, editable admission, Apply, insertion, inspection export or save-authorization policy changed.

### Commands and actual results

All commands ran from the repository root except the frontend checks. .NET tests used Release and `--no-restore`; no test was removed, disabled or skipped.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0` |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~UnidentifiedContextFormulasInsideEitherSection\|FullyQualifiedName~ContextFormulasPreserveNotesMarkers' --logger 'trx;LogFileName=context-before-fix.trx' --results-directory artifacts/autonomous-cycle2/red` | Before fix: **eight expected regression failures**, two passing controls, zero skips. The old analyzer silently accepted the missing identifiers. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorksheetAnalysisContextTests --logger 'trx;LogFileName=workbook-context.trx' --results-directory artifacts/autonomous-cycle2/focused` | Final focused run: **five passed**, zero failed/skipped. See development failures below for earlier attempts. |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=application-final.trx' --results-directory artifacts/autonomous-cycle2/application-final` | **109 passed**, zero failed/skipped. |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=workbooks-final.trx' --results-directory artifacts/autonomous-cycle2/workbooks-final` | **340 passed**, zero failed/skipped, including retained reference/provenance comparisons and all new synthetic preservation tests. |
| `dotnet test tests/TsekSheet.Desktop.Tests/TsekSheet.Desktop.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=desktop-final.trx' --results-directory artifacts/autonomous-cycle2/desktop-final` | **15 passed**, zero failed/skipped. Its normal dependency install, frontend typecheck, production build and desktop build succeeded. |
| `npm.cmd run typecheck`; `npm.cmd run test -- --run` in `src/TsekSheet.Frontend` | Typecheck passed; **70 tests in eight files passed**, zero failed. |
| SHA-256 comparison against `artifacts/autonomous-cycle2/protected-baseline.json` | **141 reference/fixture/branding/lock files checked, zero changed**. |
| `git diff --check` | Passed; informational LF-to-CRLF notices only. |

Development failures: the first workbook regression compilation used ambiguous `SaveOptions` (`CS0104`); qualifying `ClosedXML.Excel.SaveOptions` corrected it. The first executing workbook run had four passes and one failure because the numeric cache `125.50` projects as `125.5`; the fixture now uses the exact representable cache literal `125.5`, retaining nonzero-cache coverage without changing product projection behavior. That failed run is retained as `focused/workbook-context-first.trx`; the expected application red run remains under `red/`. Final suites have no failures/skips.

Not executed this cycle: standalone evaluation/file-commit harnesses, locked restore, Windows publish, new native Excel captures, private-workbook qualification/privacy audit, actual accounting-owner review, physical/native-dialog acceptance, clean-machine offline installation and end-to-end OCR. Historical results do not count as current executions. Existing manual/compatibility and MVP gates remain open.

### Remaining work and next cycle decision

**CONTINUE**. This cycle made verifiable progress: **464 .NET tests across all three test projects** and **70 frontend tests** passed. The selected older-planner repair is complete.

Next task: the Phase 3 acceptance criterion already requires explicitly verified synthetic values and application-enforced verification before mapping. Begin a small headless verified-field state/validation contract, independently testable without workbook mutation, WPF or OCR tooling. Record an implementation plan before code changes. Bind verification to exact field value/source/revision; corrections invalidate it. Reject draft, stale or missing required values, with both reimbursement and non-reimbursement artificial controls. This satisfies a documented core prerequisite; it does not depend on resolving actual accounting identities or authorizing complex-workbook editing. Do not add UI/bridge commands, OCR/extraction adapters, persistence, mapping application, insertion or export in that slice. Its tests do not establish OCR human-verification or end-to-end MVP acceptance.

Later remaining work is unchanged: actual accounting-owner hierarchy/identity review, preservation proof and approved editing lifecycle for complex workbooks, full Phase 3 template/integration delivery, Phase 4 local OCR/redistribution/source-linked review, and Phase 5 offline installation and complete-workflow acceptance. These later external/manual gates do not block the independent next contract slice. MVP completion is not claimed.

## Cycle 1 - 2026-11-02

### Confirmed baseline

- Read AGENTS.md, GOALS.md, PLANS.md, architecture overview/ADRs, Step 56 verification, compatibility strategy, Section Review guide and relevant application/workbook contracts and tests.
- Starting HEAD: `1f96d44`. Existing untracked `adas` and `automation/` belong to the user/supervisor and must be preserved.
- Latest recorded evidence (Step 56, historical, not rerun yet): 420 solution tests, 52 frontend tests, safety harnesses and Windows publish passed. Read-only inspection/review and narrow synthetic workbook editing/integration/saving foundations exist. Phase 2 remains incomplete; production Apply, inspection editing/insertion/export remain disabled.
- Current .NET SDK resolves to the pinned `10.0.403`; Node resolves to `v24.16.0`. Local restored assets and frontend dependencies are present.

### Selected bounded task and implementation plan (completed)

Repair read-only row accounting when a literal entity has financial context but no identifier. Currently formula-bearing or truncated context does not count as context, allowing these rows to be classified as entity-only notes instead of unresolved records. This is within existing complete-row-accounting and human-verification requirements, not a new mutation capability.

1. Add synthetic cases for literal, cached/uncached formula and truncated context, with and without identifiers and on hidden rows.
2. Demonstrate the regression, then conservatively retain meaningful context as record-shaping evidence without evaluating or verifying it.
3. Run application and related workbook/frontend regressions; record actual results and any environment failures. Preserve original files, reference fixtures, branding and lockfiles.

### Remaining MVP work and gates

- Accounting-owner validation of real worksheet hierarchy, unresolved identities, review queues and physical/native dialog usability remains unexecuted. Never manufacture these decisions.
- Complex-workbook preservation proof and approved editing lifecycle remain unavailable; existing compatibility/admission/mutation restrictions stay in force.
- Phase 3 delivery remains incomplete. Reviewed separate-output and portable review persistence are design-only and do not authorize enabling export.
- Phase 4 OCR/tool redistribution, source-linked verification and process supervision are not implemented; roadmap dependencies on Phase 3 remain unsatisfied.
- Phase 5 offline clean-machine installation and full document-to-workbook acceptance remain unverified. A build cannot establish MVP completion.

### Completed work

- Meaningful configured context now includes stored formulas (even without caches), retained values and truncation evidence when deciding whether an entity row without identifiers needs review. These rows retain `Context without identifier`, a review queue entry and unresolved-identity diagnostics. No financial value is evaluated or approved.
- Entity-only notes remain notes. Configured subtotal/summary labels with formula context keep their previous marker classification; literal record context and identifiers still conflict with markers.
- Added ten table-driven cases across inside/outside-section hidden rows, plus entity-only/identity-formula and configured-marker controls. Added a synthetic XLSX inspector/review lifecycle test with cached/uncached formulas, truncated amounts, rejection of an identity assertion that would conceal a missing identifier, missing-information acknowledgment/retraction, unchanged session revision and source/fixture hashes.
- Existing production Apply, inspection mutation/export, admission, preservation and explicit-save policies are unchanged. No private workbook was opened or written this cycle. Test writes use newly generated artificial files only.

### Exact changed files

- `src/TsekSheet.Application/Workflows/ReadOnlySectionAnalysis.cs`: conservative record-shaping context, separate established marker-conflict check.
- `tests/TsekSheet.Application.Tests/ReadOnlySectionAnalysisTests.cs`: twelve additional synthetic cases/controls.
- `tests/TsekSheet.Workbooks.Tests/SectionReviewIntegrationTests.cs`: one synthetic inspector/review regression with source-preservation checks.
- `docs/development/read-only-payables-section-analysis.md`: classification behavior and configured-marker exception.
- `PLANS.md`: current repair and dependency-ready next cycle task.
- `docs/development/AUTONOMOUS_PROGRESS.md`: this tracker.
- `automation/agent-status.txt`: final supervisor status only; other existing automation files and `adas` are preserved.

Ignored diagnostic files are under `artifacts/autonomous-cycle1/` (protected-file baseline and TRX results); they are development evidence, not product persistence.

### Commands and actual results

Commands run from the repository root unless stated otherwise. All .NET tests used Release and `--no-restore`; no checks were disabled or removed.

| Command / check | Actual result |
| --- | --- |
| `dotnet --version`; `node --version` | `10.0.403`; `v24.16.0` |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ReadOnlySectionAnalysisTests --logger 'trx;LogFileName=baseline.trx' --results-directory artifacts/autonomous-cycle1/baseline` | Baseline: 40 passed, zero failed/skipped |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~EntityWithUnidentifiedFinancialContext\|FullyQualifiedName~FinancialContextDoesNotVerify' --logger 'trx;LogFileName=regression-before-fix.trx' --results-directory artifacts/autonomous-cycle1/red` | Before fix: eight expected regression failures and three passing controls; defects reproduced |
| `dotnet test tests/TsekSheet.Application.Tests/TsekSheet.Application.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=application-final.trx' --results-directory artifacts/autonomous-cycle1/application-final` | Final: **99 passed**, zero failed/skipped |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=workbooks-final.trx' --results-directory artifacts/autonomous-cycle1/workbooks-final` | Final: **335 passed**, zero failed/skipped; includes synthetic source preservation and retained native-reference comparisons |
| `dotnet test tests/TsekSheet.Workbooks.Tests/TsekSheet.Workbooks.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SectionReviewIntegrationTests --logger 'trx;LogFileName=workbook-review-final.trx' --results-directory artifacts/autonomous-cycle1/focused` | Five passed, zero failed/skipped, after adding explicit cache/truncation assertions; subsequently covered by final full workbook run |
| `npm.cmd run typecheck`; `npm.cmd run test -- --run` in `src/TsekSheet.Frontend` | Typecheck passed; **70 tests / eight files passed**, zero failed |
| `dotnet test tests/TsekSheet.Desktop.Tests/TsekSheet.Desktop.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=desktop.trx' --results-directory artifacts/autonomous-cycle1/desktop` | 15 passed, zero failed/skipped before final configured-marker control; superseded by final rerun below |
| `dotnet test tests/TsekSheet.Desktop.Tests/TsekSheet.Desktop.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=desktop-final.trx' --results-directory artifacts/autonomous-cycle1/desktop-final` | Final: **15 passed**, zero failed/skipped |
| Desktop build's normal npm clean install, typecheck and production frontend build | Passed in both desktop runs; dependency/lockfiles unchanged |
| SHA-256 inventory of synthetic reference/fixture workbooks/JSON, dependency lockfiles and SVG branding | **141 files checked, zero changed** |
| `git diff --check` | Passed; informational LF-to-CRLF notice on edited Markdown only |

Development failures: the first new integration test used the wrong snapshot property name and failed compilation (`CS1061`); corrected to `IdentityQueue` before passing tests. The initial desktop build retried one access-denied generated-cache operation and completed successfully. The initial fingerprint-check script wrapped the parsed JSON array incorrectly; corrected validation checked all 141 entries with zero changes. Expected red regression failures above are retained, not hidden.

Not executed this cycle: standalone evaluation/file-commit harnesses, Windows publish, new native Excel capture, private-workbook requalification/privacy audit, physical/native-dialog manual acceptance, clean-machine offline install and end-to-end OCR. Historical Step 56 results do not count as current executions. No release/MVP completion claim is made.

### Next cycle decision

**CONTINUE**. Final runs passed **449 .NET tests across the three test projects** and **70 frontend tests**, with no failures/skips. Verifiable progress was made and another independent documented accounting-correctness repair is available.

Next task: add cached/uncached formula-context cases for a named entity with both identifiers blank **inside declared data sections** in `WorksheetSectionAnalyzer.Record` (`src/TsekSheet.Application/Workflows/WorksheetAnalysis.cs`). Its current context condition requires `Formula == null`, so these possible records can still disappear as notes in the older planner. Require conservative `AMBIGUOUS_RECORD` rejection, retain pure entity-only note controls and unchanged source/session state, and run related application/workbook/desktop regressions. Preserve existing marker handling and outside-section formula-summary allowances; do not introduce schema changes or enable Apply. This task needs no human decision.

Accounting-owner review, complex-workbook mutation proof and offline deployment acceptance remain later external/manual gates, not blockers for that next task.

