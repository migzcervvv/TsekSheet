param([Parameter(Mandatory=$true)][string]$CorpusDirectory)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\step45-reference'))+'\'
$root=(Resolve-Path -LiteralPath $CorpusDirectory).Path
if (-not ($root+'\').StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only generated Step 45 corpus paths are accepted.' }
$walk=Get-Item -LiteralPath $root
while ($walk.FullName.Length -ge $repoRoot.Length) { if ($walk.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected path.' }; $walk=$walk.Parent }
$indexPath=Join-Path $root 'formatting.index.json'
$index=Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
if ($index.schemaVersion -ne 1 -or $index.fixtureKind -ne 'generated-artificial-formatting-only') { throw 'Unknown fixture provenance.' }
if (Test-Path -LiteralPath (Join-Path $root 'excel-formatting-evidence.json')) { throw 'Preserve existing capture; no overwrite.' }
if (Get-Process EXCEL -ErrorAction SilentlyContinue) { throw 'Excel already running; no automation started.' }
foreach ($case in $index.cases) {
    if ($case.inputFile -notmatch '^[a-z-]+\.input\.xlsx$') { throw 'Unsafe filename.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $case.inputFile)).Hash -ne $case.inputSha256) { throw 'Input fingerprint mismatch.' }
}
$excel=$null; $results=@()
try {
    $excel=New-Object -ComObject Excel.Application
    $excel.Visible=$false; $excel.DisplayAlerts=$false; $excel.EnableEvents=$false; $excel.AskToUpdateLinks=$false; $excel.AutomationSecurity=3
    $version=[string]$excel.Version; $build=[string]$excel.Build
    foreach ($case in $index.cases) {
        $inputPath=Join-Path $root $case.inputFile; $outputFile=$case.spec.id+'.excel.xlsx'; $outputPath=Join-Path $root $outputFile
        if (Test-Path -LiteralPath $outputPath) { throw 'Reference output exists; refusing overwrite.' }
        $book=$null; $sheet=$null; $observations=@()
        try {
            $book=$excel.Workbooks.Open($inputPath,0,$true); $sheet=$book.Worksheets.Item('Ledger')
            $openedHeight=[double]$sheet.Rows.Item(16).RowHeight; $openedRangeHeight=[double]$sheet.Rows.Item(16).Height
            foreach ($operation in $case.operations) {
                $boundary=[int]$operation.boundary; $count=@($operation.records).Count; $last=$boundary+$count-1
                $format=[string]$sheet.Cells.Item([int]$operation.subtotalRow,5).NumberFormat
                $null=$sheet.Range("${boundary}:${last}").EntireRow.Insert()
                $raw=@{}
                for ($r=1;$r -le 50;$r++) { $cell=$sheet.Cells.Item($r,5); if ($cell.HasFormula) { $raw['E'+$r]=[string]$cell.Formula } }
                for ($offset=0;$offset -lt $count;$offset++) {
                    $row=$boundary+$offset; $template=[int]$operation.templateRow
                    $target=$sheet.Range("A${row}:H${row}"); $null=$sheet.Range("A${template}:H${template}").Copy($target); $null=$target.ClearContents()
                    $sheet.Rows.Item($row).RowHeight=$sheet.Rows.Item($template).RowHeight
                    foreach ($field in $operation.records[$offset].fields) {
                        if ([string]::IsNullOrEmpty($field.value)) { continue }
                        $cell=$sheet.Cells.Item($row,[int]$field.column)
                        if ($field.kind -eq 'decimal') { $cell.Value2=[double]::Parse($field.value,[Globalization.CultureInfo]::InvariantCulture) }
                        elseif ($field.kind -eq 'date') { $cell.Value2=[datetime]::ParseExact($field.value,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture).ToOADate() }
                        elseif ($field.kind -eq 'text' -and $field.value -notmatch '^[=+@-]') { $cell.Value2=[string]$field.value }
                        else { throw 'Unsupported literal.' }
                    }
                }
                $subtotal=[int]$operation.subtotalRow+$count; $first=[int]$operation.firstDataRow; $end=$subtotal-1
                $sheet.Cells.Item($subtotal,5).Formula="=SUM(E${first}:E${end})"; $sheet.Cells.Item($subtotal,5).NumberFormat=$format
                $observations += [ordered]@{ boundary=$boundary; count=$count; rawFormulas=$raw; templateHeight=[double]$sheet.Rows.Item([int]$operation.templateRow).RowHeight; insertedHeight=[double]$sheet.Rows.Item($boundary).RowHeight }
            }
            $excel.CalculateFullRebuild(); $beforeSaveHeight=[double]$sheet.Rows.Item(16).RowHeight
            $null=$book.SaveAs($outputPath,51)
        }
        finally { if ($book) { $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book) }; if ($sheet) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet) } }
        $reopened=$null; $reopenSheet=$null
        try { $reopened=$excel.Workbooks.Open($outputPath,0,$true); $reopenSheet=$reopened.Worksheets.Item('Ledger'); $reopenedHeight=[double]$reopenSheet.Rows.Item(16).RowHeight }
        finally { if ($reopened) { $reopened.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reopened) }; if ($reopenSheet) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reopenSheet) } }
        if ((Get-FileHash -LiteralPath $inputPath).Hash -ne $case.inputSha256) { throw 'Source changed.' }
        $results += [ordered]@{ id=$case.spec.id; inputSha256=$case.inputSha256; outputFile=$outputFile; outputSha256=(Get-FileHash -LiteralPath $outputPath).Hash; openedHeight=$openedHeight; openedRangeHeight=$openedRangeHeight; beforeSaveHeight=$beforeSaveHeight; reopenedHeight=$reopenedHeight; operations=$observations }
    }
    $exe=(Get-ItemProperty 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe').'(default)'
    $evidence=[ordered]@{ schemaVersion=1; provenance='Microsoft Excel isolated synthetic no-op and insertion formatting experiments'; excelVersion=$version; excelBuild=$build; executableVersion=(Get-Item -LiteralPath $exe).VersionInfo.FileVersion; scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash; indexSha256=(Get-FileHash -LiteralPath $indexPath).Hash; generatedUtc=[datetime]::UtcNow.ToString('o'); cases=$results }
    $writer=New-Object IO.StreamWriter([IO.File]::Open((Join-Path $root 'excel-formatting-evidence.json'),[IO.FileMode]::CreateNew))
    try { $writer.Write(($evidence|ConvertTo-Json -Depth 24)) } finally { $writer.Dispose() }
    "Excel $version build ${build}: $($results.Count) generated formatting references."
}
finally { if ($excel) { $excel.Quit(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel) }; [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect(); [GC]::WaitForPendingFinalizers() }
