#!/usr/bin/env nix-shell 
#!nix-shell -i bash -p wget unzip mesa mesa-gl-headers libgbm mesa-demos

set -e

# 1. Download headless StarCraft II Linux build
#    (update the version if newer when you read this)
SC2_ZIP_URL="http://blzdistsc2-a.akamaihd.net/Linux/SC2.4.10.zip"
SC2_ZIP="SC2.4.10.zip"

if [ ! -d "./StarCraftII" ]; then
    echo "Downloading StarCraft II headless build..."
    if [ ! -f "$SC2_ZIP" ]; then
        wget -O "$SC2_ZIP" "$SC2_ZIP_URL"
    fi

    echo "Extracting..."
    echo "password is iagreetotheeula"
    unzip -o "$SC2_ZIP" -d .
    rm "$SC2_ZIP"
else
    echo "StarCraft II already installed at ./StarCraftII"
fi

# 2. (Optional) Download map packs (e.g. ladder maps)
MAPS_URL="http://blzdistsc2-a.akamaihd.net/MapPacks/Melee.zip"
MAPS_ZIP="Melee.zip"

if [ ! -d "./StarCraftII/Maps/Melee" ]; then
    echo "Downloading default map pack..."
    wget -O "$MAPS_ZIP" "$MAPS_URL"
    unzip -o "$MAPS_ZIP" -d ./StarCraftII/Maps
    rm "$MAPS_ZIP"
else
    echo "Maps already installed."
fi

# 3. Launch SC2 Server directly
echo "Launching StarCraft II Headless Server..."
SC2_BINARY=$(find ./StarCraftII/Versions -name "SC2_x64" | head -n 1)

if [ -z "$SC2_BINARY" ]; then
    echo "Error: SC2_x64 binary not found."
    exit 1
fi

# Ensure executable permissions
chmod +x "$SC2_BINARY"

echo "Starting SC2 on port 5000..."

# $(find /nix/store -name libOSMesa.so.8 | head -n 1)


# "$SC_BINARY" -listen 127.0.0.1 -port 5000 -dataDir "$(pwd)/StarCraftII" -tempDir "/tmp/sc2_temp"
"$SC2_BINARY" \
    -listen 127.0.0.1 \
    -port 5000 \
    -eglpath "/nix/store/z88avybj8n2svi9wv1hl937k2k3mbc2d-libglvnd-1.7.0/lib/libEGL.so" \
    -dataDir "$(pwd)/StarCraftII" \
    -tempDir "/tmp/sc2_temp"
