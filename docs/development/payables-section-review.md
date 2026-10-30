# Read-only Payables section review

Step 55 adds a local, memory-only review panel to inspection sessions. It uses the [bounded section analyzer](read-only-payables-section-analysis.md); it never calls editable workflow planning or Step 45 candidate approval. See [measured verification](step55-verification.md).

## Using the panel

1. Choose **Inspect workbook read-only**, select a worksheet and wait for its viewport.
2. In **Section Review**, choose **Select profile and start review**. The native dialog accepts a configured, versioned read-only JSON profile. There is no private default catalog. Without a configured profile, review is unavailable; importing declarations does not mean they have been reviewed.
3. Choose a category: **Overview**, **Sections & Subtotals**, **Missing Vendors**, **Potential Duplicates**, **Unassigned Rows**, or **Review History**. Overview shows compact before/after counts; technical capture/coverage details are expandable. All diagnostic warnings remain under Unassigned Rows → Diagnostic details and all warnings. Inspect headers, mapped columns, section parents/headings and every proposed boundary. Row buttons navigate the existing bounded viewport. Selected source/evidence coordinates appear in the decision form.
4. Choose an assertion type, actual captured row, applicable section/role/evidence and a documented reason. Confirm explicitly. The backend validates it and recomputes the entire capture. Invalid assertions return an error and leave the previous review revision intact.
5. Review uncertain vendor cases and repeated identifiers separately. After confirming a section ending on an originally record-shaped row, a guided prompt offers **Review subtotal / non-record classification**. Inspect the row and provide a separate reason before confirming that classification. These remain two independently retractable decisions; confirming an end never automatically removes an identity warning. Retract decisions from Review History.
6. Close review or switch worksheet/workbook to release its state. With one or more decisions, **Discard review decisions?** reports the count and permanent loss. **Continue Reviewing** is focused by default; Escape also preserves the review. **Discard & Close** performs the requested close/replacement/switch. Profile replacement uses the same protection. If a decision response times out or fails identity validation, the prompt explains that the outcome is unconfirmed and conservatively reports up to the possible decision count. Native window closing has its own equivalent default-preserving dialog. Zero-decision reviews without an uncertain operation close normally. No decision save/restore or export exists; process failure cannot preserve memory-only state.

## Browsing the worksheet

Use the worksheet's vertical and horizontal scrollbars, mouse wheel or trackpad. **Go To Cell** accepts A1 coordinates such as `C52` or `G900` (through `XFD1048576`). Arrow keys move the selection, Page Up/Down move twenty rows, Home/End select the first/last display column, and Ctrl+Home/End select the display corners. Source row/column labels and selected-cell details stay attached to the original coordinates. Selection survives nearby scrolling; direct and keyboard navigation bring the target into the bounded window. Row/column inputs and paging remain available.

The default **Useful data** rectangle ends at the last observed value or formula in a successful complete, budgeted selected-sheet scan. Only trailing formatting/empty cell elements are trimmed. All internal blank rows/cells, hidden values, distant values, subtotals and uncached formulas remain. Shared-string indices count conservatively even if the resolved string is empty. Objects, merges, spill areas and formatting alone do not define this content rectangle; this is not an Excel visual used-range certificate. A failed scan supplies no certified bounds.

Choose **Inspect full stored range** to inspect stored cells and row elements, including empty tails. Declared dimensions never prove content. Explicit cell/start-coordinate navigation outside useful/stored bounds opens that requested area without changing source coordinates. Display bounds never alter section capture coverage, editing admission or the 50,000-cell editable guard.

Scroll changes coalesce over 80 ms and use one viewport request at a time. Obsolete queued movement is canceled and late results are ignored. An already admitted ZIP/XML viewport scan finishes within the existing backend budgets before the latest window runs; viewport movement does not forcibly abort that scan. The existing eight-window/2 MiB cache remains. No whole workbook is eagerly loaded and no disk cache is added.

