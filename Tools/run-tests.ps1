<#
.SYNOPSIS
  Roda os testes do projeto em -batchmode.
.EXAMPLE
  .\Tools\run-tests.ps1                 # Core (dotnet, se instalado) + EditMode + PlayMode
  .\Tools\run-tests.ps1 -Platform EditMode
  .\Tools\run-tests.ps1 -SkipDotnet
.NOTES
  Feche o projeto no Editor antes (o Unity não abre o mesmo projeto duas vezes).
  Resultados: TestResults\<Plataforma>.xml e .log. Sai com código != 0 se algo falhar.
#>
param(
    [ValidateSet('EditMode', 'PlayMode', 'All')]
    [string]$Platform = 'All',
    [switch]$SkipDotnet
)
. "$PSScriptRoot\UnityCommon.ps1"

$resultsDir = Join-Path $ProjectRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
$failed = $false

if (-not $SkipDotnet) {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        Write-Host "== Core (dotnet)" -ForegroundColor Cyan
        dotnet test (Join-Path $ProjectRoot 'Tools\CoreTests\CoreTests.csproj') --nologo
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    } else {
        Write-Host "dotnet não instalado: pulando o espelho Core (os mesmos testes rodam no EditMode)." -ForegroundColor DarkYellow
    }
}

$platforms = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }
foreach ($p in $platforms) {
    Write-Host "== Unity $p" -ForegroundColor Cyan
    $xml = Join-Path $resultsDir "$p.xml"
    $log = Join-Path $resultsDir "$p.log"
    Remove-Item $xml -ErrorAction SilentlyContinue

    # Obs.: -runTests NÃO pode ser usado com -quit.
    $code = Invoke-Unity @('-batchmode', '-nographics', '-projectPath', $ProjectRoot,
        '-runTests', '-testPlatform', $p, '-testResults', $xml, '-logFile', $log)

    if (-not (Test-Path $xml)) {
        Write-Host "  Sem arquivo de resultado (código $code). Veja o log: $log" -ForegroundColor Red
        $failed = $true
        continue
    }
    [xml]$doc = Get-Content $xml
    $run = $doc.'test-run'
    $color = if ([int]$run.failed -gt 0) { 'Red' } else { 'Green' }
    Write-Host ("  total {0} | passou {1} | falhou {2} | pulou {3}" -f $run.total, $run.passed, $run.failed, $run.skipped) -ForegroundColor $color
    if ([int]$run.failed -gt 0) {
        $doc.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
            Write-Host "   x $($_.fullname)" -ForegroundColor Red
            $msg = $_.SelectSingleNode('failure/message')
            if ($msg) { Write-Host "     $($msg.InnerText)" -ForegroundColor DarkRed }
        }
    }
    if ($code -ne 0 -or [int]$run.failed -gt 0) { $failed = $true }
}

if ($failed) { Write-Host "TESTES FALHARAM" -ForegroundColor Red; exit 1 }
Write-Host "TODOS OS TESTES PASSARAM" -ForegroundColor Green
exit 0
