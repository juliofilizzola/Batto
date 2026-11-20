#!/usr/bin/env bash
# Removes the per-user install. Settings are kept unless --purge is given.
# Options:
#   --purge   also remove settings (~/.config/Batto) and the udev rule (needs sudo)
set -euo pipefail

BIN="${HOME}/.local/bin/batto"
DESKTOP="${HOME}/.local/share/applications/batto.desktop"
ICON="${HOME}/.local/share/icons/hicolor/256x256/apps/batto.png"
AUTOSTART="${XDG_CONFIG_HOME:-$HOME/.config}/autostart/batto.desktop"
SETTINGS_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/Batto"
UDEV_DEST="/etc/udev/rules.d/70-batto-logitech.rules"

PURGE=0
for arg in "$@"; do
    case "$arg" in
        --purge) PURGE=1 ;;
        *) echo "unknown option: $arg" >&2; exit 2 ;;
    esac
done

if pgrep -x batto >/dev/null 2>&1; then
    echo "error: Batto is running. Quit it from the tray menu (Sair) and run the uninstall again." >&2
    exit 1
fi

echo ">>> Removing Batto"
rm -f "$BIN" "$DESKTOP" "$ICON" "$AUTOSTART"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$(dirname "$DESKTOP")" || true

if (( PURGE )); then
    rm -rf "$SETTINGS_DIR"
    if [[ -f "$UDEV_DEST" ]]; then
        echo ">>> Removing the udev rule (sudo)"
        sudo rm -f "$UDEV_DEST"
        sudo udevadm control --reload-rules
    fi
    echo "[ok] settings removed"
else
    echo "[ok] settings kept in ${SETTINGS_DIR} (use --purge to remove them)"
fi
echo ">>> Batto removed."
