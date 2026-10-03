# Runs the desktop debug build in self-test mode: scripted putter swing on hole 1, screenshots, report.
# Usage: powershell -File Tools\smoke-test.ps1   (build first with Automation.SetupAndBuildAll)
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "Builds\Desktop\WorldsOfMiniGolf_Desktop.exe"
$out = Join-Path $root "Builds\Desktop\SmokeTest"
if (-not (Test-Path $exe)) { Write-Output "Missing $exe. Build it first."; exit 1 }
if (Test-Path $out) { Remove-Item $out -Recurse }
$p = Start-Process $exe -ArgumentList '-smoketest', '-screen-width', '1600', '-screen-height', '900', '-screen-fullscreen', '0' -PassThru
if (-not $p.WaitForExit(120000)) { $p.Kill(); Write-Output "TIMEOUT"; exit 1 }
Get-Content (Join-Path $out "report.txt")
Write-Output "Screenshots: $out"
exit $p.ExitCode
