# Real-workbook compatibility strategy — Step 54

## Step 55 read-only review

The [Section Review panel](payables-section-review.md) now exposes bounded profile selection, source navigation, human assertions, retraction and independent coverage metrics. [Qualification](step55-verification.md) retains the real 25 uncertain identities, 21 duplicate warnings and 484 unassigned rows without fabricated human decisions. A matching private diagnostic profile is loaded only for the active source/sheet review, never added to the editable/global catalog. The real hierarchy and all insertion/export authority remain unavailable. Step 56 should obtain actual accounting-owner decisions and test the resulting hierarchy; separate-output design and package-preservation proof remain separate work.

## Current decision — Step 54 read-only evidence

[Step 54](step54-verification.md) implements safe stylesheet isolation and a separate generic read-only capture/section analyzer. The actual duplicate is unchanged. All 23,452 numeric cells were considered: **21 qualified**, **3,703 General/raw**, **19,728 unsupported**, including **5,862 explicit precision rejections**. The low-ID issue no longer poisons unrelated formats, but most private financial formats remain unsupported; no grouped accounting/currency/percentage displays were certified.

Complete target role capture covers 20,904 coordinates and 1,519 hidden rows. Explicit header aliases recognize both headers; **25 blank-vendor cases**, seven marker conflicts and 484 unassigned rows remain visible. The nine candidate rules do not establish a complete hierarchy. Candidate boundary matches are provisional, every `CanInsert` is false, and no vendor membership/absence conclusion is promoted to a safe placement.

The next smallest milestone is an explicit read-only profile/section-review interface for the larger aggregation boundaries and unresolved identities, plus narrowly evidenced date/accounting grammar work where useful. Preserve the unknowns rather than infer vendors, select intermediate totals or clean the package. [Reviewed separate-output design](reviewed-payables-output-design.md) makes option D concrete but remains unimplemented. Option A still cannot solve the many independent preservation blockers; B inside C still requires a new independent proof for actual shared references, names, links, opaque parts and anchors before any mutation can be considered.

The Step 53 decision and earlier conditional comparisons below remain historical supporting context. Step 54 changes read-only interpretation/diagnostics only, and Phase 2 remains incomplete.

## Step 53 actual-workbook decision

The authorized duplicate is now supplied, inspected and unchanged by private SHA-256 verification. [Step 53 evidence](step53-verification.md) supersedes the unavailable-input status below: five visible tabs are browsable, with 439,321 stored cells, 11,075 names (9,399 containing literal broken references), 290 external-link XML parts, VML/comments/printer settings and complex shared formulas/formatting. Editable admission actually stops at 50,001 cells. The remaining package restrictions are independently observed, not waived.

This is **not a resource-only extension**. Actual numeric interpretation also falls back to raw for all 23,452 numeric cells because an explicit built-in ID 43 format definition fails the current stylesheet parser's custom-ID condition. Fields and an external subtotal span are identifiable, but repeated headers, marker/entity column differences, incomplete hierarchical bounds and 25 identifier-bearing rows with blank vendors block trustworthy production planning. No complete section partition or editing preservation proof exists.

Recommend Step 54 as a narrow **read-only compatibility/layout milestone**: independently qualify explicit low-ID formats and isolate unsupported formats while retaining inherited-format ambiguity and Step 50 over-precision rejection; expose missing identities and repeated-header/hierarchical-section rules; prove complete read-only role coverage. Do not infer a vendor from the previous row or turn a provisional section into an insertion plan.

For the first future posting deliverable, **D, a separate reviewed transaction/output schedule**, has the smallest evidence-supported preservation burden: it need not transform this workbook's names, links, shared expressions, drawings or print context. It is proposed, not implemented, and inspection export remains disabled. If direct workbook editing is required later, investigate **B inside C**, a targeted package transformation in an isolated transaction with independent semantic/package proof. Structural shifts must account for formulas, aggregation intent, names, anchors/print context, unknown relationships and existing defects; unchanged bytes alone do not prove unaffected semantics. The existing narrow oracle rejects this source and cannot simply be reused as a broader certificate. A larger editable envelope alone cannot support the observed package safely.

| Evidence-based alternative | Current fit and minimum proof |
| --- | --- |
| A: extend editable limits | Insufficient alone: cells exceed limits, but links, opaque parts, advanced formatting and structural dependencies remain. Require measured admission/copy costs plus preservation proof for every new feature. |
| B: targeted OOXML | Potential long-term fit for strictly scoped changes; preserve all unaffected parts, graph IDs/content types and external links without resolution. Row shifts require complete affected dependency and anchor transforms. Not implemented/qualified. |
| C: isolated patch transaction | Required companion to B: source/copy/destination revisions, baseline hash, candidate review, independent before/after semantic/package oracle, replay/conflict rejection and separate new-file commit. It does not supply missing transformation correctness. |
| D: reviewed separate output | Smallest initial posting path when direct mutation cannot be proven; validate reviewed records and an independent output without reconstructing the source package. No automatic Merge/import/export into the complex workbook. |

