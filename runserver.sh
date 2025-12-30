#!/usr/bin/env bash
set -euo pipefail

# Default configuration: DebugOpt. Override with CONFIG=Release (or pass -c ...).
CONFIG=DebugOpt
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      CONFIG="$2"
      shift 2
      ;;
    *)
      EXTRA_ARGS+=("$1")
      shift
      ;;
  esac
done

# Build once, then run the built DLL directly so assembly resolution uses bin/Content.Server.
dotnet build -c "$CONFIG" Content.Server/Content.Server.csproj

pushd bin/Content.Server >/dev/null
dotnet Content.Server.dll --contentroot "$(pwd)/.." "${EXTRA_ARGS[@]}"
popd >/dev/null
