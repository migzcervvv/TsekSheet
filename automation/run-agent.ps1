param(
    [ValidateRange(1,1000)]
    [int]$MaxCycles = 30
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$prompt = Join-Path $PSScriptRoot 'agent-prompt.md'
$status = Join-Path $PSScriptRoot 'agent-status.txt'
$logDir = Join-Path $PSScriptRoot 'logs'

if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw 'codex CLI was not found. Install it and sign in first.'
}
if (-not (Test-Path (Join-Path $root 'AGENTS.md'))) {
    throw "AGENTS.md not found in project root: $root"
}
if (-not (Test-Path $prompt)) {
    throw "Missing prompt: $prompt"
}
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Push-Location $root
try {
    Write-Host "Project: $root"
    Write-Host "Runs locally; Ctrl+C stops scheduling further cycles. Maximum cycles: $MaxCycles"
    for ($cycle = 1; $cycle -le $MaxCycles; $cycle++) {
        Write-Host "`n===== CODEX CYCLE $cycle / $MaxCycles =====" -ForegroundColor Cyan
        Set-Content -LiteralPath $status -Value 'RUNNING' -Encoding ascii
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $logFile = Join-Path $logDir "$stamp-cycle-$cycle.log"
        $summaryFile = Join-Path $logDir "$stamp-cycle-$cycle-summary.txt"

        # Pass the prompt through CMD file redirection to avoid
        # PowerShell stdin-pipeline issues on Windows.
        $command = 'codex.cmd exec --json --cd "{0}" --sandbox workspace-write --output-last-message "{1}" - < "{2}" 2>&1' -f $root, $summaryFile, $prompt

        & cmd.exe /d /c $command | Tee-Object -FilePath $logFile

        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            Write-Warning "Codex exited with code $exitCode. Inspect $logFile. Stopping."
            break
        }
        $decision = (Get-Content -LiteralPath $status -Raw).Trim()
        if ($decision -eq 'COMPLETE') {
            Write-Host 'Codex reported MVP complete. Check the acceptance evidence in AUTONOMOUS_PROGRESS.md.' -ForegroundColor Green
            break
        }
        if ($decision -eq 'BLOCKED') {
            Write-Warning 'Codex reported a blocker. Read docs/development/AUTONOMOUS_PROGRESS.md.'
            break
        }
        if ($decision -ne 'CONTINUE') {
            Write-Warning "Unexpected status '$decision'. Stopping rather than looping indefinitely."
            break
        }
        Write-Host 'Cycle completed. Starting the next cycle.' -ForegroundColor Green
    }
    Write-Host "`nSupervisor stopped. Progress: docs/development/AUTONOMOUS_PROGRESS.md"
    Write-Host "Logs: $logDir"
}
finally {
    Pop-Location
}
