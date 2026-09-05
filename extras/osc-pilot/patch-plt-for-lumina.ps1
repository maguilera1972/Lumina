# Patch OSC/PILOT .plt for Lumina Live
#
# Usage:
#   1. Sur l'iPad : partagez / exportez votre projet .plt vers ce dossier
#      (ou créez un projet minimal sur OSC/PILOT desktop et enregistrez-le ici)
#   2. Placez-le sous le nom : input.plt   (dans ce même dossier)
#   3. Exécutez :
#        powershell -ExecutionPolicy Bypass -File .\patch-plt-for-lumina.ps1
#   4. Ouvrez Lumina-Live.plt dans OSC/PILOT (iPad upload / Files)
#
# Le script remplace les adresses OSC des boutons/pads par /lumina/pad/N
# et configure le socket A : Send To Port = 9000 (Host à régler = IP du PC).

param(
    [string]$InputFile = (Join-Path $PSScriptRoot "input.plt"),
    [string]$OutputFile = (Join-Path $PSScriptRoot "Lumina-Live.plt"),
    [int]$PadCount = 16,
    [int]$OscPort = 9000
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputFile)) {
    Write-Host @"

Fichier introuvable : $InputFile

Comment obtenir un .plt à patcher :
  • iPad OSC/PILOT → menu ☰ → partagez / exportez le projet vers Fichiers
  • Copiez le .plt dans :
      $PSScriptRoot\input.plt
  • Relancez ce script

Sans modèle .plt d'OSC/PILOT, on ne peut pas générer un fichier valide
(format propriétaire avec IDs numériques non documentés).

"@
    exit 1
}

$raw = [System.IO.File]::ReadAllText($InputFile)
$json = $null
try {
    $json = $raw | ConvertFrom-Json -Depth 100
} catch {
    Write-Host "Le fichier n'est pas du JSON lisible. OSC/PILOT .plt devrait être du JSON."
    throw
}

# Deep walk: replace string values that look like OSC addresses with Lumina pads.
$script:padIndex = 0
$script:replacements = 0

function Walk($node) {
    if ($null -eq $node) { return }
    if ($node -is [System.Collections.IEnumerable] -and $node -isnot [string] -and $node -isnot [System.Collections.IDictionary]) {
        foreach ($item in @($node)) { Walk $item }
        return
    }
    # PSCustomObject / hashtable
    $props = $null
    try { $props = $node.PSObject.Properties } catch { return }
    foreach ($p in @($props)) {
        $v = $p.Value
        if ($v -is [string]) {
            $s = $v
            # OSC address fields
            if ($s -match '^/' -and $s -notmatch '^/lumina/' -and $s -notmatch '^/Workspace' -and $s -notmatch '^/OSCpilot') {
                if ($script:padIndex -lt $PadCount) {
                    $script:padIndex++
                    $p.Value = "/lumina/pad/$($script:padIndex)"
                    $script:replacements++
                }
            }
            # Common On/Off value fields sometimes stored as strings
            elseif ($s -eq '' -and $p.Name -match 'On|off|OSC') {
                # leave
            }
        }
        elseif ($v -is [int] -or $v -is [long] -or $v -is [double]) {
            # Heuristic: listen/send ports often 8000/8001/9000 range stored as numbers named vaguely
            if ($p.Name -match '(?i)send|port' -and $v -ge 1024 -and $v -le 65535) {
                if ($p.Name -match '(?i)send|out|to') {
                    $p.Value = $OscPort
                    $script:replacements++
                }
            }
        }
        else {
            Walk $v
        }
    }
}

Walk $json

# Also do text-level fixes for port patterns and insert project-specific OSC hint
$text = $json | ConvertTo-Json -Depth 100 -Compress:$false

# Force common port literals seen in OSC/PILOT projects
$text = [regex]::Replace($text, '("SendToPort"\s*:\s*)\d+', "`${1}$OscPort")
$text = [regex]::Replace($text, '("sendToPort"\s*:\s*)\d+', "`${1}$OscPort")
$text = [regex]::Replace($text, '("Send To Port"\s*:\s*)\d+', "`${1}$OscPort")

[System.IO.File]::WriteAllText($OutputFile, $text, [System.Text.UTF8Encoding]::new($false))

Write-Host "Écrit : $OutputFile"
Write-Host "Remplacements heuristiques : $($script:replacements) (pads assignés : $($script:padIndex))"
Write-Host ""
Write-Host "Dans OSC/PILOT, vérifiez socket A :"
Write-Host "  Host         = IP du PC (affichée dans Lumina Live)"
Write-Host "  Send To Port = $OscPort"
Write-Host "Dans Lumina Live : démarrer OSC UDP sur le port $OscPort"
