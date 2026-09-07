#!/usr/bin/env sh
set -eu
exec pwsh -NoProfile -File "$(dirname "$0")/setup.ps1" "$@"
