using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.Systems;

#nullable enable
namespace SpaceTradeEngine.AI
{
    /// <summary>
    /// Advanced AI behaviors for NPC ships: patrols, escorts, trading, combat tactics.
    /// </summary>
    public class AIBehaviorSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly SpatialPartitioningSystem _spatialSystem;
        private readonly EventSystem _eventSystem;

        public AIBehaviorSystem(EntityManager entityManager, SpatialPartitioningSystem spatialSystem, EventSystem eventSystem)
        {
            _entityManager = entityManager;
            _spatialSystem = spatialSystem;
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<AIBehaviorComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var ai = entity.GetComponent<AIBehaviorComponent>();
                if (ai == null) continue;

                UpdateBehavior(entity, ai, deltaTime);
            }
        }

        private void UpdateBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            // Update state machine
            if (ai.CurrentBehavior == null)
            {
                ai.CurrentBehavior = ai.DefaultBehavior;
                ai.StateData.Clear();
            }

            switch (ai.CurrentBehavior)
            {
                case AIBehaviorType.Idle:
                    UpdateIdleBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Patrol:
                    UpdatePatrolBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Escort:
                    UpdateEscortBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Trade:
                    UpdateTradeBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Attack:
                    UpdateAttackBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Flee:
                    UpdateFleeBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Mine:
                    UpdateMineBehavior(entity, ai, deltaTime);
                    break;

                case AIBehaviorType.Follow:
                    UpdateFollowBehavior(entity, ai, deltaTime);
                    break;
            }

            // Check for behavior transitions
            CheckBehaviorTransitions(entity, ai);
        }

        private void UpdateIdleBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            // Drift slightly, rotate slowly
            var velocity = entity.GetComponent<VelocityComponent>();
            if (velocity != null)
            {
                velocity.LinearVelocity *= 0.98f; // Slow down
            }

            ai.IdleTime += deltaTime;
            if (ai.IdleTime > 5f && ai.WanderRange > 0f)
            {
                // Pick random nearby point to wander to
                var transform = entity.GetComponent<TransformComponent>();
                if (transform != null)
                {
                    var angle = (float)(new Random().NextDouble() * Math.PI * 2);
                    var dist = (float)(new Random().NextDouble() * ai.WanderRange);
                    var target = transform.Position + new Vector2((float)Math.Cos(angle) * dist, (float)Math.Sin(angle) * dist);
                    ai.StateData["wanderTarget"] = target;
                    ai.CurrentBehavior = AIBehaviorType.Follow; // Reuse follow for wandering
                }
                ai.IdleTime = 0f;
            }
        }

        private void UpdatePatrolBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            if (ai.PatrolWaypoints.Count == 0)
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var transform = entity.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || velocity == null) return;

            if (!ai.StateData.ContainsKey("currentWaypointIndex"))
            {
                ai.StateData["currentWaypointIndex"] = 0;
            }

            int wpIndex = (int)ai.StateData["currentWaypointIndex"];
            Vector2 target = ai.PatrolWaypoints[wpIndex];

            float dist = Vector2.Distance(transform.Position, target);
            if (dist < 100f)
            {
                // Reached waypoint, go to next
                wpIndex = (wpIndex + 1) % ai.PatrolWaypoints.Count;
                ai.StateData["currentWaypointIndex"] = wpIndex;
                target = ai.PatrolWaypoints[wpIndex];
            }

            // Steer towards waypoint
            Vector2 dir = target - transform.Position;
            if (dir.LengthSquared() > 0.01f)
            {
                dir.Normalize();
                velocity.LinearVelocity += dir * ai.Aggressiveness * 50f * deltaTime;
                if (velocity.LinearVelocity.LengthSquared() > ai.CruiseSpeed * ai.CruiseSpeed)
                {
                    velocity.LinearVelocity = Vector2.Normalize(velocity.LinearVelocity) * ai.CruiseSpeed;
                }
            }
        }

        private void UpdateEscortBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            if (ai.EscortTargetId <= 0)
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var target = _entityManager.GetEntity(ai.EscortTargetId);
            if (target == null || !target.IsActive)
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var transform = entity.GetComponent<TransformComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || targetTransform == null || velocity == null) return;

            // Stay within escort range
            float dist = Vector2.Distance(transform.Position, targetTransform.Position);
            if (dist > ai.EscortRange * 1.5f)
            {
                // Catch up
                Vector2 dir = targetTransform.Position - transform.Position;
                dir.Normalize();
                velocity.LinearVelocity += dir * ai.CruiseSpeed * 2f * deltaTime;
            }
            else if (dist < ai.EscortRange * 0.5f)
            {
                // Slow down
                velocity.LinearVelocity *= 0.95f;
            }
            else
            {
                // Match velocity
                var targetVel = target.GetComponent<VelocityComponent>();
                if (targetVel != null)
                {
                    velocity.LinearVelocity = Vector2.Lerp(velocity.LinearVelocity, targetVel.LinearVelocity, deltaTime * 2f);
                }
            }

            // Check for threats near escort target
            var threats = _spatialSystem.QueryRadius(targetTransform.Position, ai.DetectionRange);
            foreach (var threat in threats)
            {
                if (IsHostile(entity, threat))
                {
                    ai.StateData["attackTarget"] = threat.Id;
                    ai.CurrentBehavior = AIBehaviorType.Attack;
                    break;
                }
            }
        }

        private void UpdateTradeBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            // Simplified: just fly to trade destination
            if (!ai.StateData.ContainsKey("tradeDestination"))
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var transform = entity.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || velocity == null) return;

            Vector2 dest = (Vector2)ai.StateData["tradeDestination"];
            float dist = Vector2.Distance(transform.Position, dest);
            
            if (dist < 200f)
            {
                // Arrived, complete trade
                ai.CurrentBehavior = AIBehaviorType.Idle;
                ai.StateData.Remove("tradeDestination");
                return;
            }

            Vector2 dir = dest - transform.Position;
            dir.Normalize();
            velocity.LinearVelocity += dir * ai.CruiseSpeed * deltaTime;
        }

        private void UpdateAttackBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            if (!ai.StateData.ContainsKey("attackTarget"))
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            int targetId = (int)ai.StateData["attackTarget"];
            var target = _entityManager.GetEntity(targetId);
            if (target == null || !target.IsActive)
            {
                ai.StateData.Remove("attackTarget");
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var transform = entity.GetComponent<TransformComponent>();
            var targetTransform = target.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || targetTransform == null || velocity == null) return;

            float dist = Vector2.Distance(transform.Position, targetTransform.Position);

            // Check health, flee if low
            var health = entity.GetComponent<HealthComponent>();
            if (health != null && health.CurrentHealth < health.MaxHealth * 0.3f)
            {
                ai.CurrentBehavior = AIBehaviorType.Flee;
                ai.StateData["fleeFrom"] = targetId;
                return;
            }

            // Combat tactics based on aggressiveness
            if (ai.Aggressiveness > 0.7f)
            {
                // Aggressive: charge head-on
                Vector2 dir = targetTransform.Position - transform.Position;
                dir.Normalize();
                velocity.LinearVelocity += dir * ai.CruiseSpeed * ai.Aggressiveness * deltaTime;
            }
            else
            {
                // Cautious: strafe and circle
                Vector2 toTarget = targetTransform.Position - transform.Position;
                Vector2 perpendicular = new Vector2(-toTarget.Y, toTarget.X);
                perpendicular.Normalize();

                if (dist > 500f)
                {
                    // Close distance
                    toTarget.Normalize();
                    velocity.LinearVelocity += toTarget * ai.CruiseSpeed * deltaTime;
                }
                else if (dist < 300f)
                {
                    // Back off
                    toTarget.Normalize();
                    velocity.LinearVelocity -= toTarget * ai.CruiseSpeed * 0.5f * deltaTime;
                }
                else
                {
                    // Strafe
                    velocity.LinearVelocity += perpendicular * ai.CruiseSpeed * 0.7f * deltaTime;
                }
            }

            // Clamp velocity
            if (velocity.LinearVelocity.LengthSquared() > ai.CruiseSpeed * ai.CruiseSpeed)
            {
                velocity.LinearVelocity = Vector2.Normalize(velocity.LinearVelocity) * ai.CruiseSpeed;
            }
        }

        private void UpdateFleeBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            if (!ai.StateData.ContainsKey("fleeFrom"))
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            int threatId = (int)ai.StateData["fleeFrom"];
            var threat = _entityManager.GetEntity(threatId);
            
            var transform = entity.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || velocity == null) return;

            Vector2 fleeDir;
            if (threat != null && threat.IsActive)
            {
                var threatTransform = threat.GetComponent<TransformComponent>();
                if (threatTransform != null)
                {
                    fleeDir = transform.Position - threatTransform.Position;
                    float dist = fleeDir.Length();
                    if (dist > ai.DetectionRange * 2f)
                    {
                        // Safe distance, stop fleeing
                        ai.CurrentBehavior = AIBehaviorType.Idle;
                        ai.StateData.Remove("fleeFrom");
                        return;
                    }
                }
                else
                {
                    fleeDir = velocity.LinearVelocity;
                }
            }
            else
            {
                // Threat gone
                ai.CurrentBehavior = AIBehaviorType.Idle;
                ai.StateData.Remove("fleeFrom");
                return;
            }

            fleeDir.Normalize();
            velocity.LinearVelocity += fleeDir * ai.CruiseSpeed * 2f * deltaTime; // Flee faster
        }

        private void UpdateMineBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            // Find nearest resource node
            var transform = entity.GetComponent<TransformComponent>();
            var mining = entity.GetComponent<MiningComponent>();
            if (transform == null || mining == null)
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var nearby = _spatialSystem.QueryRadius(transform.Position, mining.Range * 2f);
            var nodes = nearby.Where(e => e.HasComponent<ResourceNodeComponent>()).ToList();
            
            if (nodes.Count == 0)
            {
                // No nodes, go idle
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var nearest = nodes.OrderBy(n => Vector2.DistanceSquared(transform.Position, n.GetComponent<TransformComponent>()!.Position)).First();
            var nodeTransform = nearest.GetComponent<TransformComponent>();
            if (nodeTransform == null) return;

            float dist = Vector2.Distance(transform.Position, nodeTransform.Position);
            if (dist > mining.Range)
            {
                // Move closer
                var velocity = entity.GetComponent<VelocityComponent>();
                if (velocity != null)
                {
                    Vector2 dir = nodeTransform.Position - transform.Position;
                    dir.Normalize();
                    velocity.LinearVelocity += dir * ai.CruiseSpeed * 0.5f * deltaTime;
                }
            }
            // MiningSystem handles extraction automatically
        }

        private void UpdateFollowBehavior(Entity entity, AIBehaviorComponent ai, float deltaTime)
        {
            // Generic follow: used for following entities or waypoints
            Vector2? target = null;

            if (ai.StateData.ContainsKey("followTarget"))
            {
                int targetId = (int)ai.StateData["followTarget"];
                var targetEntity = _entityManager.GetEntity(targetId);
                if (targetEntity != null && targetEntity.IsActive)
                {
                    var targetTransform = targetEntity.GetComponent<TransformComponent>();
                    if (targetTransform != null)
                        target = targetTransform.Position;
                }
            }
            else if (ai.StateData.ContainsKey("wanderTarget"))
            {
                target = (Vector2)ai.StateData["wanderTarget"];
            }

            if (!target.HasValue)
            {
                ai.CurrentBehavior = AIBehaviorType.Idle;
                return;
            }

            var transform = entity.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            if (transform == null || velocity == null) return;

            float dist = Vector2.Distance(transform.Position, target.Value);
            if (dist < 100f)
            {
                // Reached target
                ai.CurrentBehavior = AIBehaviorType.Idle;
                ai.StateData.Remove("followTarget");
                ai.StateData.Remove("wanderTarget");
                return;
            }

            Vector2 dir = target.Value - transform.Position;
            dir.Normalize();
            velocity.LinearVelocity += dir * ai.CruiseSpeed * deltaTime;
        }

        private void CheckBehaviorTransitions(Entity entity, AIBehaviorComponent ai)
        {
            // Threat detection
            var transform = entity.GetComponent<TransformComponent>();
            if (transform == null) return;

            if (ai.CurrentBehavior == AIBehaviorType.Idle || ai.CurrentBehavior == AIBehaviorType.Patrol)
            {
                var nearby = _spatialSystem.QueryRadius(transform.Position, ai.DetectionRange);
                foreach (var other in nearby)
                {
                    if (IsHostile(entity, other))
                    {
                        ai.StateData["attackTarget"] = other.Id;
                        ai.CurrentBehavior = AIBehaviorType.Attack;
                        break;
                    }
                }
            }
        }

        private bool IsHostile(Entity entity, Entity other)
        {
            // Clan component check
            var clan = entity.GetComponent<ClanComponent>();
            var otherClan = other.GetComponent<ClanComponent>();
            
            if (clan != null && otherClan != null)
            {
                // Both have clans - check clan relationship
                // For now, same clan = not hostile, different clan = check if hostile clans
                if (clan.ClanId == otherClan.ClanId)
                    return false; // Same clan members don't attack each other
                // In production, would check _clanSystem.AreClansHostile(clan.ClanId, otherClan.ClanId)
            }

            var faction = entity.GetComponent<FactionComponent>();
            var otherFaction = other.GetComponent<FactionComponent>();
            if (faction == null || otherFaction == null) return false;
            return faction.FactionId != otherFaction.FactionId; // Simplified
        }
    }

    /// <summary>
    /// Component for AI-controlled entities.
    /// </summary>
    public class AIBehaviorComponent : Component
    {
        public AIBehaviorType DefaultBehavior { get; set; } = AIBehaviorType.Idle;
        public AIBehaviorType? CurrentBehavior { get; set; }
        public Dictionary<string, object> StateData { get; } = new();
        
        // Behavior parameters
        public float Aggressiveness { get; set; } = 0.5f; // 0 = defensive, 1 = aggressive
        public float CruiseSpeed { get; set; } = 200f;
        public float DetectionRange { get; set; } = 1000f;
        public float WanderRange { get; set; } = 500f;
        public float IdleTime { get; set; } = 0f;

        // Advanced tactical behavior (veteran+ ranks)
        public bool UseAdvancedTactics { get; set; } = false;
        public float FormationDiscipline { get; set; } = 0.5f; // How well they maintain formation
        public float TacticalRetreatThreshold { get; set; } = 0.3f; // Health % to retreat
        public bool FlankingEnabled { get; set; } = false; // Use flanking maneuvers
        public bool CoverFireEnabled { get; set; } = false; // Provide covering fire

        // Patrol
        public List<Vector2> PatrolWaypoints { get; } = new();

        // Escort
        public int EscortTargetId { get; set; } = -1;
        public float EscortRange { get; set; } = 300f;

        // Clan support
        public string? ClanId { get; set; } // Clan membership
        public bool RequestAlliedSupport { get; set; } = false;
        public bool CallAlliesOnThreat { get; set; } = true; // Call clan/allied clan help when threatened
    }

    public enum AIBehaviorType
    {
        Idle,
        Patrol,
        Escort,
        Trade,
        Attack,
        Flee,
        Mine,
        Follow
    }
}
