#!/usr/bin/env bash
set -e

# VirtualAssistant Deploy Script
# Usage: ./deploy.sh <base-dir>
# Example: sudo ./deploy.sh /opt/olbrasoft/virtual-assistant

BASE_DIR="$1"
if [ -z "$BASE_DIR" ]; then
    echo "Usage: $0 <base-dir>"
    echo "Example: sudo $0 /opt/olbrasoft/virtual-assistant"
    exit 1
fi

PROJECT_PATH="/home/jirka/Olbrasoft/VirtualAssistant"
SERVICE_NAME="virtual-assistant.service"
LOG_SERVICE_NAME="virtual-assistant-logs.service"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║             VirtualAssistant Deploy Script                    ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Target: $BASE_DIR"
echo "╚══════════════════════════════════════════════════════════════╝"
echo

cd "$PROJECT_PATH"

# Step 1: Run tests (skip integration tests - they call real APIs)
echo "📋 Running tests..."
dotnet test --filter "FullyQualifiedName!~IntegrationTests" --verbosity minimal
if [ $? -ne 0 ]; then
    echo "❌ Tests failed! Aborting deployment."
    exit 1
fi
echo "✅ All tests passed"
echo

# Step 2: Publish to app/ subdirectory
echo "🔨 Publishing to $BASE_DIR/app..."
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
  -c Release \
  -o "$BASE_DIR/app"

echo "✅ Published to $BASE_DIR/app"
echo

# Step 3: Copy additional resources (preserve existing)
echo "📦 Copying resources..."

# Icons (for tray icon)
if [ -d "$PROJECT_PATH/icons" ]; then
    mkdir -p "$BASE_DIR/icons"
    cp -r "$PROJECT_PATH/icons/"* "$BASE_DIR/icons/" 2>/dev/null || true
    echo "  ✅ Icons copied"
fi

# Models (app-specific models like silero_vad.onnx, NOT Whisper models!)
if [ -d "$PROJECT_PATH/models" ]; then
    mkdir -p "$BASE_DIR/models"
    cp -r "$PROJECT_PATH/models/"* "$BASE_DIR/models/" 2>/dev/null || true
    echo "  ✅ Models copied"
fi

# Step 4: Create directories
mkdir -p "$BASE_DIR/data"
mkdir -p "$BASE_DIR/config"
mkdir -p "$BASE_DIR/certs"
echo "  ✅ Directories created"

# Step 5: Move appsettings.json to config/ (if not already there)
if [ -f "$BASE_DIR/app/appsettings.json" ]; then
    # Only move if config/appsettings.json doesn't exist (don't overwrite user config)
    if [ ! -f "$BASE_DIR/config/appsettings.json" ]; then
        mv "$BASE_DIR/app/appsettings.json" "$BASE_DIR/config/"
        echo "  ✅ Moved appsettings.json to config/"
    else
        rm "$BASE_DIR/app/appsettings.json"
        echo "  ℹ️  Removed appsettings.json from app/ (using existing config)"
    fi
fi

echo

# Step 6: Install systemd services if needed (user services only)
if [ "$EUID" -ne 0 ]; then
    SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
    mkdir -p "$SYSTEMD_USER_DIR"

    if [ ! -f "$SYSTEMD_USER_DIR/$SERVICE_NAME" ]; then
        echo "📦 Installing systemd service..."
        cp "$PROJECT_PATH/deploy/$SERVICE_NAME" "$SYSTEMD_USER_DIR/"
        systemctl --user daemon-reload
        systemctl --user enable "$SERVICE_NAME"
        echo "✅ Service installed and enabled"
    else
        echo "ℹ️  Service already installed"
    fi

    if [ ! -f "$SYSTEMD_USER_DIR/$LOG_SERVICE_NAME" ]; then
        echo "📦 Installing log viewer service..."
        cp "$PROJECT_PATH/deploy/$LOG_SERVICE_NAME" "$SYSTEMD_USER_DIR/"
        systemctl --user daemon-reload
        systemctl --user enable "$LOG_SERVICE_NAME"
        echo "✅ Log viewer service installed and enabled"
    else
        echo "ℹ️  Log viewer service already installed"
    fi

    # Step 7: Restart services
    echo "🔄 Restarting services..."
    systemctl --user restart "$SERVICE_NAME" || true
    systemctl --user restart "$LOG_SERVICE_NAME" || true

    # Step 8: Verify
    sleep 2
    echo
    echo "📊 Service status:"
    systemctl --user status "$SERVICE_NAME" --no-pager || true
    echo
    echo "📊 Log viewer status:"
    systemctl --user status "$LOG_SERVICE_NAME" --no-pager || true
else
    echo "⚠️  Running as root - skipping systemd user service management"
    echo "   Run 'systemctl --user restart virtual-assistant.service' manually"
fi

echo
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║               ✅ Deployment completed!                        ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Deployed to: $BASE_DIR"
echo "║  Log viewer:  http://localhost:5053                          ║"
echo "║  ScrollLock:  Toggle mute                                    ║"
echo "║  Tray icon:   Right-click for menu                           ║"
echo "╚══════════════════════════════════════════════════════════════╝"
