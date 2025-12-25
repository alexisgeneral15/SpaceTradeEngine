# Faction AI System - Autonomous NPC Behavior & Satellite Factions

## Overview
Implemented a fully autonomous faction AI system where NPCs can:
- Create satellite factions (colonies/subsidiaries) from mother factions
- Spawn their own clans and manage internal politics
- Make autonomous decisions about expansion, diplomacy, and warfare
- Develop independent economies with treasury management
- Inherit traits from mother factions with configurable autonomy

---

## 1. FactionAISystem.cs (src/AI/FactionAISystem.cs)
**Purpose:** Core AI controller for autonomous faction behavior and decision-making.

### Key Features:

#### Satellite Faction Creation
- **Mother Factions** can spawn satellite factions (colonies, outposts, subsidiaries)
- Satellites inherit 70% of mother's diplomatic relationships
- Configurable autonomy level (0=puppet, 1=independent)
- Reduced resource inheritance (50% treasury, 70% income)
- Automatic alliance with mother faction (85 standing)
- Satellites cannot create their own satellites

#### Autonomous Clan Management
- AI creates clans based on expansionist trait and treasury
- Generated clan names with prefix+suffix system
- Internal clan relationships (rivalry or cooperation)
- Clans assigned to expansion fleets automatically
- Faction-level clan relationship management

#### Territory Expansion
- Spawns expansion fleets (3-5 ships) based on expansionist trait
- Ships assigned to existing clans if available
- AI behavior configured per personality
- Treasury-gated expansion (cost check)
- Random spatial distribution

#### Diplomatic Behavior
- **Aggressive factions**: Declare wars on hostile neighbors (>0.7 aggression)
- **Diplomatic factions**: Improve relations, form alliances
- War propagation to satellite factions
- Alliance formation at 75+ standing
- Dynamic relationship modification

#### Economic System
- Per-faction treasury with income generation
- Treasury-based decision gating
- Costs for: expansion, clan creation, satellites
- Income rate varies by faction profile
- Decision cooldown system (prevents spam)

### Decision Tree (every 5 seconds):
```
1. Should create satellite? → CreateSatelliteFaction()
2. Should create clan? → CreateFactionClan()
3. Should expand? → ExpandFactionTerritory()
4. Should take diplomatic action? → TakeDiplomaticAction()
5. Should manage clans? → ManageClanRelationships()
```

### Classes:

**FactionAIController** - Runtime state per faction:
- `Treasury` - Current credits
- `MotherFactionId` - If satellite, parent faction
- `SatelliteFactions` - List of spawned colonies
- `ManagedClans` - List of clans under control
- `ExpansionCount` - Track expansions
- `TimeSinceLastAction` - Decision cooldown

**FactionAIProfile** - AI personality definition:
- **Traits** (0-1 scale):
  - `Aggressiveness` - War declaration frequency
  - `Expansionist` - Territory expansion drive
  - `Diplomatic` - Alliance/peace tendency
  - `AllowInternalRivalry` - Clan competition enabled
  
- **Economics**:
  - `StartingTreasury` - Initial credits
  - `IncomeRate` - Credits per minute
  - `ExpansionCost`, `ClanCreationCost`, `SatelliteFactionCost`
  
- **Limits**:
  - `MaxClans`, `MaxSatelliteFactions`
  - `DecisionCooldown` - Seconds between decisions
  
- **Hierarchy**:
  - `MotherFactionId` - Parent if satellite
  - `Autonomous` - Independence level
  - `SatelliteAutonomy` - How autonomous satellites will be

### Events Published:
- `FactionRegisteredEvent` - New AI faction activated
- `SatelliteFactionCreatedEvent` - Satellite spawned from mother
- `ClanCreatedByAIEvent` - AI created new clan
- `FactionExpandedEvent` - Expansion fleet deployed

---

## 2. FactionAIPanel.cs (src/UI/FactionAIPanel.cs)
**Purpose:** UI for monitoring and controlling AI faction behavior.

### Features:

#### Faction List Display
- Shows up to 5 AI factions with indented satellites
- Displays: Treasury, clan count, satellite count
- Personality traits summary (Aggr/Exp/Dipl)
- Visual hierarchy (satellites indented with └─)
- Color coding: main factions=LightYellow, satellites=LightGray

#### Selected Faction Details
- Full financial breakdown (treasury, income rate)
- Expansion count tracking
- Mother faction link display (for satellites)
- Loyalty standing to mother faction
- Managed clans list (name + ship count)
- Satellite factions list

#### Interactive Elements
- "Select" button per faction for detailed view
- Real-time treasury updates
- Relationship state indicators
- Standing color coding (-100 to +100)

---

## 3. faction_ai_profiles.json (assets/data/faction_ai_profiles.json)
**Purpose:** Data-driven configuration for faction AI behavior.

### Content:

