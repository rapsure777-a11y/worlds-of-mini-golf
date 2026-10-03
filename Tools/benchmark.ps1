# GPU benchmark of the built desktop player (offscreen 4320x2160, fixed Hole 1 viewpoints).
# Usage: powershell -File Tools\benchmark.ps1 [-Label before]   Output: Builds\Desktop\Benchmark\benchmark.txt
param([string]$Label = "")
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "Builds\Desktop\WorldsOfMiniGolf_Desktop.exe"
$out = Join-Path $root "Builds\Desktop\Benchmark"
if (Test-Path $out) { Remove-Item $out -Recurse }
$p = Start-Process $exe -ArgumentList '-benchmark', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0' -PassThru
if (-not $p.WaitForExit(240000)) { $p.Kill(); Write-Output "TIMEOUT"; exit 1 }
$text = Get-Content (Join-Path $out "benchmark.txt")
$text
if ($Label) {
    New-Item -ItemType Directory -Force (Join-Path $root "Docs\perf") | Out-Null
    $text | Set-Content (Join-Path $root "Docs\perf\benchmark_$Label.txt") -Encoding utf8
}
