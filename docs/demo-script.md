# Script de démo live — PoC Telemetry

Runbook pour la démo (~3 min) de la présentation. Public mixte, format court.
**Idée maîtresse :** montrer la corrélation _en un clic_ et une **alerte Discord réelle**.

---

## 0. Pré-vol (la veille / 10 min avant)

```powershell
# Stack observabilité (une fois)
docker compose -f observability/docker-compose.yml up -d
# Projet applicatif
docker compose -f docker-compose.yml up -d
```

Vérifier que tout répond :

| Service | URL | Attendu |
|---|---|---|
| Grafana | http://localhost:3000 | login anonyme (Admin) |
| App Angular | http://localhost:4200 | l'app de produits |
| API | http://localhost:5000/api/products | JSON |
| demo-python | http://localhost:5001/ | liste des endpoints |
| Discord | ton salon | webhook branché |

Ouvrir d'avance, dans des onglets :
1. Grafana → dashboard **Error Traces** (`/d/error-traces`)
2. Grafana → dashboard **Traces Overview**
3. Grafana → **Observability Overview** (sélecteur `$project`)
4. Le salon **Discord**
5. Un terminal PowerShell

> ⚠️ **Onglet Grafana en navigation privée** si tu projettes — évite d'exposer d'autres onglets.

> 🛑 **Piège 502 Bad Gateway sur localhost:4200** : le front `web` (nginx) met en cache l'IP
> de l'`api` à son démarrage. Si tu as recréé/rebuild l'`api`, nginx tape l'ancienne IP → 502.
> **Réflexe :** redémarrer le front après toute recréation de l'api —
> `docker restart poc-telemetry-web-1` — puis vérifier `curl http://localhost:4200/api/products`.

---

## 1. Pré-armer l'alerte (⏱ à lancer ~3 min AVANT d'arriver au slide démo)

La règle *Taux d'erreur* attend `for: 2m` + un cycle de scrape. Pour que le 🔴 **FIRING**
tombe **pendant** la démo, lance le générateur d'erreurs dès le début de la présentation
(slide « Pourquoi » par ex.) :

```powershell
# Génère des erreurs en continu sur demo-python (laisser tourner)
while ($true) {
  curl.exe -s -o $null http://localhost:5001/errors/throw
  curl.exe -s -o $null http://localhost:5001/errors/badrequest
  Start-Sleep -Milliseconds 400
}
```

→ Le taux d'erreur monte ; l'alerte passe *pending* puis *firing* ~2,5 min plus tard,
et le message part dans Discord. **Ne ferme pas ce terminal** avant le moment « alerte ».

---

## 2. Déroulé de la démo (~3 min)

### a) Trafic « sain » (30 s)
- Sur **l'app Angular** (http://localhost:4200), créer / lister quelques produits.
- _« Chaque action génère des traces, des logs et des métriques, automatiquement. »_

### b) Traces & service graph (40 s)
- Onglet **Traces Overview**.
- Montrer le **débit par service**, la **heatmap de latence**, et le **service graph**
  (les dépendances dessinées à partir des traces).
- _« On voit le système réel, pas un schéma théorique. »_

### c) Drill-down sur une erreur — LE moment fort (50 s)
- Onglet **Error Traces**.
- Pointer le **taux d'erreur** qui grimpe (effet du pré-armement).
- Cliquer une ligne de la table **Traces with errors** → ouvre la **waterfall** de la trace.
- Depuis un span → **Logs for this span** (saut Tempo → Loki) → montrer le log corrélé.
- _« D'un graphe à la trace aux logs exacts, en deux clics. »_

### d) Multi-projets (20 s)
- Onglet **Observability Overview**, dérouler `$project` : **poc-telemetry** vs **demo-python**.
- _« Une seule stack, plusieurs projets isolés par une étiquette. »_

### e) L'alerte Discord (40 s) — le clou
- Basculer sur **Discord** : le message 🔴 **FIRING « Taux d'erreur élevé »** doit être là.
- _« L'observabilité ne fait pas que regarder — elle prévient. »_
- **Couper le générateur d'erreurs** (Ctrl+C dans le terminal de l'étape 1).
- Optionnel si le temps le permet : montrer le ✅ **RESOLVED** qui arrive ~1-2 min après.

---

## 3. Variante « Service down » (si tu préfères une alerte déterministe)

Plus rapide et sans attendre `for: 2m`. Geler un service le rend « down » proprement :

```powershell
docker pause poc-telemetry-worker-1     # ~1-2 min plus tard : 🔴 FIRING (critical)
# ... montrer Discord ...
docker unpause poc-telemetry-worker-1   # ~1-2 min plus tard : ✅ RESOLVED
```

> `docker stop` ne marche PAS pour la démo : le conteneur disparaît de la découverte →
> la série `up` devient absente (pas `0`) → pas d'alerte. **`pause`** garde la cible visible.

---

## 4. Plan B (si le live échoue)

- **Pas de réseau / Grafana lent** → captures d'écran préparées des dashboards + du salon Discord.
- **Alerte pas encore firing** → montrer les messages Discord **déjà reçus** lors des tests
  (scroller l'historique du salon), et expliquer le cycle FIRING/RESOLVED.
- **Trace introuvable** → utiliser le lien direct du dashboard Error Traces (déjà filtré).

---

## 5. Après la démo / reset

```powershell
# Arrêter tout générateur d'erreurs encore en cours (Ctrl+C),
# s'assurer qu'aucun service n'est resté en pause :
docker unpause poc-telemetry-worker-1 2>$null

# (Optionnel) tout arrêter
# docker compose -f docker-compose.yml down
# docker compose -f observability/docker-compose.yml down
```

---

## Aide-mémoire timing (présentation ~10 min)

| Bloc | Cible |
|---|---|
| Titre + agenda | 30 s |
| Pourquoi | 1 min  ⟵ _lancer le générateur d'erreurs ici_ |
| Architecture + stack | 1 min 40 |
| 3 piliers + corrélation | 1 min 45 |
| Multi-projets | 45 s |
| Alerting (2 slides) | 1 min 15 |
| **Démo live** | **3 min**  ⟵ _l'alerte doit être firing_ |
| Bilan + merci | 45 s |

Annexe (Difficultés, scrape multi-réseaux) = **pour le Q&A uniquement**.