#### Main Faction Profiles (6 factions):
1. **Human Federation** - Democratic expansionist
   - High treasury (50k), high income (500/min)
   - Moderate aggression (0.4), high expansion (0.7)
   - Creates autonomous colonies (0.7 autonomy)
   - Max 4 satellites, 6 clans

2. **Pirate Confederacy** - Aggressive raiders
   - Moderate treasury (30k), medium income (300/min)
   - Very high aggression (0.9), medium expansion (0.6)
   - Not diplomatic, allows internal rivalry
   - Max 3 satellites, 8 clans (loose confederation)

3. **Trade Guild** - Wealthy merchants
   - Highest treasury (80k), highest income (800/min)
   - Low aggression (0.2), medium expansion (0.5)
   - Very diplomatic, creates trading posts
   - Max 5 satellites, 5 clans

4. **Military Coalition** - Strong military
   - High treasury (60k), medium income (400/min)
   - High aggression (0.8), medium expansion (0.5)
   - Creates garrison outposts (0.5 autonomy)
   - Max 3 satellites, 4 clans

5. **Alien Collective** - Mysterious expansionists
   - Medium treasury (45k), medium income (350/min)
   - Medium aggression (0.6), high expansion (0.8)
   - Not diplomatic, hive-mind structure
   - Max 4 satellites, 3 clans

6. **Rebel Alliance** - Decentralized resistance
   - Low treasury (20k), low income (200/min)
   - High aggression (0.7), low expansion (0.4)
   - Allows internal rivalry, highly autonomous (0.95)
   - Max 2 satellites, 7 clans (cell structure)

#### Civilian Faction Templates (4 types):
1. **Independent Colony** - Civilian settlements
2. **Mining Consortium** - Resource extraction corps
3. **Mercenary Company** - For-hire military
4. **Research Institute** - Scientific organizations

#### Global AI Settings:
- Update interval: 5 seconds
- Min treasury for actions: 500 credits
- Satellite loyalty decay: 0.5/min
- Clan rivalry chance: 15%
- Diplomatic event chance: 30%
- Rebellion threshold: -75 standing

#### Economic Modifiers:
- Trade income: +20%
- Military income: -20%
- Mining income: +50%
- Tax rate: 10%
- Ship construction: 500 credits
- Station construction: 2000 credits

---

## 4. GameEngine Integration (src/Core/GameEngine.cs)

### System Registration:
- Added `_factionAISystem` field
- Initialized after ClanSystem (requires diplomacy/clan systems)
- Proper dependency injection

### Demo Hotkey:
- **F1** - `SpawnFactionAIDemo()`
  - Creates 3 AI-controlled factions:
    - **Red Corsairs** (pirates): Aggressive, expansionist, creates clans
    - **Merchant Collective** (traders): Diplomatic, wealthy, satellites
    - **Frontier Explorers** (colonists): High expansion, moderate diplomacy
  - Sets up diplomatic web:
    - Pirates vs Traders: -80 (hostile)
    - Traders vs Colonists: +60 (friendly)
    - Pirates vs Colonists: -40 (unfriendly)
  - Player relationships configured

### Updated Controls:
- Added "F1 FactionAI" to on-screen help
- Reorganized controls display for clarity

---

## Architecture & Behavior Flow

### Faction Lifecycle:
```
Registration (F1)
  ↓
Initial Diplomacy Setup
  ↓
AI Update Loop (every 5 seconds)
  ├─→ Check Treasury
  ├─→ Evaluate Decisions
  │   ├─→ Create Satellite?
  │   ├─→ Create Clan?
  │   ├─→ Expand Territory?
  │   ├─→ Diplomatic Action?
  │   └─→ Manage Clans?
  └─→ Execute Action → Cooldown
```

### Satellite Creation Flow:
```
Mother Faction has sufficient treasury
  ↓
Check: < max satellites, expansionist trait
  ↓
Create Satellite Faction
  ├─→ Inherit 50% treasury
  ├─→ Inherit 70% income rate
  ├─→ Inherit 80% aggressiveness
  ├─→ Inherit 60% expansionist
  ├─→ Set mother faction link
  └─→ Copy 70% of diplomatic relations
  ↓
Set mutual relationship: +85 standing
  ↓
Satellite begins autonomous behavior
```

### Clan Creation Flow:
```
Faction AI checks: treasury + clan limit
  ↓
Generate unique clan name
  ↓
Create clan via ClanSystem
  ↓
Set relationships with existing clans
  ├─→ Random standing: -30 to +60
  └─→ Allows internal competition
  ↓
Add to faction's managed clans list
```

### Expansion Flow:
```
Faction decides to expand
  ↓
Deduct expansion cost from treasury
  ↓
Spawn 3-5 ships at random location
  ├─→ Assign to existing clan (if any)
  ├─→ Configure AI behavior
  ├─→ Set faction/clan components
  └─→ Add to clan membership
  ↓
Increment expansion counter
```

---

## Key AI Behaviors

