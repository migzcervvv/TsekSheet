# Read-only hierarchical section and identity analysis

Step 54 adds a separate inspection analysis path. It is not a workflow planner, an insertion preview, or a preservation certificate. Step 55 adds the [read-only Section Review panel](payables-section-review.md), native configured-profile import, source-grounded assertions, missing-identity/duplicate queues and pure reanalysis of one retained capture. Profile editing and decision save/restore remain unavailable. The original Step 54 analysis contract below remains the baseline without supplemental assertions.

## Architecture and boundaries

`IInspectionAnalysisSession.CaptureInspectionRegion` in the Application contracts is implemented only by `InspectionSession`. It retains the same read-only source handle and ZIP/XML reader as normal inspection. A request includes exact session, worksheet, revision, source fingerprint, start/end rows and explicit column numbers. Wrong/stale bindings, disposal, malformed XML, cancellation and resource failures reject. It cannot create a snapshot, editable session, working copy, candidate or export.

One capture streams the complete selected worksheet, resolves requested shared strings and qualified shared formulas, and retains only the requested role rectangle. It does not reuse XML offsets as ZIP seek positions. It does not write a sidecar or disk cache. Every requested coordinate appears once in the result, including blank/styled and hidden rows. Declared dimensions do not allocate a rectangle. `LastRowElement` records row elements beyond the last stored cell, so styled/hidden empty tails cannot silently disappear from whole-row coverage checks.

Capture limits: at most 2,000 requested rows, 32 distinct columns, 40,000 retained coordinates and 16 MiB of accounted payload; the existing selected-sheet two-million-cell, byte/depth/element, shared-string and ten-second cooperative operation budgets also apply. Shared-text retention is bounded separately. Accounting includes raw/display/formula/format strings and fixed allowances; it is an admission estimate, not a hard CLR heap quota. Returned captures belong to the caller and are not added to the eight-window UI cache. Callers must not accumulate unbounded captures.

`ReadOnlySectionAnalysis.Read` coordinates capture, validates source/session/profile bindings before and after, and invokes a pure `Analyze` function. `Analyze` accepts a complete captured region and a separately versioned `ReadOnlySectionProfile`; it does not call the editable workflow planner. The pure pass has explicit bounds on fields, sections, names, candidate/dependency lists and diagnostics, plus cancellation checks. It is not a hard wall-clock service; callers can additionally cancel it by deadline.

Every result is bound to source/session/worksheet/revision/profile fingerprints. `CanInsert` is **always false**, on the report and on each candidate section. `BoundaryRuleMatched` means one configured boundary rule matched the captured facts. `Status` still calls it **provisional**. It is not global hierarchy verification, a chosen destination or approval to move rows.

## Profile configuration

`ParseProfile` accepts at most 65,536 characters/depth 12 and rejects duplicate or unknown JSON members. Typed validation rejects unknown boundary kinds, conflicting header aliases, missing parents, cycles, oversized scopes and marker columns outside the capture. These profiles are not accepted by `WorkflowProfiles.Parse` and never enter the approved editable profile catalog.

Configurable roles include field headers/explicit aliases, separate marker columns, entity and identifier fields, context fields, repeated-header policy, section parents/data-start offsets/end rules, summary markers and exclusions. Normalization is NFC plus case folding and whitespace collapse; punctuation and legal-name suffixes are retained. Header aliases are configuration, not vendor aliases or inferred entity equivalence.

Artificial example, unrelated to the private workbook:

```json
{
  "id": "artificial-purchases", "version": 1,
  "startRow": 1, "endRow": 40, "columns": [1, 3, 4, 5, 6],
  "fields": [
    {"key": "entity", "header": "Entity", "headerAliases": ["Payee"]},
    {"key": "request", "header": "Request"},
    {"key": "invoice", "header": "Invoice"},
    {"key": "amount", "header": "Amount"}
  ],
  "entityField": "entity", "identifierFields": ["request", "invoice"],
  "contextFields": ["amount"],
  "sections": [
    {"id": "purchases", "label": "Artificial purchases", "parentId": null,
     "heading": {"text": "Purchases", "columns": [1]},
     "end": {"kind": "marker", "marker": {"text": "Purchases total", "columns": [1]}}},
    {"id": "equipment", "label": "Artificial equipment", "parentId": "purchases",
     "heading": {"text": "Equipment", "columns": [1]},
     "end": {"kind": "marker", "marker": {"text": "Equipment total", "columns": [1]}}}
  ],
  "summaryMarkers": [{"text": "Ledger overview", "columns": [1]}],
  "exclusionMarkers": [{"text": "Reviewed note", "columns": [1]}],
  "allowRepeatedHeaders": true
}
```

