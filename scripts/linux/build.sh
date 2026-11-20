#!/usr/bin/env bash
# Builds the Linux app as one self-contained executable (no .NET needed on the target machine).
# Output: publish/linux-x64/batto. Requires the .NET 10 SDK; set DOTNET to its path if it is not on PATH.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET:-dotnet}"
OUT="${ROOT}/publish/linux-x64"

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1; then
    echo "error: .NET SDK not found. Install dotnet-sdk 10 or set DOTNET=/path/to/dotnet." >&2
    exit 1
fi

echo ">>> Publishing Batto for linux-x64 into ${OUT}"
"$DOTNET_BIN" publish "${ROOT}/src/Batto.Linux/Batto.Linux.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT"

echo ">>> Done: ${OUT}/batto"
