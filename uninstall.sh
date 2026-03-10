#!/usr/bin/env bash
set -euo pipefail

# Use explicit $HOME paths to avoid snap/flatpak sandbox overrides of XDG vars.
INSTALL_DIR="$HOME/.local/share/aggy"
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
warn()  { printf '\033[1;33m[aggy]\033[0m WARNING: %s\n' "$*"; }

# ---------------------------------------------------------------------------
# Preflight check
# ---------------------------------------------------------------------------

if ! grep -qF "$MARKER_BEGIN" "$ZSHRC" 2>/dev/null; then
    warn "aggy does not appear to be installed (no marker found in $ZSHRC)."
    exit 0
fi

# ---------------------------------------------------------------------------
# 1. Stop and disable systemd user service
# ---------------------------------------------------------------------------

if command -v systemctl >/dev/null 2>&1; then
    if systemctl --user is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
        info "Stopping $SERVICE_NAME..."
        systemctl --user stop "$SERVICE_NAME"
    fi

    if systemctl --user is-enabled --quiet "$SERVICE_NAME" 2>/dev/null; then
        info "Disabling $SERVICE_NAME..."
        systemctl --user disable "$SERVICE_NAME"
    fi

    if [ -f "$SERVICE_FILE" ]; then
        info "Removing service file $SERVICE_FILE..."
        rm -f "$SERVICE_FILE"
        systemctl --user daemon-reload
    fi
else
    warn "'systemctl' not found — skipping service removal."
fi

# ---------------------------------------------------------------------------
# 2. Remove installed binaries
# ---------------------------------------------------------------------------

if [ -d "$INSTALL_DIR/console" ] || [ -d "$INSTALL_DIR/worker" ]; then
    info "Removing installed binaries from $INSTALL_DIR ..."
    rm -rf "$INSTALL_DIR/console" "$INSTALL_DIR/worker"
fi

# Leave $INSTALL_DIR/data intact so the user's database is not deleted.
if [ -d "$INSTALL_DIR/data" ]; then
    warn "Leaving database at $INSTALL_DIR/data — remove it manually if you want to delete your news data."
fi

# ---------------------------------------------------------------------------
# 3. Remove aggy block from ~/.zshrc
# ---------------------------------------------------------------------------

info "Removing aggy block from $ZSHRC..."

# Use awk fixed-string matching to delete everything between (and including) the markers.
awk -v begin="$MARKER_BEGIN" -v end="$MARKER_END" '
index($0, begin) { skip=1 }
!skip { print }
index($0, end)   { skip=0 }
' "$ZSHRC" > "${ZSHRC}.aggy_tmp" && mv "${ZSHRC}.aggy_tmp" "$ZSHRC"

ok "Uninstallation complete."
echo
echo "  Restart your terminal or run:"
echo "    source ~/.zshrc"