All paths retain source immutability, explicit review, formula-cache uncertainty, bounded memory/time, no overwrite and disabled production insertion. Actual workbook figures and limitations are redacted in the verification report; private source hashes/records are excluded here.

Historical Step 52 checkpoint: exact provenance/native-reference eligibility was restored and all 359 solution tests passed, but the authorized workbook directory was absent then. The Step 53 assessment above supersedes that input status. See [Step 52 verification](step52-verification.md); no editing limit or approval gate changed.

## Historical Step 51 evidence and decision boundary

On 2026-10-30 the designated `local-test-workbooks/` directory was absent. Real-workbook qualification is **UNEXECUTED**. No worksheet names, vendor identities, values, counts, performance measurements or package blockers have been inferred from historical descriptions. This is diagnostic readiness documentation, not an architecture approval or an expanded editing envelope. See [verification and supply instructions](step51-verification.md).

Do not choose a mutation strategy until the actual package and proposed operation have been qualified. Full reconstruction from visible cells is unsafe: the viewport omits workbook relationships, names, styles, hidden context and opaque content. Retaining source bytes or successfully reopening output alone does not prove preservation.

## Detection rules documented in Step 51

Rules below come from `src/TsekSheet.Workbooks/PackageGuard.cs`, `WorkbookLimits.cs`, `WorkbookInspection.cs` and the [structural envelope](structural-preservation-oracle.md). Earlier rejection can prevent later detection; a first error is not a complete inventory.

| Feature | Current rule and affected operation | Future preservation evidence required |
| --- | --- | --- |
| External links | Editable guard rejects part names containing `externallinks` or `connections`; external relationship mode, URL or file targets also reject. Inspection categorizes external data and never follows targets. | Preserve link parts, relationship IDs/targets and formula references unchanged; prove no resolution or refresh. Insertion additionally needs dependency analysis of affected references. |
| Defined names | Inspection counts names. Ordinary package facts retain name text/scope; there is no dedicated name-count limit. Metadata XML/node budgets still apply. Structural envelope rejects every defined name, including print names. | Inventory scope, target and inbound use; classify affected/unaffected names independently. Exact unchanged-part evidence for unaffected names and independent literal/native reference evidence for affected names. |
| Broken names | Existing inspection count is not a broken-reference audit. A literal `#REF!` scan would only detect a subset; no complete resolution claim is justified. | Bounded classification distinguishing existing defects from new damage; never delete or repair names implicitly. |
| Embedded objects/images/binary parts | Editable guard rejects non-directory parts lacking `.xml`, `.rels` or `.psmdcp`; inspection categorizes without rendering/executing. | Exact uncompressed part hashes plus relationship/content-type graph integrity and independent tests that edits cannot invalidate anchors or references. Byte retention alone cannot prove anchor correctness after row shifts. |
| Stored cells | Editable admission stops at 50,001 spreadsheet `c` elements, including styled blanks, across scanned XML. Inspection separately bounds selected-sheet scans at 2,000,000 stored cells and 64 MiB selected XML. | Actual full counts, populated/styled-only distinctions and observed extent; memory/time evidence for opening, candidates, copies and validation. Raising one count does not solve other budgets. |
| Sheet count | Editable admission rejects the 33rd local-name `sheet` element. Inspection allows up to 512 tab declarations. | Whole-package dependencies and measured clone/validation costs; navigation is not copy eligibility. |
| Cross-section formulas/subtotals | Structural scope permits only its narrow same-sheet reference/addition/SUM grammar. Cross-sheet, external, shared/array and other unsupported expressions reject. Formulas outside the selected sheet reject. | Independent expected dependency transforms, boundary expansion intent, downstream references and invalidation of formula caches. No assumption that cached totals are current. |
| Formatting/boundaries | Structural scope rejects merges, tables, hidden/grouped dimensions and advanced content. Row-height approval retains the narrow independently proven policy. | Explicit row templates and non-overlapping section bounds, exact style/row preservation and native reference controls. Headings/subtotals must never become vendor records. |
| Unknown relationships | Editable relationship allowlist failures create `UNKNOWN_PARTS` and disable snapshots; unsafe/dangling internal targets reject. Inspection checks internal targets and categorizes unknown relationship types. | Part inventory, unchanged relationship graph and content types; deny edits with unknown inbound dependencies. Never strip unknown parts. |