Profiles use the existing strict `ReadOnlySectionProfile` schema: camel-case JSON, positive version, explicit capture rectangle/role columns, header fields/aliases, entity/identifier/context roles, section markers/parents/end rules and summary/exclusion markers. Unknown/duplicate members, cycles, excessive text and capture budgets reject. Native import rejects UNC and reparse-point ancestors, reads an ordinary local file without write sharing and limits its bytes to 64 KiB. The application parser also limits characters/depth. No path can be supplied through the bridge. The synthetic profile in `SectionReviewFixture` demonstrates the schema without private accounting data; the editable workflow catalog is a different contract.

## Source evidence and supported decisions

| Assertion | Backend evidence rule |
| --- | --- |
| Header variant / repeated header | Literal text at an established role column, in a captured header-like row with at least half the configured roles (minimum two) already recognized. Each confirmed variant remains scoped to that row. At least one original complete header mapping is required. Shifted roles are not guessed. |
| Heading | Literal configured marker at a permitted marker column; explicitly select the row when several matches exist. |
| Final boundary | Select an actual proposed end for the section. Never choose the nearest SUM or invent an end. Selection is an assertion about intent, not proof of aggregation correctness. |
| Parent / child | Existing configured IDs and the actual heading row; reject cycles, unbounded/non-contained asserted parents and newly introduced overlap conflicts. Existing unrelated conflicts remain visible so the configuration can be reviewed incrementally. |
| Intermediate subtotal | Actual captured lexical SUM candidate, distinct from a selected final boundary. |
| Summary / exclusion / non-record | Meaningful captured row, explicit classification and reason; non-record requires an originally record-shaped row. Original row accounting and duplicate evidence remain visible. |
| Investigation / genuinely missing | Captured evidence plus reason; missing information remains an uncertain identity. Investigation prevents reviewed completion. |
| Vendor confirmation | Originally missing literal identity without a marker conflict; explicit evidence row and entity column must identify another captured literal-vendor record. No typed replacement, predecessor fill-down, formula-derived evidence or similar-name merging. |
| Duplicate warning reviewed | Stable digest of the exact warning, plus one of its affected rows and a reason. No warning/record deletion, uniqueness claim or posting permission. |

Reviewer-confirmed identities are supplemental facts labeled **Reviewer-confirmed identity**, with the evidence coordinates and reason retained in the decision list. The blank source cell remains blank. The queue retains originally uncertain cases after review and shows their new status; reviewed non-records remain in original record accounting. Formula-derived, truncated, overlength or marker-conflicting identity cases need investigation or explicit classification; arbitrary identity correction is deliberately unsupported.

Potential duplicate diagnostics retain the approved normalized exact-entity/identifier policy. The original warning set is unioned with recomputed warnings, using stable content digests rather than positional indexes. A new supplemental identity may reveal further matches; prior warnings do not disappear. The UI shows identifiers/roles, affected rows, candidate section ownership, exact vendor basis and separate similarity warnings. Missing identities can conceal additional duplicates. Acknowledgment does not prove a unique transaction.

## Architecture, revisions and lifetime

`SectionReview` in Application owns one immutable inspection capture, the selected declarations, a baseline report and supplemental decisions. The analyzer receives separate review facts; source cells are never rewritten. Parent assertions produce an effective profile for reanalysis. Its report fingerprint may differ from the originally selected profile fingerprint; the review binding always retains the original declarations and separately advances the decision revision.

Every decision/retraction carries the complete binding: review ID, inspection session, private source SHA-256, worksheet ID/order, capture revision, original profile version/fingerprint and review revision. Exact mismatch rejects, including an old retraction. Successful decisions require fresh nonempty GUIDs; retracted IDs cannot be reused. Assertion slots require explicit retraction before replacement. Dependent parent assertions may need retraction before their boundaries can be retracted. Invalid reanalysis commits neither a decision nor a revision.

