using Microsoft.Xna.Framework;
using SpaceTradeEngine.AI;
using SpaceTradeEngine.AI.Behaviors;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.AI.Templates
{
    /// <summary>
    /// Pre-built behavior tree templates for common NPC types
    /// </summary>
    public static class AITemplates
    {
        /// <summary>
        /// Fighter AI - Aggressive combat behavior
        /// Finds enemies, attacks them, flees when damaged
        /// </summary>
        public static BehaviorTree CreateFighterAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Fighter AI")
                .AddChild(
                    // If health low, flee to safety
                    new SequenceNode("Retreat When Damaged")
                        .AddChild(SpaceBehaviors.SetAIState("RETREAT"))
                        .AddChild(SpaceBehaviors.IsHealthLow(0.3f))
                        .AddChild(SpaceBehaviors.Log("Health low! Retreating..."))
                        .AddChild(SpaceBehaviors.FindNearestAlly(spatialSystem, 2000f))
                        .AddChild(SpaceBehaviors.FleeFromEntity("Enemy", 250f, 500f))
                )
                .AddChild(
                    // Combat sequence
                    new SequenceNode("Combat")
                        .AddChild(SpaceBehaviors.SetAIState("COMBAT"))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 1000f))
                        .AddChild(
                            new SelectorNode("Engage Enemy")
                                .AddChild(
                                    // If in range, attack
                                    new SequenceNode("Attack")
                                        .AddChild(SpaceBehaviors.IsTargetInRange())
                                        .AddChild(SpaceBehaviors.HasLineOfSight())
                                        .AddChild(SpaceBehaviors.AttackTarget(600f, 0.5f))
                                )
                                .AddChild(
                                    // Otherwise, move closer
                                    SpaceBehaviors.MoveToEntity("Enemy", 200f, 550f)
                                )
                        )
                )
                .AddChild(
                    // Default: Patrol
                    new SequenceNode("Patrol")
                        .AddChild(SpaceBehaviors.SetAIState("PATROL"))
                        .AddChild(SpaceBehaviors.Log("No enemies detected, patrolling..."))
                        .AddChild(SpaceBehaviors.Patrol(
                            new Vector2[] {
                                new Vector2(0, 0),
                                new Vector2(500, 500),
                                new Vector2(-500, 500),
                                new Vector2(-500, -500)
                            },
                            150f
                        ))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Trader AI - Travels between stations, avoids combat
        /// </summary>
        public static BehaviorTree CreateTraderAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Trader AI")
                .AddChild(
                    // If enemies nearby, flee
                    new SequenceNode("Flee From Danger")
                        .AddChild(SpaceBehaviors.SetAIState("FLEE"))
                        .AddChild(SpaceBehaviors.AreEnemiesNearby(spatialSystem, 400f))
                        .AddChild(SpaceBehaviors.Log("Danger detected! Fleeing..."))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 600f))
                        .AddChild(SpaceBehaviors.FleeFromEntity("Enemy", 200f, 600f))
                )
                .AddChild(
                    // If damaged, seek repairs
                    new SequenceNode("Seek Repairs")
                        .AddChild(SpaceBehaviors.SetAIState("REPAIR"))
                        .AddChild(SpaceBehaviors.IsHealthLow(0.5f))
                        .AddChild(SpaceBehaviors.Log("Seeking repairs..."))
                        .AddChild(SpaceBehaviors.SeekHealing(spatialSystem, 3000f))
                        .AddChild(SpaceBehaviors.MoveToEntity("HealTarget", 150f, 100f))
                )
                .AddChild(
                    // Normal trading behavior
                    new SequenceNode("Trade Route")
                        .AddChild(SpaceBehaviors.SetAIState("TRADE"))
                        .AddChild(SpaceBehaviors.FindTradeStation(spatialSystem, 5000f))
                        .AddChild(SpaceBehaviors.MoveToEntity("TradeStation", 150f, 150f))
                        .AddChild(new WaitNode(2f, "Dock/Trade"))
                        .AddChild(SpaceBehaviors.Log("Trade complete, seeking next station..."))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Patrol AI - Guards an area, engages threats
        /// </summary>
        public static BehaviorTree CreatePatrolAI(Entity entity, SpatialPartitioningSystem spatialSystem, 
            Vector2[] patrolPoints)
        {
            var root = new SelectorNode("Patrol AI")
                .AddChild(
                    // If enemies in patrol zone, engage
                    new SequenceNode("Defend Zone")
                        .AddChild(SpaceBehaviors.SetAIState("DEFEND"))
                        .AddChild(SpaceBehaviors.AreEnemiesNearby(spatialSystem, 600f))
                        .AddChild(SpaceBehaviors.Log("Intruder detected! Engaging..."))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 800f))
                        .AddChild(
                            new SelectorNode("Combat")
                                .AddChild(
                                    new SequenceNode("Attack")
                                        .AddChild(SpaceBehaviors.IsTargetInRange())
                                        .AddChild(SpaceBehaviors.AttackTarget(600f, 1f))
                                )
                                .AddChild(
                                    SpaceBehaviors.MoveToEntity("Enemy", 180f, 550f)
                                )
                        )
                )
                .AddChild(
                    // Otherwise, patrol route
                    new SequenceNode("Patrol Route")
                        .AddChild(SpaceBehaviors.SetAIState("PATROL"))
                        .AddChild(SpaceBehaviors.Patrol(patrolPoints, 120f))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Coward AI - Flees from everything, never fights
        /// </summary>
        public static BehaviorTree CreateCowardAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Coward AI")
                .AddChild(
                    // Always flee from enemies
                    new SequenceNode("Panic!")
                        .AddChild(SpaceBehaviors.SetAIState("PANIC"))
                        .AddChild(SpaceBehaviors.AreEnemiesNearby(spatialSystem, 800f))
                        .AddChild(SpaceBehaviors.Log("PANIC! Running away!"))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 1000f))
                        .AddChild(SpaceBehaviors.FleeFromEntity("Enemy", 300f, 1000f))
                )
                .AddChild(
                    // Find allies for protection
                    new SequenceNode("Seek Protection")
                        .AddChild(SpaceBehaviors.SetAIState("SEEK"))
                        .AddChild(SpaceBehaviors.Log("Seeking allies..."))
                        .AddChild(SpaceBehaviors.FindNearestAlly(spatialSystem, 2000f))
                        .AddChild(SpaceBehaviors.MoveToEntity("Ally", 150f, 100f))
                )
                .AddChild(
                    // Wander randomly
                    new SequenceNode("Wander")
                        .AddChild(SpaceBehaviors.SetAIState("WANDER"))
                        .AddChild(SpaceBehaviors.Log("Wandering aimlessly..."))
                        .AddChild(SpaceBehaviors.RandomWait(2f, 5f))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Berserker AI - Extremely aggressive, attacks until destroyed
        /// </summary>
        public static BehaviorTree CreateBerserkerAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Berserker AI")
                .AddChild(
                    // Always seek and destroy
                    new SequenceNode("DESTROY EVERYTHING")
                        .AddChild(SpaceBehaviors.SetAIState("ATTACK"))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 2000f))
                        .AddChild(SpaceBehaviors.Log("TARGET ACQUIRED! ATTACK!"))
                        .AddChild(
                            new ParallelNode(ParallelNode.ParallelPolicy.RequireOne)
                                .AddChild(
                                    new SequenceNode("Attack")
                                        .AddChild(SpaceBehaviors.IsTargetInRange())
                                        .AddChild(SpaceBehaviors.AttackTarget(600f, 0.3f))
                                )
                                .AddChild(
                                    SpaceBehaviors.MoveToEntity("Enemy", 300f, 500f)
                                )
                        )
                )
                .AddChild(
                    // If no enemies, search aggressively
                    new SequenceNode("Hunt")
                        .AddChild(SpaceBehaviors.SetAIState("SEARCH"))
                        .AddChild(SpaceBehaviors.Log("No targets... SEARCHING!"))
                        .AddChild(SpaceBehaviors.Patrol(
                            new Vector2[] {
                                new Vector2(1000, 1000),
                                new Vector2(-1000, 1000),
                                new Vector2(-1000, -1000),
                                new Vector2(1000, -1000)
                            },
                            250f
                        ))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Miner AI - Collects resources, avoids combat
        /// </summary>
        public static BehaviorTree CreateMinerAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Miner AI")
                .AddChild(
                    // Flee if threatened
                    new SequenceNode("Avoid Danger")
                        .AddChild(SpaceBehaviors.SetAIState("AVOID"))
                        .AddChild(SpaceBehaviors.AreEnemiesNearby(spatialSystem, 500f))
                        .AddChild(SpaceBehaviors.Log("Danger! Returning to base..."))
                        .AddChild(SpaceBehaviors.FindTradeStation(spatialSystem, 5000f))
                        .AddChild(SpaceBehaviors.MoveToEntity("TradeStation", 200f, 150f))
                )
                .AddChild(
                    // Return to station when cargo full
                    new SequenceNode("Unload Cargo")
                        .AddChild(SpaceBehaviors.SetAIState("UNLOAD"))
                        .AddChild(new InverterNode(SpaceBehaviors.HasCargoSpace()))
                        .AddChild(SpaceBehaviors.Log("Cargo full! Returning to station..."))
                        .AddChild(SpaceBehaviors.FindTradeStation(spatialSystem, 5000f))
                        .AddChild(SpaceBehaviors.MoveToEntity("TradeStation", 150f, 100f))
                        .AddChild(new WaitNode(3f, "Unloading"))
                )
                .AddChild(
                    // Mine asteroids
                    new SequenceNode("Mining")
                        .AddChild(SpaceBehaviors.SetAIState("MINE"))
                        .AddChild(SpaceBehaviors.Log("Searching for asteroids..."))
                        .AddChild(new ActionNode(context =>
                        {
                            // Find nearest asteroid
                            var transform = context.Entity.GetComponent<TransformComponent>();
                            var asteroid = spatialSystem.FindNearestMatching(
                                transform.Position,
                                e => e.GetComponent<TagComponent>()?.HasTag("asteroid") ?? false,
                                1000f
                            );

                            if (asteroid != null)
                            {
                                context.SetValue("MiningTarget", asteroid);
                                return NodeStatus.Success;
                            }
                            return NodeStatus.Failure;
                        }, "FindAsteroid"))
                        .AddChild(SpaceBehaviors.MoveToEntity("MiningTarget", 100f, 50f))
                        .AddChild(new WaitNode(5f, "Mining"))
                        .AddChild(SpaceBehaviors.Log("Mining complete!"))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Escort AI - Follows and protects a target
        /// </summary>
        public static BehaviorTree CreateEscortAI(Entity entity, Entity protectTarget, 
            SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Escort AI")
                .AddChild(
                    // Defend protected target
                    new SequenceNode("Defend Target")
                        .AddChild(SpaceBehaviors.SetAIState("DEFEND TARGET"))
                        .AddChild(new ConditionNode(context =>
                        {
                            var targetPos = protectTarget.GetComponent<TransformComponent>()?.Position ?? Vector2.Zero;
                            var nearbyEnemies = spatialSystem.QueryRadius(targetPos, 600f);
                            
                            foreach (var enemy in nearbyEnemies)
                            {
                                var faction = enemy.GetComponent<FactionComponent>();
                                var myFaction = entity.GetComponent<FactionComponent>();
                                
                                if (faction?.FactionId != myFaction?.FactionId)
                                {
                                    context.SetValue("Enemy", enemy);
                                    return true;
                                }
                            }
                            return false;
                        }, "ThreatsNearTarget"))
                        .AddChild(SpaceBehaviors.Log("Defending target from threat!"))
                        .AddChild(
                            new SelectorNode("Engage")
                                .AddChild(
                                    new SequenceNode("Attack")
                                        .AddChild(SpaceBehaviors.IsTargetInRange())
                                        .AddChild(SpaceBehaviors.AttackTarget(600f, 0.8f))
                                )
                                .AddChild(
                                    SpaceBehaviors.MoveToEntity("Enemy", 220f, 550f)
                                )
                        )
                )
                .AddChild(
                    // Stay in formation with protected target
                    new SequenceNode("Maintain Formation")
                        .AddChild(SpaceBehaviors.SetAIState("FORMATION"))
                        .AddChild(new ActionNode(context =>
                        {
                            context.SetValue("FormationTarget", protectTarget);
                            return NodeStatus.Success;
                        }, "SetFormationTarget"))
                        .AddChild(SpaceBehaviors.MoveToEntity("FormationTarget", 180f, 150f))
                );

            return new BehaviorTree(entity, root);
        }

        /// <summary>
        /// Kamikaze AI - Rams into enemies
        /// </summary>
        public static BehaviorTree CreateKamikazeAI(Entity entity, SpatialPartitioningSystem spatialSystem)
        {
            var root = new SelectorNode("Kamikaze AI")
                .AddChild(
                    new SequenceNode("RAMMING SPEED!")
                        .AddChild(SpaceBehaviors.SetAIState("RAM"))
                        .AddChild(SpaceBehaviors.FindNearestEnemy(spatialSystem, 2000f))
                        .AddChild(SpaceBehaviors.Log("FOR THE EMPIRE! RAMMING SPEED!"))
                        .AddChild(SpaceBehaviors.MoveToEntity("Enemy", 400f, 0f)) // Max speed, no stop distance
                )
                .AddChild(
                    new SequenceNode("Search For Target")
                        .AddChild(SpaceBehaviors.SetAIState("SEARCH"))
                        .AddChild(SpaceBehaviors.Log("Seeking target to ram..."))
                        .AddChild(SpaceBehaviors.Patrol(
                            new Vector2[] {
                                new Vector2(800, 0),
                                new Vector2(0, 800),
                                new Vector2(-800, 0),
                                new Vector2(0, -800)
                            },
                            300f
                        ))
                );

            return new BehaviorTree(entity, root);
        }
    }
}
