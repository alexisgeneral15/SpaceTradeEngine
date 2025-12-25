using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.AI;
using SpaceTradeEngine.AI.Behaviors;
using SpaceTradeEngine.AI.Templates;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.Examples
{
    /// <summary>
    /// Examples showing how to use the Behavior Tree AI system
    /// </summary>
    public class BehaviorTreeExample
    {
        private EntityManager _entityManager;
        private SpatialPartitioningSystem _spatialSystem;
        private BehaviorTreeSystem _behaviorSystem;
        private SpaceTradeEngine.Systems.EventSystem _eventSystem;

        public void Initialize()
        {
            _entityManager = new EntityManager();

            // Setup spatial system
            Rectangle worldBounds = new Rectangle(-5000, -5000, 10000, 10000);
            _spatialSystem = new SpatialPartitioningSystem(worldBounds);
            _entityManager.RegisterSystem(_spatialSystem);

            // Setup behavior tree system (pass entity manager for accurate stats)
            _behaviorSystem = new BehaviorTreeSystem(_entityManager);
            _entityManager.RegisterSystem(_behaviorSystem);

            // Event system for AI state changes
            _eventSystem = new SpaceTradeEngine.Systems.EventSystem();
            SpaceTradeEngine.AI.Behaviors.SpaceBehaviors.Events = _eventSystem;

            // Subscribe to AI state changes
            _eventSystem.Subscribe<SpaceTradeEngine.Events.AIStateChangedEvent>(evt =>
            {
                var e = _entityManager.GetEntity(evt.EntityId);
                var name = e?.Name ?? evt.EntityId.ToString();
                var from = string.IsNullOrWhiteSpace(evt.FromState) ? "" : evt.FromState;
                if (!string.IsNullOrEmpty(from))
                    Console.WriteLine($"[AI] {name}: {from} -> {evt.ToState}");
                else
                    Console.WriteLine($"[AI] {name}: {evt.ToState}");
            }, deliverLastSticky: false);

            Console.WriteLine("Behavior Tree System initialized!");
        }

        #region Example 1: Simple Custom Behavior Tree

        /// <summary>
        /// Example: Build a custom behavior tree from scratch
        /// </summary>
        public Entity CreateShipWithCustomAI(string name, Vector2 position, string factionId)
        {
            var ship = CreateBasicShip(name, position, factionId);

            // Build behavior tree manually
            var behaviorTree = new BehaviorTree(ship,
                new SelectorNode("Custom Ship AI")
                    .AddChild(
                        // Priority 1: Flee if damaged
                        new SequenceNode("Survival")
                            .AddChild(new ConditionNode(ctx =>
                            {
                                var health = ctx.Entity.GetComponent<HealthComponent>();
                                return health != null && health.HealthPercent < 0.3f;
                            }, "IsHealthLow"))
                            .AddChild(new ActionNode(ctx =>
                            {
                                Console.WriteLine($"{ctx.Entity.Name}: Health low! Retreating!");
                                var velocity = ctx.Entity.GetComponent<VelocityComponent>();
                                if (velocity != null)
                                    velocity.LinearVelocity = new Vector2(-200, 0);
                                return NodeStatus.Running;
                            }, "Flee"))
                    )
                    .AddChild(
                        // Priority 2: Attack if enemy nearby
                        new SequenceNode("Combat")
                            .AddChild(SpaceBehaviors.FindNearestEnemy(_spatialSystem, 800f))
                            .AddChild(SpaceBehaviors.MoveToEntity("Enemy", 200f, 600f))
                    )
                    .AddChild(
                        // Priority 3: Default patrol
                        SpaceBehaviors.Patrol(
                            new Vector2[] {
                                new Vector2(0, 0),
                                new Vector2(500, 500),
                                new Vector2(-500, -500)
                            },
                            150f
                        )
                    )
            );

            // Add behavior tree component
            ship.AddComponent(new BehaviorTreeComponent(behaviorTree));

            return ship;
        }

        #endregion

        #region Example 2: Using Pre-built Templates

        /// <summary>
        /// Example: Use pre-built AI templates
        /// </summary>
        public void SpawnAIFleet()
        {
            // Fighter squadron
            for (int i = 0; i < 5; i++)
            {
                var fighter = CreateBasicShip($"Fighter_{i}", new Vector2(i * 100, 0), "human");
                var fighterAI = AITemplates.CreateFighterAI(fighter, _spatialSystem);
                fighter.AddComponent(new BehaviorTreeComponent(fighterAI));
            }

            // Trade convoy with escorts
            var trader = CreateBasicShip("Trader", new Vector2(0, 500), "human");
            var traderAI = AITemplates.CreateTraderAI(trader, _spatialSystem);
            trader.AddComponent(new BehaviorTreeComponent(traderAI));

            // Escorts protecting the trader
            for (int i = 0; i < 2; i++)
            {
                var escort = CreateBasicShip($"Escort_{i}", new Vector2(i * 100 - 50, 450), "human");
                var escortAI = AITemplates.CreateEscortAI(escort, trader, _spatialSystem);
                escort.AddComponent(new BehaviorTreeComponent(escortAI));
            }

            // Enemy berserkers
            for (int i = 0; i < 3; i++)
            {
                var berserker = CreateBasicShip($"Berserker_{i}", new Vector2(i * 150, 1000), "alien");
                var berserkerAI = AITemplates.CreateBerserkerAI(berserker, _spatialSystem);
                berserker.AddComponent(new BehaviorTreeComponent(berserkerAI));
            }

            Console.WriteLine("AI Fleet spawned!");
        }

        #endregion

        #region Example 3: Advanced Custom Behavior

        /// <summary>
        /// Example: Complex behavior with parallel execution
        /// </summary>
        public Entity CreateAdvancedAI(string name, Vector2 position)
        {
            var ship = CreateBasicShip(name, position, "neutral");

            var behaviorTree = new BehaviorTree(ship,
                new SelectorNode("Advanced AI")
                    .AddChild(
                        // Emergency behavior
                        new SequenceNode("Emergency")
                            .AddChild(SpaceBehaviors.IsHealthLow(0.2f))
                            .AddChild(
                                new ParallelNode(ParallelNode.ParallelPolicy.RequireOne)
                                    .AddChild(SpaceBehaviors.Log("EMERGENCY! Calling for help!"))
                                    .AddChild(SpaceBehaviors.FindNearestAlly(_spatialSystem, 2000f))
                                    .AddChild(SpaceBehaviors.FleeFromEntity("Enemy", 300f, 800f))
                            )
                    )
                    .AddChild(
                        // Tactical combat
                        new SequenceNode("Tactical Combat")
                            .AddChild(SpaceBehaviors.FindNearestEnemy(_spatialSystem, 1000f))
                            .AddChild(
                                new SelectorNode("Combat Tactics")
                                    .AddChild(
                                        // Kiting: Attack from range
                                        new SequenceNode("Kite")
                                            .AddChild(new ConditionNode(ctx =>
                                            {
                                                var targeting = ctx.Entity.GetComponent<TargetingComponent>();
                                                return targeting?.TargetDistance < 400f; // Too close!
                                            }, "IsTooClose"))
                                            .AddChild(SpaceBehaviors.Log("Enemy too close! Maintaining distance..."))
                                            .AddChild(SpaceBehaviors.FleeFromEntity("Enemy", 250f, 500f))
                                    )
                                    .AddChild(
                                        // Attack from optimal range
                                        new SequenceNode("Attack")
                                            .AddChild(SpaceBehaviors.IsTargetInRange())
                                            .AddChild(SpaceBehaviors.HasLineOfSight())
                                            .AddChild(SpaceBehaviors.AttackTarget(600f, 0.5f))
                                    )
                                    .AddChild(
                                        // Close distance
                                        SpaceBehaviors.MoveToEntity("Enemy", 220f, 550f)
                                    )
                            )
                    )
                    .AddChild(
                        // Opportunistic behavior
                        new SequenceNode("Scavenge")
                            .AddChild(SpaceBehaviors.Log("No threats detected, searching for opportunities..."))
                            .AddChild(new WaitNode(3f))
                    )
            );

            ship.AddComponent(new BehaviorTreeComponent(behaviorTree));
            return ship;
        }

        #endregion

        #region Example 4: Behavior Tree with Decorators

        /// <summary>
        /// Example: Using decorator nodes for advanced control flow
        /// </summary>
        public Entity CreateDecoratedAI(string name, Vector2 position)
        {
            var ship = CreateBasicShip(name, position, "pirate");

            var behaviorTree = new BehaviorTree(ship,
                new SelectorNode("Decorated AI")
                    .AddChild(
                        // Retry finding enemies up to 3 times
                        new RetryNode(
                            SpaceBehaviors.FindNearestEnemy(_spatialSystem, 1000f),
                            maxAttempts: 3
                        )
                    )
                    .AddChild(
                        // Timeout: Don't chase for more than 10 seconds
                        new TimeoutNode(
                            SpaceBehaviors.MoveToEntity("Enemy", 250f, 500f),
                            timeoutSeconds: 10f
                        )
                    )
                    .AddChild(
                        // Invert: Do something if NOT damaged
                        new InverterNode(
                            SpaceBehaviors.IsHealthLow(0.3f)
                        )
                    )
                    .AddChild(
                        // Always succeed (useful for optional behaviors)
                        new SucceederNode(
                            SpaceBehaviors.Log("This always succeeds, even if log fails")
                        )
                    )
                    .AddChild(
                        // Repeat patrol indefinitely
                        new RepeaterNode(
                            SpaceBehaviors.Patrol(
                                new Vector2[] { Vector2.Zero, new Vector2(500, 500) },
                                150f
                            ),
                            repeatCount: -1 // Infinite
                        )
                    )
            );

            ship.AddComponent(new BehaviorTreeComponent(behaviorTree));
            return ship;
        }

        #endregion

        #region Example 5: Blackboard Usage

        /// <summary>
        /// Example: Using blackboard for state management
        /// </summary>
        public Entity CreateStatefulAI(string name, Vector2 position)
        {
            var ship = CreateBasicShip(name, position, "neutral");

            var behaviorTree = new BehaviorTree(ship,
                new SequenceNode("Stateful AI")
                    .AddChild(
                        // Set initial state
                        new ActionNode(ctx =>
                        {
                            if (!ctx.HasValue("PatrolCount"))
                                ctx.SetValue("PatrolCount", 0);
                            return NodeStatus.Success;
                        }, "InitializeState")
                    )
                    .AddChild(
                        new SelectorNode("Behavior")
                            .AddChild(
                                // After 5 patrols, take a break
                                new SequenceNode("Rest")
                                    .AddChild(new ConditionNode(ctx =>
                                    {
                                        int count = ctx.GetValue<int>("PatrolCount");
                                        return count >= 5;
                                    }, "PatrolledEnough"))
                                    .AddChild(SpaceBehaviors.Log("Taking a break..."))
                                    .AddChild(new WaitNode(10f))
                                    .AddChild(new ActionNode(ctx =>
                                    {
                                        ctx.SetValue("PatrolCount", 0);
                                        return NodeStatus.Success;
                                    }, "ResetPatrolCount"))
                            )
                            .AddChild(
                                // Normal patrol
                                new SequenceNode("Patrol")
                                    .AddChild(SpaceBehaviors.Patrol(
                                        new Vector2[] { Vector2.Zero, new Vector2(300, 300) },
                                        120f
                                    ))
                                    .AddChild(new ActionNode(ctx =>
                                    {
                                        int count = ctx.GetValue<int>("PatrolCount");
                                        ctx.SetValue("PatrolCount", count + 1);
                                        Console.WriteLine($"Patrol {count + 1} completed");
                                        return NodeStatus.Success;
                                    }, "IncrementPatrolCount"))
                            )
                    )
            );

            ship.AddComponent(new BehaviorTreeComponent(behaviorTree));
            return ship;
        }

        #endregion

        #region Example 6: Dynamic Behavior Switching

        /// <summary>
        /// Example: Switch between different AI behaviors at runtime
        /// </summary>
        public void DemonstrateAISwitching()
        {
            var ship = CreateBasicShip("Adaptive Ship", Vector2.Zero, "human");

            // Start with fighter AI
            var fighterAI = AITemplates.CreateFighterAI(ship, _spatialSystem);
            ship.AddComponent(new BehaviorTreeComponent(fighterAI));

            Console.WriteLine("Ship created with Fighter AI");

            // After some time, switch to trader AI
            // (In real game, this would be triggered by events)
            var traderAI = AITemplates.CreateTraderAI(ship, _spatialSystem);
            var btComponent = ship.GetComponent<BehaviorTreeComponent>();
            btComponent.Tree = traderAI;

            Console.WriteLine("Switched to Trader AI");

            // Or switch to coward AI if heavily damaged
            var health = ship.GetComponent<HealthComponent>();
            if (health.HealthPercent < 0.2f)
            {
                var cowardAI = AITemplates.CreateCowardAI(ship, _spatialSystem);
                btComponent.Tree = cowardAI;
                Console.WriteLine("Switched to Coward AI (heavily damaged)");
            }
        }

        #endregion

        #region Example 7: Complete Battle Scenario

        /// <summary>
        /// Example: Full battle scenario with multiple AI types
        /// </summary>
        public void CreateBattleScenario()
        {
            Console.WriteLine("=== Creating Battle Scenario ===");

            // Create space stations
            CreateStation("Human Base", new Vector2(-1000, 0), "human");
            CreateStation("Alien Base", new Vector2(1000, 0), "alien");

            // Human forces
            Console.WriteLine("Spawning human forces...");
            
            // Fighter squadron
            for (int i = 0; i < 5; i++)
            {
                var fighter = CreateBasicShip($"Human_Fighter_{i}", 
                    new Vector2(-800 + i * 50, i * 50), "human");
                fighter.AddComponent(new BehaviorTreeComponent(
                    AITemplates.CreateFighterAI(fighter, _spatialSystem)
                ));
            }

            // Patrol ships
            Vector2[] humanPatrolRoute = new Vector2[] {
                new Vector2(-500, 0),
                new Vector2(-500, 500),
                new Vector2(0, 500),
                new Vector2(0, 0)
            };
            
            for (int i = 0; i < 3; i++)
            {
                var patrol = CreateBasicShip($"Human_Patrol_{i}", 
                    new Vector2(-600, i * 100), "human");
                patrol.AddComponent(new BehaviorTreeComponent(
                    AITemplates.CreatePatrolAI(patrol, _spatialSystem, humanPatrolRoute)
                ));
            }

            // Trade ships with escorts
            var trader = CreateBasicShip("Human_Trader", new Vector2(-800, 300), "human");
            trader.AddComponent(new BehaviorTreeComponent(
                AITemplates.CreateTraderAI(trader, _spatialSystem)
            ));

            var escort1 = CreateBasicShip("Escort_1", new Vector2(-850, 350), "human");
            escort1.AddComponent(new BehaviorTreeComponent(
                AITemplates.CreateEscortAI(escort1, trader, _spatialSystem)
            ));

            // Alien forces
            Console.WriteLine("Spawning alien forces...");
            
            // Aggressive berserkers
            for (int i = 0; i < 4; i++)
            {
                var berserker = CreateBasicShip($"Alien_Berserker_{i}", 
                    new Vector2(800 + i * 60, i * 60), "alien");
                berserker.AddComponent(new BehaviorTreeComponent(
                    AITemplates.CreateBerserkerAI(berserker, _spatialSystem)
                ));
            }

            // Kamikaze drones
            for (int i = 0; i < 3; i++)
            {
                var kamikaze = CreateBasicShip($"Alien_Kamikaze_{i}", 
                    new Vector2(750, i * 100), "alien");
                kamikaze.AddComponent(new BehaviorTreeComponent(
                    AITemplates.CreateKamikazeAI(kamikaze, _spatialSystem)
                ));
            }

            // Neutral miners (will flee from combat)
            Console.WriteLine("Spawning neutral miners...");
            for (int i = 0; i < 2; i++)
            {
                var miner = CreateBasicShip($"Miner_{i}", 
                    new Vector2(0, -500 + i * 100), "neutral");
                miner.AddComponent(new BehaviorTreeComponent(
                    AITemplates.CreateMinerAI(miner, _spatialSystem)
                ));
            }

            Console.WriteLine("Battle scenario ready!");
            Console.WriteLine($"Total AI entities: {_behaviorSystem.GetStats().TotalTrees}");
        }

        #endregion

        #region Helper Methods

        private Entity CreateBasicShip(string name, Vector2 position, string factionId)
        {
            var ship = _entityManager.CreateEntity(name);

            ship.AddComponent(new TransformComponent { Position = position });
            ship.AddComponent(new VelocityComponent());
            ship.AddComponent(new CollisionComponent { Radius = 25f });
            ship.AddComponent(new HealthComponent { MaxHealth = 100f });
            ship.AddComponent(new FactionComponent(factionId));
            ship.AddComponent(new TagComponent("ship", "combat"));
            ship.AddComponent(new TargetingComponent
            {
                MaxRange = 600f,
                AutoTarget = true,
                ProjectileSpeed = 300f
            });

            return ship;
        }

        private Entity CreateStation(string name, Vector2 position, string factionId)
        {
            var station = _entityManager.CreateEntity(name);

            station.AddComponent(new TransformComponent { Position = position });
            station.AddComponent(new CollisionComponent { Radius = 100f, IsTrigger = true });
            station.AddComponent(new HealthComponent { MaxHealth = 5000f });
            station.AddComponent(new FactionComponent(factionId));
            station.AddComponent(new TagComponent("station", "structure", "repair", "trade"));

            return station;
        }

        public void Update(float deltaTime)
        {
            _entityManager.Update(deltaTime);
        }

        public void PrintStatistics()
        {
            var aiStats = _behaviorSystem.GetStats();
            var spatialStats = _spatialSystem.GetStats();

            Console.WriteLine("=== AI System Statistics ===");
            Console.WriteLine(aiStats.ToString());
            Console.WriteLine(spatialStats.ToString());
        }

        #endregion
    }

    /// <summary>
    /// Quick demo of behavior tree system
    /// </summary>
    public class BehaviorTreeDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== Behavior Tree AI System Demo ===\n");

            var example = new BehaviorTreeExample();
            example.Initialize();

            Console.WriteLine("\n--- Creating Battle Scenario ---");
            example.CreateBattleScenario();

            Console.WriteLine("\n--- Simulating 20 frames ---");
            for (int i = 0; i < 20; i++)
            {
                example.Update(1 / 60f);
                
                if (i % 10 == 0)
                {
                    Console.WriteLine($"\nFrame {i}:");
                    example.PrintStatistics();
                }
            }

            Console.WriteLine("\n=== Demo Complete ===");
        }
    }
}
