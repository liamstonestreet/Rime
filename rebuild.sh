#!/usr/bin/env bash
# Developer shortcut (Git Bash): rebuild everything and reinstall. Same as running install.ps1.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./install.ps1 "$@"
