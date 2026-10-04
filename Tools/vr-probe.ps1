# VR frame-cost probe: runs the PCVR build with -probe, waits for the report and copies it to Docs\perf.
# Usage: powershell -File Tools\vr-probe.ps1 [-Quality rich] [-Label rich90]
# Put the headset on, stand still at the tee looking down the lane, and set SteamVR to the refresh rate under test (90 Hz by default target).
# This starts the PCVR player and therefore appears in the headset: run it only when Andrew is ready.
param([string]$Quality = "rich", [string]$Label = "")
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "Builds\Windows\WorldsOfMiniGolf.exe"
$sessions = Join-Path $env:USERPROFILE "AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf\Sessions"
$before = @(Get-ChildItem $sessions -Filter "probe_*.txt" -ErrorAction SilentlyContinue | ForEach-Object Name)
$p = Start-Process $exe -ArgumentList '-probe', '-quality', $Quality -PassThru
$deadline = (Get-Date).AddMinutes(8)
$report = $null
while ((Get-Date) -lt $deadline -and -not $report) {
    Start-Sleep 5
    $report = Get-ChildItem $sessions -Filter "probe_*.txt" -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Name } | Select-Object -First 1
}
if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep 3; if (-not $p.HasExited) { $p.Kill() } }
if (-not $report) { Write-Output "No probe report produced"; exit 1 }
$text = Get-Content $report.FullName
$text
if ($Label) {
    New-Item -ItemType Directory -Force (Join-Path $root "Docs\perf") | Out-Null
    $text | Set-Content (Join-Path $root "Docs\perf\probe_$Label.txt") -Encoding utf8
}
