# Runs a Blender script headless and waits for it (the Store launcher detaches, and Blender's stdout does not reach the shell, so scripts log to
# Logs\blender_arch.log). Usage: powershell -File Tools\blender-run.ps1 Tools\Blender\hero_temple.py [-- piece ...]
param([Parameter(Mandatory = $true)][string]$Script, [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest)
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
New-Item -ItemType Directory -Force Logs | Out-Null
$log = Join-Path $root "Logs\blender_arch.log"
if (Test-Path $log) { Remove-Item $log }
$launcher = "$env:LOCALAPPDATA\Microsoft\WindowsApps\BlenderFoundation.Blender_ppwjx1n5r4v9t\blender-launcher.exe"
$argList = @('-b', '--factory-startup', '--python', $Script)
if ($Rest) { $argList += '--'; $argList += ($Rest | Where-Object { $_ -ne '--' }) }
& $launcher @argList | Out-Null
Start-Sleep 3
$deadline = (Get-Date).AddMinutes(25)
while ((Get-Process blender -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep 2 }
if (Test-Path $log) { Get-Content $log } else { "no log written" }
