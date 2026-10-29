# Step 45 retained synthetic formatting evidence

Seventeen artificial inputs, seventeen TsekSheet baselines/candidates and seventeen actual Microsoft Excel outputs are retained. Eight cases are no-op saves; nine are insertion/reference operations. None is a private accounting workbook.

`formatting.index.json` records literal expectations, exact manifests and input/candidate hashes. `excel-formatting-evidence.json` records Excel 16.0.20326.20232, procedure/index hashes, output hashes and opened/pre-save/reopened COM observations. Fingerprinted `formatting-comparison-*.json` reports retain direct row/default/custom-height attributes, current editor/oracle/verifier identities and independent comparisons. The earlier unversioned comparison is retained as an initial diagnostic, not overwritten.

Exact saved height values differ even after no-op Excel saving: 22 becomes 21.95; 20 becomes 20.100000000000001. COM still reports 22/20 after reopening. These values are not treated as equivalent. Tested 21-point insertion cases retain exact height and custom-height attributes, including batch, repeated raw insertion, custom currency, decorated styling and a blank template experiment. Other exact observations do not automatically expand approval or production support.

Only fixed 21-point templates with tested defaults may receive review-only approval. All production adoption remains disabled. Repeated raw insertion and blank-template references do not establish a supported production lifecycle. See `docs/development/step45-verification.md`.

Do not overwrite retained files or relabel baselines as Excel results. New capture versions require new artifact directories and metadata. Existing inputs and outputs are verified by their stored SHA-256.
