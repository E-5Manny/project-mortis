#!/usr/bin/env bash
# Build and launch Mortis on Linux. Arguments are passed on to the game (e.g. ./run.sh --selftest).
set -e
cd "$(dirname "$0")"
# the csproj pins win-x64, so name the runtime; dotnet may only be in ~/.dotnet
command -v dotnet >/dev/null || { export DOTNET_ROOT="$HOME/.dotnet"; PATH="$PATH:$DOTNET_ROOT"; }
dotnet build -nologo -v q -r linux-x64
exec bin/Debug/net10.0/linux-x64/Mortis "$@"
