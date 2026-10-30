$ErrorActionPreference = 'Stop'
# No input paths: exclusively creates new artificial workbooks in an owned directory.
$root = Join-Path $PSScriptRoot '../artifacts/step49'
$run = Join-Path $root ('native-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$cases = @(
    @(1234.5, '#,##0.00', '1,234.50'),
    @(-1234.5, '#,##0.00;(#,##0.00)', '(1,234.50)'),
    @(1234.5, '"₱" #,##0.00', '₱ 1,234.50'),
    @(-1234.5, '"₱" #,##0.00;("₱" #,##0.00)', '(₱ 1,234.50)'),
    @(0, '0.00;(0.00);"-"', '-'),
    @(0, '0.00;(0.00);""', ''),
    @(0.125, '0.00%', '12.50%'),
    @(-0.125, '0.0%;(0.0%)', '(12.5%)'),
    @(1.235, '0.00', '1.24'),
    @('0000123', '@', '0000123'),
    @(61, 'yyyy-mm-dd', '1900-03-01'),
    @(61.5, 'yyyy-mm-dd hh:mm:ss', '1900-03-01 12:00:00'),
    @(12.5, '[$-409]#,##0.00', '12.50'),
    @(0, '0.00;(0.00);', ''),
    @(0, '0.00', '2.00')
)
$excel = $null; $book = $null
try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false; $excel.DisplayAlerts = $false; $excel.EnableEvents = $false
    $excel.AutomationSecurity = 3; $excel.AskToUpdateLinks = $false
    $version = $excel.Version; $build = $excel.Build
    $evidence = @()
    foreach ($date1904 in @($false, $true)) {
        $book = $excel.Workbooks.Add(); $book.Date1904 = $date1904
        $sheet = $book.Worksheets.Item(1); $sheet.Columns.Item(1).ColumnWidth = 48
        for ($i=0; $i -lt $cases.Count; $i++) {
            $cell = $sheet.Cells.Item($i+1,1); $cell.NumberFormat = $cases[$i][1]
            if ($i -eq 14) { $cell.Formula = '=1+1' } else { $value=if($cases[$i][0] -is [string]){$cases[$i][0]}else{[double]$cases[$i][0]}; $cell.GetType().InvokeMember("Value2",[Reflection.BindingFlags]::SetProperty,$null,$cell,@($value)) }
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($cell)
        }
        $excel.Calculate()
        $label = if($date1904){'1904'}else{'1900'}
        $inputPath = Join-Path $run ($label + '.input.xlsx'); $outputPath = Join-Path $run ($label + '.output.xlsx')
        $book.SaveAs($inputPath,51); $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book); $book=$null
        $before = (Get-FileHash $inputPath).Hash
        $book = $excel.Workbooks.Open($inputPath,0,$true); $sheet = $book.Worksheets.Item(1)
        $rows = @()
        for($i=0;$i -lt $cases.Count;$i++){
            $cell=$sheet.Cells.Item($i+1,1); $expected=$cases[$i][2]
            if($date1904 -and $i -eq 10){$expected='1904-03-02'}
            if($date1904 -and $i -eq 11){$expected='1904-03-02 12:00:00'}
            $rows += [ordered]@{row=$i+1; expected=$expected; text=[string]$cell.Text; value2=$cell.Value2; numberFormat=[string]$cell.NumberFormat; formula=[string]$cell.Formula}
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($cell)
        }
        $book.SaveCopyAs($outputPath); $book.Close($false); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book); $book=$null
        if((Get-FileHash $inputPath).Hash -ne $before){throw 'Synthetic input changed'}
        $evidence += [ordered]@{date1904=$date1904; file=[IO.Path]::GetFileName($outputPath); inputSha256=$before; outputSha256=(Get-FileHash $outputPath).Hash; rows=$rows}
    }
    [ordered]@{excelVersion=$version; excelBuild=$build; provenance='Actual Excel COM; synthetic only; links disabled, events disabled, AutomationSecurity=3; 48-character column'; workbooks=$evidence} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'capture.json') -Encoding utf8
    Write-Output $run
} finally {
    if($book){$book.Close($false);[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($book)}
    if($excel){$excel.Quit();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel)}
    [GC]::Collect();[GC]::WaitForPendingFinalizers()
}
