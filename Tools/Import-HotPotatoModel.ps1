param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunId,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$AssetName = 'Experimental'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot "Training/results/$RunId/HotPotatoV1.onnx"
if (-not (Test-Path -LiteralPath $source)) { throw "No exported model found: $source" }
$folder = Join-Path $projectRoot 'Assets/Resources/HotPotato/Models'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$destination = Join-Path $folder ($AssetName + '.onnx')
Copy-Item -LiteralPath $source -Destination $destination
foreach ($asset in @($folder, $destination)) {
    if (-not (Test-Path -LiteralPath ($asset + '.meta'))) {
        [System.IO.File]::WriteAllText($asset + '.meta', "fileFormatVersion: 2`nguid: " + [guid]::NewGuid().ToString('N') + "`n")
    }
}
$record = [ordered]@{
    run_id = $RunId
    behavior = 'HotPotatoV1'
    asset = $AssetName
    sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    imported_utc = [DateTime]::UtcNow.ToString('o')
    status = 'Experimental; no difficulty rating'
}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot ('Training/' + $AssetName.ToLowerInvariant() + '-model.json')) -Encoding UTF8
Write-Output "Imported $RunId as $AssetName.onnx. Rebuild before evaluating."
