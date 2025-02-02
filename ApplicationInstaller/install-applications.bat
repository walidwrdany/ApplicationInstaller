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


setlocal
powershell.exe -executionpolicy remotesigned -Command "Invoke-Expression $([System.IO.File]::ReadAllText('%~f0'))"
endlocal

exit /b 0


#>
# here write your powershell commands...
# note all lines start with '#' they are comment
# {Black | DarkBlue | DarkGreen | DarkCyan | DarkRed | DarkMagenta | DarkYellow | Gray | DarkGray | Blue | Green | Cyan | Red | Magenta | Yellow | White}





Push-Location "files"

# Define applications and their properties
$Applications = @(
    @{ Name = "7-Zip"; Arguments = "/S"; FileName = "7z2408-x64.exe" },
    @{ Name = "Notepad++"; Arguments = "/S"; FileName = "npp.8.7.2.Installer.x64.exe" },
    @{ Name = "VLC"; Arguments = "/S"; FileName = "vlc-3.0.21-win64.exe" },
    @{ Name = "WinRAR"; Arguments = "/S"; FileName = "winrar-x64-701.exe" },
    @{ Name = "Microsoft Visual C++"; Arguments = "/silent /norestart"; FileName = "Microsoft_Visual_C++_Pack_v3.1_Repack.exe" },
    @{ Name = "Internet Download Manager"; Arguments = "idman642build26.exe"; FileName = "InstallIDM-v2.exe" },
    @{ Name = "Firefox"; Arguments = "/quiet /norestart"; FileName = "Firefox-Setup-133.0.3.msi" },
    @{ Name = "Brave"; Arguments = ""; FileName = "BraveBrowserStandaloneSilentSetup.exe" },
    @{ Name = "Google Chrome"; Arguments = "/silent /install"; FileName = "ChromeStandaloneSetup64.exe" },
    @{ Name = "1Password"; Arguments = "--silent"; FileName = "1PasswordSetup-latest.exe" },
    @{ Name = "Node.js"; Arguments = "/quiet /norestart"; FileName = "node-v23.0.0-x64.msi" },
    @{ Name = "PowerShell 7-x64"; Arguments = "/quiet /norestart"; FileName = "PowerShell-7.4.6-win-x64.msi" },
    @{ Name = "Python"; Arguments = "/quiet InstallAllUsers=1 PrependPath=1"; FileName = "python-3.13.0-amd64.exe" },
    @{ Name = "Java 8 Update 431 (64-bit)"; Arguments = "/s"; FileName = "jre-8u431-windows-x64.exe" },
    @{ Name = "Git"; Arguments = "/verysilent"; FileName = "Git-2.47.1-64-bit.exe" },
    @{ Name = "Visual Studio Code"; Arguments = "/silent /mergetasks=!runcode,addcontextmenufiles,addcontextmenufolders,associatewithfiles,addtopath"; FileName = "VSCodeSetup-x64-1.96.2.exe" },
    @{ Name = "TeraCopy"; Arguments = "/verysilent"; FileName = "teracopy.exe" },
    @{ Name = "StartAllBack"; Arguments = "/silent /allusers"; FileName = "StartAllBack_3.8.13_setup.exe" },
    @{ Name = "DirectX"; Arguments = "/silent"; FileName = "directx_Jun2010_redist\DXSETUP.exe" },
    @{ Name = "WSL"; Arguments = "/quiet /norestart"; FileName = "wsl.2.3.26.0.x64.msi" }
)

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