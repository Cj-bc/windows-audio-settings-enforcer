param(
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

# Constants
$ServiceName    = 'AudioSettingsEnforcer'
$ServiceDep     = 'Audiosrv'
$ServiceDisplay = 'Audio Settings Enforcer'
$ServiceDesc    = 'Enforces master audio volume and mute state for the default playback device.'
$InstallDir     = "$env:ProgramFiles\AudioSettingsEnforcer"
$BinaryName     = 'windows-audio-settings-enforcer.exe'
$ConfigName     = 'appsettings.json'
$ProjectRoot    = $PSScriptRoot
$PublishDir     = "$ProjectRoot\bin\Release\net10.0-windows\win-x64\publish"

# Helper functions
function Write-Step {
    param([string]$Message)
    Write-Host "`n[*] $Message" -ForegroundColor Cyan
}

function Write-OK {
    param([string]$Message)
    Write-Host "    OK: $Message" -ForegroundColor Green
}

# Elevation check
function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$id
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    Write-Warning "This script must run as Administrator."
    Write-Host "Re-launching elevated..." -ForegroundColor Yellow
    $argList = @('-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($Uninstall) { $argList += '-Uninstall' }
    Start-Process pwsh -Verb RunAs -ArgumentList $argList
    exit
}

try {
    if ($Uninstall) {
        # ========== UNINSTALL FLOW ==========
        Write-Step "Checking for installed service..."
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if (-not $svc) {
            Write-Host "Service '$ServiceName' is not installed. Nothing to uninstall." -ForegroundColor Yellow
            exit 0
        }

        Write-Step "Stopping service '$ServiceName'..."
        if ($svc.Status -ne 'Stopped') {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            $svc.WaitForStatus('Stopped', (New-TimeSpan -Seconds 15))
            Write-OK "Service stopped."
        } else {
            Write-OK "Service was already stopped."
        }

        Write-Step "Removing service registration..."
        Remove-Service -Name $ServiceName -ErrorAction Stop
        Write-OK "Service deleted."

        Write-Step "Removing install directory '$InstallDir'..."
        if (Test-Path $InstallDir) {
            Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction Stop
            Write-OK "Directory removed."
        } else {
            Write-OK "Directory already absent."
        }

        Write-Host ""
        Write-Host "Uninstallation complete." -ForegroundColor Green
        Write-Host ""
    } else {
        # ========== INSTALL FLOW ==========
        Write-Step "Checking for existing installation..."
        $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($existingService) {
            Write-Warning "Service '$ServiceName' is already installed (Status: $($existingService.Status))."
            $answer = Read-Host "Reinstall? This will stop and replace the existing service. [y/N]"
            if ($answer -notmatch '^[Yy]$') {
                Write-Host "Installation skipped." -ForegroundColor Yellow
                exit 0
            }
        }

        $backupConfig = $null
        if ($existingService -and (Test-Path (Join-Path $InstallDir $ConfigName))) {
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

        if ($existingService -and $existingService.Status -ne 'Stopped') {
            Write-Step "Stopping existing service..."
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            $existingService.WaitForStatus('Stopped', (New-TimeSpan -Seconds 15))
            Write-OK "Service stopped."
        }

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

        Write-Step "Registering Windows service..."
        $installedBinary = Join-Path $InstallDir $BinaryName

        if ($existingService) {
            sc.exe config $ServiceName binPath= "`"$installedBinary`"" start= auto | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "sc.exe config failed."
            }
            Write-OK "Service registration updated."
        } else {
            New-Service `
                -Name        $ServiceName `
                -DisplayName $ServiceDisplay `
                -Description $ServiceDesc `
                -BinaryPathName "`"$installedBinary`"" `
                -StartupType Automatic `
                -DependsOn   $ServiceDep `
                -ErrorAction Stop | Out-Null
            Write-OK "Service registered."
        }

        sc.exe config $ServiceName depend= $ServiceDep | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to set service dependency. The service may not start if Audiosrv is unavailable."
        }

        Write-Step "Starting service '$ServiceName'..."
        Start-Service -Name $ServiceName -ErrorAction Stop
        $svc = Get-Service -Name $ServiceName
        try {
            $svc.WaitForStatus('Running', (New-TimeSpan -Seconds 10))
        } catch {
            # WaitForStatus throws if timeout; we want to warn, not fail
        }

        $finalStatus = (Get-Service -Name $ServiceName).Status
        if ($finalStatus -eq 'Running') {
            Write-OK "Service is running."
        } else {
            Write-Host "    WARNING: Service status is '$finalStatus' (expected 'Running')." -ForegroundColor Yellow
            Write-Host "    Check Event Viewer for details." -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "Installation complete." -ForegroundColor Green
        Write-Host "  Install dir : $InstallDir"
        Write-Host "  Config file : $(Join-Path $InstallDir $ConfigName)"
        Write-Host "  Edit the config file to change volume/mute settings; the service reloads it live."
        Write-Host ""
    }
} catch {
    Write-Host ""
    Write-Host "[FAILED] $_" -ForegroundColor Red
    Write-Host "The operation did not complete. Review the message above." -ForegroundColor Red
    exit 1
}
