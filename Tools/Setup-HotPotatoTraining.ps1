param([string]$BootstrapPython = 'python')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$env:UV_PYTHON_INSTALL_DIR = Join-Path $projectRoot '.tools/python'
$env:UV_CACHE_DIR = Join-Path $projectRoot '.tools/cache'
$uvRoot = Join-Path $projectRoot '.tools/uv'
$uv = Join-Path $uvRoot 'bin/uv.exe'
if (-not (Test-Path -LiteralPath $uv)) {
    & $BootstrapPython -m pip install --target $uvRoot uv==0.9.5
    if ($LASTEXITCODE -ne 0) { throw 'Could not install the local Python environment manager.' }
}
if (-not (Test-Path -LiteralPath '.venv-ml/Scripts/python.exe')) {
    & $uv venv --python 3.10.12 --python-preference only-managed .venv-ml
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the Python 3.10.12 environment.' }
}
$requirements = if (Test-Path -LiteralPath 'Training/requirements-lock.txt') { 'Training/requirements-lock.txt' } else { 'Training/requirements.txt' }
& $uv pip install --python .venv-ml/Scripts/python.exe -r $requirements
if ($LASTEXITCODE -ne 0) { throw 'Training dependency installation failed.' }
& ./.venv-ml/Scripts/python.exe -c "import sys, torch, mlagents.trainers; print(sys.version); print('PyTorch:', torch.__version__); print('ML-Agents:', mlagents.trainers.__version__)"
if ($LASTEXITCODE -ne 0) { throw 'Training imports failed.' }
$helpText = & ./.venv-ml/Scripts/mlagents-learn.exe --help
if ($LASTEXITCODE -ne 0) { throw 'Training command failed to load.' }
$helpText | Select-Object -First 8
