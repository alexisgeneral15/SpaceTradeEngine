using System;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.AI;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Combat system that applies rank bonuses to weapon accuracy, range, and damage.
    /// Handles military/clan tactical behaviors and combat effectiveness scaling.
    /// </summary>
    public class CombatSystem : ECS.System
    {
        private readonly Random _random = new();

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<WeaponComponent>() || entity.HasComponent<WeaponSlotComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Combat bonuses are applied on-demand during weapon fire
            // This system can handle periodic tactical updates
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var rank = entity.GetComponent<RankComponent>();
                if (rank == null) continue;

                // Update tactical behavior for military/clan members
                if (rank.EntityType == EntityType.Military || rank.EntityType == EntityType.ClanMember)
                {
                    UpdateTacticalBehavior(entity, rank, deltaTime);
                }
            }
        }

        /// <summary>
        /// Get effective weapon range with rank bonuses applied.
        /// </summary>
        public float GetEffectiveRange(Entity entity, float baseRange)
        {
            var rank = entity.GetComponent<RankComponent>();
            if (rank == null) return baseRange;

            // Apply range bonus (0.05% to 25% for military, up to 28% for clan)
            return baseRange * (1f + rank.RangeBonus);
        }

        /// <summary>
        /// Check if shot hits target based on accuracy bonus.
        /// </summary>
        public bool RollAccuracy(Entity shooter, Entity target, float baseAccuracy = 0.85f)
        {
            var rank = shooter.GetComponent<RankComponent>();
            if (rank == null) return _random.NextDouble() < baseAccuracy;

            // Apply accuracy bonus
            float finalAccuracy = baseAccuracy + rank.AccuracyBonus;
            
            // Target's evasion reduces accuracy
            var targetRank = target.GetComponent<RankComponent>();
            if (targetRank != null)
            {
                finalAccuracy -= targetRank.EvasionBonus * 0.5f; // Evasion reduces incoming accuracy
            }

            finalAccuracy = Math.Clamp(finalAccuracy, 0.05f, 0.99f); // Cap between 5% and 99%
            
            return _random.NextDouble() < finalAccuracy;
        }

        /// <summary>
        /// Calculate final damage with offensive tactics bonus.
        /// </summary>
        public float ApplyOffensiveTactics(Entity attacker, float baseDamage)
        {
            var rank = attacker.GetComponent<RankComponent>();
            if (rank == null) return baseDamage;

            // Offensive tactics boost damage output
            float multiplier = 1f + rank.OffensiveTacticsBonus;
            
            return baseDamage * multiplier;
        }

        /// <summary>
        /// Calculate damage reduction from defensive tactics.
        /// </summary>
        public float ApplyDefensiveTactics(Entity defender, float incomingDamage)
        {
            var rank = defender.GetComponent<RankComponent>();
            if (rank == null) return incomingDamage;

            // Defensive tactics reduce incoming damage
            float reduction = rank.DefensiveTacticsBonus;
            
            return incomingDamage * (1f - reduction);
        }

        /// <summary>
        /// Update tactical AI behavior based on rank and situation.
        /// </summary>
        private void UpdateTacticalBehavior(Entity entity, RankComponent rank, float deltaTime)
        {
            var ai = entity.GetComponent<AIBehaviorComponent>();
            if (ai == null) return;

            var health = entity.GetComponent<HealthComponent>();
            if (health == null) return;

            // Elite units can use advanced tactics
            if (rank.CurrentRank >= Rank.Veteran && ai.UseAdvancedTactics)
            {
                float healthPercent = health.CurrentHealth / health.MaxHealth;
                
                // Tactical retreat when low health
                if (healthPercent < ai.TacticalRetreatThreshold)
                {
                    ai.StateData["TacticalRetreat"] = true;
                    // Could trigger defensive behavior here
                }
                else
                {
                    ai.StateData["TacticalRetreat"] = false;
                }

                // Enable flanking for elite units in combat
                if (rank.CurrentRank >= Rank.Elite)
                {
                    ai.FlankingEnabled = true;
                    ai.CoverFireEnabled = true;
                }
            }

            // Confidence affects behavior
            rank.ConfidenceLevel = 0.3f + (rank.CurrentRank switch
            {
                Rank.Rookie => 0.1f,
                Rank.Regular => 0.2f,
                Rank.Experienced => 0.3f,
                Rank.Veteran => 0.4f,
                Rank.Elite => 0.5f,
                _ => 0.2f
            });
        }

        /// <summary>
        /// Get patrol efficiency multiplier for idle/patrol behavior.
        /// Affects detection range and response time.
        /// </summary>
        public float GetPatrolEfficiency(Entity entity)
        {
            var rank = entity.GetComponent<RankComponent>();
            if (rank == null) return 1f;

            // Patrol efficiency increases detection and reduces reaction time
            return 1f + rank.PatrolEfficiencyBonus;
        }

        /// <summary>
        /// Apply all rank bonuses to weapon stats for display/calculation.
        /// </summary>
        public WeaponStats GetModifiedWeaponStats(Entity entity, WeaponComponent weapon)
        {
            var rank = entity.GetComponent<RankComponent>();
            
            var stats = new WeaponStats
            {
                BaseDamage = weapon.Damage,
                BaseRange = weapon.Range,
                BaseAccuracy = 0.85f
            };

            if (rank != null)
            {
                stats.DamageMultiplier = rank.DamageMultiplier * (1f + rank.OffensiveTacticsBonus);
                stats.EffectiveRange = weapon.Range * (1f + rank.RangeBonus);
                stats.EffectiveAccuracy = stats.BaseAccuracy + rank.AccuracyBonus;
            }
            else
            {
                stats.DamageMultiplier = 1f;
                stats.EffectiveRange = weapon.Range;
                stats.EffectiveAccuracy = stats.BaseAccuracy;
            }

            return stats;
        }
    }

    /// <summary>
    /// Weapon statistics with rank modifiers applied.
    /// </summary>
    public class WeaponStats
    {
        public float BaseDamage { get; set; }
        public float BaseRange { get; set; }
        public float BaseAccuracy { get; set; }
        
        public float DamageMultiplier { get; set; }
        public float EffectiveRange { get; set; }
        public float EffectiveAccuracy { get; set; }
        
        public float FinalDamage => BaseDamage * DamageMultiplier;
    }
}
