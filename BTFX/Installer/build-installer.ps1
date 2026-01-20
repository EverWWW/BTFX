# ============================================================================
# BTFX Build Installer Script
# ============================================================================

param(
    [switch]$SkipPublish,
    [switch]$SkipInnoSetup,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Get directories
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$SolutionDir = Split-Path -Parent $ProjectDir
$PublishDir = Join-Path $SolutionDir "publish\$Runtime"
$OutputDir = Join-Path $ScriptDir "Output"

# Color output functions
function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Yellow
}

# Display build info
Write-Step "BTFX Installer Build Script"
Write-Info "Project Dir: $ProjectDir"
Write-Info "Publish Dir: $PublishDir"
Write-Info "Output Dir: $OutputDir"
Write-Info "Configuration: $Configuration"
Write-Info "Runtime: $Runtime"

# Step 1: Clean publish directory
Write-Step "Step 1/4: Clean publish directory"
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force
    Write-Success "Cleaned publish directory"
}
else {
    Write-Info "Publish directory does not exist"
}

New-Item -Path $PublishDir -ItemType Directory -Force | Out-Null
Write-Success "Created publish directory"

# Step 2: Execute dotnet publish
if (-not $SkipPublish) {
    Write-Step "Step 2/4: Execute dotnet publish"
    
    $ProjectFile = Join-Path $ProjectDir "BTFX.csproj"
    
    if (-not (Test-Path $ProjectFile)) {
        Write-Error "Project file not found: $ProjectFile"
        exit 1
    }
    
    Write-Info "Publishing project..."
    
    $publishArgs = @(
        "publish",
        $ProjectFile,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", "true",
        "-o", $PublishDir,
        "/p:PublishReadyToRun=true",
        "/p:DebugType=none",
        "/p:DebugSymbols=false"
    )
    
    & dotnet $publishArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed"
        exit 1
    }
    
    Write-Success "Project published successfully"
}
else {
    Write-Step "Step 2/4: Skip dotnet publish"
    Write-Info "Using existing publish files"
}

# Check publish directory
$ExeFile = Join-Path $PublishDir "BTFX.exe"
if (-not (Test-Path $ExeFile)) {
    Write-Error "Publish failed: BTFX.exe not found"
    exit 1
}

$PublishSize = (Get-ChildItem -Path $PublishDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Info "Publish directory size: $([math]::Round($PublishSize, 2)) MB"

# Step 3: Compile Inno Setup installer
if (-not $SkipInnoSetup) {
    Write-Step "Step 3/4: Compile Inno Setup installer"

    # First try to find ISCC.exe from registry
    $ISCC = $null

    Write-Info "Searching for Inno Setup..."

    # Method 1: Check registry
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($regPath in $registryPaths) {
        try {
            $apps = Get-ItemProperty $regPath -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like "*Inno Setup*" }

            if ($apps) {
                foreach ($app in $apps) {
                    if ($app.InstallLocation) {
                        $isccPath = Join-Path $app.InstallLocation "ISCC.exe"
                        if (Test-Path $isccPath) {
                            $ISCC = $isccPath
                            Write-Info "Found Inno Setup $($app.DisplayVersion) at: $($app.InstallLocation)"
                            break
                        }
                    }
                }
            }
            if ($ISCC) { break }
        }
        catch {
            # Continue searching
        }
    }

    # Method 2: Search in common directories
    if (-not $ISCC) {
        $InnoSetupBasePaths = @(
            "${env:ProgramFiles(x86)}",
            "${env:ProgramFiles}"
        )

        foreach ($basePath in $InnoSetupBasePaths) {
            if (Test-Path $basePath) {
                $isccFiles = Get-ChildItem -Path $basePath -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue -Depth 2 | 
                             Where-Object { $_.DirectoryName -like "*Inno Setup*" } |
                             Select-Object -First 1

                if ($isccFiles) {
                    $ISCC = $isccFiles.FullName
                    Write-Info "Found ISCC.exe at: $ISCC"
                    break
                }
            }
        }
    }

    # Method 3: Try specific paths
    if (-not $ISCC) {
        $InnoSetupPaths = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6.7\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6.7\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
            "C:\Program Files (x86)\Inno Setup 6.7\ISCC.exe",
            "C:\Program Files\Inno Setup 6.7\ISCC.exe",
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe"
        )

        foreach ($path in $InnoSetupPaths) {
            if (Test-Path $path) {
                $ISCC = $path
                Write-Info "Found ISCC.exe at: $ISCC"
                break
            }
        }
    }

    if (-not $ISCC) {
        Write-Error "Inno Setup compiler (ISCC.exe) not found"
        Write-Info "Download from https://jrsoftware.org/isinfo.php"
        Write-Info ""
        Write-Info "Run 'check-inno-setup.ps1' to diagnose the issue"
        exit 1
    }
    
    Write-Info "Using Inno Setup: $ISCC"
    
    $IssFile = Join-Path $ScriptDir "BTFX.iss"
    
    if (-not (Test-Path $IssFile)) {
        Write-Error "Inno Setup script not found: $IssFile"
        exit 1
    }
    
    if (-not (Test-Path $OutputDir)) {
        New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
    }
    
    Write-Info "Compiling installer..."
    
    & $ISCC $IssFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup compilation failed"
        exit 1
    }
    
    Write-Success "Installer compiled successfully"
}
else {
    Write-Step "Step 3/4: Skip Inno Setup compilation"
}

# Step 4: Complete
Write-Step "Step 4/4: Build complete"

# Display output files
if (Test-Path $OutputDir) {
    $OutputFiles = Get-ChildItem -Path $OutputDir -Filter "*.exe"
    if ($OutputFiles) {
        Write-Success "Generated installers:"
        foreach ($file in $OutputFiles) {
            $Size = [math]::Round($file.Length / 1MB, 2)
            Write-Host "  - $($file.Name) ($Size MB)" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Success "Build complete! Installer location: $OutputDir"
Write-Host ""
