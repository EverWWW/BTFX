# Inno Setup Detection Script
# ============================================================================

Write-Host "Searching for Inno Setup installation..." -ForegroundColor Cyan
Write-Host ""

# Method 1: Check registry
Write-Host "Method 1: Checking Windows Registry..." -ForegroundColor Yellow
$registryPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$innoSetupFound = $false
foreach ($regPath in $registryPaths) {
    try {
        $apps = Get-ItemProperty $regPath -ErrorAction SilentlyContinue |
                Where-Object { $_.DisplayName -like "*Inno Setup*" }
        
        if ($apps) {
            foreach ($app in $apps) {
                Write-Host "Found: $($app.DisplayName)" -ForegroundColor Green
                Write-Host "  Version: $($app.DisplayVersion)" -ForegroundColor Gray
                Write-Host "  Install Location: $($app.InstallLocation)" -ForegroundColor Gray
                
                if ($app.InstallLocation) {
                    $isccPath = Join-Path $app.InstallLocation "ISCC.exe"
                    if (Test-Path $isccPath) {
                        Write-Host "  ISCC.exe: $isccPath" -ForegroundColor Green
                        $innoSetupFound = $true
                    }
                }
            }
        }
    }
    catch {
        # Ignore errors
    }
}

if (-not $innoSetupFound) {
    Write-Host "No Inno Setup found in registry" -ForegroundColor Red
}

Write-Host ""
Write-Host "Method 2: Searching file system..." -ForegroundColor Yellow

$searchPaths = @(
    "C:\Program Files (x86)",
    "C:\Program Files",
    "$env:LOCALAPPDATA",
    "$env:ProgramFiles",
    "${env:ProgramFiles(x86)}"
)

$foundPaths = @()

foreach ($searchPath in $searchPaths) {
    if (Test-Path $searchPath) {
        Write-Host "Searching in $searchPath..." -ForegroundColor Gray
        
        $isccFiles = Get-ChildItem -Path $searchPath -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue -Depth 3
        
        foreach ($file in $isccFiles) {
            Write-Host "Found ISCC.exe: $($file.FullName)" -ForegroundColor Green
            $foundPaths += $file.FullName
        }
    }
}

if ($foundPaths.Count -eq 0) {
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor Red
    Write-Host "ISCC.exe NOT FOUND!" -ForegroundColor Red
    Write-Host "=" * 60 -ForegroundColor Red
    Write-Host ""
    Write-Host "Inno Setup may not be installed correctly." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please:" -ForegroundColor Yellow
    Write-Host "1. Download Inno Setup from: https://jrsoftware.org/isinfo.php" -ForegroundColor Cyan
    Write-Host "2. Run the installer" -ForegroundColor Cyan
    Write-Host "3. Use default installation path" -ForegroundColor Cyan
    Write-Host "4. Run this script again to verify" -ForegroundColor Cyan
}
else {
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor Green
    Write-Host "SUCCESS! Found $($foundPaths.Count) ISCC.exe location(s)" -ForegroundColor Green
    Write-Host "=" * 60 -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run build-installer.bat" -ForegroundColor Green
}

Write-Host ""
Write-Host "Method 3: Checking PATH environment variable..." -ForegroundColor Yellow
$pathEnv = $env:PATH -split ';'
$innoInPath = $pathEnv | Where-Object { $_ -like "*Inno*" }

if ($innoInPath) {
    Write-Host "Inno Setup in PATH:" -ForegroundColor Green
    foreach ($path in $innoInPath) {
        Write-Host "  $path" -ForegroundColor Gray
    }
}
else {
    Write-Host "Inno Setup not in PATH" -ForegroundColor Yellow
}

Write-Host ""
Read-Host "Press Enter to exit"
