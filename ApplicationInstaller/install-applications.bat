<# : batch script

@echo off
:: BatchGotAdmin ==============
IF "%PROCESSOR_ARCHITECTURE%" EQU "amd64" (
  >nul 2>&1 "%SYSTEMROOT%\SysWOW64\cacls.exe" "%SYSTEMROOT%\SysWOW64\config\system"
) ELSE (
  >nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
)
if '%errorlevel%' NEQ '0' (
  echo Requesting administrative privileges...
  echo CreateObject^("Shell.Application"^).ShellExecute "cmd.exe", "/c ""%~s0"" %*", "", "runas", 1 > "%temp%\getadmin.vbs"
  "%temp%\getadmin.vbs"
  del "%temp%\getadmin.vbs"
  exit /B
)

:gotAdmin
pushd "%cd%"
cd /d "%~dp0"
:: =============================

mode con:cols=110 lines=49
setlocal
powershell.exe -executionpolicy remotesigned -Command "Invoke-Expression $([System.IO.File]::ReadAllText('%~f0'))"
endlocal

exit /b 0

#>
# PowerShell script starts here


# Read applications from JSON file
$jsonFilePath = "applications.json" # Path to the JSON file
if (-not (Test-Path $jsonFilePath)) {
    Write-Host " * ERROR: JSON file '$jsonFilePath' not found. Exiting..." -ForegroundColor Red
    Pause
    exit
}

try {
    $Applications = Get-Content -Path $jsonFilePath -Raw | ConvertFrom-Json
} catch {
    Write-Host " * ERROR: Failed to parse JSON file '$jsonFilePath'. Exiting..." -ForegroundColor Red
    Pause
    exit
}

# Function to check if an application is installed
function Is-ApplicationInstalled {
    param ([string]$AppName)
    $RegistryPaths = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    foreach ($Path in $RegistryPaths) {
        if (Get-ItemProperty -Path $Path -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like "*$AppName*" }) {
            return $true
        }
    }
    return $false
}

# Function to installation
function Install-Application {
    param (
        [Parameter(Mandatory = $true)]
        [hashtable]$App
    )

    Write-Host " + Installing $($App.Name)..." -ForegroundColor Cyan

    # Determine if the installer is an MSI or executable
	$Process = if ($app.FileName -like "*.msi") {
		if ([string]::IsNullOrWhiteSpace($app.Arguments)) {
			# If no arguments, run msiexec without arguments
			Start-Process -FilePath "msiexec.exe" -ArgumentList "/i `"$($app.FileName)`"" -Wait -PassThru
		} else {
			# Run msiexec with arguments
			Start-Process -FilePath "msiexec.exe" -ArgumentList "/i `"$($app.FileName)`" $($app.Arguments)" -Wait -PassThru
		}
	} else {
		if ([string]::IsNullOrWhiteSpace($app.Arguments)) {
			# If no arguments, run executable without arguments
			Start-Process -FilePath "$($app.FileName)" -Wait -PassThru
		} else {
			# Run executable with arguments
			Start-Process -FilePath "$($app.FileName)" -ArgumentList "$($app.Arguments)" -Wait -PassThru
		}
	}

    # Check the installation result
    if ($Process.ExitCode -eq 0) {
        Write-Host " + $($App.Name) installed successfully!" -ForegroundColor Green
    } else {
        Write-Host " + ERROR: $($App.Name) installation failed with code $($Process.ExitCode)." -ForegroundColor Red
    }
}



Push-Location "files"

# Install applications
foreach ($app in $Applications) {
    Write-Host "`n * Checking $($app.Name)..."

    if (-not (Test-Path "./$($app.FileName)")) {
        Write-Host " * ERROR: Installer for $($app.Name) ('$($app.FileName)') not found in 'files' folder. Skipping..." -ForegroundColor Red
        continue
	}

    Install-Application -App $app
}


Write-Host "`t =============================================================================="
Write-Host "`t Installation complete. Restart if necessary." -ForegroundColor Cyan
Write-Host "`t =============================================================================="
Pop-Location

Pause