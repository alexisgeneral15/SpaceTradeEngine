using System;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Simple damage system that applies damage/heal and publishes events.
    /// Integrates with RankSystem for damage modifiers and XP rewards.
    /// </summary>
    public class DamageSystem : ECS.System
    {
        private readonly EventSystem _eventSystem;
        private RankSystem? _rankSystem;

        public DamageSystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
        }

        public void SetRankSystem(RankSystem rankSystem)
        {
            _rankSystem = rankSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<HealthComponent>();
        }

        public override void Update(float deltaTime)
        {
            // No periodic work needed; damage is applied via methods.
        }

        public void ApplyDamage(Entity target, float amount, int? attackerId = null)
        {
            if (target == null || amount <= 0f) return;
            var health = target.GetComponent<HealthComponent>();
            var shields = target.GetComponent<ShieldComponent>();
            if (health == null) return;

            // Apply target's rank defense bonuses
            var targetRank = target.GetComponent<RankComponent>();
            if (targetRank != null)
            {
                // Evasion chance reduces incoming damage
                if (targetRank.EvasionBonus > 0)
                {
                    var random = new Random();
                    if (random.NextDouble() < targetRank.EvasionBonus)
                    {
                        amount *= 0.5f; // 50% damage reduction on successful dodge
                    }
                }
                
                // Defense bonus (traders get extra protection)
                if (targetRank.DefenseBonus > 0)
                {
                    amount *= (1f - targetRank.DefenseBonus); // Elite civilian: 30% damage reduction
                }
            }

            float remaining = amount;
            if (shields != null)
            {
                remaining = shields.AbsorbDamage(remaining);
            }

            if (remaining <= 0f) return;

            float prev = health.CurrentHealth;
            health.TakeDamage(remaining);
            float now = health.CurrentHealth;

            _eventSystem.Publish(new EntityDamagedEvent(target.Id, amount, now, attackerId, EventFactory.Now()));
            
            // Award XP to attacker on kill
            if (prev > 0 && now <= 0)
            {
                _eventSystem.Publish(new EntityDestroyedEvent(target.Id, attackerId, EventFactory.Now()));
                
                // Award kill XP via rank system
                if (_rankSystem != null && attackerId.HasValue)
                {
                    var attacker = GetEntityById(attackerId.Value);
                    if (attacker != null)
                    {
                        _rankSystem.AwardKillExperience(attacker, target);
                    }
                }
            }
        }

        private Entity? GetEntityById(int id)
        {
            // Find entity from tracked entities
            foreach (var entity in _entities)
            {
                if (entity.Id == id) return entity;
            }
            return null;
        }

        public void Heal(Entity target, float amount)
        {
            if (target == null || amount <= 0f) return;
            var health = target.GetComponent<HealthComponent>();
            if (health == null) return;

            health.Heal(amount);
            // Optional: publish a heal event type if added in GameEvents.
        }
    }
}
