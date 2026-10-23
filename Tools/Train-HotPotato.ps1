param(
    [string]$RunId = ('duel-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch]$Smoke,
    [switch]$Curriculum,
    [switch]$Resume,
    [int]$Seed = 12345
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$trainer = './.venv-ml/Scripts/mlagents-learn.exe'
$player = './Builds/Training/HotPotatoTraining.exe'
if (-not (Test-Path -LiteralPath $trainer)) { throw 'Run Tools/Setup-HotPotatoTraining.ps1 first.' }
if (-not (Test-Path -LiteralPath $player)) { throw 'Run Tools/Build-HotPotatoTraining.ps1 first.' }
if ($Smoke -and $Curriculum) { throw 'Choose Smoke or Curriculum, not both.' }
$config = if ($Smoke) { 'Training/smoke.yaml' } elseif ($Curriculum) { 'Training/curriculum.yaml' } else { 'Training/duel.yaml' }
$trainArgs = @($config, '--env', $player, '--run-id', $RunId,
    '--results-dir', 'Training/results', '--seed', $Seed, '--no-graphics', '--torch-device', 'cpu')
if ($Resume) { $trainArgs += '--resume' }
$trainArgs += @('--env-args', '--potato-seed', $Seed)
& $trainer @trainArgs
if ($LASTEXITCODE -ne 0) { throw 'Training failed; inspect the trainer output.' }
Write-Output ('Model: Training/results/' + $RunId + '/HotPotatoV1.onnx')
