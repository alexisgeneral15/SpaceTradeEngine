using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages resource extraction from asteroids and nodes.
    /// </summary>
    public class MiningSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly SpatialPartitioningSystem _spatialSystem;

        public MiningSystem(EntityManager entityManager, SpatialPartitioningSystem spatialSystem)
        {
            _entityManager = entityManager;
            _spatialSystem = spatialSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<MiningComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var miner = entity.GetComponent<MiningComponent>();
                var transform = entity.GetComponent<TransformComponent>();
                var cargo = entity.GetComponent<CargoComponent>();

                if (miner == null || transform == null || cargo == null) continue;

                // Update mining cooldown
                if (miner.CooldownRemaining > 0)
                {
                    miner.CooldownRemaining = Math.Max(0, miner.CooldownRemaining - deltaTime);
                    continue;
                }

                // Find nearby resource nodes
                if (miner.CurrentTargetId <= 0)
                {
                    var target = FindNearestResourceNode(transform.Position, miner.Range);
                    if (target != null)
                    {
                        miner.CurrentTargetId = target.Id;
                    }
                }

                // Mine current target
                if (miner.CurrentTargetId > 0)
                {
                    var target = _entityManager.GetEntity(miner.CurrentTargetId);
                    if (target != null && target.IsActive)
                    {
                        TryMine(entity, miner, cargo, target, deltaTime);
                    }
                    else
                    {
                        miner.CurrentTargetId = -1;
                    }
                }
            }
        }

        private Entity? FindNearestResourceNode(Vector2 position, float range)
        {
            var nearby = _spatialSystem.QueryRadius(position, range);
            Entity? nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var candidate in nearby)
            {
                var resource = candidate.GetComponent<ResourceNodeComponent>();
                if (resource == null || resource.Depleted) continue;

                var candidateTransform = candidate.GetComponent<TransformComponent>();
                if (candidateTransform == null) continue;

                float dist = Vector2.Distance(position, candidateTransform.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void TryMine(Entity miner, MiningComponent mining, CargoComponent cargo, Entity target, float deltaTime)
        {
            var resource = target.GetComponent<ResourceNodeComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();
            var minerTransform = miner.GetComponent<TransformComponent>();

            if (resource == null || targetTransform == null || minerTransform == null) return;

            float dist = Vector2.Distance(minerTransform.Position, targetTransform.Position);
            if (dist > mining.Range)
            {
                mining.CurrentTargetId = -1;
                return;
            }

            // Check cargo space
            if (cargo.UsedCapacity >= cargo.Capacity)
            {
                mining.CurrentTargetId = -1;
                return;
            }

            // Extract resources
            float extractRate = mining.ExtractionRate * deltaTime;
            float extracted = Math.Min(extractRate, resource.Quantity);
            extracted = Math.Min(extracted, cargo.Capacity - cargo.UsedCapacity);

            if (extracted > 0)
            {
                resource.Quantity -= extracted;
                cargo.Add(resource.ResourceType, (int)extracted);
                mining.TotalExtracted += extracted;

                if (resource.Quantity <= 0)
                {
                    resource.Depleted = true;
                    mining.CurrentTargetId = -1;
                }
            }

            mining.CooldownRemaining = mining.CycleDuration;
        }
    }

    /// <summary>
    /// Mining equipment component for ships that can extract resources.
    /// </summary>
    public class MiningComponent : Component
    {
        public float ExtractionRate { get; set; } = 10f; // units per second
        public float Range { get; set; } = 150f;
        public float CycleDuration { get; set; } = 1f; // seconds between extractions
        public float CooldownRemaining { get; set; } = 0f;
        public int CurrentTargetId { get; set; } = -1;
        public float TotalExtracted { get; set; } = 0f;
        public HashSet<string> SupportedResources { get; } = new() { "ore", "ice", "gas" };
    }

    /// <summary>
    /// Resource node component for asteroids and extractable objects.
    /// </summary>
    public class ResourceNodeComponent : Component
    {
        public string ResourceType { get; set; } = "ore";
        public float Quantity { get; set; } = 1000f;
        public float MaxQuantity { get; set; } = 1000f;
        public bool Depleted { get; set; } = false;
        public float RespawnTime { get; set; } = 0f; // 0 = never respawns
        public float TimeSinceDepleted { get; set; } = 0f;
        public int Richness { get; set; } = 1; // 1-5 quality multiplier

        public override void Update(float deltaTime)
        {
            if (Depleted && RespawnTime > 0)
            {
                TimeSinceDepleted += deltaTime;
                if (TimeSinceDepleted >= RespawnTime)
                {
                    Quantity = MaxQuantity;
                    Depleted = false;
                    TimeSinceDepleted = 0f;
                }
            }
        }

        public float PercentRemaining => MaxQuantity > 0 ? (Quantity / MaxQuantity) : 0f;
    }
}
