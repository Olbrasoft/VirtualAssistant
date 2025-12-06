#!/bin/bash
set -e

# VirtualAssistant Deploy Script
# Builds and deploys VirtualAssistant Service

PROJECT_PATH="/home/jirka/Olbrasoft/VirtualAssistant"
DEPLOY_TARGET="/home/jirka/virtual-assistant/main"
SERVICE_NAME="virtual-assistant.service"
LOG_SERVICE_NAME="virtual-assistant-logs.service"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║             VirtualAssistant Deploy Script                    ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo

cd "$PROJECT_PATH"

# Step 1: Run tests
echo "📋 Running tests..."
dotnet test
if [ $? -ne 0 ]; then
    echo "❌ Tests failed! Aborting deployment."
    exit 1
fi
echo "✅ All tests passed"
echo

# Step 2: Build and publish
echo "🔨 Building and publishing..."
dotnet publish src/VirtualAssistant.Service/VirtualAssistant.Service.csproj \
  -c Release \
  -o "$DEPLOY_TARGET" \
  --no-self-contained

echo "✅ Published to $DEPLOY_TARGET"
echo

# Step 3: Install systemd services if needed
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

# Step 4: Restart services
echo "🔄 Restarting services..."
systemctl --user restart "$SERVICE_NAME" || true
systemctl --user restart "$LOG_SERVICE_NAME" || true

# Step 5: Verify
sleep 2
echo
echo "📊 Service status:"
systemctl --user status "$SERVICE_NAME" --no-pager || true
echo
echo "📊 Log viewer status:"
systemctl --user status "$LOG_SERVICE_NAME" --no-pager || true

echo
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║               ✅ Deployment completed!                        ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Log viewer: http://localhost:5053                           ║"
echo "║  ScrollLock: Toggle mute                                     ║"
echo "║  Tray icon: Right-click for menu                             ║"
echo "╚══════════════════════════════════════════════════════════════╝"
