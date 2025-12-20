#!/bin/bash
set -e

# VirtualAssistant Deploy Script
# Builds, tests, and deploys all components

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="/home/jirka/apps/virtual-assistant"

echo "=========================================="
echo "VirtualAssistant Deploy Script"
echo "=========================================="
echo ""

cd "$SCRIPT_DIR"

# Step 1: Run tests
echo "[1/5] Running tests..."
dotnet test --verbosity minimal
if [ $? -ne 0 ]; then
    echo "❌ Tests failed! Aborting deployment."
    exit 1
fi
echo "✓ All tests passed"
echo ""

# Step 2: Publish VirtualAssistant.Service
echo "[2/5] Publishing VirtualAssistant.Service..."
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
    -c Release \
    --no-self-contained \
    --verbosity minimal
echo "✓ VirtualAssistant.Service published to $DEPLOY_DIR/"
echo ""

# Step 3: Publish VirtualAssistant.PushToTalk.Service
echo "[3/5] Publishing VirtualAssistant.PushToTalk.Service..."
dotnet publish src/VirtualAssistant.PushToTalk.Service/VirtualAssistant.PushToTalk.Service.csproj \
    -c Release \
    --no-self-contained \
    --verbosity minimal
echo "✓ VirtualAssistant.PushToTalk.Service published to $DEPLOY_DIR/push-to-talk/"
echo ""

# Step 4: Build and deploy TypeScript plugins
echo "[4/5] Building TypeScript plugins..."
PLUGINS_SRC="$SCRIPT_DIR/plugins"
PLUGINS_DEST="$DEPLOY_DIR/plugins"

if [ -d "$PLUGINS_SRC" ]; then
    mkdir -p "$PLUGINS_DEST"

    for plugin_dir in "$PLUGINS_SRC"/*/; do
        plugin_name=$(basename "$plugin_dir")
        echo "  Building $plugin_name..."

        cd "$plugin_dir"

        # Install dependencies if node_modules doesn't exist
        if [ ! -d "node_modules" ]; then
            npm install --silent
        fi

        # Build TypeScript
        npm run build --silent

        # Clean destination and copy ONLY runtime files (NO source files!)
        dest_plugin="$PLUGINS_DEST/$plugin_name"
        rm -rf "$dest_plugin"
        mkdir -p "$dest_plugin"

        # Copy only: dist/ (compiled JS), package.json, node_modules/
        cp -r dist "$dest_plugin/"
        cp package.json "$dest_plugin/"
        if [ -d "node_modules" ]; then
            cp -r node_modules "$dest_plugin/"
        fi

        echo "  ✓ $plugin_name built and deployed (dist + node_modules only)"
    done
    cd "$SCRIPT_DIR"
fi
echo "✓ All plugins deployed"
echo ""

# Step 5: Restart services
echo "[5/5] Restarting services..."
systemctl --user daemon-reload

if systemctl --user is-active --quiet virtual-assistant.service; then
    systemctl --user restart virtual-assistant.service
    echo "  ✓ virtual-assistant.service restarted"
else
    echo "  ⚠ virtual-assistant.service not running (skipped)"
fi

if systemctl --user is-active --quiet push-to-talk.service; then
    systemctl --user restart push-to-talk.service
    echo "  ✓ push-to-talk.service restarted"
else
    echo "  ⚠ push-to-talk.service not running (skipped)"
fi
echo ""

# Verify services
echo "=========================================="
echo "Deployment complete! Service status:"
echo "=========================================="
systemctl --user status virtual-assistant.service --no-pager -l 2>/dev/null | head -5 || true
echo ""

echo "Done!"
