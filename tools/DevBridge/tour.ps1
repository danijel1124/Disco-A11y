# Visits one area and photographs it, for writing area descriptions from what is
# actually there rather than from memory of the game.
#
# Two-step on purpose: TeleportTo needs a spawn marker that exists in the target scene,
# and the marker names are only knowable once the scene is loaded. So we teleport in with
# any marker (which loads the scene but leaves the player standing in the void outside the
# level), ask the now-loaded scene for its arrival locations, and teleport again properly.
#
#   .\tour.ps1 Whirling-int-f1
param(
    [Parameter(Mandatory = $true)][string]$Scene,
    [string]$OutDir = "$env:TEMP\tour",
    [int]$LoadWait = 22
)

$ErrorActionPreference = 'Stop'
$client = Join-Path $PSScriptRoot 'bridge-client.ps1'
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Send([string]$cmd) { & $client $cmd }

Write-Host "== $Scene"

Send "goto $Scene entry" | Out-Null
Start-Sleep -Seconds $LoadWait

$state = Send 'state'
$loaded = ($state | Select-String '^scene: (.+)$').Matches.Groups[1].Value.Trim()
if ($loaded -ne $Scene) {
    Write-Host "FAILED: scene did not load (still in $loaded)"
    exit 1
}

# Markers of the scene we just loaded. Markers that report no area belong to whatever
# scene was open before - using one of those teleports the player into the void outside
# the level, which looks exactly like a broken area and is how the first pass lied to us.
$markers = @()
$unowned = @()
foreach ($line in (Send 'destinations')) {
    if ($line -match '^Arrival Location (.+?)\s+\(area: (.*)\)$') {
        if ($Matches[2] -eq $Scene) { $markers += $Matches[1] }
        elseif ($Matches[2] -eq '') { $unowned += $Matches[1] }
    }
}
if ($markers.Count -eq 0 -and $unowned.Count -gt 0) {
    Write-Host "no markers own this scene; falling back to unowned: $($unowned -join ', ')"
    $markers = $unowned
}

if ($markers.Count -eq 0) {
    Write-Host "no arrival markers found - the player stays wherever the teleport dropped them"
} else {
    # "main" is the game's own name for an area's front door where it exists.
    $marker = if ($markers -contains 'main') { 'main' } else { $markers[0] }
    Write-Host "markers: $($markers -join ', ')  -> using '$marker'"
    Send "goto $Scene $marker" | Out-Null
    Start-Sleep -Seconds $LoadWait
}

$shot = Join-Path $OutDir "$Scene.png"
Send "screenshot $shot" | Out-Null
Start-Sleep -Seconds 4

Send 'state'
Send 'objects 10'
Write-Host "screenshot: $shot"
