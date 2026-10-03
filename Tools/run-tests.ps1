# Runs the PlayMode test suite in batch mode and prints a one-line-per-test summary.
# Usage: powershell -File Tools\run-tests.ps1 [-Filter <name>] [-Category <cat>]
param([string]$Filter = "", [string]$Category = "")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
$results = Join-Path $root "Logs\playmode-results.xml"
$log = Join-Path $root "Logs\tests.log"
New-Item -ItemType Directory -Force (Join-Path $root "Logs") | Out-Null
if (Test-Path $results) { Remove-Item $results }
$args = @('-batchmode', '-projectPath', "`"$root`"", '-runTests', '-testPlatform', 'PlayMode',
          '-testResults', "`"$results`"", '-logFile', "`"$log`"")
if ($Filter) { $args += @('-testFilter', $Filter) }
if ($Category) { $args += @('-testCategory', $Category) }
$p = Start-Process $unity -ArgumentList $args -PassThru -Wait
Select-String -Path $log -Pattern "error CS" | ForEach-Object { $_.Line }
if (-not (Test-Path $results)) { Write-Output "No results (exit $($p.ExitCode)). See $log"; exit 1 }
Select-String -Path $log -Pattern "^\[Test\]" | ForEach-Object { $_.Line }
[xml]$x = Get-Content $results
$x.SelectNodes("//test-case") | ForEach-Object {
    $msg = if ($_.failure) { " :: " + ($_.failure.message.'#cdata-section' -replace '\s+', ' ') } else { "" }
    "{0,-7} {1}{2}" -f $_.result, $_.name, $msg
}
$run = $x.SelectSingleNode("//test-run")
"TOTAL {0} passed, {1} failed (exit {2})" -f $run.passed, $run.failed, $p.ExitCode
