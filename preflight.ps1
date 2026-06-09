<#
.SYNOPSIS
  Pré-vol démo PoC Telemetry : démarre les deux stacks et vérifie que tout répond.

.EXAMPLE
  .\preflight.ps1            # démarre (sans rebuild) et vérifie
  .\preflight.ps1 -Build     # force le rebuild des images applicatives (1er run)
#>
param(
  [switch]$Build
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Say($msg, $color = 'White') { Write-Host $msg -ForegroundColor $color }
function Title($msg) { Write-Host ""; Write-Host "==== $msg ====" -ForegroundColor Cyan }

# Renvoie le code HTTP (0 si injoignable)
function Get-HttpCode($url, $timeout = 5) {
  try {
    $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec $timeout -ErrorAction Stop
    return [int]$r.StatusCode
  } catch {
    if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
    return 0
  }
}

# Attend qu'une URL réponde 200 (ou un code accepté), avec réessais
function Wait-Url($name, $url, $retries = 20, $delay = 3, $accept = @(200)) {
  Write-Host ("{0,-16} " -f $name) -NoNewline
  for ($i = 1; $i -le $retries; $i++) {
    $code = Get-HttpCode $url
    if ($accept -contains $code) {
      Say "OK ($code)" Green
      return $true
    }
    Write-Host "." -NoNewline
    Start-Sleep -Seconds $delay
  }
  $final = Get-HttpCode $url
  Say "ECHEC (dernier code: $final)" Red
  return $false
}

# ---------------------------------------------------------------------------
Title "1. Docker en marche ?"
try {
  docker info *> $null
  Say "Docker OK" Green
} catch {
  Say "Docker ne répond pas. Démarre Docker Desktop puis relance ce script." Red
  exit 1
}

Title "2. Réseau 'observability'"
$net = docker network ls --format '{{.Name}}' | Where-Object { $_ -eq 'observability' }
if (-not $net) {
  docker network create observability | Out-Null
  Say "Réseau créé." Green
} else {
  Say "Réseau déjà présent." Green
}

Title "3. Webhook Discord (.env)"
if (Test-Path 'observability/.env') {
  Say "observability/.env présent — notifications Discord actives." Green
} else {
  Say "ATTENTION : observability/.env absent." Yellow
  Say "  -> cp observability/.env.example observability/.env, puis colle DISCORD_WEBHOOK_URL." Yellow
  Say "  -> Sans lui, les alertes ne partiront PAS dans Discord (le reste fonctionne)." Yellow
}

Title "4. Démarrage des stacks"
Say "Observability stack..." Gray
docker compose -f observability/docker-compose.yml up -d | Out-Null
Say "App stack$(if ($Build) {' (rebuild)'})..." Gray
if ($Build) { docker compose up --build -d | Out-Null } else { docker compose up -d | Out-Null }
Say "Conteneurs lancés." Green

Title "5. Vérification des services (peut prendre ~1 min au démarrage)"
$ok = $true
$ok = (Wait-Url 'Grafana'     'http://localhost:3000/api/health') -and $ok
$ok = (Wait-Url 'API'         'http://localhost:5000/api/products') -and $ok
$ok = (Wait-Url 'demo-python' 'http://localhost:5001/health') -and $ok

# Front : gère le piège 502 (nginx garde l'IP de l'api en cache au démarrage)
Write-Host ("{0,-16} " -f 'Front (4200)') -NoNewline
$code = Get-HttpCode 'http://localhost:4200/api/products' 8
if ($code -ne 200) {
  Say "code $code -> redémarrage du front (fix 502)..." Yellow
  docker restart poc-telemetry-web-1 | Out-Null
  Start-Sleep -Seconds 5
  $code = Get-HttpCode 'http://localhost:4200/api/products' 8
}
if ($code -eq 200) { Say "OK (200)" Green } else { Say "ECHEC ($code)" Red; $ok = $false }

# Infra (non bloquant pour la démo)
Title "6. Infra observabilité (info)"
Wait-Url 'Mimir'   'http://localhost:9009/ready' 10 2 @(200) | Out-Null
Wait-Url 'Loki'    'http://localhost:3100/ready' 10 2 @(200) | Out-Null
Wait-Url 'Tempo'   'http://localhost:3200/ready' 10 2 @(200) | Out-Null
Wait-Url 'RabbitMQ' 'http://localhost:15672' 10 2 @(200) | Out-Null

# ---------------------------------------------------------------------------
Title "Résumé"
Say "Grafana       http://localhost:3000   (anonymous Admin)" Gray
Say "App Angular   http://localhost:4200" Gray
Say "API           http://localhost:5000/api/products" Gray
Say "demo-python   http://localhost:5001" Gray
Say "RabbitMQ UI   http://localhost:15672  (guest/guest)" Gray

Write-Host ""
if ($ok) {
  Say "PRÊT POUR LA DÉMO. Pense à pré-armer l'alerte ~3 min avant (voir docs/demo-script.md)." Green
} else {
  Say "Certains services ne répondent pas — attends quelques secondes et relance, ou vérifie 'docker ps'." Red
}
