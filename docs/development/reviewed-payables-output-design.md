# Reviewed separate Payables output — design only

Step 55 implements [memory-only section and identity review](payables-section-review.md), not this proposed output lifecycle. Its assertions are retractable review facts, not financial approval, posting approval or a portable schedule. No review-profile/decision export or restore exists; a future schema must retain private evidence, original/effective profile fingerprints, source/sheet/order/capture/decision bindings and independent new-file commit/retention validation.

Step 54 documents a future reviewed posting schedule. No output/export command, serializer, save dialog, staging directory or approval capability for it is implemented. Existing inspection export stays disabled. A schedule would record reviewed proposed transactions; it would **not update the source ledger or represent integrated workbook rows**.

## Proposed artifact and bindings

A bounded local document could contain a versioned manifest and transaction rows. The initial format, packaging and maximum batch size must be independently specified/tested before implementation; a spreadsheet or human-readable report must not be reconstructed from the complex original workbook.

| Proposed field group | Contents and purpose |
| --- | --- |
| Provenance | Schema version, schedule ID/revision, generation-tool version, source workbook SHA-256, selected worksheet identity/order, profile fingerprint and capture/session revision. Real source hashes/identities remain local and private. |
| Source references | Original record/section coordinates, selected candidate/section reference, boundary confidence, role-coverage result and unresolved diagnostics. References identify evidence, not permission to insert. |
| Reviewed transaction | Literal vendor identity, RFP and invoice identifiers, date with raw/verified interpretation distinction, particulars, amount, other charges, VAT, total/EWT/net/payment/CV fields where explicitly reviewed. No guessed formulas or invented tax calculations. |
| Review decisions | Missing-identity resolution with reviewer evidence; explicit name/section choice; every duplicate warning and acknowledgment; the reviewed field values and their revision-bound digest. |
| Requested destination | Human-selected destination label/reference and confidence. A provisional section remains explicitly provisional; its selection does not establish formula or row-shift safety. |
| Approval and artifact integrity | Review state, reviewer confirmation, approved schedule revision/digest, output revision, source-binding digest and final artifact fingerprint. Approval applies only to the separate schedule. |

Private vendor/source identifiers belong in the user-selected local artifact only after review, never tracked source, ordinary logs, browser storage or diagnostics telemetry. Before implementation, define sensitive-output retention, explicit disposal and recovery behavior without a database or disk-backed inspection cache.

## Proposed lifecycle

1. Capture bounded read-only facts from the immutable source and create an in-memory draft schedule. Preserve raw values and stale-cache warnings. Original workbook bytes remain unchanged.
2. Resolve each missing identity by explicit human verification; never inherit the preceding vendor. Similar legal names remain separate unless an explicit reviewed alias/identity decision is supplied. Surface all incomplete search/section coverage and possible duplicates.
3. Review all supplied financial fields against their sources. Unsupported display formats and formula caches cannot stand in for verified amounts/dates. Reconfirm actual accounting calculations elsewhere or through a separately specified, tested process; this design supplies no Excel-equivalent calculation engine.
4. Freeze the schedule revision, source/capture/profile bindings, chosen identities, fields and warning decisions into a digest. Changing any field, source/profile binding, section choice or review decision invalidates approval. Reopening an inspection obtains new session identities and requires explicit rebinding/review.
5. An explicit **approve separate schedule** action could approve only those exact draft facts. Unresolved required identities/fields must block approval. Incomplete original-workbook hierarchy must remain visible; schedule approval does not upgrade a candidate section or authorize an insertion destination.
6. A separate explicit **create reviewed schedule** action could write a brand-new local artifact after validation and an independently tested no-overwrite commit. Its receipt would identify schedule/output revision and artifact hash, with source immutability checked again. It must never write to the source or an existing destination, auto-save, auto-import or invoke Merge.

Suggested draft states are `Draft`, `NeedsIdentityReview`, `NeedsFinancialReview`, `NeedsDuplicateReview`, `ReadyForScheduleReview`, `ApprovedForSeparateSchedule` and `CreatedSeparateArtifact`. “Posted”, “Applied”, “Merged”, or “Integrated” are not schedule states. An external/manual posting action, if ever tracked, needs a separate explicitly reviewed provenance record and must not be inferred from artifact creation.

## Implementation gates still required

Future work must define the artifact schema, date/decimal serialization without locale/precision loss, formula-injection rejection for text fields, redacted diagnostics, bounded batch handling, approvals/replay/conflicts/cancellation, output reopen/semantic checks, no-overwrite commit and offline recovery. Synthetic tests must prove stale approvals and unresolved identities cannot be exported as approved records. Existing-file replacement and source overwrite remain excluded.

TsekSheet's current new-file-only workbook saving code is qualified for its existing supported workbook contract, not automatically for this new artifact type or a complex-source reconstruction. Reuse needs an explicit review of the relevant commit boundaries and validation responsibilities.

If the ledger must eventually receive rows, that is a separate engineering objective requiring a preservation-aware transformation and independent package/reference oracle for the actual feature envelope. A reviewed schedule, a complete read-only scan and a passing native display comparison supply no such mutation proof. See [compatibility strategy](real-workbook-compatibility-strategy.md) and [read-only analysis](read-only-payables-section-analysis.md).
