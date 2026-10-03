# Art iteration: rebuild the generated world (kit, island, dressing) and capture review screenshots.
# Usage: powershell -File Tools\art-shots.ps1 [-Quality lean|balanced|rich]   Output: Screenshots\*.png, log in Logs\art.log
param([string]$Quality = "balanced")
$env:GB_QUALITY = $Quality
$root = Split-Path $PSScriptRoot -Parent
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
$log = Join-Path $root "Logs\art.log"
$shots = Join-Path $root "Screenshots"
if (Test-Path $shots) { Remove-Item $shots -Recurse }
$p = Start-Process $unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$root`"", '-executeMethod',
    'Gamebreak.MiniGolf.Editor.Automation.SetupAndCapture', '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(900000)) { Write-Output "TIMEOUT"; exit 1 }
Select-String -Path $log -Pattern "error CS|Shader error|Exception|\[Gamebreak\] (Built|Screenshots)" | Select-Object -Unique -First 20 | ForEach-Object { $_.Line }
Write-Output "exit $($p.ExitCode); $((Get-ChildItem $shots -ErrorAction SilentlyContinue).Count) screenshots"
