# Excel-native editing proof of concept — 2026-11-03

## Result and decision

**Current decision after the Gate B revision: stop; B remains blocked and C was
not entered.** Complete-theme and literal-font controls preserve Calibri. The
original missing-theme control still produces Aptos Narrow. Layout/package
qualification remains incomplete, and additional runs exposed unconfirmed native
cleanup/worker timeouts. See [the revision results](#gate-b-revision--controlled-themefont-results).
The original failed fixtures and reports remain unchanged.

The table and initial-run narrative below retain the original experiment result.

| Gate | Actual outcome |
| --- | --- |
| A: isolated worker | Passed the bounded normal-lifecycle experiment on this host, including final instance-setting restoration and verified Excel process exit |
| B: no-op preservation | **Not qualified**: the declared font changed from Calibri to Aptos Narrow; additional package/layout differences remain unapproved |
| C: insertion | Not implemented or executed because B did not pass |
| D: realistic native complexity/failures | Not entered because B did not pass. Parser/preflight unit controls are recorded separately |

This is an experimental negative preservation result, not a production editing
feature and not a rejection of Excel-native editing in principle. Five matching
worksheet names do not establish compatibility with the real Payables template.
The actual Payables workbook remains inspection-only and editing-unqualified.

Only newly generated artificial workbooks were used. No accounting file was
opened, copied, changed or used to construct fixtures during this experiment.
The existing inspector, OOXML gates, verification rules, receipt Apply/Save As,
accepted workbook-centered UI and all production source/dependencies remain
unchanged. No commit, push or deployment occurred. OCR and worksheet-eligibility
implementation remain paused.

## Plan and architecture

[The focused plan](../../plans/excel-native-editing-poc.md) preceded code. Read
AGENTS, GOALS, PLANS/progress, the real-workbook investigation, architecture,
compatibility/preservation boundaries and prior native formatting evidence.

`experiments/TsekSheet.ExcelNative.Poc/` is an independent Windows x64 executable,
with separate contracts, provider, process supervision, literal fixture generator
and read-only evidence modules. Its separate test project uses existing pinned
MSTest/Test SDK versions. Neither project is added to/referenced by the production
solution. The provider has no Office PIA or workbook-library package dependency.
The revision adds Open XML SDK only to the independent experimental tests.

- **Contracts:** `IExperimentalWorkbookProvider` accepts a bounded experimental
  request and returns plain lifecycle/evidence data. COM objects do not cross it.
  This leaves the application compatible with future alternative providers without
  implementing another provider or Google Sheets integration now.
- **Supervisor:** launches a fixed local .NET host with the experiment assembly in
  worker mode; no shell, PATH fallback or arbitrary executable. Requests/responses
  are bounded, operations have a 45-second deadline and five-second cleanup grace.
  Pre-cancellation stops before process launch. No mutation is automatically retried.
- **Worker:** explicit STA, OLE initialization, Windows Forms message pump, COM
  message filter that cancels rejected calls rather than replaying them. Commands
  are serialized with an experiment-local mutex. WPF/business logic are uninvolved.
- **Filesystem:** only absolute regular new GUID run directories under
  `artifacts/excel-native-poc/`; reparse ancestors are rejected. Fixed source/work/
  candidate filenames and create-new behavior prohibit output aliasing/overwrite.
  Native source admission requires the exact deterministic generator SHA-256 plus
  package/provenance checks; a forged marker or matching tab name is insufficient.
- **Evidence:** independent ZIP/XML and semantic inspection use no COM object,
  ClosedXML edit state or reference translator. They never mutate or authorize a
  workbook. Raw part hashes/facts remain a backstop to the narrower interpreted
  cell/style/layout projection.

### Ownership and cleanup

Before activation the worker records existing Excel PIDs/start-time availability.
It creates `Excel.Application`; it never uses GetActiveObject/ROT attachment.
Before settings or workbook calls, it requires a returned HWND/PID absent from
that baseline, an Excel process created during activation in the same session,
and an empty workbook set. It records PID/start ticks/HWND/session in an owned
run journal. Before workbook operations and Quit it rechecks that identity and
the exact expected workbook set/path. Unexpected documents or changed identity
deny destructive cleanup.

The final normal lifecycle also reads back restoration of six instance properties:
Visible, AutomationSecurity, EnableEvents, AskToUpdateLinks, DisplayAlerts and
ScreenUpdating. Restoration occurs after all owned workbooks close, before Quit.
No global registry/Trust Center edit is performed.

There is **no Excel process-kill path**. Normal cleanup uses Quit only after renewed
ownership checks and then waits with message pumping for the exact process to
exit. If a COM call blocks, the supervisor can signal cancellation and terminate
only its own child worker after grace; it reports unknown outcome and retains the
ownership journal/candidate. Excel may remain unconfirmed in that case. This is
explicitly not a promise of reliable in-flight COM cancellation or complete orphan
recovery. Those native failure modes remain untested because Gate D was blocked.

Actual A/B captures had no pre-existing Excel process. A native test with a user's
existing open documents was not executed. The implementation denies baseline PIDs
and unexpected workbook sets; the normal-lifecycle evidence does not certify all
concurrent-user/add-in/activation scenarios or hostile local interference.

### Security and limits

Only the exact versioned artificial fixture can enter this worker. It has no macro,
XLM, ActiveX, embedded object, external relationship, DDE or refreshable connection.
Preflight additionally rejects active/foreign parts, external/dangling relationships,
unsafe ZIP paths, DTDs and excessive allocations. Unknown XML can be inventoried
for comparison but cannot become an approved native source: its hash differs.

The owned instance uses AutomationSecurity=ForceDisable, events off, no link prompts,
and `Workbooks.Open(UpdateLinks=0, CorruptLoad=0, Notify=false, AddToMru=false)`.
Passwords are explicit empty strings; there is no repair/extract-data fallback.
DisplayAlerts stays enabled; no overwrite confirmation is used as an authorization
mechanism. The fixture requests manual calculation, and the worker checks it after
open. It never calls refresh, Calculate, Save or SaveAs; SaveCopyAs targets only a
distinct experimental candidate. Candidate reopening is read-only.

Microsoft documents programmatic macro defaults, the XLM exception to ForceDisable,
and native save behavior. These settings are mitigation, not a universal active-
content or network sandbox. Full add-in/security/network isolation, encrypted/
malicious content and automatic-repair detection are not qualified here. The
normal-load flag is recorded; no claim of complete repair-mode detection is made.
[Workbooks.Open](https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open),
[AutomationSecurity](https://learn.microsoft.com/en-us/office/vba/api/excel.application.automationsecurity),
[SaveCopyAs](https://learn.microsoft.com/en-us/office/vba/api/excel.workbook.savecopyas),
[STA requirements](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments).

Initial limits: input 4 MiB; expanded package 16 MiB; 128 parts; 2 MiB per XML part;
10,000 actual worksheet-cell elements; depth 64; 200,000 XML elements; 2,000 reported
XML differences; request 16 KiB; response 256 KiB; stderr 4 KiB. CalcChain entries
remain subject to XML bounds but do not consume the experimental worksheet-cell
budget. Production counters were not changed. Input/protocol/time bounds and
measured process peaks are not a hard process-memory sandbox.

## Fixture and no-op procedure

The deterministic initial fixture contains five visible tabs, in this order:
`Unpaid RFPs - SEP`, AUG, JUL, JUN and MAY using the same `Unpaid RFPs - ` prefix.
Only the first tab is a destination-shaped artificial schedule. It has two literal
artificial records at rows 3–4, a declared subtotal at row 5, and unrelated lower
content at row 7. It contains **18 stored cells** across the five tabs, including
one styled blank and nine text values. These coordinates exist solely in the
generated experiment; there is no real-workbook insertion-row assumption.

Two literal formulas are `SUM(B3:B4)` and a downstream reference to `B5`, each
with a stored cache of 30. Three explicit row heights are 21/customHeight=1 and
sheet default heights are 15. The style declares Calibri with the minor font
scheme but does not embed a theme part. This is a material fixture limitation:
the theme-dependent font is not a fully self-contained visual baseline.

The worker:

1. Opens and holds `baseline.xlsx` read-only with writer/deletion exclusion,
   checks its hash/provenance and verifies the exact supported generator version.
2. Copies bytes to a create-new `working.xlsx` and verifies exact equality **before**
   Excel opens it. Excel never receives the baseline path.
3. Opens only the working copy, checks identity/tab order/manual calculation, makes
   no editing call, and serializes `candidate.xlsx` with SaveCopyAs.
4. Closes the working copy without saving; reopens the candidate read-only; checks
   identity and closes it without saving. Rechecks baseline SHA-256, restores
   instance settings and confirms owned-process exit.
5. Runs independent package and semantic comparisons after the worker exits. Native
   `COMPLETED` means transport/lifecycle completion, **not preservation acceptance**.

## Actual Gate B findings

Two independent no-op runs reproduced the same result on Excel **16.0 / build
20326**, Windows x64. The final capture has 4,294-byte baseline and 12,389-byte
candidate. There are **16 changed/added parts: 11 changed, five added, zero removed**,
and 710 positional XML-fact differences. Reordering can contribute multiple facts;
710 is not a count of business-data errors.

The independent semantic projection compares decoded inline/shared text, numeric/
error kinds, formula text/attributes, caches, resolved explicit style resources,
names, worksheet identity/order/visibility, merges, dimensions, row/column layout
and print XML. It does not replace the raw-part comparison or certify every feature.

| Finding | Evidence and classification |
| --- | --- |
| Cells and values | All 18 stored cells retained. Nine inline strings become shared-string references; decoded values match. Positive evidence for this fixture, not a general string-normalization exemption |
| Formulas/references/caches | Both expressions and cached results match; downstream reference remains. No explicit recalculation was requested; Excel's internal behavior is not inferred from that fact |
| Worksheet identities | Five names, sheet IDs, order and visibility match after relationship resolution; relationship identifiers themselves were rewritten |
| **Font** | Source font name **Calibri**, candidate **Aptos Narrow**. All 18 explicit cell style projections differ. This is an unapproved formatting change and blocks qualification |
| Row/layout metadata | `x14ac:dyDescent=0.25` added to five default-row records and nine stored rows. Explicit `ht=21`, custom-height flags and default height 15 remain. These precise additions are observed, not automatically harmless |
| Calculation chain | A new calcChain with two entries is created. Formula caches remain in this measured projection. No blanket exemption for calcChain changes |
| Added package parts | sharedStrings, theme, calcChain, core properties and application properties added; no parts removed |
| Styles/theme | Font substitution, theme addition and stylesheet/default style/colour-table changes need independent visual/semantic qualification. Successful reopen does not approve them |
| Other serialization | Content types/relationships reordered and expanded, rIds rewritten; workbook view/revision/version metadata added; custom origin property canonical facts retained despite changed serialization hash |
| Names/errors/complex features | Initial fixture has no defined names, broken references, external links, merges, hidden/grouped rows, comments or printer binary. Preservation of those features is **untested**, not passed |

There are **32 semantic-projection differences**: 18 style projections, nine row
layout records and five default-row layout records. Formula/cache/identity flags
pass; the full gate does not. These fields deliberately distinguish unchanged
data from a broader failed preservation qualification.

The missing embedded theme and minor-font scheme are a plausible contributor to
the font substitution; this is an **inference**, not a proven universal cause or
permission to normalize fonts. No visual-equivalence claim was made. A controlled
explicit-theme/font baseline is required before deciding which changes can be
accepted. The prior [native formatting evidence](step45-verification.md) also
warns against treating Excel-generated layout changes as harmless by default.

### Retained comparison and review policy

[The versioned review JSON](../../experiments/TsekSheet.ExcelNative.Poc/evidence/initial-noop-v1.json)
retains exact synthetic source/candidate hashes, final worker observations, measured
resource data and individual classifications. Full baseline/work/candidate files,
raw before/after part hashes and 710 XML differences, semantic deltas, request and
ownership journals remain in these ignored runs:

- Gate A final: `artifacts/excel-native-poc/0b9f7822c0b94e0292181a6025802c3b/`.
- Gate B final: `artifacts/excel-native-poc/0ef13cb9836b47f3bef060a20edfd599/`.
- Gate B first completed capture: `artifacts/excel-native-poc/b007fcb80dfb48e0b9dade7248843d52/`.

The exact observed font change is explicitly **not approved**. Encoding equivalence
and preserved fields are recorded as positive observations only. The executable's
native-normalization allowlist remains empty; an unexplained changed/added/removed
part fails qualification. Existing production preservation tests/oracles are not
weakened or repurposed to accept the native candidate.

No row insertion, batch, repeat insertion or progressive native complexity fixture
was attempted after the negative no-op result. No candidate was adopted.

## Resource and lifecycle measurements

Final B capture, one sample (not a performance guarantee):

| Measurement | Actual |
| --- | ---: |
| Excel activation plus ownership checks | 1,627 ms |
| Open exact working copy | 520 ms |
| SaveCopyAs candidate | 55 ms |
| Read-only candidate reopen | 51 ms |
| Worker total including cleanup | 5,067 ms |
| Supervisor elapsed | 5,396 ms |
| Observed Excel peak working set | 221,798,400 bytes / 211.52 MiB |
| Observed worker peak working set | 57,806,848 bytes / 55.13 MiB |

The first completed B capture observed 2,594 ms activation, 1,305 ms open, 80 ms
serialization, 105 ms reopen and 8,018 ms worker total. Warm/cold processes and OS/
Office caches differ. Peaks are process observations at different times, not an
additive simultaneous peak or maximum-bound budget qualification. Large accounting
workbook load/copy/insertion cost was not measured. All captured owned Excel
processes exited normally; no forced Excel termination occurred.

## Verification and remaining gates

Executed:

- Final experimental build and **27 tests passed**, zero failed/skipped. Tests
  cover deterministic artificial content, exact-source version binding, deliberate
  value/formula/cache/height/font/style/identity defects, unknown parts, unsafe/
  foreign packages, input/part/entry/depth/report budgets, worksheet versus calcChain
  accounting, request/path and gate rejection, and early cancellation/source/output
  collisions **before COM activation**.
- Actual Gate A normal lifecycle and final private-instance setting restoration
  passed. Actual B copy/save/reopen/cleanup/source-hash checks passed while the
  independent preservation gate failed. This intentional negative result remains.
- Existing production workbook suite **457 passed** and application suite
  **608 passed**, unchanged, with zero failed/skipped. No frontend/desktop UI suite
  was rerun: there is no production/UI integration or source change in this slice.
- Source/working-copy hash evidence and final process observations confirm the
  generated baselines stayed unchanged and no captured owned Excel remained.

Development failures: the first supervisor read process memory after worker exit
and lost its result; its journal identified the instance and subsequent process
inspection showed no Excel remained. Metrics now sample while the child is alive,
and native responses are persisted before final result assembly. A misplaced
`--ignore-failed-sources` switch was rejected by dotnet test; restore/test were
separated. Sandboxed restore used another user's empty cache and failed NU1102;
native restore reused existing pinned packages. Early restore emitted NU1900
because vulnerability metadata was unreachable; final native restore/build passed
without disabling audit or changing product packages. A patch-tool failure left a
new experiment source incomplete; it was restored and the final native A/B and
test build verified the reconstructed source. A new 10,000-entry calcChain parser
control correctly hit the existing comparison-report bound; the assertion now
expects that specific safe rejection, without widening the bound. Its initial
26-pass/one-failure TRX is retained, followed by 27 passing tests.

Unexecuted/deferred because B failed: C single-record/batch/repeated insertion;
D progressive shared-formula/calcChain/layout/name/cross-sheet/external-metadata/
broken-reference/comment/printer preservation; native timeout/cancel during COM,
Excel crash/forced unexpected exit, native permission failures, source-change races
after activation, corrupt-file native opening, existing user-document concurrency,
malicious/add-in/network isolation, multiple Excel versions/locales and clean-machine
offline deployment. Early preflight tests are not passed versions of these cases.

## Original next revision proposal (historical)

1. Retain this failed capture. Define a fully explicit synthetic theme/font
   baseline and a controlled comparison against the theme-dependent case. Independently
   test appearance/style/row geometry and document the permitted differences.
2. Qualify a small versioned normalization contract for specific understood native
   deltas, with deliberate font/height/formula/name/relationship-loss controls.
   Do not allow arbitrary Excel changes or weaken the existing OOXML oracle.
3. Repeat B and only proceed when every relevant delta is explained and the gate
   passes. Then implement C's manifest-bound single artificial record, followed by
   batch/repeated fresh candidates and progressive D fixtures/faults as planned.
4. Keep any production provider integration behind separate preservation,
   operation-bound authority, explicit user approval and accounting acceptance.
   This experiment grants none of those permissions to real Payables workbooks.

## Gate B revision — controlled theme/font results

[The revision plan](../../plans/excel-native-gate-b-revision.md) preceded changes.
The current machine-readable review is
[gate-b-theme-controls-v1.json](../../experiments/TsekSheet.ExcelNative.Poc/evidence/gate-b-theme-controls-v1.json).
The original initial-noop-v1.json, both original failed captures, and every file
present in those captures retain their original hashes. New analyses use distinct
v3 filenames; no original evidence was replaced. All native work used artificial
generated inputs; no private accounting file was accessed.

### Controlled sources and causal finding

The original writer remains byte-identical: SHA-256
`CE86AA36A022D766D5C91027E4F315F353449DC6CAEE2FABDE9CF2E66CB9D5AB`.
Three exact generator hashes are now admitted by the experimental worker; neither
the marker nor a selected sheet name admits arbitrary files. No production gate
changed. Each source retains the same five sheets, 18 stored cells, artificial
values, two formulas/caches and layout. Only font/theme dependencies differ.

| Variant | Source definition | Native candidate observation |
| --- | --- | --- |
| incomplete-theme-v1 | Calibri 11; minor scheme; color theme 1; no theme part/relationship | Font becomes Aptos Narrow; Excel supplies its Office theme |
| explicit-theme-v1 | Identical styles; complete linked DrawingML theme with major/minor Latin Calibri, color/font/format schemes | Declared and resolved Latin font remain Calibri; theme expanded-name XML facts remain identical |
| literal-font-v1 | Calibri 11; no font scheme; explicit FF000000; no theme dependency for this font/color | Calibri and literal RGB remain; Excel adds a theme whose minor font is Aptos Narrow, but this cell font does not reference it |

The complete theme seed came **only** from theme1.xml in the retained generated
synthetic candidate. Major/minor Latin typefaces were explicitly set to Calibri;
the full valid format/color/font structures were retained. This is not claimed
as an independent Excel-authored workbook baseline. It contains font names, not
embedded font files. Open XML SDK **3.5.1**, read-only, Microsoft365 schema validation
reports zero errors for all three complete source packages. This dependency is
test-only; the provider and production dependencies remain unchanged. The negative
source is schema-valid but has unresolved semantic theme dependencies: schema
validity alone does not establish appearance.

On Excel 16.0/build 20326 here, adding the complete Calibri theme while retaining
the original style definition prevents substitution. Removing the scheme and
using literal font/color also prevents it. This establishes a missing-theme/
minor-scheme fallback condition for the observed substitution, rather than a
general Excel rule rewriting literal Calibri. The negative candidate's supplied
minor theme is Aptos Narrow. The exact internal transition during Open versus
SaveCopyAs was not isolated. Other Office builds, font installations, locales,
complex scripts, high-contrast/system colors and rendered glyph equivalence remain
untested. The expected font remains **Calibri**; Aptos Narrow is never approved as
an equivalent expected font.

### Independent preservation contract and classifications

PackageEvidence.Compare remains strict: every raw changed/added/removed part
prevents its Qualified result. The production OOXML oracle is untouched.
PreservationReview adds a separate versioned review with these classifications:

| Class | Precisely bounded interpretation |
| --- | --- |
| accepted-serialization | Plain inline/shared text with identical independently decoded cell values/addresses/types; or equal expanded XML names, ordered elements, text/attributes and existing namespace bindings, without waiving mixed content, comments or processing instructions |
| requires-review | Added dependencies/parts, remaining raw facts, descent hints, or omission of the two explicit false calculation flags while all other calculation attributes remain identical |
| always-fail | Unresolved source theme baseline; changed declared/resolved font, scheme, font definition/color/size; changed values/formulas/caches/styles/names/worksheet targets/identities/ranges/explicit geometry/calculation mode; lost dependency edges, package parts, XML comments or processing instructions |

Each accepted interpretation has deliberate regression controls. String equivalence
does not approve rich-text changes, unrelated worksheet facts, a new table's package
relationships, or other content in the same part. XML equivalence does not approve
changed prefix meaning, element order or lost content. Even accepted interpretations
retain exact raw part hashes and all bounded XML differences. They do not grant
insertion or make a changed package pass the strict oracle. A future finite native
contract still needs review and tests for each remaining fact before it could
qualify B. No blanket whitelist of Excel-generated parts exists.
The executable requires both the strict raw result and the independent review to
pass, alongside confirmed source/copy/reopen/ownership/settings/cleanup checks.
Neither an absent review nor encoding equivalence can promote a native outcome.

SemanticEvidence now additionally retains resolved worksheet target identities and
explicit/absent calculation settings. Font analysis independently records declared
name, scheme, effective Latin theme font, font definition, size and resolved color.
System theme colors retain both the system name and lastClr fallback: a fallback
is not a promise about another machine's appearance. Existing relationship edges
are compared by source/type/resolved target, independently of rId numbering.

### Fresh no-op evidence and actual boundaries

Fresh Gate A: `e3ff9c335393433588c9bc46a9e8e2e1` passed the normal ownership,
restored-settings and verified-exit checks. Each B source was locked read-only,
hashed, copied to a new working path before activation, saved only to candidate,
closed without saving the working source and reopened read-only. No insertion,
calculation, original save, link refresh or candidate adoption was requested.

| Fresh B run | Font result | Raw changed / added / removed parts; XML facts | v3 semantic differences | Lifecycle / B result |
| --- | --- | --- | --- | --- |
| d6063112eaa54e52b5c10d79c288efbc — complete theme | Calibri → Calibri | 12 / 4 / 0; 224 | 15: 9 row + 5 default-row descent, 1 calculation settings | Source/copy/reopen/settings/cleanup all confirmed; B blocked |
| e3c6c096e6574c39afe2e4f20dffdd84 — literal | Calibri → Calibri | 11 / 5 / 0; 709 | Same 15 | Provider reports cleanup unconfirmed; supervisor timeout/unknown; B invalidated |
| e30ef468986b4a8cb609442cd52eba81 — negative | Calibri → Aptos Narrow | 11 / 5 / 0; 710 | Same 15 plus 18 style changes | Cleanup unconfirmed; supervisor timeout/unknown; preservation also fails |

All runs are under `artifacts/excel-native-poc/<run>/`. Source and working hashes
match exactly after the runs; originals remain unchanged. Candidate hashes, sizes,
ownership tuples, timings and statuses are retained in the versioned JSON above.
Raw files are comparison-v3.json and semantics-v3.json; classifications are
preservation-review-v3.json. Earlier comparison/semantics files remain as captured.
The v3 projection adds calculation settings, explaining the extra semantic record
relative to earlier counts. Counts are projection facts, not independent defect
counts or a claim of visual equivalence.

In the independent projection, every control retains all 18 cells and decoded
artificial values; SUM(B3:B4) and B5 remain exact with both caches 30; all five
sheet names/IDs/order/visibility/targets, dimensions, explicit 21-point/custom
heights, default 15-point heights, column definitions, referenced cell styles
(except negative font), print margins, empty merges and empty names are retained.
The four context sheets preserve their interpreted data/formulas/styles but their
raw worksheet XML also changes. They are **not** claimed byte-identical or visually
qualified. Empty names/merges do not qualify nonempty real-world structures.

### Native changes understood and still blocked

- Inline strings become shared strings. Nine plain text values decode identically;
  this narrow encoding equivalence is understood and tested. Other worksheet facts
  remain reviewed separately.
- The complete theme and custom origin property have unchanged expanded-name XML
  facts despite raw encoding changes. The stronger serialization-only checks pass
  for these parts; raw hashes remain different and recorded.
- Existing dependency edges remain; relationship IDs/target spellings change.
  New core/app/sharedStrings/calcChain relationships resolve to present parts.
  New metadata/dependencies still require a finite approved contract.
- All five sheetFormatPr elements and nine row elements gain dyDescent=0.25.
  This specifies baseline placement and can affect custom-height semantics, even
  when a customHeight value says false. Therefore equal explicit heights do not
  establish equivalent layout or later auto-height behavior. Independent rendered
  comparison and row-height-behavior proof are absent. This remains a blocker.
  [Microsoft's dyDescent specification](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-xlsx/f11dfda4-46de-4035-8418-d76b0d3898f1)
- Excel omits explicit fullCalcOnLoad=0 and forceFullCalc=0 while retaining manual
  mode and calcId. This is **not** evidence that recalculation occurred: caches
  match. Excel documents a false default for omitted forceFullCalc, but the
  experiment does not yet approve the full calculation-settings conversion or
  future behavior across versions. A changed calculation mode is a hard failure.
  [Microsoft's calculation-property implementation notes](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oe376/18db658d-f608-490e-906a-f30d23a1f5f6)
- A two-entry calcChain refers to B5/B7 on sheet ID 1; no formula loss was observed.
  Chain flags/order, subsequent calculation and stale-cache behavior are not
  qualified. Source caches matching once do not approve arbitrary chain rebuilds.
- Excel adds zero dxfs, table/pivot defaults, slicer/timeline style extensions and
  knownFonts, workbook version/revision/calc-feature metadata, window geometry,
  defaultThemeVersion, an absolute experiment path and core/app properties.
  Their presence is explained by native serialization; their exact permitted
  values/effects are **not all preservation-approved**. Absolute paths, environment
  identifiers and window/default-style behavior need explicit scope decisions and
  tests. These remaining facts still block the gate.

### Native lifecycle failures retained; no false pass

An attempted supporting font/geometry reader captured 29 bounded cells before
save and after reopening. In complete-theme/literal captures its readbacks matched
(Calibri 11, measured geometry, explicit row heights). These are native observations,
not independent rendered appearance proof, and the associated runs failed lifecycle
qualification. The reader was removed from the worker; only its plain diagnostic
DTOs remain. It must not be treated as a verified feature.

Diagnostic runs dc125706e71643679e05613a0458097e, e6c6b9e3d23e4654bace1937b2ba5236,
b93f4d61eed0451a9444da96d61a086a and 749c8b0a1bdd463c811c6deb05328ded are retained.
The get_Range late-bound diagnostic also produced RuntimeBinderException; it was
removed. Explicit reflection property access still did not qualify cleanup.
Literal/negative no-op runs **after removing the reader** also failed cleanup, so
the reader is not established as the sole cause. Journals show close, settings
restoration and Quit returning, then the five-second owned-exit check failing.
Some workers persisted their invalidated provider result but did not leave their
outer message loop before the supervisor's 45+5-second timeout. This is an unresolved
RCW/lifecycle/message-pump investigation, not an approved timeout relaxation.

Later read-only native process inventory shows no Excel remaining. Later exit
does not retroactively confirm cleanup within the contract or turn an unknown
outcome into a pass. No Excel process was attached to or forcibly terminated;
only the supervisor's own timed-out child worker was stopped as already designed.
No automatic native retry or candidate adoption occurred. Native work stopped
after the control matrix because these issues remain unexplained.

### Revision validation and next boundary

Final experimental tests: **54 passed, zero failed/skipped**. They include schema
validation, exact original hash, theme/literal resolution, declared/effective font
substitution, formula/value/cache/layout/name/target/calculation/relationship loss,
descent versus explicit-height classification, encoding equivalence, namespace/
element-order/comment/part retention, plus all original safety tests. Interim
46-, 51- and 53-test green runs are retained; they are superseded by the final run.
Existing workbook **457** and application **608** tests pass unchanged. Results
are under `artifacts/excel-native-gate-b-revision/`. A 321-file hash inventory covers
all production/test sources plus initial failed evidence: zero changes. No frontend,
desktop UI, independent rendered visual, new native complexity/fault qualification,
other Office version or real workbook test was performed in this revision.

**Gate B did not pass. Gate C was not entered or implemented.** Next qualification
must first resolve owned-instance cleanup/STA-loop exit without broad kills or
deadline inflation, then independently prove rendering/auto-height behavior and
approve a tested finite package/calculation difference contract. Preserve all
negative controls and raw reports. Only a subsequently passed B permits planning
the one artificial insertion; no real Payables boundary or broad complexity
qualification follows from it. Production editing, receipt insertion/output and
OCR remain disabled/paused. No product source, UI, admission, original accounting
workbook, commit, push or deployment changed.
