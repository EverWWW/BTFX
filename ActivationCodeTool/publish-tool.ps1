param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $ScriptDir "ActivationCodeTool.csproj"
$PublishDir = Join-Path $SolutionDir "publish\internal-tools\ActivationCodeTool"

if (Test-Path $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

New-Item -Path $PublishDir -ItemType Directory -Force | Out-Null

dotnet publish $ProjectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $PublishDir `
    /p:PublishSingleFile=false `
    /p:DebugType=none `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "ActivationCodeTool publish failed."
}

Write-Host "Activation code tool published to: $PublishDir" -ForegroundColor Green