This illustrates configuration, not a ready-made profile for arbitrary books. Fields are mapped by headers inside the explicit role columns, not engine-specific addresses. Header aliases must be authored/reviewed explicitly. Repeated complete headers must retain the same mapping; shifted headers reject. Header-like partial matches are diagnosed and excluded from vendor interpretation while keeping coverage unverified.

## Boundary and coverage rules

End rules are `marker`, `explicit-row`, or `contiguous-sum`. A marker must have one matching end after the configured data start; an explicit row is a profile assertion, not independently inferred intent. The SUM rule accepts only a complete local single-column range beginning at the configured data start and ending immediately above its formula row. Unsupported/truncated shared expressions do not qualify. **Every matching candidate is retained; no first/nearest SUM is selected.** A unique intermediate SUM can still leave downstream records unassigned, which invalidates the overall partition.

Parents/children must be strictly contained. Partial overlaps, undeclared nesting and overlapping siblings are diagnosed. Record ownership requires one deepest containing candidate; ties or no owner remain unassigned. Heading/end-marker uniqueness currently applies over the captured region; repeated child labels that cannot be distinguished by the configured markers are rejected, not automatically scoped/inferred.

The report includes headers and their mappings, section/boundary candidates, parent IDs, intermediate subtotal candidates, summary/excluded/entity-only rows, unassigned rows, hidden-row inclusion, identity cases, duplicate/similarity suggestions and typed diagnostics. Formula-bearing context outside verified candidate regions needs an explicit summary classification. Lexical summary references are reported with `Complete=false`: this is not a dependency parser, formula evaluation or a name/external-link resolver.

Coverage is deliberately separated:

- `EntireWorksheetRowsCaptured`: the explicit request covers all observed cell/row elements, independent of an inflated dimension declaration.
- `RoleColumnsComplete`: every configured role mapped and no captured content was silently truncated. This is not a claim to capture every Excel column.
- `RecordCoverageComplete`: no unassigned meaningful rows, conflicting headers or marker/record exclusions remain.
- `HierarchyVerified`: the complete bounded record partition satisfies all configured boundary/nesting/summary rules. It still does not certify structural insertion.
- `IdentitiesVerified`: complete record coverage with literal, bounded identities and no unresolved identity/marker conflicts.

## Identity and review behavior

Literal entity plus at least one literal identifier becomes a candidate record. Blank/non-text vendors with identifiers, formula-derived identities, truncated/overlength roles and contextual rows without identifiers are unresolved. **No vendor is filled down.** A heading/subtotal/summary/excluded row that resembles a record is retained as a conflict rather than silently hidden.

Known literal vendors are grouped conservatively; memberships stay provisional while section/identity coverage is incomplete. `SectionCoverageComplete` is false in that state. Similar names generate review suggestions only; distinct entities are never automatically merged. The bounded suggestion set covers punctuation-compacted equality and one-edit differences, not an exhaustive legal-entity equivalence search.

Potential duplicates group the same normalized vendor with any identical nonblank configured identifier, preserving the contributing rows and fields. Unknown vendors prevent a complete duplicate review claim. A repeated identifier is a warning, not proof that a transaction should be deleted or posted.

The approved destination rules remain specifications: entire-selected-sheet search; after the last entry in a single matching section; explicit choice across sections; absent-vendor placement before the configured external subtotal; human review of names and identifiers. This component returns **no placement proposal or workflow plan** even when its read-only hierarchy passes.

## Actual qualification and limits

[Step 54 verification](step54-verification.md) records full target role capture, both header rows, 25 missing vendors and a rejected partial partition. The private profile is an explicit diagnostic hypothesis, not an approved production template. It contains private header/marker text only in ignored artifacts. Its SUM rules intentionally demonstrate that the previous intermediate-total shortcut is unsafe.

Full hierarchy certification is still unavailable for the real book. Explicit profile review, hierarchical aggregation intent and complete formula/name/reference handling remain future work. No style/name/link cleanup, mutation-envelope expansion or private-data export is enabled.
