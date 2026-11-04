param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Repository)
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (@(Get-ChildItem -LiteralPath $output -Filter '*.running.json').Count -gt 0) { throw 'Choose a fresh output directory to retain existing trial evidence.' }
$results = @()
$pending = [Collections.Generic.List[object]]::new()
function Update-Owned {
    for ($i=$pending.Count-1; $i -ge 0; $i--) {
        $item = $pending[$i]
        if ($item.Process.WaitForExit(0)) {
            $item.Process.Refresh()
            $exit = $item.Process.ExitTime.ToUniversalTime()
            $item.Row.ExitUtc = $exit.ToString('O')
            $item.Row.ExitSeconds = ($exit-$item.Quit).TotalSeconds
            $item.Row.AliveAtObservationLimit = $false
        }
        if ($item.Process.WaitForExit(0) -or [DateTime]::UtcNow -ge $item.Limit) {
            $item.Row.ObservationEndedUtc = [DateTime]::UtcNow.ToString('O')
            $item.Process.Dispose(); $pending.RemoveAt($i)
        }
    }
}
for ($repeat=1; $repeat -le 3; $repeat++) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory=$root; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $info.Environment['TSEKSHEET_LIFECYCLE_OUTPUT']=$output
    foreach ($arg in @('test','tests/TsekSheet.Excel.Tests/TsekSheet.Excel.Tests.csproj','-c','Release','--no-build','--no-restore',
        '--filter',"FullyQualifiedName~OriginalT2Run$repeat",'--logger',"trx;LogFileName=t2-original-matrix-$repeat.trx")) { $info.ArgumentList.Add($arg) }
    $controller = [Diagnostics.Process]::Start($info)
    $seen = [Collections.Generic.HashSet[int]]::new()
    $limit = [DateTime]::UtcNow.AddSeconds(180)
    try {
        while (!$controller.HasExited -and [DateTime]::UtcNow -lt $limit) {
            foreach ($file in Get-ChildItem -LiteralPath $output -Filter "original-*-$repeat-*.running.json") {
                $trial = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
                if (!$seen.Add($trial.Pid)) { continue }
                try {
                    $process=[Diagnostics.Process]::GetProcessById($trial.Pid)
                    if ($process.StartTime.ToUniversalTime() -ne [DateTime]$trial.StartUtc) { $process.Dispose(); throw 'Owned PID identity changed.' }
                    $null=$process.SafeHandle
                    $row=[pscustomobject]@{Repeat=$repeat;Scenario=$trial.Scenario;Pid=$trial.Pid;StartUtc=$trial.StartUtc;
                        ExitUtc=$null;ExitSeconds=$null;AliveAtObservationLimit=$true;ObservationEndedUtc=$null}
                    $results += $row
                    $pending.Add(@{Process=$process;Row=$row;Quit=[DateTime]$trial.QuitReturnedUtc;Limit=([DateTime]$trial.QuitReturnedUtc).AddSeconds(90)})
                } catch [ArgumentException] { }
            }
            Update-Owned
            Start-Sleep -Milliseconds 50
            $controller.Refresh()
        }
        if (!$controller.HasExited) {
            # Stop only this owned diagnostic dotnet controller, never Excel.
            $controller.Kill(); $controller.WaitForExit()
            throw 'Original-workflow diagnostic exceeded its fixed 180-second controller bound.'
        }
        Write-Output "Original T2 repeat ${repeat}: test exit=$($controller.ExitCode)"
        ConvertTo-Json -InputObject $results -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'post-controller-observation.json')
    } finally { $controller.Dispose() }
}
while ($pending.Count -gt 0) {
    Update-Owned
    ConvertTo-Json -InputObject $results -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'post-controller-observation.json')
    if ($pending.Count -gt 0) { Start-Sleep -Milliseconds 100 }
}
