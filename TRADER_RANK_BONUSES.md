# Trader Rank & XP Bonus System

## Overview
Enhanced civilian traders (EntityType.Civilian) now gain significant advantages as they level up through trading experience. Unlike military units that gain XP from combat, traders earn experience from profitable trades.

## Rank Progression
Traders advance through 5 ranks based on accumulated experience:

| Rank | XP Required | Base Effectiveness |
|------|-------------|-------------------|
| **Rookie** | 0 | 95% |
| **Regular** | 100 | 100% |
| **Experienced** | 300 | 120% |
| **Veteran** | 800 | 150% |
| **Elite** | 2000 | 250% |

## Trader-Specific Bonuses

### 1. Trade Margin Bonus (Buy Lower, Sell Higher)
**Formula:** `TradeMarginBonus = (effectiveness - 1.0) × 0.4`

| Rank | Effectiveness | Trade Margin Bonus | Buy Price | Sell Price |
|------|--------------|-------------------|-----------|------------|
| Rookie | 0.95× | -2% | **+2% penalty** | **-2% penalty** |
| Regular | 1.0× | 0% | Standard | Standard |
| Experienced | 1.2× | +8% | **-8% cheaper** | **+8% higher** |
| Veteran | 1.5× | +20% | **-20% cheaper** | **+20% higher** |
| Elite | 2.5× | +60% | **-60% cheaper** | **+60% higher** |

**Example Trade:**
- Base buy price: 1000 credits
- Base sell price: 1200 credits
- Base profit: 200 credits

**Elite Trader:**
- Actual buy: 1000 × 0.4 = **400 credits** (60% discount)
- Actual sell: 1200 × 1.6 = **1920 credits** (60% markup)
- Actual profit: **1520 credits** (7.6× base profit!)

### 2. Evasion Bonus (Dodge Attacks)
**Formula:** `EvasionBonus = (effectiveness - 1.0) × 0.25`

Traders get 25% better dodge than military units (military: 20%).

| Rank | Evasion Chance | Effect |
|------|---------------|---------|
| Rookie | -1.25% | Slightly worse dodge |
| Regular | 0% | No bonus |
| Experienced | 5% | 5% chance to dodge 50% damage |
| Veteran | 12.5% | 12.5% chance to dodge 50% damage |
| Elite | 37.5% | **37.5% chance to dodge 50% damage** |

### 3. Defense Bonus (Damage Reduction)
**Formula:** `DefenseBonus = (effectiveness - 1.0) × 0.3`

| Rank | Defense Bonus | Incoming Damage |
|------|--------------|-----------------|
| Rookie | -1.5% | 101.5% damage taken |
| Regular | 0% | 100% damage taken |
| Experienced | 6% | 94% damage taken |
| Veteran | 15% | 85% damage taken |
| Elite | 45% | **55% damage taken** |

### 4. Health Bonus
**Formula:** `HealthMultiplier = 1.0 + (effectiveness - 1.0) × 0.5`

| Rank | Health Multiplier |
|------|------------------|
| Rookie | 0.975× |
| Regular | 1.0× |
| Experienced | 1.1× |
| Veteran | 1.25× |
| Elite | 1.75× |

## Experience Gain System

### Trade XP Formula
```
Base XP = 5 + (profit_margin × 100)
Scaled XP = Base XP × (1 + cargo_quantity / 100)
Final XP = min(Scaled XP, 50)  // Cap at 50 per trade
```

**Examples:**
- 10% margin, 20 cargo: `(5 + 10) × 1.2 = 18 XP`
- 25% margin, 50 cargo: `(5 + 25) × 1.5 = 45 XP`
- 40% margin, 100 cargo: `(5 + 40) × 2.0 = 90 → 50 XP` (capped)

### Leveling Timeline
Assuming average 25 XP per trade:

| Rank Target | Trades Needed | Total Trades |
|------------|--------------|-------------|
| Regular | 4 | 4 |
| Experienced | 8 | 12 |
| Veteran | 20 | 32 |
| Elite | 48 | 80 |

## Combat Survival

Elite traders are incredibly durable in combat:

**Damage Calculation:**
1. Incoming: 100 base damage
2. Evasion check: 37.5% chance → 50 damage (if success)
3. Defense reduction: 50 × 0.55 = **27.5 final damage**
4. Health pool: 1.75× normal

**Result:** Elite trader takes ~1/4 the effective damage of a rookie trader and has 75% more health. **Total effective durability: ~7× rookie trader!**

## Implementation Details

### Code Integration
- **RankSystem.cs:** `AwardTradeExperience()` - XP calculation
- **TraderAISystem.cs:** Applies buy/sell modifiers in `HandleBuyingState()` and `HandleSellingState()`
- **DamageSystem.cs:** Applies evasion + defense reduction in `ApplyDamage()`
- **RankComponent:** Added `TradeMarginBonus` and `DefenseBonus` properties

### Visual Feedback
- Economy panel shows "Rank Bonuses: Buy/Sell margins & dodge +"
- Experience gain events published with reason (e.g., "Profitable trade (+25% margin)")
- Rank promotion events track civilian progression

## Balance Notes

### Risk vs Reward
- Rookie traders struggle with thin margins and vulnerability
- Veteran traders (32+ trades) become profitable powerhouses
- Elite traders (80+ trades) are near-invincible trading machines

### Faction Relations Synergy
Combines with diplomacy bonuses:
- Allied faction: +50% profit bonus
- Elite trader: +60% margin bonus
- **Combined:** Elite trader with allied faction = +110% profit multiplier!

### Pirate Threat Balancing
Elite traders' defense makes piracy less effective:
- Rookie trader: Easy prey
- Veteran trader: Risky target
- Elite trader: Dangerous to attack (37.5% dodge + 45% reduction)

## Testing & Demos

### F2 Key - Economy Demo
- 12 civilian traders with mixed ranks (Rookie, Regular, Experienced, Veteran)
- Watch profit rates vary dramatically by rank
- Elite traders accumulate wealth rapidly

### Visual Indicators
- Economy panel shows individual trader stats
- Profit totals reflect buy/sell bonuses
- XP events appear in event log

## Future Enhancements

### Potential Additions
1. **Specialization:** Experienced traders prefer certain goods
2. **Route Memory:** Veterans remember profitable routes
3. **Risk Assessment:** Higher rank = better hostile zone avoidance
4. **Convoy Formation:** Elite traders lead rookie escorts
5. **Insurance:** Higher rank = lower piracy losses
6. **Reputation:** Elite traders unlock faction-specific contracts

---

## Quick Reference

**Elite Trader Benefits Summary:**
- Buys at **40%** of base price
- Sells at **160%** of base price
- **37.5%** chance to dodge half damage
- Takes **55%** incoming damage
- Has **175%** base health
- **~7× durability** vs rookie

**Trade to Elite:** ~80 successful trades with 25% average margin
