param([Parameter(Mandatory=$true)][string]$CorpusDirectory)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/step54-style-reference'))+'\'
$root=(Resolve-Path -LiteralPath $CorpusDirectory).Path
if (-not ($root+'\').StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only owned artificial format-reference directories are accepted.' }
for ($walk=Get-Item -LiteralPath $root; $null -ne $walk; $walk=$walk.Parent) { if ($walk.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected corpus path rejected.' } }
$indexPath=Join-Path $root 'resolution.index.json'
$index=Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
if ($index.schemaVersion -ne 1 -or $index.fixtureKind -ne 'step54-artificial-format-resolution' -or @($index.cases).Count -ne 19) { throw 'Unknown synthetic provenance.' }
if ((Get-FileHash -LiteralPath $PSCommandPath).Hash -ne $index.procedureSha256) { throw 'Capture procedure fingerprint changed.' }
if (Test-Path -LiteralPath (Join-Path $root 'capture.json')) { throw 'Preserve existing capture; no overwrite.' }
if (Get-Process EXCEL -ErrorAction SilentlyContinue) { throw 'Excel is already running; no automation started.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($case in $index.cases) {
    if ($case.inputFile -notmatch '^[a-z-]+\.input\.xlsx$') { throw 'Unsafe synthetic input filename.' }
    $inputPath=Join-Path $root $case.inputFile
    if ((Get-FileHash -LiteralPath $inputPath).Hash -ne $case.inputSha256) { throw 'Synthetic input fingerprint mismatch.' }
    $zip=[IO.Compression.ZipFile]::OpenRead($inputPath)
    try { if (@($zip.Entries | Where-Object { $_.FullName -match '(?i)vba|externalLink|connection|activeX|embedding|customXml|_xmlsignatures' }).Count) { throw 'Linked or active fixture content rejected.' } }
    finally { $zip.Dispose() }
}
$excel=$null; $book=$null; $results=@()
try {
    $excel=New-Object -ComObject Excel.Application
    $excel.Visible=$false; $excel.DisplayAlerts=$false; $excel.EnableEvents=$false; $excel.AskToUpdateLinks=$false; $excel.AutomationSecurity=3
    $version=[string]$excel.Version; $build=[string]$excel.Build
    foreach ($case in $index.cases) {
        $inputPath=Join-Path $root $case.inputFile
        $outputFile=$case.spec.id+'.excel.xlsx'; $outputPath=Join-Path $root $outputFile
        if (Test-Path -LiteralPath $outputPath) { throw 'Native output already exists; no overwrite.' }
        $sheet=$null; $cell=$null; $observation=$null
        try {
            $book=$excel.Workbooks.Open($inputPath,0,$true)
            if ($book.Worksheets.Count -ne 1) { throw 'Unexpected generated worksheet count.' }
            $sheet=$book.Worksheets.Item('FormatProbe'); $cell=$sheet.Range('A1')
            $observation=[ordered]@{id=$case.spec.id; status='observed'; text=[string]$cell.Text; value2=$cell.Value2; numberFormat=[string]$cell.NumberFormat; originalNumberFormatId=$case.spec.formatId; originalFormatCode=$case.spec.code; rawInput=$case.spec.raw; literalExpected=$case.spec.expectedDisplay; formula=[string]$cell.Formula; outputFile=$outputFile}
            $book.SaveCopyAs($outputPath)
            $observation.outputSha256=(Get-FileHash -LiteralPath $outputPath).Hash
        } catch {
            if ($null -ne $case.spec.expectedDisplay) { throw }
            $observation=[ordered]@{id=$case.spec.id; status='native-rejected'; error=$_.Exception.Message; rawInput=$case.spec.raw; originalNumberFormatId=$case.spec.formatId; originalFormatCode=$case.spec.code}
        } finally {
            if ($cell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($cell) }
            if ($sheet) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet) }
            if ($book) { $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book); $book=$null }
        }
        if ((Get-FileHash -LiteralPath $inputPath).Hash -ne $case.inputSha256) { throw 'Native capture changed the synthetic input.' }
        $results += $observation
    }
    $capture=[ordered]@{schemaVersion=1; provenance='Actual Microsoft Excel; isolated generated format-resolution inputs only'; excelVersion=$version; excelBuild=$build; generatedUtc=[datetime]::UtcNow.ToString('o'); indexSha256=(Get-FileHash -LiteralPath $indexPath).Hash; scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash; cases=$results}
    $stream=[IO.File]::Open((Join-Path $root 'capture.json'),[IO.FileMode]::CreateNew); $writer=New-Object IO.StreamWriter($stream)
    try { $writer.Write(($capture | ConvertTo-Json -Depth 12)) } finally { $writer.Dispose() }
    "Excel $version build ${build}: captured $($results.Count) artificial format-resolution cases."
} finally {
    if ($book) { $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book) }
    if ($excel) { $excel.Quit(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel) }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
