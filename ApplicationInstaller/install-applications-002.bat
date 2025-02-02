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
# {Black | DarkBlue | DarkGreen | DarkCyan | DarkRed | DarkMagenta | DarkYellow | Gray | DarkGray | Blue | Green | Cyan | Red | Magenta | Yellow | White}


# Read applications from JSON file
# $jsonFilePath = "applications.json" # Path to the JSON file
# if (-not (Test-Path $jsonFilePath)) {
#     Write-Host " * ERROR: JSON file '$jsonFilePath' not found. Exiting..." -ForegroundColor Red
#     Pause
#     exit
# }
# 
# try {
#     $Applications = Get-Content -Path $jsonFilePath -Raw | ConvertFrom-Json
# } catch {
#     Write-Host " * ERROR: Failed to parse JSON file '$jsonFilePath'. Exiting..." -ForegroundColor Red
#     Pause
#     exit
# }



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
    @{ Name = "VMware Workstation"; Arguments = "/s /v/qn EULAS_AGREED=1 SERIALNUMBER='MC60H-DWHD5-H80U9-6V85M-8280D' AUTOSOFTWAREUPDATE=0 DATACOLLECTION=0 REBOOT=ReallySuppress"; FileName = "VMware-workstation-17.6.1-24319023.exe" },
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



# Function to display menu and get user selection
function Show-Menu {
    Clear-Host
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host " Application Installer Menu" -ForegroundColor Yellow
    Write-Host "=============================================" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Applications.Count; $i++) {
        $app = $Applications[$i]
        $installed = Is-ApplicationInstalled -AppName $app.Name
        if ($installed) {
            Write-Host "$($i + 1). $($app.Name) " -NoNewline
            Write-Host "[✔ Installed]" -ForegroundColor DarkGreen
        } else {
            Write-Host "$($i + 1). $($app.Name)"
        }
    }
    Write-Host "A. Install All Applications"
    Write-Host "Q. Quit"
    Write-Host "=============================================" -ForegroundColor Cyan
}



# Function to installation
function Install-Application {
    param (
        [Parameter(Mandatory = $true)]
        [hashtable]$App
    )

    # Check if the application is already installed
    if (Is-ApplicationInstalled -AppName $App.Name) {
        if (-not (Confirm-Reinstall -AppName $App.Name)) {
            Write-Host " * Skipping installation of $($App.Name)." -ForegroundColor Yellow
            return
        }
    }

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



# Function to confirm reinstallation
function Confirm-Reinstall {
    param (
        [Parameter(Mandatory = $true)]
        [string]$AppName
    )

    Write-Host " * $AppName is already installed." -ForegroundColor Yellow
    Write-Host " Do you want to reinstall it? (Y/N) [Default: N, timeout in 10 seconds]"

    # Timer and user input logic
    $counter = 10
    $response = $null
    while ($counter -gt 0) {
        if ($Host.UI.RawUI.KeyAvailable) {
            $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            if ($key.Character -eq 'Y' -or $key.Character -eq 'y') {
                $response = 'Y'
                break
            } elseif ($key.Character -eq 'N' -or $key.Character -eq 'n') {
                $response = 'N'
                break
            }
        }
        Start-Sleep -Seconds 1
        $counter--
        # Display countdown on the same line
        Write-Host "`rTimeout in $counter seconds... " -NoNewline -ForegroundColor Gray
    }

    # Move to the next line after the countdown
    Write-Host

    # Default to skipping if no input is provided
    if (-not $response) {
        Write-Host " No input received. Skipping reinstallation of $AppName." -ForegroundColor Yellow
        return $false
    }

    return ($response -eq 'Y')
}



# Function to install a single application
function Install-SingleApplication {
    param (
        [Parameter(Mandatory = $true)]
        [int]$AppNumber
    )

    if ($AppNumber -ge 1 -and $AppNumber -le $Applications.Count) {
        $app = $Applications[$AppNumber - 1]
        if (-not (Test-Path "./$($app.FileName)")) {
            Write-Host " * ERROR: Installer for $($app.Name) ('$($app.FileName)') not found in 'files' folder. Skipping..." -ForegroundColor Red
            return
        }
        Install-Application -App $app
    } else {
        Write-Host " * Invalid selection: $AppNumber. Skipping..." -ForegroundColor Red
    }
}



# Function to install multiple applications
function Install-MultipleApplications {
    param (
        [Parameter(Mandatory = $true)]
        [string[]]$AppNumbers
    )

    foreach ($number in $AppNumbers) {
        if ($number -match '^\d+$' -and [int]$number -ge 1 -and [int]$number -le $Applications.Count) {
            Install-SingleApplication -AppNumber ([int]$number)
        } else {
            Write-Host " * Invalid selection: $number. Skipping..." -ForegroundColor Red
        }
    }
}




Push-Location "files"

# Main script logic
do {
    Show-Menu
    $choice = Read-Host "Please select an option (e.g., 1, 1,2,3, 1-3, A, Q)"
    switch -Regex ($choice) {
        '^\d+$' {
            # Single app installation (e.g., 1)
            Install-SingleApplication -AppNumber ([int]$choice)
            Pause
        }
        '^\d+(,\d+)*$' {
            # Comma-separated numbers (e.g., 1,2,3)
            $AppNumbers = $choice -split ','
            Install-MultipleApplications -AppNumbers $AppNumbers
            Pause
        }
        '^\d+-\d+$' {
            # Range of numbers (e.g., 1-3)
            $start, $end = $choice -split '-'
            $AppNumbers = $start..$end
            Install-MultipleApplications -AppNumbers $AppNumbers
            Pause
        }
        'A' {
            foreach ($app in $Applications) {
                if (-not (Test-Path "./$($app.FileName)")) {
                    Write-Host " * ERROR: Installer for $($app.Name) ('$($app.FileName)') not found in 'files' folder. Skipping..." -ForegroundColor Red
                    continue
                }
                Install-Application -App $app
            }
            Pause
        }
        'Q' {
            Write-Host "Exiting..." -ForegroundColor Cyan
            exit
        }
        default {
            Write-Host "Invalid option. Please try again." -ForegroundColor Red
            Pause
        }
    }
} while ($choice -ne 'Q')


Write-Host "`t =============================================================================="
Write-Host "`t Installation complete. Restart if necessary." -ForegroundColor Cyan
Write-Host "`t =============================================================================="
Pop-Location

Pause