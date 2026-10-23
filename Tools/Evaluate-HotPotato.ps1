param([int]$Episodes = 200, [int]$Seed = 700000, [switch]$Scripted)
$ErrorActionPreference = 'Stop'
if ($Episodes -lt 1) { throw 'Episodes must be positive.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$player = Join-Path $projectRoot 'Builds/Training/HotPotatoTraining.exe'
$mode = if ($Scripted) { 'scripted' } else { 'model' }
$output = Join-Path $projectRoot ('Training/evaluation/' + $mode + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$modelRecord = Join-Path (Split-Path -Parent $player) 'evaluation-model.json'
if (-not $Scripted -and (Test-Path -LiteralPath $modelRecord)) {
    Copy-Item -LiteralPath $modelRecord -Destination (Join-Path $output 'evaluation-model.json')
}
$log = Join-Path $output 'player.log'
$evalArgs = @('-batchmode', '-nographics', '--potato-evaluate', $Episodes, '--potato-seed', $Seed,
    '-logFile', ('"' + $log + '"'))
if ($Scripted) { $evalArgs += '--potato-scripted' }
$process = Start-Process -FilePath $player -ArgumentList $evalArgs -WorkingDirectory $output -WindowStyle Hidden -PassThru
Write-Output ('Evaluating ' + $mode + ', PID ' + $process.Id)
if (-not $process.WaitForExit([Math]::Max(120000, $Episodes * 3000))) {
    Stop-Process -Id $process.Id
    throw "Evaluation timed out. See $log"
}
$result = Join-Path $output 'HotPotato-evaluation.txt'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $result)) { throw "Evaluation failed. See $log" }
Get-Content -LiteralPath $result
Write-Output $output
