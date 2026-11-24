# Funções comuns aos scripts de Tools/. Importar com:  . "$PSScriptRoot\UnityCommon.ps1"
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-ProjectUnityVersion {
    $file = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    $line = Get-Content $file | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
    if (-not $line) { throw "Não achei m_EditorVersion em $file" }
    return ($line -replace '^m_EditorVersion:\s*', '').Trim()
}

# Ordem de busca: $env:UNITY_PATH -> versão exata no Unity Hub -> a mais nova 6000.3.x instalada.
function Get-UnityPath {
    if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) { return $env:UNITY_PATH }

    $hubRoots = @("$env:ProgramFiles\Unity\Hub\Editor", "${env:ProgramFiles(x86)}\Unity\Hub\Editor", 'C:\Unity\Hub\Editor') |
        Where-Object { $_ -and (Test-Path $_) }

    $version = Get-ProjectUnityVersion
    foreach ($root in $hubRoots) {
        $exact = Join-Path $root "$version\Editor\Unity.exe"
        if (Test-Path $exact) { return $exact }
    }
    foreach ($root in $hubRoots) {
        $candidate = Get-ChildItem $root -Directory -Filter '6000.3.*' -ErrorAction SilentlyContinue |
            Sort-Object { [version](($_.Name -replace '[a-z].*$', '')) } -Descending |
            Select-Object -First 1
        if ($candidate) {
            $exe = Join-Path $candidate.FullName 'Editor\Unity.exe'
            if (Test-Path $exe) {
                Write-Warning "Unity $version não encontrado; usando $($candidate.Name)."
                return $exe
            }
        }
    }
    throw "Unity 6.3 não encontrado. Instale pelo Unity Hub ou defina `$env:UNITY_PATH com o caminho do Unity.exe."
}

# Roda o Unity e espera terminar (Unity.exe é app de janela; '&' não espera). Retorna o exit code.
function Invoke-Unity([string[]]$Arguments) {
    $unity = Get-UnityPath
    $quoted = $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    Write-Host ">> $unity $($quoted -join ' ')" -ForegroundColor DarkGray
    $proc = Start-Process -FilePath $unity -ArgumentList ($quoted -join ' ') -Wait -PassThru -NoNewWindow
    return $proc.ExitCode
}

function Get-AdbPath {
    $unity = Get-UnityPath
    $adb = Join-Path (Split-Path $unity) 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
    if (Test-Path $adb) { return $adb }
    $fromPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    throw 'adb não encontrado. Instale o módulo "Android Build Support" (com Android SDK) no Unity Hub.'
}
