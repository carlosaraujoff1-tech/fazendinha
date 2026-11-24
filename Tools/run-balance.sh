#!/usr/bin/env bash
# Roda o simulador de balanceamento (1 h, 3 h, 8 h x 3 estratégias) e grava Docs/balance/*.csv e resumo.md.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FAZENDA_WRITE_BALANCE=1 dotnet test "$ROOT/Tools/CoreTests/CoreTests.csproj" --nologo --filter "TestCategory=Balance" --logger "console;verbosity=normal"
