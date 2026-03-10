#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Use explicit $HOME paths to avoid snap/flatpak sandbox overrides of XDG vars.
INSTALL_DIR="$HOME/.local/share/aggy"
DATA_DIR="$HOME/.local/share/aggy/data"
SERVICE_NAME="aggy-worker"
SERVICE_FILE="$HOME/.config/systemd/user/${SERVICE_NAME}.service"
ZSHRC="$HOME/.zshrc"
MARKER_BEGIN="# >>> aggy >>>"
MARKER_END="# <<< aggy <<<"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

info()  { printf '\033[1;34m[aggy]\033[0m %s\n' "$*"; }
ok()    { printf '\033[1;32m[aggy]\033[0m %s\n' "$*"; }
die()   { printf '\033[1;31m[aggy]\033[0m ERROR: %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# Preflight checks
# ---------------------------------------------------------------------------

command -v dotnet >/dev/null 2>&1 || die "'dotnet' not found. Install .NET 10 SDK first."
command -v systemctl >/dev/null 2>&1 || die "'systemctl' not found. systemd is required."

if grep -qF "$MARKER_BEGIN" "$ZSHRC" 2>/dev/null; then
    die "aggy is already installed. Run uninstall.sh first to reinstall."
fi

# ---------------------------------------------------------------------------
# 1. Build
# ---------------------------------------------------------------------------

info "Building solution..."
dotnet build "$REPO_DIR/Aggregator.sln" --configuration Release --nologo

# ---------------------------------------------------------------------------
# 2. Test
# ---------------------------------------------------------------------------

info "Running tests..."
dotnet test "$REPO_DIR/Aggregator.sln" --configuration Release --no-build --nologo

# ---------------------------------------------------------------------------
# 3. Publish
# ---------------------------------------------------------------------------

info "Publishing console app to $INSTALL_DIR/console ..."
dotnet publish "$REPO_DIR/src/Aggregator.Console/Aggregator.Console.csproj" \
    --configuration Release \
    --output "$INSTALL_DIR/console" \
    --nologo

info "Publishing background service to $INSTALL_DIR/worker ..."
dotnet publish "$REPO_DIR/src/Aggregator.BackgroundService/Aggregator.BackgroundService.csproj" \
    --configuration Release \
    --output "$INSTALL_DIR/worker" \
    --nologo

# ---------------------------------------------------------------------------
# 4. Create data directory
# ---------------------------------------------------------------------------

mkdir -p "$DATA_DIR"

# ---------------------------------------------------------------------------
# 5. Install systemd user service
# ---------------------------------------------------------------------------

info "Installing systemd user service ($SERVICE_NAME)..."
mkdir -p "$(dirname "$SERVICE_FILE")"

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=Aggy News Aggregator Worker
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/dotnet ${INSTALL_DIR}/worker/Aggregator.BackgroundService.dll
Environment="ConnectionStrings__Default=Data Source=${DATA_DIR}/news.db"
WorkingDirectory=${INSTALL_DIR}/worker
Restart=on-failure
RestartSec=10

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
systemctl --user enable --now "$SERVICE_NAME"
ok "Background service started."

# ---------------------------------------------------------------------------
# 6. Add aggy to ~/.zshrc
# ---------------------------------------------------------------------------

info "Adding aggy to $ZSHRC ..."

cat >> "$ZSHRC" <<EOF

${MARKER_BEGIN}
# Aggy CLI — news aggregator
alias aggy="ConnectionStrings__Default='Data Source=${DATA_DIR}/news.db' dotnet ${INSTALL_DIR}/console/Aggregator.Console.dll"
aggy top
${MARKER_END}
EOF

ok "Installation complete."
echo
echo "  Restart your terminal or run:"
echo "    source ~/.zshrc"
echo
echo "  Then try:  aggy --help"
