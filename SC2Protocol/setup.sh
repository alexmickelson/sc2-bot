#!/bin/bash
# Setup script for SC2Protocol - StarCraft II Protocol Buffers for C#

set -e  # Exit on error

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "==================================="
echo "SC2Protocol Setup Script"
echo "==================================="
echo ""

# Check if s2client-proto exists
if [ ! -d "$PROJECT_ROOT/s2client-proto" ]; then
    echo "Cloning s2client-proto repository..."
    cd "$PROJECT_ROOT"
    git clone https://github.com/Blizzard/s2client-proto.git
    echo "✓ Repository cloned"
else
    echo "✓ s2client-proto repository already exists"
fi

# Create proto directory structure
echo ""
echo "Setting up proto files..."
mkdir -p "$SCRIPT_DIR/Protos/s2clientprotocol"

# Copy proto files
echo "Copying proto files..."
cp "$PROJECT_ROOT/s2client-proto/s2clientprotocol"/*.proto "$SCRIPT_DIR/Protos/s2clientprotocol/"
echo "✓ Proto files copied"

# Build the project
echo ""
echo "Building SC2Protocol project..."
cd "$SCRIPT_DIR"
dotnet restore
dotnet build

echo ""
echo "==================================="
echo "✓ Setup complete!"
echo "==================================="
echo ""
echo "The following C# classes have been generated:"
echo "  - Common.cs (Point, Size2DI, PointI, Rectangle, etc.)"
echo "  - Data.cs (AbilityData, UnitTypeData, UpgradeData, etc.)"
echo "  - Debug.cs (DebugCommand, DebugDraw, etc.)"
echo "  - Error.cs (ActionResult, Error codes)"
echo "  - Query.cs (RequestQuery, ResponseQuery, etc.)"
echo "  - Raw.cs (Unit, Observation, Action, etc.)"
echo "  - Sc2Api.cs (Request, Response, Status, etc.)"
echo "  - Score.cs (Score, CategoryScoreDetails, etc.)"
echo "  - Spatial.cs (ObservationFeatureLayer, etc.)"
echo "  - Ui.cs (ObservationUI, ActionUI, etc.)"
echo ""
echo "You can now reference SC2Protocol from other projects:"
echo "  dotnet add reference ../SC2Protocol/SC2Protocol.csproj"
echo ""
