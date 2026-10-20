param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checkRoot = Join-Path $projectRoot 'Temp/HotPotatoChecks'
$validationRoot = Join-Path $checkRoot 'Project'
$validationLog = Join-Path $checkRoot 'Unity-smoke.log'
$processRecord = Join-Path $checkRoot 'unity.pid'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null

# Only stop a previous check instance after confirming its isolated project path.
if (Test-Path -LiteralPath $processRecord) {
    $previousProcessId = [int](Get-Content -LiteralPath $processRecord)
    $previous = Get-CimInstance Win32_Process -Filter "ProcessId = $previousProcessId" -ErrorAction SilentlyContinue
    if ($null -ne $previous -and $previous.Name -eq 'Unity.exe' -and $previous.CommandLine.Contains($validationRoot)) {
        Stop-Process -Id $previousProcessId
        Wait-Process -Id $previousProcessId -ErrorAction SilentlyContinue
    }
}
foreach ($directory in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination $validationRoot -Recurse -Force
}
$unityArgs = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $validationRoot + '"'),
    '-executeMethod', 'HotPotato.Editor.PotatoChecks.RunAll', '-screen-width', '1280', '-screen-height', '800',
    '-logFile', ('"' + $validationLog + '"'))
$validationProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
[System.IO.File]::WriteAllText($processRecord, $validationProcess.Id.ToString())
Write-Output ('Running isolated Unity checks, PID ' + $validationProcess.Id)
$validationProcess.WaitForExit()
Select-String -Path $validationLog -Pattern 'HOT_POTATO_|error CS|Shader error' | ForEach-Object { $_.Line }
$result = Join-Path $validationRoot 'HotPotato-smoke.txt'
if (Test-Path -LiteralPath $result) { Get-Content -LiteralPath $result }
Write-Output ('Log: ' + $validationLog)
exit $validationProcess.ExitCode
