param([Parameter(Mandatory=$true)][string]$CorpusDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\step44-reference')) + [IO.Path]::DirectorySeparatorChar
$corpusRoot = (Resolve-Path -LiteralPath $CorpusDirectory).Path
if (-not ($corpusRoot + '\').StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only owned Step 44 artificial corpus directories are accepted.' }
$walk = Get-Item -LiteralPath $corpusRoot
while ($walk.FullName.Length -ge $repoRoot.Length) { if ($walk.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected corpus paths are forbidden.' }; $walk = $walk.Parent }
$indexPath = Join-Path $corpusRoot 'corpus.index.json'
$index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
if ($index.schemaVersion -ne 1 -or $index.fixtureKind -ne 'generated-artificial-ledger-only' -or $index.fixtureGenerator -ne 'ClosedXML 0.105.1; NOT Microsoft Excel') { throw 'Unknown corpus provenance.' }
$evidencePath = Join-Path $corpusRoot 'excel-evidence.json'
if (Test-Path -LiteralPath $evidencePath) { throw 'Excel evidence already exists; preserve the accepted run.' }
if (Get-Process EXCEL -ErrorAction SilentlyContinue) { throw 'Excel is already running; isolated automation was not started.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($case in @($index.cases | Where-Object { $_.spec.supported })) {
    if ($case.inputFile -notmatch '^[a-z-]+\.input\.xlsx$' -or $case.spec.count -lt 1 -or $case.spec.count -gt 20 -or $case.spec.boundary -gt 2000) { throw 'Unsafe fixture operation.' }
    $inputPath = Join-Path $corpusRoot $case.inputFile
    if ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $case.inputSha256) { throw 'Input fingerprint mismatch.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($inputPath)
    try { if (@($zip.Entries | Where-Object { $_.FullName -match '(?i)vba|externalLink|connection|activeX|embedding|customXml|calcChain|_xmlsignatures' }).Count) { throw 'Active/linked content is forbidden.' } }
    finally { $zip.Dispose() }
}
$excel = $null
$results = @()
try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false; $excel.DisplayAlerts = $false; $excel.EnableEvents = $false; $excel.AskToUpdateLinks = $false; $excel.AutomationSecurity = 3
    $excelVersion = [string]$excel.Version; $excelBuild = [string]$excel.Build
    foreach ($case in @($index.cases | Where-Object { $_.spec.supported })) {
        $inputPath = Join-Path $corpusRoot $case.inputFile
        $outputFile = $case.spec.id + '.excel.xlsx'; $outputPath = Join-Path $corpusRoot $outputFile
        if (Test-Path -LiteralPath $outputPath) { throw 'Reference output already exists; no overwrite is permitted.' }
        $book = $null; $sheet = $null
        try {
            # UpdateLinks=0 and ReadOnly=true. Never save back to the input.
            $book = $excel.Workbooks.Open($inputPath, 0, $true)
            $sheet = $book.Worksheets.Item('Ledger')
            $subtotalFormat = [string]$sheet.Cells.Item([int]$case.spec.subtotal, 5).NumberFormat
            $boundary = [int]$case.spec.boundary; $count = [int]$case.spec.count
            $last = $boundary + $count - 1
            $null = $sheet.Range("${boundary}:${last}").EntireRow.Insert()
            $rawFormulas = @{}
            for ($row = 1; $row -le 48; $row++) { $cell = $sheet.Cells.Item($row, 5); if ($cell.HasFormula) { $rawFormulas['E' + $row] = [string]$cell.Formula } }
            for ($offset = 0; $offset -lt $count; $offset++) {
                $row = $boundary + $offset; $template = [int]$case.spec.template
                $sourceRange = $sheet.Range("A${template}:H${template}")
                $targetRange = $sheet.Range("A${row}:H${row}")
                $null = $sourceRange.Copy($targetRange); $null = $targetRange.ClearContents()
                $sheet.Rows.Item($row).RowHeight = $sheet.Rows.Item($template).RowHeight
                foreach ($field in $case.manifest.records[$offset].fields) {
                    if ([string]::IsNullOrEmpty($field.value)) { continue }
                    $cell = $sheet.Cells.Item($row, [int]$field.column)
                    if ($field.kind -eq 'decimal') { $cell.Value2 = [double]::Parse($field.value, [Globalization.CultureInfo]::InvariantCulture) }
                    elseif ($field.kind -eq 'date') { $cell.Value2 = [datetime]::ParseExact($field.value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).ToOADate() }
                    elseif ($field.kind -eq 'text' -and $field.value -notmatch '^[=+@-]') { $cell.Value2 = [string]$field.value }
                    else { throw 'Unsupported reference field.' }
                }
            }
            # Explicit business operation, distinct from Excel's raw insertion semantics captured above.
            $subtotal = [int]$case.spec.subtotal + $count; $first = [int]$case.spec.firstData; $end = $subtotal - 1
            $sheet.Cells.Item($subtotal, 5).Formula = "=SUM(E${first}:E${end})"
            $sheet.Cells.Item($subtotal, 5).NumberFormat = $subtotalFormat
            $excel.CalculateFullRebuild()
            $calculatedTotals = [ordered]@{ selectedSection=[double]$sheet.Cells.Item($subtotal, 5).Value2; grandSummary=[double]$sheet.Cells.Item((25 + $count), 5).Value2; verification='Excel CalculateFullRebuild on this generated fixture only' }
            $formatObservations = [ordered]@{ amountFormat=[string]$sheet.Cells.Item($boundary, 5).NumberFormat; dateFormat=[string]$sheet.Cells.Item($boundary, 6).NumberFormat; insertedRowHeight=[double]$sheet.Rows.Item($boundary).RowHeight }
            $null = $book.SaveAs($outputPath, 51)
        }
        finally {
            if ($book) { $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book) }
            if ($sheet) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet) }
        }
        if ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $case.inputSha256) { throw 'Excel changed an original input.' }
        $results += [ordered]@{ id=$case.spec.id; outputFile=$outputFile; inputSha256=$case.inputSha256; outputSha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash; rawAfterInsertionFormulas=$rawFormulas; formatObservations=$formatObservations; calculatedTotals=$calculatedTotals; status='Excel-generated; semantic cross-check required' }
    }
    $excelExecutable = (Get-ItemProperty 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe').'(default)'
    $evidence = [ordered]@{ schemaVersion=1; provenance='Microsoft Excel COM automation on generated isolated fixtures'; excelVersion=$excelVersion; excelBuild=$excelBuild; executableVersion=(Get-Item -LiteralPath $excelExecutable).VersionInfo.FileVersion; scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash; indexSha256=(Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash; generatedUtc=[datetime]::UtcNow.ToString('o'); cases=$results }
    $json = $evidence | ConvertTo-Json -Depth 20
    $stream = [IO.File]::Open($evidencePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $writer = New-Object IO.StreamWriter($stream)
    try { $writer.Write($json) } finally { $writer.Dispose() }
    "Excel $excelVersion build $excelBuild generated $($results.Count) isolated references: $evidencePath"
}
finally {
    if ($excel) { $excel.Quit(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel) }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