Editable bounds also include 16 MiB compressed, 64 MiB expanded, 1,024 entries, 500,000 XML elements and 4,096 `xf` elements. Inspection has independent resource/security limits; neither path may bypass malformed-package rejection. [Analyzer documentation](workbook-compatibility-analyzer.md) records the full budgets.

## Historical Step 51 capability qualification ledger

Every real-file capability below is **UNEXECUTED** in this run.

| Capability | Existing implementation boundary to evaluate when supplied |
| --- | --- |
| Metadata | Separate bounded ZIP/XML inspector; no worksheet scan implied. |
| Worksheet navigation | Selected ordinary XML cells only; hidden tabs remain context, charts/images are not rendered. Each uncached window scans the selected sheet. |
| Dates/amounts | Partial conservative interpretation; raw values and format codes retained. Unsupported formats/over-precision reject interpretation, not raw display. |
| Formula viewing | Stored expression/caches plus qualified shared-formula reconstruction; no recalculation or cache freshness guarantee. |
| Sections/vendor analysis | Inspection sessions cannot use workflow planning. Existing approved profiles require a validated non-inspection session. No bypass or new private-file analysis engine was added. |
| Editable session/copies/ordinary edits | Separate editable admission and snapshot eligibility required; viewing grants none of these. |
| Structural insertion/Merge | Production Apply disabled; Step 45 public tickets permanently review-only. Internal synthetic experiments do not qualify a real workbook. |
| Export | Inspection export disabled. Qualified ordinary sessions only have explicit validated new-file-only Save As; source overwrite is unavailable. |

Future layout qualification must establish actual header mappings, section/subtotal boundaries, record versus heading rules, repeated vendors across all sections, complete selected-sheet scan coverage and subtotal/reference impacts. The historical fields/rows are hypotheses only. The built-in synthetic profile maps a subset of accounting fields; VAT/EWT/net/payment calculations are not implemented. Keep approved matching, ambiguity, alias and duplicate rules unchanged. If safe isolated read-only feasibility analysis is needed, plan and test it separately after input evidence exists.

## Historical Step 51 conditional engineering alternatives

| Concern | A: extend existing editable envelope | B: targeted OOXML editor | C: isolated patch transaction and independent validation | D: separate reviewed output from read-only analysis |
| --- | --- | --- | --- | --- |
| Data preservation | Each added feature needs serializer/copy round-trip proof. | Restrict writable parts; retain every other part byte-for-byte. | Freeze source/candidate; validate exact authorized semantic delta before adoption. | Leaves complex workbook unchanged; output is a new artifact, not a reconstructed workbook. |
| Formulas/references | Prove library transformations and cache policy. | Explicit dependency grammar; reject unknown inbound references. | Independent expected transforms and invalidation checks; reopen alone insufficient. | Carry formulas only with explicit provenance; no claim to update original totals. |
| Names/external links | New preservation support must be proven, not waived. | Preserve unaffected names/link parts; affected dependencies block until supported. | Validate scope/text/graph against frozen baseline; never resolve external targets. | No original names/links transformed. |
| Package integrity | Validate all serializer effects and opaque resources. | Entry set, content types, relationship IDs and targets validated. | Independent before/after package and semantic oracle; intentional defect controls. | Separate schema/content validation; no original-package preservation claim needed. |
| Performance/memory | Full-context copies remain costly; benchmark actual sizes. | Streaming unaffected parts can reduce retention; edited XML still bounded. | Validation adds scans/storage; hard admission and cancellation required. | Bounded scans and small output possible; completeness still must be demonstrated. |
| Conflicts/source immutability | Existing revision/source hashes and new-file-only commit retained. | Source opened read-only; authorized patch bound to baseline hash. | Exact source/copy/destination revisions and candidate hashes; reject divergence/replay. | Hash source before/after; output uses a new path and explicit review. |
| Merge/export | Requalify full chain for every added feature. | Cannot reuse recorded-cell Merge for row shifts. | Transaction covers candidate, review, adoption, integration and new-file export independently. | No automatic Merge/import/export into original. |
| Complexity | Low for a proven resource-bound-only case; high for advanced preservation. | High: package/reference dependency correctness dominates. | High; complements B rather than replacing its transformation rules. | Lower mutation risk, but requires a clearly specified deliverable and complete analysis. |
| Actual workbook fit | **Unknown: no input.** | **Unknown: no input.** | **Unknown: no input.** | **Unknown: no input.** |

At Step 51, the smallest viable next step was to supply and qualify the real workbook. The conditional options were A for resource-only cases, B inside C for provably isolated edits, and D when dependencies remained unsafe. None was implemented or selected for production. Step 53 now supplies the actual evidence and current recommendation at the top of this document; these historical unknown-input entries are retained as the earlier decision record.
