using System;
using System.Collections.Generic;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.AI;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages rank progression and experience for all entity types.
    /// Ranks affect combat effectiveness, survivability, and AI behavior.
    /// </summary>
    public class RankSystem : ECS.System
    {
        private readonly EventSystem _eventSystem;
        private readonly Dictionary<int, float> _experienceTracking = new();

        public RankSystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<RankComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var rank = entity.GetComponent<RankComponent>();
                if (rank == null) continue;

                // Check for rank up
                if (rank.Experience >= rank.ExperienceForNextRank && rank.CurrentRank < Rank.Elite)
                {
                    PromoteEntity(entity, rank);
                }

                // Passive XP gain for active entities (small amount)
                if (rank.EntityType == EntityType.Military)
                {
                    rank.Experience += 0.1f * deltaTime; // Slow passive gain
                }
            }
        }

        /// <summary>
        /// Award experience points to an entity.
        /// </summary>
        public void AwardExperience(Entity entity, float amount, string reason = "")
        {
            var rank = entity.GetComponent<RankComponent>();
            if (rank == null) return;

            rank.Experience += amount;
            rank.TotalExperienceEarned += amount;

            _eventSystem.Publish(new ExperienceGainedEvent
            {
                EntityId = entity.Id,
                Amount = amount,
                Reason = reason,
                NewTotal = rank.Experience,
                Timestamp = DateTime.UtcNow
            });

            // Check for immediate promotion
            if (rank.Experience >= rank.ExperienceForNextRank && rank.CurrentRank < Rank.Elite)
            {
                PromoteEntity(entity, rank);
            }
        }

        /// <summary>
        /// Promote entity to next rank.
        /// </summary>
        private void PromoteEntity(Entity entity, RankComponent rank)
        {
            var oldRank = rank.CurrentRank;
            rank.CurrentRank = GetNextRank(rank.CurrentRank);
            rank.Experience = 0; // Reset XP for new rank
            rank.ExperienceForNextRank = GetExperienceRequirement(rank.CurrentRank);

            // Apply stat bonuses
            ApplyRankBonuses(entity, rank);

            _eventSystem.Publish(new RankPromotionEvent
            {
                EntityId = entity.Id,
                EntityName = entity.Name,
                OldRank = oldRank,
                NewRank = rank.CurrentRank,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Apply stat bonuses based on rank.
        /// </summary>
        private void ApplyRankBonuses(Entity entity, RankComponent rank)
        {
            var multiplier = GetEffectivenessMultiplier(rank.CurrentRank);
            
            // Update health based on rank
            var health = entity.GetComponent<HealthComponent>();
            if (health != null)
            {
                var baseHealth = health.MaxHealth / rank.HealthMultiplier; // Get original
                rank.HealthMultiplier = 1f + (multiplier - 1f) * 0.5f; // Half effectiveness bonus for health
                health.MaxHealth = baseHealth * rank.HealthMultiplier;
                health.CurrentHealth = Math.Min(health.CurrentHealth, health.MaxHealth);
            }

            // Update combat stats
            var combat = entity.GetComponent<CombatComponent>();
            if (combat != null)
            {
                rank.DamageMultiplier = multiplier;
            }

            // Military-specific bonuses (combat-focused)
            if (rank.EntityType == EntityType.Military)
            {
                // Accuracy: Rookie: 0.05%, Elite: 25%
                rank.AccuracyBonus = 0.0005f + (multiplier - 0.95f) * 0.16f;
                
                // Dodge/Evasion: Rookie: 0.05%, Elite: 25%
                rank.EvasionBonus = 0.0005f + (multiplier - 0.95f) * 0.16f;
                
                // Weapon Range: Rookie: 0.05%, Elite: 25%
                rank.RangeBonus = 0.0005f + (multiplier - 0.95f) * 0.16f;
                
                // Defense (damage reduction): Rookie: 0.05%, Elite: 25%
                rank.DefenseBonus = 0.0005f + (multiplier - 0.95f) * 0.16f;
                
                // Tactical bonuses
                rank.OffensiveTacticsBonus = (multiplier - 0.95f) * 0.15f; // Elite: 23.25%
                rank.DefensiveTacticsBonus = (multiplier - 0.95f) * 0.15f; // Elite: 23.25%
                rank.PatrolEfficiencyBonus = (multiplier - 0.95f) * 0.12f; // Elite: 18.6%

                // AI behavior updates
                var ai = entity.GetComponent<AIBehaviorComponent>();
                if (ai != null)
                {
                    // Higher ranks are more aggressive and confident
                    ai.Aggressiveness = Math.Min(1f, 0.4f + (multiplier - 0.95f) * 0.35f);
                    
                    // Elite units use advanced tactics
                    if (rank.CurrentRank >= Rank.Veteran)
                    {
                        ai.UseAdvancedTactics = true;
                        ai.FormationDiscipline = 0.7f + (multiplier - 1.5f) * 0.3f;
                    }
                }
            }
            // Clan member bonuses (balanced between combat and survival)
            else if (rank.EntityType == EntityType.ClanMember)
            {
                // Clans get slightly better combat bonuses than military
                rank.AccuracyBonus = 0.0005f + (multiplier - 0.95f) * 0.18f; // Elite: 28%
                rank.EvasionBonus = 0.0005f + (multiplier - 0.95f) * 0.18f; // Elite: 28%
                rank.RangeBonus = 0.0005f + (multiplier - 0.95f) * 0.18f; // Elite: 28%
                rank.DefenseBonus = 0.0005f + (multiplier - 0.95f) * 0.2f; // Elite: 31%
                
                // Clan tactics (more aggressive, less organized)
                rank.OffensiveTacticsBonus = (multiplier - 0.95f) * 0.18f; // Elite: 28%
                rank.DefensiveTacticsBonus = (multiplier - 0.95f) * 0.12f; // Elite: 18.6%
                rank.PatrolEfficiencyBonus = (multiplier - 0.95f) * 0.1f; // Elite: 15.5%

                var ai = entity.GetComponent<AIBehaviorComponent>();
                if (ai != null)
                {
                    // Clans are more aggressive than military
                    ai.Aggressiveness = Math.Min(1f, 0.6f + (multiplier - 0.95f) * 0.3f);
                }
            }
            // Trader-specific bonuses for civilian entities
            else if (rank.EntityType == EntityType.Civilian)
            {
                // Experienced traders get better prices and defensive bonuses
                rank.TradeMarginBonus = (multiplier - 1f) * 0.4f; // Up to 60% better margins at Elite
                rank.EvasionBonus = (multiplier - 1f) * 0.25f; // 25% of effectiveness as dodge
                rank.DefenseBonus = (multiplier - 1f) * 0.3f; // 30% damage reduction at Elite
            }
        }

        /// <summary>
        /// Get effectiveness multiplier for rank (0.95 = 95%, 2.5 = 250%).
        /// </summary>
        public static float GetEffectivenessMultiplier(Rank rank)
        {
            return rank switch
            {
                Rank.Rookie => 0.95f,       // 95%
                Rank.Regular => 1.0f,       // 100%
                Rank.Experienced => 1.2f,   // 120%
                Rank.Veteran => 1.5f,       // 150%
                Rank.Elite => 2.5f,         // 250%
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get XP required to reach next rank.
        /// </summary>
        private float GetExperienceRequirement(Rank currentRank)
        {
            return currentRank switch
            {
                Rank.Rookie => 100f,
                Rank.Regular => 300f,
                Rank.Experienced => 800f,
                Rank.Veteran => 2000f,
                Rank.Elite => float.MaxValue, // Max rank
                _ => 100f
            };
        }

        private Rank GetNextRank(Rank current)
        {
            return current switch
            {
                Rank.Rookie => Rank.Regular,
                Rank.Regular => Rank.Experienced,
                Rank.Experienced => Rank.Veteran,
                Rank.Veteran => Rank.Elite,
                _ => Rank.Elite
            };
        }

        /// <summary>
        /// Award XP for killing an enemy. Higher rank enemies give more XP.
        /// </summary>
        public void AwardKillExperience(Entity killer, Entity victim)
        {
            var victimRank = victim.GetComponent<RankComponent>();
            if (victimRank == null) return;

            // Base XP based on victim's rank
            float baseXP = victimRank.CurrentRank switch
            {
                Rank.Rookie => 10f,
                Rank.Regular => 25f,
                Rank.Experienced => 50f,
                Rank.Veteran => 100f,
                Rank.Elite => 250f,
                _ => 10f
            };

            // Bonus for killing higher rank
            var killerRank = killer.GetComponent<RankComponent>();
            if (killerRank != null && victimRank.CurrentRank > killerRank.CurrentRank)
            {
                baseXP *= 1.5f; // 50% bonus for killing superior
            }

            AwardExperience(killer, baseXP, $"Defeated {victimRank.CurrentRank} enemy");
        }

        /// <summary>
        /// Award XP for successful trade - amount based on profit margin and difficulty.
        /// Traders gain XP from commerce, not combat.
        /// </summary>
        public void AwardTradeExperience(Entity trader, float profitMargin, int cargoQuantity)
        {
            var rank = trader.GetComponent<RankComponent>();
            if (rank == null || rank.EntityType != EntityType.Civilian) return;

            // Base XP: 5-30 based on profit margin
            float xp = 5f + (profitMargin * 100f); // 20% margin = 25 XP
            
            // Scale by cargo size (larger trades = more XP)
            xp *= (1f + cargoQuantity / 100f);
            
            // Cap at 50 XP per trade to prevent abuse
            xp = Math.Min(xp, 50f);

            AwardExperience(trader, xp, $"Profitable trade (+{profitMargin:P0} margin)");
        }

        /// <summary>
        /// Get display name for rank.
        /// </summary>
        public static string GetRankName(Rank rank, EntityType type)
        {
            if (type == EntityType.Civilian)
            {
                return rank switch
                {
                    Rank.Rookie => "Novice",
                    Rank.Regular => "Trader",
                    Rank.Experienced => "Merchant",
                    Rank.Veteran => "Master Trader",
                    Rank.Elite => "Trade Baron",
                    _ => "Unknown"
                };
            }
            else if (type == EntityType.ClanMember)
            {
                return rank switch
                {
                    Rank.Rookie => "Initiate",
                    Rank.Regular => "Member",
                    Rank.Experienced => "Warrior",
                    Rank.Veteran => "Champion",
                    Rank.Elite => "Legend",
                    _ => "Unknown"
                };
            }
            else // Military
            {
                return rank switch
                {
                    Rank.Rookie => "Ensign",
                    Rank.Regular => "Lieutenant",
                    Rank.Experienced => "Commander",
                    Rank.Veteran => "Captain",
                    Rank.Elite => "Admiral",
                    _ => "Unknown"
                };
            }
        }
    }

    /// <summary>
    /// Component tracking entity rank, experience, and combat multipliers.
    /// </summary>
    public class RankComponent : Component
    {
        public Rank CurrentRank { get; set; } = Rank.Rookie;
        public EntityType EntityType { get; set; } = EntityType.Military;
        public float Experience { get; set; } = 0f;
        public float ExperienceForNextRank { get; set; } = 100f;
        public float TotalExperienceEarned { get; set; } = 0f;

        // Universal combat stat multipliers
        public float DamageMultiplier { get; set; } = 0.95f;
        public float HealthMultiplier { get; set; } = 0.95f;
        public float AccuracyBonus { get; set; } = 0f;
        public float EvasionBonus { get; set; } = 0f;
        public float DefenseBonus { get; set; } = 0f; // Damage reduction
        
        // Military/Clan combat bonuses
        public float RangeBonus { get; set; } = 0f; // Weapon range increase
        public float OffensiveTacticsBonus { get; set; } = 0f; // Attack efficiency
        public float DefensiveTacticsBonus { get; set; } = 0f; // Defense efficiency
        public float PatrolEfficiencyBonus { get; set; } = 0f; // Idle/patrol effectiveness
        
        // Trader-specific bonuses (Civilian entity type)
        public float TradeMarginBonus { get; set; } = 0f; // Better buy/sell prices

        // Behavior modifiers
        public float ConfidenceLevel { get; set; } = 0.5f; // Affects retreat threshold
        public int KillCount { get; set; } = 0;
        public int MissionsCompleted { get; set; } = 0;
    }

    public enum Rank
    {
        Rookie,      // 95% effectiveness
        Regular,     // 100% effectiveness
        Experienced, // 120% effectiveness
        Veteran,     // 150% effectiveness
        Elite        // 250% effectiveness
    }

    public enum EntityType
    {
        Military,
        Civilian,
        ClanMember
    }

    // Events
    public class ExperienceGainedEvent
    {
        public int EntityId { get; set; }
        public float Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public float NewTotal { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class RankPromotionEvent
    {
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public Rank OldRank { get; set; }
        public Rank NewRank { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Combat component for tracking damage dealing.
    /// </summary>
    public class CombatComponent : Component
    {
        public float BaseDamage { get; set; } = 10f;
        public float AttackSpeed { get; set; } = 1f; // Attacks per second
        public float Range { get; set; } = 100f;
        public float TimeSinceLastAttack { get; set; } = 0f;

        public float GetEffectiveDamage(RankComponent? rank)
        {
            if (rank == null) return BaseDamage;
            return BaseDamage * rank.DamageMultiplier;
        }
    }
}
