#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ExcelExecutable,
    [Parameter(Mandatory)][string]$SyntheticSeed,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$StackTool
)
$ErrorActionPreference = 'Stop'
$excel = (Resolve-Path -LiteralPath $ExcelExecutable).Path
$seed = (Resolve-Path -LiteralPath $SyntheticSeed).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Choose a fresh evidence directory.' }
$signature = Get-AuthenticodeSignature -LiteralPath $excel
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Excel signature is not valid Microsoft Authenticode.' }
if ($excel -notlike "$env:ProgramFiles\Microsoft Office\*\EXCEL.EXE") { throw 'Only the installed Microsoft Office executable is accepted.' }
$preflight = @{
    Executable=$excel; Sha256=(Get-FileHash -LiteralPath $excel -Algorithm SHA256).Hash
    SignatureStatus=$signature.Status.ToString(); Signer=$signature.SignerCertificate.Subject
    PreflightUtc=[DateTime]::UtcNow.ToString('O'); Startup=@(); Options=@()
}
# Inventory only: no registry/settings writes. Unexpected startup files are never launched.
$startup = @((Join-Path $env:APPDATA 'Microsoft\Excel\XLSTART'), (Join-Path (Split-Path $excel) 'XLSTART'))
foreach ($path in $startup) {
    $files = @(Get-ChildItem -LiteralPath $path -Force -ErrorAction SilentlyContinue)
    $preflight.Startup += @{Path=$path; Count=$files.Count; Entries=@($files | ForEach-Object Name)}
    if ($files.Count -gt 0) { throw "Startup directory is populated: $path" }
}
foreach ($hive in @('HKCU','HKLM')) {
    $path = "${hive}:\Software\Microsoft\Office\16.0\Excel\Options"
    if (Test-Path -LiteralPath $path) {
        $options = Get-ItemProperty -LiteralPath $path
        foreach ($property in $options.PSObject.Properties | Where-Object Name -Match '^(OPEN\d*|AltStartup)$') {
            $preflight.Options += @{Hive=$hive;Name=$property.Name;Value=$property.Value}
            if ($property.Value) { throw "Configured auto-open/startup option: $($property.Name)" }
        }
    }
}
$probe = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\ExcelActivation.Experiment.exe'
$start = [Diagnostics.ProcessStartInfo]::new($probe)
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.WindowStyle='Hidden'
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
foreach ($argument in @($excel,$seed,$output,[IO.Path]::GetFullPath($StackTool))) { $start.ArgumentList.Add($argument) }
$controller = [Diagnostics.Process]::Start($start)
$stdout = $controller.StandardOutput.ReadLineAsync()
$stderr = $controller.StandardError.ReadLineAsync()
$outText = [Text.StringBuilder]::new()
$errText = [Text.StringBuilder]::new()
$deadline = [DateTime]::UtcNow.AddMinutes(20)
$drainDeadline = $null
# Hard outer bound for an accepted native COM call that never returns; never terminate Excel.
while ($true) {
    if ($null -ne $stdout -and $stdout.IsCompleted) {
        $line = $stdout.GetAwaiter().GetResult()
        if ($null -eq $line) { $stdout=$null } else { [void]$outText.AppendLine($line); $stdout=$controller.StandardOutput.ReadLineAsync() }
    }
    if ($null -ne $stderr -and $stderr.IsCompleted) {
        $line = $stderr.GetAwaiter().GetResult()
        if ($null -eq $line) { $stderr=$null } else { [void]$errText.AppendLine($line); $stderr=$controller.StandardError.ReadLineAsync() }
    }
    if ($controller.WaitForExit(0)) {
        if ($null -eq $drainDeadline) { $drainDeadline=[DateTime]::UtcNow.AddSeconds(2) }
        # Rejected Excel children can inherit the controller's stdout handle. Do not wait for their EOF.
        if (($null -eq $stdout -and $null -eq $stderr) -or [DateTime]::UtcNow -ge $drainDeadline) { break }
    }
    if ([DateTime]::UtcNow -ge $deadline) {
        $controller.Kill()
        [void]$errText.AppendLine('Twenty-minute controller bound exceeded; Excel was not terminated.')
        break
    }
    Start-Sleep -Milliseconds 25
}
if (-not (Test-Path -LiteralPath $output)) { New-Item -ItemType Directory -Path $output | Out-Null }
$preflight | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'launch-preflight.json')
$outText.ToString() | Set-Content -LiteralPath (Join-Path $output 'controller.log')
$errText.ToString() | Set-Content -LiteralPath (Join-Path $output 'controller.stderr.log')
Write-Output "Experiment controller exit=$($controller.ExitCode); evidence=$output"
exit $controller.ExitCode
