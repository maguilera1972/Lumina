param(
    [string]$Message = "chore: sync Lumina to GitLab"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$tokenFile = Join-Path $env:USERPROFILE ".config\gitlab\personal_access_token"
if (-not $env:GITLAB_TOKEN -and (Test-Path $tokenFile)) {
    $env:GITLAB_TOKEN = (Get-Content $tokenFile -Raw).Trim()
}

git add -A
$pending = git status --porcelain
if ($pending) {
    git commit -m $Message
}

$remote = git remote get-url origin 2>$null
if (-not $remote) {
    Write-Host "Pas de remote origin. Ajoutez https://gitlab.aguilera.ch/Aguilera_Lab/laser.git puis relancez."
    exit 1
}

git push origin HEAD
Write-Host "Aguilera_Lab/laser mis a jour sur https://gitlab.aguilera.ch"