### 1. Autonomous Decision Making
- **Trait-based**: Personality dictates behavior patterns
- **Resource-gated**: All actions require sufficient treasury
- **Cooldown-limited**: Prevents decision spam (15-30 seconds)
- **Probabilistic**: Chances based on traits (e.g., 15% for satellite if expansionist=1.0)

### 2. Satellite Faction Dynamics
- **Inheritance**: Traits, economy, diplomacy from mother
- **Autonomy Levels**:
  - 0.0-0.3: Puppet state (follows mother exactly)
  - 0.4-0.6: Moderate autonomy (some independence)
  - 0.7-0.9: High autonomy (mostly independent)
  - 1.0: Full independence (still allied)
- **Loyalty Mechanics**: Standing with mother can decay
- **War Support**: Satellites join mother's wars

### 3. Internal Politics
- **Clan Rivalry**: If enabled, factions encourage competition
- **Unity Directives**: Without rivalry, factions improve clan relations
- **Leadership**: Clans have leaders assigned during expansion
- **Resource Sharing**: Members contribute to clan strength

### 4. Diplomatic Strategy
- **Aggressive** (>0.7): Declares wars on -50 standing
- **Moderate** (0.4-0.6): Balanced approach
- **Diplomatic** (flag): Actively improves relations, seeks alliances
- **War Propagation**: Mother's wars affect satellites

---

## Integration with Existing Systems

### With DiplomacySystem:
- Factions use SetRelationship/ModifyRelationship
- DeclareWar/DeclareAlliance for major events
- GetAllRelationships for AI decision-making
- Satellite diplomacy inheritance

### With ClanSystem:
- CreateClan for internal organization
- AddMemberToClan for ship assignment
- SetClanRelationship for internal politics
- Clan-based expansion fleets

### With AIBehaviorSystem:
- Ships spawned with AIBehaviorComponent
- Behavior types: Patrol, Attack, Trade
- ClanId set for group coordination
- Aggressiveness from faction profile

### With EntityManager:
- CreateEntity for ship spawning
- Component attachment (Transform, Health, Faction, Clan)
- System registration and updates

---

## Compile Status
✅ **BUILD SUCCESSFUL**
- 0 Errors
- 10 Warnings (non-critical nullable annotations)
- DLL generated successfully

---

## Testing & Demo

### Hotkey: F1
**SpawnFactionAIDemo()** creates:
- 3 autonomous AI factions with distinct personalities
- Diplomatic relationship web
- Player relationships
- Real-time behavior begins immediately

### Observable Behaviors (after F1):
1. **Every 5 seconds**: AI evaluates decisions
2. **~20-30 seconds**: First clan creation (if traits allow)
3. **~30-60 seconds**: First expansion fleet spawns
4. **~60-90 seconds**: Possible satellite faction creation
5. **Ongoing**: Diplomatic actions, relationship changes

### Monitoring:
- Open FactionAIPanel (future UI integration) to watch:
  - Treasury changes
  - Clan creation events
  - Satellite spawning
  - Expansion fleets
  - Diplomatic shifts

---

## Future Expansion Opportunities

### Advanced AI Behaviors:
1. **Rebellion Mechanics** - Satellites can revolt if loyalty < -75
2. **Economic Warfare** - Trade blockades, embargoes
3. **Technology Sharing** - Research propagation through hierarchy
4. **Migration** - Population movement between satellites
5. **Coup Attempts** - Clan leaders seize faction control

### Strategic Depth:
1. **Alliance Networks** - Multi-faction coalition warfare
2. **Proxy Wars** - Factions fight through satellites
3. **Espionage** - Sabotage, intelligence gathering
4. **Resource Competition** - Factions compete for mining zones
5. **Cultural Victory** - Diplomatic dominance

### Dynamic World:
1. **Emergent Empires** - Successful satellites become majors
2. **Faction Collapse** - Zero treasury triggers dissolution
3. **Mercenary Markets** - Clans switch factions for payment
4. **Border Conflicts** - Territory-based warfare
5. **Trade Routes** - Economic links between factions

---

## Files Created/Modified

### Created:
- `src/AI/FactionAISystem.cs` - 460 lines
- `src/UI/FactionAIPanel.cs` - 192 lines
- `assets/data/faction_ai_profiles.json` - 200+ lines

### Modified:
- `src/Core/GameEngine.cs` - Added system + F1 demo

### Total New Code: ~850 lines + JSON config

---

## Summary
Successfully implemented autonomous faction AI that:
- ✅ Creates satellite factions with inheritance
- ✅ Manages internal clan politics
- ✅ Makes independent expansion decisions
- ✅ Conducts diplomacy and warfare
- ✅ Operates with configurable personalities
- ✅ Hierarchical faction structures (mother→satellite)
- ✅ Economic simulation with treasury management
- ✅ Event-driven architecture
- ✅ Data-driven configuration
- ✅ Full integration with existing systems
- ✅ Zero compilation errors
- ✅ Ready for runtime testing with F1 demo

The AI creates a living, breathing galaxy where factions grow, spawn colonies, form alliances, and wage wars autonomously!
