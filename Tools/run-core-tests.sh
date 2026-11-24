#!/usr/bin/env bash
# Roda os testes de C# puro (Game.Core) fora do Unity, via dotnet. Uso: Tools/run-core-tests.sh [--coverage]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARGS=(--nologo)
if [[ "${1:-}" == "--coverage" ]]; then ARGS+=(--collect:"XPlat Code Coverage" --results-directory "$ROOT/TestResults/core"); fi
dotnet test "$ROOT/Tools/CoreTests/CoreTests.csproj" "${ARGS[@]}"
