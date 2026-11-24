<#
.SYNOPSIS
  Gera o APK de depuração (IL2CPP, ARM64) em -batchmode e, opcionalmente, instala e abre no celular.
.EXAMPLE
  .\Tools\build-android-debug.ps1             # só gera Builds\Android\FazendaMercadinho-debug.apk
  .\Tools\build-android-debug.ps1 -Install    # gera, instala via USB e abre o app
.NOTES
  Requer o módulo "Android Build Support" (OpenJDK + Android SDK & NDK) no Unity Hub.
  Feche o projeto no Editor antes. A primeira build IL2CPP demora (10-20 min).
#>
param(
    [string]$Output = 'Builds\Android\FazendaMercadinho-debug.apk',
    [switch]$Install
)
. "$PSScriptRoot\UnityCommon.ps1"

$apk = Join-Path $ProjectRoot $Output
$log = Join-Path $ProjectRoot 'Builds\build-android-debug.log'
New-Item -ItemType Directory -Force -Path (Split-Path $apk) | Out-Null

$code = Invoke-Unity @('-batchmode', '-quit', '-nographics', '-projectPath', $ProjectRoot,
    '-buildTarget', 'Android', '-executeMethod', 'Game.Editor.BuildAndroid.BuildDebugApk',
    '-outputPath', $apk, '-logFile', $log)

if ($code -ne 0 -or -not (Test-Path $apk)) {
    Write-Host "BUILD FALHOU (código $code). Log: $log" -ForegroundColor Red
    Select-String -Path $log -Pattern 'error|Exception' -SimpleMatch:$false | Select-Object -Last 20 | ForEach-Object { $_.Line }
    exit 1
}
$sizeMb = [math]::Round((Get-Item $apk).Length / 1MB, 1)
Write-Host "APK gerado: $apk ($sizeMb MB)" -ForegroundColor Green

if ($Install) {
    $adb = Get-AdbPath
    $devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\tdevice$' }
    if (-not $devices) {
        Write-Host 'Nenhum celular autorizado. Ative a Depuração USB e aceite o aviso "Permitir depuração USB" no aparelho.' -ForegroundColor Red
        exit 2
    }
    & $adb install -r $apk
    if ($LASTEXITCODE -ne 0) { Write-Host 'Falha ao instalar.' -ForegroundColor Red; exit 3 }
    & $adb shell monkey -p com.fazendamercadinho.tycoon -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host 'Instalado e aberto no celular.' -ForegroundColor Green
}
