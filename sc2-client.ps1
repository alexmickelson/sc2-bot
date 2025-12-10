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
    if (-not (Test-Path "./StarCraftII")) {
        Write-Host "StarCraft II not found in current directory." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "IMPORTANT: Blizzard does not provide headless Windows builds." -ForegroundColor Cyan
        Write-Host "You need to install the full StarCraft II client from Battle.net." -ForegroundColor Cyan
        Write-Host ""
        
        # Check if Battle.net installation exists
        if (Test-Path $BATTLE_NET_PATH) {
            Write-Host "Found StarCraft II installation at: $BATTLE_NET_PATH" -ForegroundColor Green
            Write-Host "Creating symbolic link..." -ForegroundColor Cyan
            
            try {
                New-Item -ItemType SymbolicLink -Path "./StarCraftII" -Target $BATTLE_NET_PATH -ErrorAction Stop | Out-Null
                Write-Host "Successfully linked to Battle.net installation!" -ForegroundColor Green
            } catch {
                Write-Host "Failed to create symbolic link. Trying junction..." -ForegroundColor Yellow
                try {
                    cmd /c "mklink /J StarCraftII `"$BATTLE_NET_PATH`""
                    Write-Host "Successfully created junction to Battle.net installation!" -ForegroundColor Green
                } catch {
                    Write-Host "Error: Could not create link." -ForegroundColor Red
                    Write-Host "Please run as Administrator or manually create a link:" -ForegroundColor Yellow
                    Write-Host "  New-Item -ItemType SymbolicLink -Path './StarCraftII' -Target '$BATTLE_NET_PATH'" -ForegroundColor Gray
                    exit 1
                }
            }
        } else {
            Write-Host "SETUP REQUIRED:" -ForegroundColor Red
            Write-Host ""
            Write-Host "1. Install StarCraft II from Battle.net:" -ForegroundColor Yellow
            Write-Host "   https://www.blizzard.com/download/" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "2. After installation, run this script again." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "3. Or if installed elsewhere, create a symlink manually:" -ForegroundColor Yellow
            Write-Host "   New-Item -ItemType SymbolicLink -Path './StarCraftII' -Target 'YOUR_SC2_PATH'" -ForegroundColor Gray
            Write-Host ""
            exit 1
        }
    } else {
        Write-Host "StarCraft II found at ./StarCraftII" -ForegroundColor Green
    }
}

function Install-Maps {
    # 2. (Optional) Download map packs (e.g. ladder maps)
    if (-not (Test-Path "./StarCraftII/Maps/Melee")) {
        Write-Host "Downloading default map pack..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $MAPS_URL -OutFile $MAPS_ZIP
        
        if (Get-Command "7z" -ErrorAction SilentlyContinue) {
            & 7z x $MAPS_ZIP -o"./StarCraftII/Maps"
        } else {
            Expand-Archive -Path $MAPS_ZIP -DestinationPath "./StarCraftII/Maps" -Force
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
    param([int]$Port)
    
    # 3. Launch SC2 Server directly
    Write-Host "Launching StarCraft II Server..." -ForegroundColor Cyan
    
    $SC2_BINARY = Get-ChildItem -Path "./StarCraftII/Versions" -Filter "SC2_x64.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $SC2_BINARY) {
        Write-Host "Error: SC2_x64.exe binary not found." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Starting SC2 on port $Port..." -ForegroundColor Green
    Write-Host "Using binary: $SC2_BINARY" -ForegroundColor Gray
    
    $dataDir = Resolve-Path "./StarCraftII"
    $tempDir = "$env:TEMP\sc2_temp"
    
    # Ensure temp directory exists
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    
    # Launch SC2
    & $SC2_BINARY `
        -listen 127.0.0.1 `
        -port $Port `
        -dataDir $dataDir `
        -tempDir $tempDir
}

function Main {
    Write-Host "=== StarCraft II Client Setup ===" -ForegroundColor Magenta
    
    Install-SC2
    Install-Maps
    
    $port = Find-AvailablePort -Port $StartPort
    Write-Host "Using port: $port" -ForegroundColor Cyan
    
    Start-SC2 -Port $port
}

# Run main function
Main
