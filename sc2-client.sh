#!/usr/bin/env nix-shell 
#!nix-shell -i bash -p wget unzip mesa-demos

set -e
set -x

SC2_ZIP_URL="http://blzdistsc2-a.akamaihd.net/Linux/SC2.4.10.zip"
SC2_ZIP="SC2.4.10.zip"
MAPS_URL="http://blzdistsc2-a.akamaihd.net/MapPacks/Melee.zip"
MAPS_ZIP="Melee.zip"
START_PORT=5000

install_sc2() {
    # 1. Download headless StarCraft II Linux build
    if [ -L "./StarCraftII" ]; then
        echo "Removing invalid StarCraftII symlink..."
        rm "./StarCraftII"
    fi

    if [ ! -d "./StarCraftII" ]; then
        echo "Downloading StarCraft II headless build..."
        if [ ! -f "$SC2_ZIP" ]; then
            wget -O "$SC2_ZIP" "$SC2_ZIP_URL"
        fi

        echo "Extracting..."
        unzip -P iagreetotheeula -n "$SC2_ZIP" -d .
        rm "$SC2_ZIP"
    else
        echo "StarCraft II already installed at ./StarCraftII"
    fi
}

install_maps() {
    # 2. (Optional) Download map packs (e.g. ladder maps)
    if [ ! -d "./StarCraftII/Maps/Melee" ]; then
        echo "Downloading default map pack..."
        wget -O "$MAPS_ZIP" "$MAPS_URL"
        unzip -o "$MAPS_ZIP" -d ./StarCraftII/Maps
        rm "$MAPS_ZIP"
    else
        echo "Maps already installed."
    fi
}

find_available_port() {
    local port=$1
    while :; do
        if (echo > /dev/tcp/127.0.0.1/$port) >/dev/null 2>&1; then
            echo "Port $port is in use, trying next..." >&2
            port=$((port + 1))
        else
            echo "$port"
            return
        fi
    done
}

launch_sc2() {
    local port=$1
    # 3. Launch SC2 Server directly
    echo "Launching StarCraft II Headless Server..."
    SC2_BINARY=$(find ./StarCraftII/Versions -name "SC2_x64" | head -n 1)

    if [ -z "$SC2_BINARY" ]; then
        echo "Error: SC2_x64 binary not found."
        exit 1
    fi

    # Ensure executable permissions
    chmod +x "$SC2_BINARY"

    echo "Starting SC2 on port $port..."

    # $(find /nix/store -name libEGL.so | head -n 1)

    # "$SC_BINARY" -listen 127.0.0.1 -port 5000 -dataDir "$(pwd)/StarCraftII" -tempDir "/tmp/sc2_temp"
    
    full_command="\"$SC2_BINARY\" -listen 127.0.0.1 -port \"$port\" -eglpath \"/nix/store/z88avybj8n2svi9wv1hl937k2k3mbc2d-libglvnd-1.7.0/lib/libEGL.so\" -dataDir \"$(pwd)/StarCraftII\" -tempDir \"/tmp/sc2_temp\""
    
    echo "Executing: $full_command"
    
    eval "$full_command"
}

main() {
    install_sc2
    install_maps
    PORT=$(find_available_port $START_PORT)
    launch_sc2 "$PORT"
}

main
