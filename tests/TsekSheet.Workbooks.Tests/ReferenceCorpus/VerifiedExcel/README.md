# Retained artificial Step 44 reference corpus

These are generated synthetic fixtures, not private accounting workbooks.

- Eleven `*.input.xlsx`: ClosedXML-generated initial workbooks, including seven negative controls.
- Four `*.tseksheet.xlsx`: TsekSheet editor outputs, not Microsoft Excel outputs.
- Four `*.excel.xlsx`: actually produced by Microsoft Excel 16.0.20326.20232 on the matching generated inputs.
- `corpus.index.json`: exact operation, literal expectations and input/candidate/tool/procedure fingerprints.
- `excel-evidence.json`: actual Excel provenance, input/output hashes, raw insertion formulas and recalculated totals.
- `semantic-comparison-v3.json`: verifier fingerprint, semantic comparison, measured height differences and package/relationship inventories.

All four native cases match the compared business/formula/style projection. Row heights differ: Excel saves 21.95 versus TsekSheet 22. Full structural equivalence and production Apply are not established. The seven negative controls have no native Excel reference and remain pending; their TsekSheet rejection is intentional.

Do not regenerate or overwrite retained files. Changed implementation/specification fingerprints require a new corpus version. Matching fixtures can be copied to owned ignored artifacts by the tests; Excel is not required to recheck existing reference evidence. See `docs/development/excel-reference-corpus.md` for the controlled capture and human fallback procedure.
