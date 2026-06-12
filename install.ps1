param(
    [switch]$Uninstall,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
AudioSettingsEnforcer Installer

USAGE:
    .\install.ps1              Install or reinstall the tray app
    .\install.ps1 -Uninstall   Uninstall the tray app
    .\install.ps1 -Help        Show this help message

DESCRIPTION:
    Installs, reinstalls, or uninstalls the AudioSettingsEnforcer tray app
    for the current user. No administrator privileges required.

    - Installs to %LocalAppData%\AudioSettingsEnforcer
    - Starts automatically at logon (HKCU Run key)
    - Preserves user-edited appsettings.json during reinstall

INSTALLATION:
    1. Run the script
    2. Tweak settings from the task tray icon (or edit appsettings.json;
       the app reloads it live)

UNINSTALLATION:
    Run the script with -Uninstall flag to remove the app and all files.

"@
    exit 0
}

$ErrorActionPreference = 'Stop'

# Constants
$AppName     = 'AudioSettingsEnforcer'
$InstallDir  = "$env:LocalAppData\AudioSettingsEnforcer"
$BinaryName  = 'windows-audio-settings-enforcer.exe'
$ConfigName  = 'appsettings.json'
$RunKeyPath  = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ProjectRoot = $PSScriptRoot
$PublishDir  = "$ProjectRoot\bin\Release\net10.0-windows\win-x64\publish"

# Helper functions
function Write-Step {
    param([string]$Message)
    Write-Host "`n[*] $Message" -ForegroundColor Cyan
}

function Write-OK {
    param([string]$Message)
    Write-Host "    OK: $Message" -ForegroundColor Green
}

function Stop-RunningInstance {
    $installedBinary = Join-Path $InstallDir $BinaryName
    $running = Get-Process -Name ([System.IO.Path]::GetFileNameWithoutExtension($BinaryName)) -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $installedBinary }
    if ($running) {
        $running | Stop-Process -Force
        $running | ForEach-Object { try { $_.WaitForExit(10000) | Out-Null } catch {} }
        Write-OK "Running instance stopped."
    } else {
        Write-OK "No running instance found."
    }
}

try {
    if ($Uninstall) {
        # ========== UNINSTALL FLOW ==========
        Write-Step "Stopping running instance..."
        Stop-RunningInstance

        Write-Step "Removing autostart entry..."
        if (Get-ItemProperty -Path $RunKeyPath -Name $AppName -ErrorAction SilentlyContinue) {
            Remove-ItemProperty -Path $RunKeyPath -Name $AppName
            Write-OK "Autostart entry removed."
        } else {
            Write-OK "Autostart entry already absent."
        }

        Write-Step "Removing install directory '$InstallDir'..."
        if (Test-Path $InstallDir) {
            Remove-Item -Path $InstallDir -Recurse -Force
            Write-OK "Directory removed."
        } else {
            Write-OK "Directory already absent."
        }

        Write-Host ""
        Write-Host "Uninstallation complete." -ForegroundColor Green
        Write-Host ""
    } else {
        # ========== INSTALL FLOW ==========
        $backupConfig = $null
        if (Test-Path (Join-Path $InstallDir $ConfigName)) {
            Write-Step "Backing up existing configuration..."
            $backupConfig = [System.IO.Path]::GetTempFileName()
            Copy-Item (Join-Path $InstallDir $ConfigName) $backupConfig -Force
            Write-OK "Configuration backed up."
        }

        Write-Step "Publishing project (Release, win-x64, self-contained)..."
        Push-Location $ProjectRoot
        try {
            dotnet publish -c Release -r win-x64 --self-contained
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet publish failed with exit code $LASTEXITCODE."
            }
        } finally {
            Pop-Location
        }
        Write-OK "Publish succeeded."

        $sourceBinary = Join-Path $PublishDir $BinaryName
        if (-not (Test-Path $sourceBinary)) {
            throw "Published binary not found at '$sourceBinary'. Aborting."
        }

        Write-Step "Stopping running instance..."
        Stop-RunningInstance

        Write-Step "Installing files to '$InstallDir'..."
        if (-not (Test-Path $InstallDir)) {
            New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        }
        Copy-Item -Path "$PublishDir\*" -Destination $InstallDir -Recurse -Force
        Write-OK "Files copied."

        if ($backupConfig) {
            Write-Step "Restoring user configuration..."
            Copy-Item $backupConfig (Join-Path $InstallDir $ConfigName) -Force
            Remove-Item $backupConfig
            Write-OK "User configuration restored."
        }

        Write-Step "Registering autostart entry..."
        $installedBinary = Join-Path $InstallDir $BinaryName
        Set-ItemProperty -Path $RunKeyPath -Name $AppName -Value "`"$installedBinary`""
        Write-OK "App will start at logon."

        Write-Step "Starting the app..."
        Start-Process -FilePath $installedBinary -WorkingDirectory $InstallDir
        Write-OK "App started."

        Write-Host ""
        Write-Host "Installation complete." -ForegroundColor Green
        Write-Host "  Install dir : $InstallDir"
        Write-Host "  Config file : $(Join-Path $InstallDir $ConfigName)"
        Write-Host "  Use the task tray icon to tweak settings or toggle enforcement."
        Write-Host ""
    }
} catch {
    Write-Host ""
    Write-Host "[FAILED] $_" -ForegroundColor Red
    Write-Host "The operation did not complete. Review the message above." -ForegroundColor Red
    exit 1
}
