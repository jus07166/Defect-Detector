param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateRange(0, 10000)]
    [int]$MaxImagesPerCategory = 0,

    [ValidateRange(0.0, 1.0)]
    [double]$MinimumRecall = 0.90,

    [ValidateRange(0.0, 1.0)]
    [double]$MinimumSpecificity = 0.60
)

$ErrorActionPreference = 'Stop'

# Normalize duplicate PATH/Path entries that can prevent MSBuild child processes.
$normalizedPath = $env:Path
Remove-Item Env:PATH -ErrorAction SilentlyContinue
$env:Path = $normalizedPath

$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vsWhere)) {
    throw "vswhere.exe was not found: $vsWhere"
}

$msBuild = @(
    & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe'
)[0]

if (-not $msBuild) {
    throw '64-bit MSBuild.exe was not found.'
}

if (-not $env:OPENCV_DIR) {
    throw 'OPENCV_DIR is not defined.'
}

$openCvRuntime = Join-Path $env:OPENCV_DIR 'x64\vc17\bin'
if (-not (Test-Path -LiteralPath $openCvRuntime)) {
    throw "OpenCV runtime directory was not found: $openCvRuntime"
}

$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$datasetRoot = Join-Path $workspaceRoot 'Sample_Data\pill'
if (-not (Test-Path -LiteralPath $datasetRoot)) {
    throw "Pill dataset was not found: $datasetRoot"
}

$project = Join-Path $PSScriptRoot 'OpenCvBackend.ImageTests.csproj'

& $msBuild $project /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /m:1 /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Image test project build failed with exit code $LASTEXITCODE."
}

$backendOutput = Join-Path $workspaceRoot "bin\$Configuration"
$modelPath = Join-Path $backendOutput 'model.onnx'
if (-not (Test-Path -LiteralPath $modelPath)) {
    throw "ONNX model was not found: $modelPath"
}

$env:Path = "$backendOutput;$openCvRuntime;$env:Path"
$testExecutable = Join-Path $PSScriptRoot "bin\$Configuration\OpenCvBackend.ImageTests.exe"
$reportPath = Join-Path $PSScriptRoot "bin\$Configuration\pill-evaluation.csv"
$maxImagesArgument = $MaxImagesPerCategory.ToString([Globalization.CultureInfo]::InvariantCulture)
$minimumRecallArgument = $MinimumRecall.ToString([Globalization.CultureInfo]::InvariantCulture)
$minimumSpecificityArgument = $MinimumSpecificity.ToString([Globalization.CultureInfo]::InvariantCulture)

Push-Location $backendOutput
try {
    & $testExecutable `
        $datasetRoot `
        $maxImagesArgument `
        $minimumRecallArgument `
        $minimumSpecificityArgument `
        $reportPath
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    throw "Image tests failed with exit code $LASTEXITCODE."
}
