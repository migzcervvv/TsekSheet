param(
    [Parameter(Mandatory)][string]$SyntheticPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$StackTool
)
$ErrorActionPreference = 'Stop'
$probe = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\ExcelLifecycle.Diagnostics.exe'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (@(Get-ChildItem -LiteralPath $output -Filter '*.running.json').Count -gt 0) { throw 'Choose a fresh output directory to retain existing trial evidence.' }
$cases = @(
    @('com','activate'), @('com','open'), @('shell','open'), @('safe','open'),
    @('com','value'), @('shell','value'), @('safe','value'),
    @('com','formula'), @('shell','formula'), @('safe','formula')
)
$baseline = @(Get-Process EXCEL -ErrorAction SilentlyContinue | ForEach-Object {
    @{Pid=$_.Id;StartUtc=$_.StartTime.ToUniversalTime().ToString('O')}
})
ConvertTo-Json -InputObject $baseline | Set-Content -LiteralPath (Join-Path $output 'processes-before.json')
$results = @()
$pending = [Collections.Generic.List[object]]::new()
function Update-Pending {
    for ($i = $pending.Count - 1; $i -ge 0; $i--) {
        $item = $pending[$i]
        if ($item.Process.WaitForExit(0)) {
            $item.Process.Refresh()
            $ended = $item.Process.ExitTime.ToUniversalTime()
            $item.Row.ExcelExitUtc = $ended.ToString('O')
            $item.Row.ExitSeconds = ($ended - $item.Quit).TotalSeconds
            $item.Row.AliveAtFinalObservation = $false
        }
        if ($item.Process.WaitForExit(0) -or [DateTime]::UtcNow -ge $item.Deadline) {
            $item.Row.ObservationEndedUtc = [DateTime]::UtcNow.ToString('O')
            $item.Process.Dispose()
            $pending.RemoveAt($i)
        }
    }
}
for ($repeat = 1; $repeat -le 3; $repeat++) {
    foreach ($case in $cases) {
        $info = [Diagnostics.ProcessStartInfo]::new($probe)
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        foreach ($arg in @($case[0],$case[1],"$repeat",[IO.Path]::GetFullPath($SyntheticPath),$output,[IO.Path]::GetFullPath($StackTool))) { $info.ArgumentList.Add($arg) }
        $controller = [Diagnostics.Process]::Start($info)
        $known = $null
        $retained = $false
        $evidence = $null
        $deadline = [DateTime]::UtcNow.AddSeconds(100)
        try {
            while (!$controller.HasExited -and [DateTime]::UtcNow -lt $deadline) {
                Update-Pending
                if ($null -eq $known) {
                    $running = @(Get-ChildItem -LiteralPath $output -Filter "$($case[0])-$($case[1])-$repeat-*.running.json")
                    if ($running.Count -eq 1) {
                        $evidence = Get-Content -LiteralPath $running[0].FullName -Raw | ConvertFrom-Json
                        try {
                            $candidate = [Diagnostics.Process]::GetProcessById($evidence.Pid)
                            if ($candidate.StartTime.ToUniversalTime() -ne [DateTime]$evidence.StartUtc) { $candidate.Dispose(); throw 'PID identity changed.' }
                            $null = $candidate.SafeHandle # Retain the OS identity and exit time after controller exit.
                            $known = $candidate
                        } catch [ArgumentException] { }
                    }
                }
                Start-Sleep -Milliseconds 50
                $controller.Refresh()
            }
            if (!$controller.HasExited) {
                # Bound the diagnostic controller, never terminate Excel.
                if ($controller.ProcessName -ne 'ExcelLifecycle.Diagnostics') { throw 'Unexpected controller identity.' }
                $controller.Kill()
                $controller.WaitForExit()
                throw 'Diagnostic controller exceeded 100 seconds; Excel was left untouched.'
            }
            if ($null -eq $evidence) { throw 'Trial did not produce ownership evidence.' }
            $afterController = [DateTime]::UtcNow
            $exitUtc = $null
            if ($null -ne $known) {
                if ($known.HasExited) { $exitUtc = $known.ExitTime.ToUniversalTime() }
            }
            $row = [pscustomobject]@{
                Environment=$case[0];Scenario=$case[1];Repeat=$repeat;Pid=$evidence.Pid;StartUtc=$evidence.StartUtc;
                ControllerExit=$controller.ExitCode;ControllerReturnedUtc=$afterController.ToString('O');
                ExcelExitUtc=if($null -ne $exitUtc){$exitUtc.ToString('O')}else{$null};
                ExitSeconds=if($null -ne $exitUtc){($exitUtc-[DateTime]$evidence.QuitReturnedUtc).TotalSeconds}else{$null};
                AliveAtFinalObservation=($null -ne $known -and !$known.HasExited);ObservationEndedUtc=$null
            }
            if ($null -ne $known -and !$known.HasExited) {
                # Observe concurrently, bounded at 90 seconds from Quit. No further COM calls.
                $pending.Add(@{Process=$known;Row=$row;Quit=[DateTime]$evidence.QuitReturnedUtc;Deadline=([DateTime]$evidence.QuitReturnedUtc).AddSeconds(90)})
                $retained = $true
            }
            $results += $row
            ConvertTo-Json -InputObject $results -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'post-controller-observation.json')
            Write-Output "$($case[0])/$($case[1]) repeat ${repeat}: controller=$($controller.ExitCode), final Excel exit seconds=$($row.ExitSeconds)"
        } finally { if ($null -ne $known -and !$retained) { $known.Dispose() }; $controller.Dispose() }
    }
}
while ($pending.Count -gt 0) {
    Update-Pending
    ConvertTo-Json -InputObject $results -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'post-controller-observation.json')
    if ($pending.Count -gt 0) { Start-Sleep -Milliseconds 100 }
}
$after = @(Get-Process EXCEL -ErrorAction SilentlyContinue | ForEach-Object {
    @{Pid=$_.Id;StartUtc=$_.StartTime.ToUniversalTime().ToString('O')}
})
ConvertTo-Json -InputObject $after | Set-Content -LiteralPath (Join-Path $output 'processes-after.json')
