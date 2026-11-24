#!/usr/bin/env bash
# Roda os testes de C# puro (Game.Core + parte pura de Game.Services) fora do Unity, via dotnet.
# Uso: Tools/run-core-tests.sh [--coverage]   (--coverage falha se a cobertura de linhas < 80%)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARGS=(--nologo)
if [[ "${1:-}" == "--coverage" ]]; then
  mkdir -p "$ROOT/TestResults/coverage"
  ARGS+=(/p:CollectCoverage=true "/p:Include=\"[Game.Core]*,[Game.Services.Pure]*\""
         /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=total
         /p:CoverletOutputFormat=cobertura "/p:CoverletOutput=$ROOT/TestResults/coverage/")
fi
dotnet test "$ROOT/Tools/CoreTests/CoreTests.csproj" "${ARGS[@]}"
