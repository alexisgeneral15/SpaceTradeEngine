using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Weapon targeting system using spatial queries
    /// </summary>
    public class TargetingSystem : ECS.System
    {
        private SpatialPartitioningSystem _spatialSystem;

        public TargetingSystem(SpatialPartitioningSystem spatialSystem)
        {
            _spatialSystem = spatialSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Process entities with targeting components
            return entity.HasComponent<TargetingComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive)
                    continue;

                var targeting = entity.GetComponent<TargetingComponent>();
                var transform = entity.GetComponent<TransformComponent>();

                if (targeting == null || transform == null)
                    continue;

                // Auto-targeting logic
                if (targeting.AutoTarget && (targeting.CurrentTarget == null || !IsValidTarget(targeting.CurrentTarget)))
                {
                    targeting.CurrentTarget = FindBestTarget(entity, targeting);
                }

                // Update targeting info
                if (targeting.CurrentTarget != null)
                {
                    UpdateTargetingInfo(entity, targeting);
                }
            }
        }

        private Entity? FindBestTarget(Entity source, TargetingComponent targeting)
        {
            var transform = source.GetComponent<TransformComponent>();
            if (transform == null)
                return null;

            // Find all entities within targeting range
            var candidates = _spatialSystem.QueryRadius(transform.Position, targeting.MaxRange);

            Entity? bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                if (candidate.Id == source.Id)
                    continue; // Skip self

                if (!IsValidTarget(candidate))
                    continue;

                // Filter by faction/team if specified
                if (targeting.TargetFilter != null && !targeting.TargetFilter(candidate))
                    continue;

                // Calculate targeting score
                float score = CalculateTargetingScore(source, candidate, targeting);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private float CalculateTargetingScore(Entity source, Entity target, TargetingComponent targeting)
        {
            var sourceTransform = source.GetComponent<TransformComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();

            if (sourceTransform == null || targetTransform == null)
                return float.MinValue;

            float distance = Vector2.Distance(sourceTransform.Position, targetTransform.Position);
            
            // Base score - prefer closer targets
            float score = targeting.MaxRange - distance;

            // Prioritize low health targets
            var targetHealth = target.GetComponent<HealthComponent>();
            if (targetHealth != null && targeting.PreferWeakTargets)
            {
                score += (1.0f - targetHealth.HealthPercent) * 100f;
            }

            // Prioritize targets in front
            Vector2 toTarget = targetTransform.Position - sourceTransform.Position;
            toTarget.Normalize();
            Vector2 forward = new Vector2((float)Math.Cos(sourceTransform.Rotation), (float)Math.Sin(sourceTransform.Rotation));
            float facingDot = Vector2.Dot(forward, toTarget);
            
            if (facingDot > 0)
                score += facingDot * 50f; // Bonus for targets in front

            return score;
        }

        private bool IsValidTarget(Entity target)
        {
            if (target == null || !target.IsActive)
                return false;

            // Must have a transform
            if (!target.HasComponent<TransformComponent>())
                return false;

            // Must be alive (if has health component)
            var health = target.GetComponent<HealthComponent>();
            if (health != null && !health.IsAlive)
                return false;

            return true;
        }

        private void UpdateTargetingInfo(Entity source, TargetingComponent targeting)
        {
            var sourceTransform = source.GetComponent<TransformComponent>();
            var targetTransform = targeting.CurrentTarget?.GetComponent<TransformComponent>();

            if (sourceTransform == null || targetTransform == null)
            {
                targeting.CurrentTarget = null;
                return;
            }

            // Update targeting data
            targeting.TargetDistance = Vector2.Distance(sourceTransform.Position, targetTransform.Position);
            targeting.TargetDirection = targetTransform.Position - sourceTransform.Position;
            targeting.TargetDirection.Normalize();

            // Check if target is in range
            targeting.IsInRange = targeting.TargetDistance <= targeting.MaxRange;

            // Calculate lead for moving targets (predictive targeting)
            var targetVelocity = targeting.CurrentTarget?.GetComponent<VelocityComponent>();
            if (targetVelocity != null && targeting.ProjectileSpeed > 0)
            {
                float timeToImpact = targeting.TargetDistance / targeting.ProjectileSpeed;
                targeting.LeadPosition = targetTransform.Position + targetVelocity.LinearVelocity * timeToImpact;
            }
            else
            {
                targeting.LeadPosition = targetTransform.Position;
            }

            // Check line of sight (using raycast)
            if (targeting.RequireLineOfSight)
            {
                var entitiesInPath = _spatialSystem.Raycast(
                    sourceTransform.Position,
                    targeting.TargetDirection,
                    targeting.TargetDistance
                );

                // Check if anything is blocking
                var currentTarget = targeting.CurrentTarget!;
                targeting.HasLineOfSight = !entitiesInPath.Exists(e => 
                    e.Id != source.Id && 
                    e.Id != currentTarget.Id &&
                    e.HasComponent<CollisionComponent>()
                );
            }
            else
            {
                targeting.HasLineOfSight = true;
            }
        }

        /// <summary>
        /// Manually set a target for an entity
        /// </summary>
        public void SetTarget(Entity source, Entity target)
        {
            var targeting = source.GetComponent<TargetingComponent>();
            if (targeting != null)
            {
                targeting.CurrentTarget = target;
            }
        }

        /// <summary>
        /// Clear target for an entity
        /// </summary>
        public void ClearTarget(Entity source)
        {
            var targeting = source.GetComponent<TargetingComponent>();
            if (targeting != null)
            {
                targeting.CurrentTarget = null;
            }
        }
    }

    /// <summary>
    /// Component for entities that can target other entities (weapons, turrets, missiles)
    /// </summary>
    public class TargetingComponent : Component
    {
        // Targeting parameters
        public float MaxRange { get; set; } = 500f;
        public bool AutoTarget { get; set; } = true;
        public bool PreferWeakTargets { get; set; } = false;
        public bool RequireLineOfSight { get; set; } = true;
        public float ProjectileSpeed { get; set; } = 300f; // For lead calculation

        // Current target
        public Entity? CurrentTarget { get; set; }
        
        // Targeting data (updated by system)
        public float TargetDistance { get; set; }
        public Vector2 TargetDirection { get; set; }
        public Vector2 LeadPosition { get; set; }
        public bool IsInRange { get; set; }
        public bool HasLineOfSight { get; set; }

        // Filtering
        public Predicate<Entity>? TargetFilter { get; set; }

        public TargetingComponent()
        {
            // Default filter - target entities with health
            TargetFilter = (entity) => entity.HasComponent<HealthComponent>();
        }
    }
}
