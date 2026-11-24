<#
.SYNOPSIS
  Aplica a configuração do projeto (Player Settings Android, URP mobile, cena Boot) em -batchmode.
  Idempotente: pode rodar quantas vezes quiser. Equivale ao menu "Fazenda > Configurar projeto (Etapa 0)".
.NOTES
  Feche o projeto no Editor antes. Rode DUAS vezes na primeira vez: a primeira importa os
  pacotes e troca o Input System; a segunda já roda com tudo carregado.
#>
. "$PSScriptRoot\UnityCommon.ps1"

$logDir = Join-Path $ProjectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'setup-project.log'

$code = Invoke-Unity @('-batchmode', '-quit', '-nographics', '-projectPath', $ProjectRoot,
    '-buildTarget', 'Android', '-executeMethod', 'Game.Editor.ProjectSetup.ApplyFromCommandLine', '-logFile', $log)

if ($code -ne 0) { Write-Host "Falhou (código $code). Log: $log" -ForegroundColor Red; exit $code }
Write-Host "Projeto configurado. Lembre de commitar ProjectSettings/ e Assets/_Project/Presentation/." -ForegroundColor Green
