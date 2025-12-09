#!/usr/bin/env nix-shell
#!nix-shell -i bash -p wget unzip uv python3

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

# 3. Install a Python bot environment (using uv)
echo "Setting up Python bot environment with uv..."
uv venv
source .venv/bin/activate
uv pip install --upgrade .

# Patch pysc2 for Python 3.11+ compatibility (random.shuffle removed 'random' arg)
echo "Patching pysc2 for Python 3.13 compatibility..."
find .venv -name "colors.py" -path "*/pysc2/lib/*" -exec sed -i 's/random.shuffle(palette, lambda: 0.5)/random.shuffle(palette)/g' {} +

# 4. Set environment variable for SC2 path
# Note: This only affects the current shell or needs to be added to user's profile manually
# because nix-shell script runs in a subshell.
export SC2PATH=$(pwd)/StarCraftII

echo "----------------------------------------------------------------"
echo "SC2 + maps + PySC2 installed."
echo ""
echo "To run a sample bot manually:"
echo "  source .venv/bin/activate"
echo "  python3 launch_sc2.py"

echo "----------------------------------------------------------------"
echo "Launching SC2 graphically with bot interface..."
export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python
python3 launch_sc2.py
echo "  export SC2PATH=~/StarCraftII"
echo "  python -m pysc2.bin.agent --map Simple64"
echo "----------------------------------------------------------------"
