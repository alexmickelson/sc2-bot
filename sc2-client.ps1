#!/usr/bin/env pwsh

param(
    [int]$StartPort = 5000
)

$ErrorActionPreference = "Stop"

# Blizzard does not provide headless Windows builds
# Users must install the full Battle.net client
$BATTLE_NET_PATH = "C:\Program Files (x86)\StarCraft II"
$MAPS_URL = "https://blzdistsc2-a.akamaihd.net/MapPacks/Melee.zip"
$MAPS_ZIP = "Melee.zip"

function Install-SC2 {
    # 1. Check for StarCraft II installation
    if (Test-Path $BATTLE_NET_PATH) {
        Write-Host "Found StarCraft II installation at: $BATTLE_NET_PATH" -ForegroundColor Green
        return $BATTLE_NET_PATH
    } elseif (Test-Path "./StarCraftII") {
        Write-Host "Found StarCraft II at ./StarCraftII" -ForegroundColor Green
        return "./StarCraftII"
    } else {
        Write-Host "SETUP REQUIRED:" -ForegroundColor Red
        Write-Host ""
        Write-Host "StarCraft II not found. Please install it:" -ForegroundColor Yellow
        Write-Host "1. Install StarCraft II from Battle.net:" -ForegroundColor Yellow
        Write-Host "   https://www.blizzard.com/download/" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "2. After installation, run this script again." -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

function Install-Maps {
    param([string]$SC2Path)
    
    # 2. (Optional) Download map packs (e.g. ladder maps)
    if (-not (Test-Path "$SC2Path/Maps/Melee")) {
        Write-Host "Downloading default map pack..." -ForegroundColor Cyan
        # Use Start-BitsTransfer for more reliable downloads
        Start-BitsTransfer -Source $MAPS_URL -Destination $MAPS_ZIP
        
        Write-Host "Note: The archive password is 'iagreetotheeula'" -ForegroundColor Yellow
        
        if (Get-Command "7z" -ErrorAction SilentlyContinue) {
            & 7z x $MAPS_ZIP -o"$SC2Path/Maps" -p"iagreetotheeula" -y
        } elseif (Get-Command "tar" -ErrorAction SilentlyContinue) {
            # Create directory if it doesn't exist because tar might not create the parent
            if (-not (Test-Path "$SC2Path/Maps")) { New-Item -ItemType Directory -Path "$SC2Path/Maps" | Out-Null }
            tar -xf $MAPS_ZIP -C "$SC2Path/Maps"
        } else {
            Expand-Archive -Path $MAPS_ZIP -DestinationPath "$SC2Path/Maps" -Force
        }
        
        Remove-Item $MAPS_ZIP
    } else {
        Write-Host "Maps already installed." -ForegroundColor Green
    }
}

function Find-AvailablePort {
    param([int]$Port)
    
    while ($true) {
        $connection = Test-NetConnection -ComputerName 127.0.0.1 -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet
        
        if ($connection) {
            Write-Host "Port $Port is in use, trying next..." -ForegroundColor Yellow
            $Port++
        } else {
            return $Port
        }
    }
}

function Start-SC2 {
    param([int]$Port, [string]$SC2Path)
    
    # 3. Launch SC2 Server directly
    Write-Host "Launching StarCraft II Server..." -ForegroundColor Cyan
    
    $SC2_BINARY = Get-ChildItem -Path "$SC2Path/Versions" -Filter "SC2_x64.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $SC2_BINARY) {
        Write-Host "Error: SC2_x64.exe binary not found." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Starting SC2 on port $Port..." -ForegroundColor Green
    Write-Host "Using binary: $SC2_BINARY" -ForegroundColor Gray
    
    $dataDir = Resolve-Path $SC2Path
    $tempDir = "$env:TEMP\sc2_temp"
    
    # Ensure temp directory exists
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    
    $supportDir = Join-Path $SC2Path "Support64"
    if (-not (Test-Path $supportDir)) {
        $supportDir = Join-Path $SC2Path "Support"
    }
    
    Write-Host "Working Directory: $supportDir" -ForegroundColor Gray

    # Launch SC2
    Start-Process -FilePath $SC2_BINARY `
        -ArgumentList "-listen 127.0.0.1", "-port $Port", "-dataDir `"$dataDir`"", "-tempDir `"$tempDir`"", "-displayMode 0" `
        -WorkingDirectory $supportDir
}

function Main {
    Write-Host "=== StarCraft II Client Setup ===" -ForegroundColor Magenta
    
    $sc2Path = Install-SC2
    Install-Maps -SC2Path $sc2Path
    
    $port = Find-AvailablePort -Port $StartPort
    Write-Host "Using port: $port" -ForegroundColor Cyan
    
    Start-SC2 -Port $port -SC2Path $sc2Path
}

# Run main function
Main
