#!/usr/bin/env bash
# Installs Batto for the current user: binary, menu entry and icon under ~/.local.
# Options:
#   --with-udev   also install the Logitech hidraw udev rule (needs sudo)
#   --start       start Batto after installing
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_BIN="${ROOT}/publish/linux-x64/batto"
ICON_SOURCE="${ROOT}/src/Batto.Linux/Assets/batto.png"
DESKTOP_SOURCE="${ROOT}/packaging/linux/batto.desktop"
UDEV_SOURCE="${ROOT}/packaging/linux/70-batto-logitech.rules"

BIN_DIR="${HOME}/.local/bin"
APPS_DIR="${HOME}/.local/share/applications"
ICON_DIR="${HOME}/.local/share/icons/hicolor/256x256/apps"
UDEV_DEST="/etc/udev/rules.d/70-batto-logitech.rules"

WITH_UDEV=0
START=0
for arg in "$@"; do
    case "$arg" in
        --with-udev) WITH_UDEV=1 ;;
        --start) START=1 ;;
        *) echo "unknown option: $arg" >&2; exit 2 ;;
    esac
done

if [[ ! -x "$SOURCE_BIN" ]]; then
    echo "error: ${SOURCE_BIN} not found. Run scripts/linux/build.sh first." >&2
    exit 1
fi

if pgrep -x batto >/dev/null 2>&1; then
    echo "error: Batto is running. Quit it from the tray menu (Sair) and run the install again." >&2
    exit 1
fi

echo ">>> Installing Batto for ${USER}"
install -Dm755 "$SOURCE_BIN" "${BIN_DIR}/batto"
install -Dm644 "$ICON_SOURCE" "${ICON_DIR}/batto.png"

# The menu entry points at the installed binary, so it works from any directory.
mkdir -p "$APPS_DIR"
sed "s|^Exec=.*|Exec=${BIN_DIR}/batto|" "$DESKTOP_SOURCE" > "${APPS_DIR}/batto.desktop"

command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS_DIR" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q "${HOME}/.local/share/icons/hicolor" 2>/dev/null || true

if (( WITH_UDEV )); then
    echo ">>> Installing the Logitech udev rule (sudo)"
    sudo install -Dm644 "$UDEV_SOURCE" "$UDEV_DEST"
    sudo udevadm control --reload-rules
    sudo udevadm trigger --subsystem-match=hidraw
fi

echo "[ok] binary: ${BIN_DIR}/batto"
echo "[ok] menu entry: ${APPS_DIR}/batto.desktop"
case ":${PATH}:" in
    *":${BIN_DIR}:"*) ;;
    *) echo "note: ${BIN_DIR} is not on PATH; start Batto from the menu or add it to PATH." ;;
esac

if (( START )); then
    echo ">>> Starting Batto"
    nohup "${BIN_DIR}/batto" --minimized >/dev/null 2>&1 &
fi
echo ">>> Install finished. To start with the session, use 'Iniciar com o sistema' in the Batto settings."
