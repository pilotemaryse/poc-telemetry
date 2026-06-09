#!/usr/bin/env bash
# Pré-vol démo PoC Telemetry : démarre les deux stacks et vérifie que tout répond.
#   ./preflight.sh          # démarre (sans rebuild) et vérifie
#   ./preflight.sh --build  # force le rebuild des images applicatives (1er run)
set -uo pipefail
cd "$(dirname "$0")"

GREEN='\033[0;32m'; RED='\033[0;31m'; YEL='\033[1;33m'; CYAN='\033[0;36m'; GRAY='\033[0;90m'; NC='\033[0m'
title() { echo -e "\n${CYAN}==== $1 ====${NC}"; }

BUILD=0
[ "${1:-}" = "--build" ] && BUILD=1

http_code() { curl -s -o /dev/null -w '%{http_code}' --max-time "${2:-5}" "$1" 2>/dev/null || echo 000; }

wait_url() { # name url [retries] [delay] [accept]
  local name="$1" url="$2" retries="${3:-20}" delay="${4:-3}" accept="${5:-200}"
  printf '%-16s ' "$name"
  for _ in $(seq 1 "$retries"); do
    local code; code=$(http_code "$url")
    if [ "$code" = "$accept" ]; then echo -e "${GREEN}OK ($code)${NC}"; return 0; fi
    printf '.'; sleep "$delay"
  done
  echo -e "${RED}ECHEC (dernier code: $(http_code "$url"))${NC}"; return 1
}

title "1. Docker en marche ?"
if docker info >/dev/null 2>&1; then echo -e "${GREEN}Docker OK${NC}"; else
  echo -e "${RED}Docker ne répond pas. Démarre Docker puis relance.${NC}"; exit 1; fi

title "2. Réseau 'observability'"
if docker network ls --format '{{.Name}}' | grep -qx observability; then
  echo -e "${GREEN}Réseau déjà présent.${NC}"
else docker network create observability >/dev/null && echo -e "${GREEN}Réseau créé.${NC}"; fi

title "3. Webhook Discord (.env)"
if [ -f observability/.env ]; then
  echo -e "${GREEN}observability/.env présent — notifications Discord actives.${NC}"
else
  echo -e "${YEL}ATTENTION : observability/.env absent.${NC}"
  echo -e "${YEL}  -> cp observability/.env.example observability/.env, puis colle DISCORD_WEBHOOK_URL.${NC}"
  echo -e "${YEL}  -> Sans lui, les alertes ne partiront PAS dans Discord (le reste fonctionne).${NC}"
fi

title "4. Démarrage des stacks"
echo -e "${GRAY}Observability stack...${NC}"
docker compose -f observability/docker-compose.yml up -d >/dev/null
echo -e "${GRAY}App stack$([ $BUILD -eq 1 ] && echo ' (rebuild)')...${NC}"
if [ $BUILD -eq 1 ]; then docker compose up --build -d >/dev/null; else docker compose up -d >/dev/null; fi
echo -e "${GREEN}Conteneurs lancés.${NC}"

title "5. Vérification des services (peut prendre ~1 min)"
OK=0
wait_url 'Grafana'     'http://localhost:3000/api/health' || OK=1
wait_url 'API'         'http://localhost:5000/api/products' || OK=1
wait_url 'demo-python' 'http://localhost:5001/health' || OK=1

# Front : gère le piège 502 (nginx garde l'IP de l'api en cache au démarrage)
printf '%-16s ' 'Front (4200)'
code=$(http_code 'http://localhost:4200/api/products' 8)
if [ "$code" != "200" ]; then
  echo -e "${YEL}code $code -> redémarrage du front (fix 502)...${NC}"
  docker restart poc-telemetry-web-1 >/dev/null; sleep 5
  code=$(http_code 'http://localhost:4200/api/products' 8)
  printf '%-16s ' 'Front (4200)'
fi
if [ "$code" = "200" ]; then echo -e "${GREEN}OK (200)${NC}"; else echo -e "${RED}ECHEC ($code)${NC}"; OK=1; fi

title "6. Infra observabilité (info)"
wait_url 'Mimir'    'http://localhost:9009/ready' 10 2 || true
wait_url 'Loki'     'http://localhost:3100/ready' 10 2 || true
wait_url 'Tempo'    'http://localhost:3200/ready' 10 2 || true
wait_url 'RabbitMQ' 'http://localhost:15672'      10 2 || true

title "Résumé"
echo -e "${GRAY}Grafana       http://localhost:3000   (anonymous Admin)"
echo -e "App Angular   http://localhost:4200"
echo -e "API           http://localhost:5000/api/products"
echo -e "demo-python   http://localhost:5001"
echo -e "RabbitMQ UI   http://localhost:15672  (guest/guest)${NC}"
echo ""
if [ $OK -eq 0 ]; then
  echo -e "${GREEN}PRÊT POUR LA DÉMO. Pense à pré-armer l'alerte ~3 min avant (voir docs/demo-script.md).${NC}"
else
  echo -e "${RED}Certains services ne répondent pas — attends quelques secondes et relance, ou vérifie 'docker ps'.${NC}"
fi
