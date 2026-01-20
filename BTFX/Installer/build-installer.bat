@echo off
REM ============================================================================
REM BTFX Installer Build Entry
REM ============================================================================

echo.
echo ========================================
echo BTFX Installer Build
echo ========================================
echo.

REM Change to script directory
cd /d "%~dp0"

REM Call PowerShell script
powershell -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" %*

REM Check result
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed with error code: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Build completed
echo.
pause