`WorkbookWorkspace` serializes operations, owns at most one review and cancels/releases it on sheet navigation, source replacement, close, explicit cancel and disposal. Cancellation can interrupt a capture independently of the serialized work queue. Page lifetime cancellation also applies. In-progress native modal selection still requires the dialog to return before its cancelled result is discarded. No global profile state survives. There are at most 512 active decisions and 2,048 used decision IDs per review; start a fresh review when exhausted. Bridge request replay limits remain independent.

Bridge commands are `sectionReview.start`, `.decide`, `.retract` and `.cancel`. Strict envelopes and exact nested members reject paths, extra approval flags and unknown fields. The React client validates bounded report structures and response/source/review revisions before replacing its displayed state. Navigation continues through the existing viewport command. No review command can produce a workflow plan, structural ticket, editable copy, Merge or export receipt.

The retained capture limits are **2,000 rows, 32 columns, 40,000 coordinates and 16 MiB accounted payload**, plus the engine's existing ZIP/XML/shared-string/operation limits. Reanalysis uses the same capture; it does not scan ZIP/XML again. The eight-window/2 MiB viewport cache is separate. Accounted bytes estimate admitted payload; they are not a hard CLR heap limit. No disk index or ZIP-offset seeking exists.

## Independent completion and coverage

The table compares baseline and current captured rows, complete observed-row capture, role columns, headers, retained record-shaped rows, provisional ownership, uncertain identities, unassigned meaningful rows, intermediate/unclassified subtotals, summaries, boundary conflicts and known incomplete dependencies. Original record-row coordinates remain separately available. Classification changes explain changed counts; they do not erase raw facts or original duplicate/dependency evidence.

*Configuration reviewed* requires explicit heading/end assertions for all configured sections, confirmation of every recognized header row, explicit originally configured parent relationships, and no header diagnostics. *Record coverage reviewed* additionally requires whole observed-row and role coverage, no unassigned meaningful rows and no investigation decisions. *Vendor cases reviewed* means each originally uncertain identity received an explicit identity/missing/non-record/summary/exclusion decision; a missing-information decision does not establish a verified identity. A bounded *hierarchy verified* result comes from complete reanalysis; investigation decisions keep it false. These flags are independent of formula/reference preservation and insertion authority.

| Term | Meaning |
| --- | --- |
| Detected section | Configured marker/end candidates; provisional evidence. |
| Human-reviewed section | Explicit, source-bound assertions about accounting intent; retractable and possibly incomplete. |
| Verified bounded read-only hierarchy | Entire captured role model satisfies analyzer containment, header, coverage and conflict checks. Not an Excel calculation or package-preservation certificate. |
| Safe structural insertion candidate | Requires independent package/reference/name/anchor preservation and a separately qualified structural lifecycle. Unavailable for the real workbook. |
| Approved ledger modification | Requires a separately authorized production editing/commit workflow. Unavailable here. |

`PreservationVerified` and `CanInsert` are always false. Section-level `CanInsert` is also always false. No formatter grammar changed: unsupported effective formats/inheritance, raw General values, over-precision numbers and stale formula caches keep their existing reasons; source-cell details explain the exact fallback.

## Privacy and future work

No database, local/session browser storage, telemetry, cloud calls, automatic persistence, autosave or accounting export was introduced. Profile text, capture, identity mappings and reasons live only in active memory. Closing clears rendered report and private drafts; unmount cancels backend state and ignores late responses. Garbage collection/process buffers are not secure erasure guarantees. Qualification reports remain in deliberately ignored private development artifacts; they are not a product cache. Synthetic screenshots contain artificial data only.

Future portable review files need a separately specified schema binding source/sheet/order/capture/profile/effective declarations and decisions, stable decision/evidence IDs, reviewer provenance, version migration, tamper/replay handling, retention/deletion policy and validated explicit new-file commit. Restoring a file must revalidate current captured evidence. No such save/restore capability is implemented. A [reviewed separate schedule](reviewed-payables-output-design.md) also remains design-only.
