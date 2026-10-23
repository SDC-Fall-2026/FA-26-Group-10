param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe',
    [string]$EvaluationModel
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot 'Temp/HotPotatoChecks/Project'
$buildPath = Join-Path $projectRoot 'Builds/Training/HotPotatoTraining.exe'
$log = Join-Path $projectRoot 'Temp/HotPotatoChecks/Training-build.log'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
foreach ($directory in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination $validationRoot -Recurse -Force
}
$modelSource = Join-Path $projectRoot 'Assets/Resources/HotPotato/Models/Experimental.onnx'
if ($EvaluationModel) {
    $modelSource = (Resolve-Path -LiteralPath $EvaluationModel).Path
    Copy-Item -LiteralPath $modelSource -Destination (Join-Path $validationRoot 'Assets/Resources/HotPotato/Models/Experimental.onnx')
}
$unityArgs = @('-batchmode', '-projectPath', ('"' + $validationRoot + '"'),
    '-executeMethod', 'HotPotato.Editor.PotatoTrainingTools.Build',
    '-potatoOutput', ('"' + $buildPath + '"'), '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
Write-Output ('Building isolated training player, PID ' + $process.Id)
$process.WaitForExit()
Select-String -Path $log -Pattern 'HOT_POTATO_|error CS|Error|Exception' | ForEach-Object { $_.Line }
if ($process.ExitCode -ne 0) { throw "Unity build failed. See $log" }
if (-not (Test-Path -LiteralPath $buildPath)) { throw 'Unity did not produce the training executable.' }
if (Test-Path -LiteralPath $modelSource) {
    [ordered]@{
        source = $modelSource
        sha256 = (Get-FileHash -LiteralPath $modelSource -Algorithm SHA256).Hash
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $buildPath) 'evaluation-model.json') -Encoding UTF8
}
Write-Output $buildPath
