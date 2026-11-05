# Workbook library experiment (not production code)

This isolated .NET 10 project is deliberately excluded from `TsekSheet.sln` and has no production project reference. It compares exact library versions against synthetic packages. It does not establish full Excel compatibility.

Security scope: [Step 41](../../docs/development/dependency-security-review.md) patches the evaluation-only cryptography dependencies while retaining NPOI 2.7.6 and its historical matrix. **ImageSharp 2.1.11 still has five advisories.** Run only the retained hash-checked synthetic fixtures or internally generated benchmarks; this is not a general workbook/image tool and is not qualified for external inputs. Audit warnings remain visible. Original research reports remain historical; the new matrix is compared against them rather than rewriting expected failures.

Implementation sequence:

1. Generate two small fixtures independently with Open XML SDK; validate their package schema and explicit semantic expectations.
2. Reopen read-only source bytes into independent memory streams for every experiment. Never pass a fixture path to a library's saving API.
3. Run no-change save, cell edit, serialized deep-copy isolation, worksheet copy, candidate replacement, and row-insertion probes where APIs and licensing permit.
4. Compare saved output through a separate ZIP/XML semantic inspector and Open XML schema validator, including cross-sheet references, tables, merged ranges, hidden state, names, styles, and metadata. Record failures as evidence rather than weakening expectations.
5. Run bounded performance samples separately, then execute harness safety tests and the unchanged application's regression suite.

Fixture generation uses create-new semantics. Re-running evaluation never regenerates or overwrites committed source fixtures. Each experiment creates an output under a fresh `artifacts/workbook-evaluation/<run-id>` directory. SHA-256 source checks before/after every probe are mandatory. Reports contain synthetic data only.

EPPlus is researched but not executed: no commercial/evaluation license was supplied and this project anticipates commercial application. Do not claim noncommercial status or embed a license key. NPOI 2.8.x binary terms are separately documented; experiments use 2.7.6 after inspecting its package terms.

Commands and observed results are recorded in `docs/research/workbook-library-evaluation.md`. Expected capability gaps appear in the experiment report; automated harness tests enforce safety and validator behavior rather than disguising those gaps as supported operations. A zero matrix-runner exit code means no execution error/source mutation; it does not mean every compatibility case passed. Read `Status`, `Differences`, and `Observations` in the report.
