# SpaceTradeEngine – Game Vision & Scope

## Vision
Create a systemic space-trading strategy game where a living economy, faction diplomacy, and emergent events drive player stories. Better than UnendingGalaxyDeluxe through deeper simulation, smarter AI, cleaner UX, and lean performance.

## Audience
- Strategy/sandbox players who enjoy emergent narratives over scripted campaigns.
- Traders/tinkerers who value simulation depth, modding, and replayability.

## Differentiators (vs UnendingGalaxyDeluxe)
- Living economy: dynamic supply/demand, contracts, price signals, shocks.
- Smarter factions: goal-driven diplomacy, negotiations, ceasefires, embargoes.
- Emergent events: systemic triggers (shortages, wars, disasters) ripple through play.
- Performance-first: spatial partitioning + lean memory (< 400MB target) for large battles.
- Modding-first: data-driven content, hot-reloadable JSON, validator tooling.
- Clean UX: focused HUD, responsive inputs, windowed-only stability.

## Core Design Pillars
1. Systems Over Scripts: mechanics interlock to produce stories without heavy authored content.
2. Economy As Engine: trading, production, logistics meaningfully impact conflicts and politics.
3. Player Agency: multiple viable roles (trader, mercenary, fixer) with risk/reward tradeoffs.
4. Scalable Simulation: thousands of entities via ECS + spatial partitioning; graceful LOD.
5. Modding & Transparency: exposed data, inspectable AI decisions, readable logs.
6. Accessibility: minimal friction to play; consistent 60 FPS on mid-range hardware.

## Scope Boundaries
In scope:
- Trading, production chains, station markets; contracts and missions.
- Sector map generation; gates; faction territories and diplomacy.
- Tactical combat with small-to-mid fleets; basic command AI.
- Save/Load, profiles, data-driven assets; single-player offline simulation.

Out of scope (initial releases):
- Multiplayer or online services.
- First-person piloting or cockpit view.
- Planetary landing/colony sim; character-level RPG.
- Cinematic campaigns; heavy cutscenes.

## MVP Feature Set (Playable Loop)
- Economy V1: wares, stations, dynamic prices, buy/sell, cargo, contracts.
- Factions V1: attitudes, treaties, embargoes; event hooks (war/peace).
- Combat V1: weapon slots, health/armor, simple AI; salvage rewards.
- Navigation: sector graph, gates, pathfinding, fog-of-war basics.
- Missions: delivery, escort, bounty; reputation and payouts.
- UI Core: HUD, inventory, contracts, map, save/load toolbar.
- Modding: JSON templates with validator; hot reload for data packs.

Acceptance criteria snapshot:
- Complete trade mission loop (accept → deliver → payout → reputation change).
- Prices react to supply shocks within 5 minutes of simulated time.
- Faction embargo propagates to markets within 2 minutes; players affected.
- 2K active entities at 60 FPS on mid-range PC; memory < 400MB.

## Success Metrics
- Performance: frame time < 16ms at 2K entities; load < 8s; autosave < 1s.
- Memory: steady-state < 400MB; no spikes > 550MB; leak-free across 30 min.
- Simulation: tick throughput ≥ 1,000 entities/ms in headless benchmark.
- Quality: crash rate < 0.5% sessions; bug backlog burn-down weekly.
- Engagement: average session > 30 min; repeat play > 40%.

## Risks & Mitigations
- Combinatorial system bugs → property-based tests + scenario harnesses.
- Performance regressions → per-commit bench + flamegraphs; LOD toggles.
- Data drift in mods → schema + validator CI; clear error reporting.

## Next Steps
- Engineer Roadmap v0.1 aligned with MVP.
- Define data schemas for wares/stations/factions/events.
- Stand up validators and a small headless benchmark runner.
