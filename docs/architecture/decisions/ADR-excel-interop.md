# Excel interop is a build-time dependency

Status: accepted by the planner, D10 (2026-11-03).

Excel desktop is the required workbook engine. Use typed `Microsoft.Office.Interop.Excel`
16.0.18925.20022 only at build time, with `PrivateAssets=all`, explicit compiler references
using `EmbedInteropTypes=true`, and `Private=false`. Package assets are excluded from runtime.
The Office core enum PIA comes from build-only `ExcelDna.Interop` 16.0.0 (package-declared MIT).
Neither Microsoft interop DLL is redistributed in application output.

The Excel package declares no license and describes an unsupported repackaging of Office
assemblies. D10 accepts the build-time-only reference; it does not assert an AGPL license grant
for Microsoft binaries. Users supply a licensed desktop Excel installation. The dependency
license scan must allowlist `Microsoft.Office.Interop.Excel` by exact package name for this
build-only use; a future change that copies a PIA to output needs a new distribution decision.
There is no active license-scan pipeline yet (T7).

`InteropReferences.props` is shared by adapter and Excel fixture tests. Reflection tests verify
embedded COM types and no PIA assembly dependency; Release output checks verify absence of
`Microsoft.Office.Interop.Excel.dll` and `office.dll`. No Excel object crosses the native adapter.

Package metadata: [Excel interop](https://www.nuget.org/packages/Microsoft.Office.Interop.Excel/16.0.18925.20022),
[Excel-DNA interop](https://www.nuget.org/packages/ExcelDna.Interop/16.0.0).
