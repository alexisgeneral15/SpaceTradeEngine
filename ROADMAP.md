# Development Roadmap

## Phase 1: Foundation (Weeks 1-2) ✅ DONE
## Roadmap v0.1 (Q1 2026) – MVP Playable Loop

Goal: deliver a performant, single-player space trading loop with dynamic economy, basic faction diplomacy, tactical combat, and clean UX — better than UnendingGalaxyDeluxe through deeper simulation and lower memory.

### Sprint 1 (Weeks 1–2): Economy V1 + Navigation
- Deliverables:
	- Wares, stations, inventories; dynamic prices (supply/demand, shocks).
	- Contracts (delivery, buy/sell), cargo management.
	- Sector graph, gates, pathfinding; map HUD basics.
- Acceptance:
	- Complete trade mission loop (accept → deliver → payout → rep change).
	- Prices react to supply shocks within 5 minutes simulated.
	- 60 FPS at 1K active entities; memory < 350MB.
- Risks/Deps: data schemas; validator tooling; pathfinding perf.

### Sprint 2 (Weeks 3–4): Factions V1 + Events
- Deliverables:
	- Faction attitudes, treaties (ceasefire, embargo), territory markers.
	- Event hooks (shortage, war, raid) with propagation to markets.
	- Reputation changes from missions and combat outcomes.
- Acceptance:
	- Embargo affects station buy/sell lists within 2 minutes.
	- Event feed/log with clear state changes; save/load durable.
	- 60 FPS at 1.5K entities; memory < 375MB.
- Risks/Deps: diplomacy rules clarity; serialization stability.

### Sprint 3 (Weeks 5–6): Combat V1 + AI Basics
- Deliverables:
	- Weapon slots, damage/armor, salvage rewards.
	- Simple ship AI (attack, flee, patrol) via Behavior Trees.
	- Selection + minimal command UI (attack, move).
- Acceptance:
	- 10v10 skirmish at stable 60 FPS; no stalls > 25ms.
	- Salvage yields wares; factions react to kills (rep).
	- Memory steady-state < 400MB.
- Risks/Deps: ECS update order; culling; LOD decisions.

### Sprint 4 (Weeks 7–8): UI Core + Modding + Perf Harness
- Deliverables:
	- HUD panels: inventory, contracts, faction standings, map.
	- JSON modding pipeline + validator + hot reload; schema docs.
	- Headless benchmark + perf dashboards; autosave stability.
- Acceptance:
	- Hot reload data packs without crash; clear errors on invalid data.
	- Headless tick ≥ 1,000 entities/ms; load < 8s; autosave < 1s.
	- Crash rate < 0.5% sessions over internal testing.
- Risks/Deps: tooling UX; trim-safe JSON; memory spikes.

### Cross-Cutting Metrics
- Performance: frame < 16ms; headless tick throughput ≥ 1,000 entities/ms.
- Memory: < 400MB steady; spikes < 550MB; 30 min leak-free.
- Quality: autosave/load reliable; errors actionable; logs readable.

### Immediate Next Steps
- Define JSON schemas: `wares`, `stations`, `factions`, `events`.
- Implement validator tool and wire CI check.
- Stand up headless benchmark (Scenario: 2K entities trade/combat mix).

---

## Legacy Phases (Historical)
[Original phase plan retained below for reference]



- [x] **BehaviorTreeSystem** - ECS integration
- [x] **Common GameEvents** - Damage, destroy, collision, target, trade, AI state, selection
- [ ] Audio system
- [ ] Weapon system
- [ ] Trading mechanics
- [ ] Spacecraft designs
## Phase 6: Polish (Weeks 17-20)

- [ ] Asset manager

## Current Status: Phase 1 Complete ✅

The foundation is ready! Next focus: Physics and Collision systems.
