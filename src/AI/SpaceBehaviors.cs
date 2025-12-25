using System;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.AI;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.Events;

#nullable enable
namespace SpaceTradeEngine.AI.Behaviors
{
    /// <summary>
    /// Collection of pre-built behavior nodes for space games
    /// </summary>
    public static class SpaceBehaviors
    {
        // Optional global event system reference for signaling AI state changes
        public static EventSystem? Events { get; set; }

        #region Movement Behaviors

        /// <summary>
        /// Move towards a target position
        /// </summary>
        public static BehaviorNode MoveToPosition(Vector2 targetPosition, float speed, float arrivalDistance = 50f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    return NodeStatus.Failure;

                Vector2 toTarget = targetPosition - transform.Position;
                float distance = toTarget.Length();

                if (distance <= arrivalDistance)
                    return NodeStatus.Success;

                toTarget.Normalize();
                velocity.LinearVelocity = toTarget * speed;

                return NodeStatus.Running;
            }, "MoveToPosition");
        }

        /// <summary>
        /// Move towards a target entity
        /// </summary>
        public static BehaviorNode MoveToEntity(string entityKey, float speed, float arrivalDistance = 50f)
        {
            return new ActionNode(context =>
            {
                var targetEntity = context.GetValue<Entity>(entityKey);
                if (targetEntity == null)
                    return NodeStatus.Failure;

                var targetTransform = targetEntity.GetComponent<TransformComponent>();
                if (targetTransform == null)
                    return NodeStatus.Failure;

                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    return NodeStatus.Failure;

                Vector2 toTarget = targetTransform.Position - transform.Position;
                float distance = toTarget.Length();

                if (distance <= arrivalDistance)
                    return NodeStatus.Success;

                toTarget.Normalize();
                velocity.LinearVelocity = toTarget * speed;

                return NodeStatus.Running;
            }, $"MoveToEntity({entityKey})");
        }

        /// <summary>
        /// Flee from a target position
        /// </summary>
        public static BehaviorNode FleeFromPosition(Vector2 dangerPosition, float speed, float safeDistance = 300f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    return NodeStatus.Failure;

                Vector2 fromDanger = transform.Position - dangerPosition;
                float distance = fromDanger.Length();

                if (distance >= safeDistance)
                    return NodeStatus.Success;

                fromDanger.Normalize();
                velocity.LinearVelocity = fromDanger * speed;

                return NodeStatus.Running;
            }, "FleeFromPosition");
        }

        /// <summary>
        /// Flee from a target entity
        /// </summary>
        public static BehaviorNode FleeFromEntity(string entityKey, float speed, float safeDistance = 300f)
        {
            return new ActionNode(context =>
            {
                var targetEntity = context.GetValue<Entity>(entityKey);
                if (targetEntity == null)
                    return NodeStatus.Success; // No threat = success

                var targetTransform = targetEntity.GetComponent<TransformComponent>();
                if (targetTransform == null)
                    return NodeStatus.Success;

                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    return NodeStatus.Failure;

                Vector2 fromDanger = transform.Position - targetTransform.Position;
                float distance = fromDanger.Length();

                if (distance >= safeDistance)
                    return NodeStatus.Success;

                fromDanger.Normalize();
                velocity.LinearVelocity = fromDanger * speed;

                return NodeStatus.Running;
            }, $"FleeFromEntity({entityKey})");
        }

        /// <summary>
        /// Patrol between waypoints
        /// </summary>
        public static BehaviorNode Patrol(Vector2[] waypoints, float speed, float arrivalDistance = 50f)
        {
            return new ActionNode(context =>
            {
                if (waypoints == null || waypoints.Length == 0)
                    return NodeStatus.Failure;

                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    return NodeStatus.Failure;

                // Get current waypoint index from blackboard
                int currentIndex = context.GetValue<int>("PatrolIndex");
                Vector2 targetWaypoint = waypoints[currentIndex];

                Vector2 toTarget = targetWaypoint - transform.Position;
                float distance = toTarget.Length();

                if (distance <= arrivalDistance)
                {
                    // Reached waypoint, move to next
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                    context.SetValue("PatrolIndex", currentIndex);
                }

                toTarget.Normalize();
                velocity.LinearVelocity = toTarget * speed;

                return NodeStatus.Running; // Patrol never completes
            }, "Patrol");
        }

        /// <summary>
        /// Stop moving
        /// </summary>
        public static BehaviorNode Stop()
        {
            return new ActionNode(context =>
            {
                var velocity = context.Entity.GetComponent<VelocityComponent>();
                if (velocity != null)
                {
                    velocity.LinearVelocity = Vector2.Zero;
                    velocity.AngularVelocity = 0f;
                }
                return NodeStatus.Success;
            }, "Stop");
        }

        #endregion

        #region Combat Behaviors

        /// <summary>
        /// Attack current target
        /// </summary>
        public static BehaviorNode AttackTarget(float weaponRange, float weaponCooldown = 1f)
        {
            return new ActionNode(context =>
            {
                var targeting = context.Entity.GetComponent<TargetingComponent>();
                if (targeting == null || targeting.CurrentTarget == null)
                    return NodeStatus.Failure;

                if (!targeting.IsInRange || !targeting.HasLineOfSight)
                    return NodeStatus.Running;

                // Check cooldown
                float lastFireTime = context.GetValue<float>("LastFireTime");
                float currentTime = context.GetValue<float>("CurrentTime");
                
                if (currentTime - lastFireTime >= weaponCooldown)
                {
                    // Fire weapon
                    context.SetValue("LastFireTime", currentTime);
                    context.SetValue("FireWeapon", true);
                    return NodeStatus.Success;
                }

                return NodeStatus.Running;
            }, "AttackTarget");
        }

        /// <summary>
        /// Check if entity has a target
        /// </summary>
        public static BehaviorNode HasTarget()
        {
            return new ConditionNode(context =>
            {
                var targeting = context.Entity.GetComponent<TargetingComponent>();
                return targeting?.CurrentTarget != null;
            }, "HasTarget");
        }

        /// <summary>
        /// Check if target is in weapon range
        /// </summary>
        public static BehaviorNode IsTargetInRange()
        {
            return new ConditionNode(context =>
            {
                var targeting = context.Entity.GetComponent<TargetingComponent>();
                return targeting?.IsInRange ?? false;
            }, "IsTargetInRange");
        }

        /// <summary>
        /// Check if target is visible (line of sight)
        /// </summary>
        public static BehaviorNode HasLineOfSight()
        {
            return new ConditionNode(context =>
            {
                var targeting = context.Entity.GetComponent<TargetingComponent>();
                return targeting?.HasLineOfSight ?? false;
            }, "HasLineOfSight");
        }

        #endregion

        #region Health & Survival Behaviors

        /// <summary>
        /// Check if health is below threshold
        /// </summary>
        public static BehaviorNode IsHealthLow(float threshold = 0.3f)
        {
            return new ConditionNode(context =>
            {
                var health = context.Entity.GetComponent<HealthComponent>();
                return health != null && health.HealthPercent <= threshold;
            }, $"IsHealthLow({threshold})");
        }

        /// <summary>
        /// Check if entity is alive
        /// </summary>
        public static BehaviorNode IsAlive()
        {
            return new ConditionNode(context =>
            {
                var health = context.Entity.GetComponent<HealthComponent>();
                return health?.IsAlive ?? true;
            }, "IsAlive");
        }

        /// <summary>
        /// Heal at nearest station or repair point
        /// </summary>
        public static BehaviorNode SeekHealing(SpatialPartitioningSystem spatialSystem, float searchRadius = 1000f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                if (transform == null)
                    return NodeStatus.Failure;

                // Find nearest station with "repair" tag
                var nearbyStations = spatialSystem.QueryRadius(transform.Position, searchRadius);
                Entity? nearestStation = null;
                float nearestDistance = float.MaxValue;

                foreach (var station in nearbyStations)
                {
                    var tag = station.GetComponent<TagComponent>();
                    if (tag != null && (tag.HasTag("station") || tag.HasTag("repair")))
                    {
                        var stationTransform = station.GetComponent<TransformComponent>();
                        if (stationTransform == null)
                            continue;
                        float dist = Vector2.Distance(transform.Position, stationTransform.Position);
                        if (dist < nearestDistance)
                        {
                            nearestDistance = dist;
                            nearestStation = station;
                        }
                    }
                }

                if (nearestStation == null)
                    return NodeStatus.Failure;

                context.SetValue("HealTarget", nearestStation);
                return NodeStatus.Success;
            }, "SeekHealing");
        }

        #endregion

        #region Detection & Awareness Behaviors

        /// <summary>
        /// Find nearest enemy using spatial queries
        /// </summary>
        public static BehaviorNode FindNearestEnemy(SpatialPartitioningSystem spatialSystem, float searchRadius = 1000f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var faction = context.Entity.GetComponent<FactionComponent>();

                if (transform == null || faction == null)
                    return NodeStatus.Failure;

                var enemy = spatialSystem.FindNearestMatching(
                    transform.Position,
                    entity =>
                    {
                        var otherFaction = entity.GetComponent<FactionComponent>();
                        var health = entity.GetComponent<HealthComponent>();
                        return otherFaction != null &&
                               otherFaction.FactionId != faction.FactionId &&
                               (health == null || health.IsAlive);
                    },
                    searchRadius
                );

                if (enemy != null)
                {
                    context.SetValue("Enemy", enemy);
                    return NodeStatus.Success;
                }

                return NodeStatus.Failure;
            }, "FindNearestEnemy");
        }

        /// <summary>
        /// Check if enemies are nearby
        /// </summary>
        public static BehaviorNode AreEnemiesNearby(SpatialPartitioningSystem spatialSystem, float alertRadius = 500f)
        {
            return new ConditionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var faction = context.Entity.GetComponent<FactionComponent>();

                if (transform == null || faction == null)
                    return false;

                var nearbyEntities = spatialSystem.QueryRadius(transform.Position, alertRadius);
                
                foreach (var entity in nearbyEntities)
                {
                    if (entity.Id == context.Entity.Id)
                        continue;

                    var otherFaction = entity.GetComponent<FactionComponent>();
                    if (otherFaction != null && otherFaction.FactionId != faction.FactionId)
                        return true;
                }

                return false;
            }, $"AreEnemiesNearby({alertRadius})");
        }

        /// <summary>
        /// Find nearest ally
        /// </summary>
        public static BehaviorNode FindNearestAlly(SpatialPartitioningSystem spatialSystem, float searchRadius = 1000f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var faction = context.Entity.GetComponent<FactionComponent>();

                if (transform == null || faction == null)
                    return NodeStatus.Failure;

                var ally = spatialSystem.FindNearestMatching(
                    transform.Position,
                    entity =>
                    {
                        if (entity.Id == context.Entity.Id)
                            return false;

                        var otherFaction = entity.GetComponent<FactionComponent>();
                        return otherFaction != null && otherFaction.FactionId == faction.FactionId;
                    },
                    searchRadius
                );

                if (ally != null)
                {
                    context.SetValue("Ally", ally);
                    return NodeStatus.Success;
                }

                return NodeStatus.Failure;
            }, "FindNearestAlly");
        }

        #endregion

        #region Trading & Economy Behaviors

        /// <summary>
        /// Check if entity has cargo space
        /// </summary>
        public static BehaviorNode HasCargoSpace()
        {
            return new ConditionNode(context =>
            {
                // TODO: Implement when cargo system is added
                return context.HasValue("CargoSpace") && context.GetValue<int>("CargoSpace") > 0;
            }, "HasCargoSpace");
        }

        /// <summary>
        /// Find nearest trade station
        /// </summary>
        public static BehaviorNode FindTradeStation(SpatialPartitioningSystem spatialSystem, float searchRadius = 2000f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                if (transform == null)
                    return NodeStatus.Failure;

                var station = spatialSystem.FindNearestMatching(
                    transform.Position,
                    entity =>
                    {
                        var tag = entity.GetComponent<TagComponent>();
                        return tag != null && (tag.HasTag("station") || tag.HasTag("trade"));
                    },
                    searchRadius
                );

                if (station != null)
                {
                    context.SetValue("TradeStation", station);
                    return NodeStatus.Success;
                }

                return NodeStatus.Failure;
            }, "FindTradeStation");
        }

        #endregion

        #region Utility Behaviors

        /// <summary>
        /// Random wait duration
        /// </summary>
        public static BehaviorNode RandomWait(float minSeconds, float maxSeconds)
        {
            var random = new Random();
            float waitTime = minSeconds + (float)random.NextDouble() * (maxSeconds - minSeconds);
            return new WaitNode(waitTime, $"RandomWait({minSeconds}-{maxSeconds})");
        }

        /// <summary>
        /// Check distance to position
        /// </summary>
        public static BehaviorNode IsNearPosition(Vector2 position, float distance)
        {
            return new ConditionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                if (transform == null)
                    return false;

                return Vector2.Distance(transform.Position, position) <= distance;
            }, $"IsNearPosition({distance})");
        }

        /// <summary>
        /// Check if entity has specific tag
        /// </summary>
        public static BehaviorNode HasTag(string tag)
        {
            return new ConditionNode(context =>
            {
                var tagComponent = context.Entity.GetComponent<TagComponent>();
                return tagComponent?.HasTag(tag) ?? false;
            }, $"HasTag({tag})");
        }

        /// <summary>
        /// Log message to console (useful for debugging)
        /// </summary>
        public static BehaviorNode Log(string message)
        {
            return new ActionNode(context =>
            {
                Console.WriteLine($"[{context.Entity.Name}] {message}");
                return NodeStatus.Success;
            }, $"Log({message})");
        }

        /// <summary>
        /// Set AI state, publish AIStateChangedEvent, and optionally log.
        /// Stores previous state in blackboard key "AIState".
        /// </summary>
        public static BehaviorNode SetAIState(string newState, bool log = true)
        {
            return new ActionNode(context =>
            {
                string? prev = context.GetValue<string>("AIState");
                context.SetValue("AIState", newState);

                if (Events != null)
                {
                    Events.Publish(new AIStateChangedEvent(context.Entity.Id, prev ?? "", newState, EventFactory.Now()));
                }

                if (log)
                {
                    var name = context.Entity.Name;
                    if (!string.IsNullOrEmpty(prev))
                        Console.WriteLine($"[{name}] State: {prev} -> {newState}");
                    else
                        Console.WriteLine($"[{name}] State: {newState}");
                }
                return NodeStatus.Success;
            }, $"SetAIState({newState})");
        }

        #endregion
    }
}
