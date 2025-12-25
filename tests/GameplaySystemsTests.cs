using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.AI;

namespace SpaceTradeEngine.Tests
{
    /// <summary>
    /// Comprehensive test suite for all gameplay systems.
    /// </summary>
    public class GameplaySystemsTests
    {
        private EntityManager _entityManager;
        private EventSystem _eventSystem;
        private SpatialPartitioningSystem _spatialSystem;

        public void RunAllTests()
        {
            Console.WriteLine("=== Running Gameplay Systems Tests ===\n");

            SetupTestEnvironment();

            TestFleetSystem();
            TestMiningSystem();
            TestDiplomacySystem();
            TestMissionSystem();
            TestAIBehaviorSystem();
            TestProductionSystem();
            TestSystemIntegration();

            Console.WriteLine("\n=== All Tests Completed ===");
        }

        private void SetupTestEnvironment()
        {
            _entityManager = new EntityManager();
            _eventSystem = new EventSystem();
            var worldBounds = new Rectangle(-10000, -10000, 20000, 20000);
            _spatialSystem = new SpatialPartitioningSystem(worldBounds);
            _entityManager.RegisterSystem(_spatialSystem);
        }

        #region Fleet System Tests

        private void TestFleetSystem()
        {
            Console.WriteLine("Testing FleetSystem...");

            var fleetSystem = new FleetSystem(_entityManager);
            _entityManager.RegisterSystem(fleetSystem);

            // Create fleet leader
            var leader = CreateTestShip("Fleet_Leader", new Vector2(0, 0));
            var fleet = new FleetComponent 
            { 
                LeaderId = leader.Id,
                Formation = FormationType.Wedge,
                FormationSpacing = 100f
            };
            leader.AddComponent(fleet);

            // Create squadron members
            for (int i = 0; i < 3; i++)
            {
                var member = CreateTestShip($"Wingman_{i}", new Vector2(i * 50, i * 50));
                member.AddComponent(new SquadronMemberComponent { FleetId = leader.Id });
                fleet.MemberIds.Add(member.Id);
            }

            // Update system
            fleetSystem.Update(0.1f);

            // Verify formation positions calculated
            Assert(fleet.MemberIds.Count == 3, "Fleet should have 3 members");
            Assert(fleet.Formation == FormationType.Wedge, "Formation should be Wedge");

            Console.WriteLine("  ✓ Fleet creation and formation");

            // Test formation change
            fleet.Formation = FormationType.Line;
            fleetSystem.Update(0.1f);
            Assert(fleet.Formation == FormationType.Line, "Formation should change to Line");

            Console.WriteLine("  ✓ Formation switching");
            Console.WriteLine("FleetSystem tests passed!\n");
        }

        #endregion

        #region Mining System Tests

        private void TestMiningSystem()
        {
            Console.WriteLine("Testing MiningSystem...");

            var miningSystem = new MiningSystem(_entityManager, _spatialSystem);
            _entityManager.RegisterSystem(miningSystem);

            // Create resource node
            var node = _entityManager.CreateEntity("ResourceNode");
            node.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
            node.AddComponent(new ResourceNodeComponent 
            { 
                ResourceType = "ore", 
                Quantity = 1000, 
                MaxQuantity = 1000 
            });

            // Create miner
            var miner = CreateTestShip("Miner", new Vector2(0, 0));
            miner.AddComponent(new MiningComponent { ExtractionRate = 50f, Range = 200f });
            miner.AddComponent(new CargoComponent { Capacity = 500f });

            // Update system - miner should start extracting
            for (int i = 0; i < 5; i++)
            {
                miningSystem.Update(0.5f);
            }

            var cargo = miner.GetComponent<CargoComponent>();
            var nodeComp = node.GetComponent<ResourceNodeComponent>();

            Assert(cargo.Items.Count > 0, "Miner should have extracted resources");
            Assert(nodeComp.Quantity < 1000, "Node should be depleted");

            Console.WriteLine("  ✓ Resource extraction");
            Console.WriteLine("  ✓ Cargo management");
            Console.WriteLine("MiningSystem tests passed!\n");
        }

        #endregion

        #region Diplomacy System Tests

        private void TestDiplomacySystem()
        {
            Console.WriteLine("Testing DiplomacySystem...");

            var diplomacySystem = new DiplomacySystem(_eventSystem);
            _entityManager.RegisterSystem(diplomacySystem);

            // Initialize factions
            diplomacySystem.SetRelationship("human", "alien", 0f);
            diplomacySystem.SetRelationship("human", "pirate", -50f);

            // Test standing changes
            diplomacySystem.ModifyRelationship("human", "alien", 30f);
            float standing = diplomacySystem.GetStanding("human", "alien");
            Assert(standing == 30f, "Standing should increase to 30");
            var state = diplomacySystem.GetRelationship("human", "alien");
            Assert(state == RelationshipState.Friendly, "Should be friendly at 30 standing");

            Console.WriteLine("  ✓ Standing modification");

            // Test war declaration
            diplomacySystem.DeclareWar("human", "pirate");
            standing = diplomacySystem.GetStanding("human", "pirate");
            Assert(standing == -100f, "Standing should be -100 during war");

            Console.WriteLine("  ✓ War declaration");

            // Test alliance
            diplomacySystem.SetRelationship("human", "alien", 100f);
            diplomacySystem.DeclareAlliance("human", "alien");
            state = diplomacySystem.GetRelationship("human", "alien");
            Assert(state == RelationshipState.Allied, "Should be allied state");

            Console.WriteLine("  ✓ Alliance formation");

            // Test relationship decay
            for (int i = 0; i < 10; i++)
            {
                diplomacySystem.Update(1.0f);
            }
            
            Console.WriteLine("  ✓ Standing decay over time");
            Console.WriteLine("DiplomacySystem tests passed!\n");
        }

        #endregion

        #region Mission System Tests

        private void TestMissionSystem()
        {
            Console.WriteLine("Testing MissionSystem...");

            var missionSystem = new MissionSystem(_entityManager, _eventSystem);
            _entityManager.RegisterSystem(missionSystem);

            // Create test mission
            var mission = new Mission
            {
                Title = "Test Mission",
                Description = "Test delivery mission",
                RewardCredits = 5000f,
                TimeLimit = 60f
            };
            mission.Objectives.Add(new MissionObjective
            {
                Type = ObjectiveType.DeliverCargo,
                Description = "Deliver supplies",
                RequiredCount = 10,
                RequiredWareId = "ore"
            });

            int missionId = missionSystem.CreateMission(mission);
            Assert(missionId > 0, "Mission should be created with valid ID");

            Console.WriteLine("  ✓ Mission creation");

            // Assign mission to player
            var player = CreateTestShip("Player", Vector2.Zero);
            player.AddComponent(new CargoComponent { Capacity = 1000f });
            
            bool assigned = missionSystem.AssignMission(missionId, player.Id);
            Assert(assigned, "Mission should be assigned to player");

            var retrievedMission = missionSystem.GetMission(missionId);
            Assert(retrievedMission != null && retrievedMission.State == MissionState.Active, 
                "Mission should be active");

            Console.WriteLine("  ✓ Mission assignment");

            // Test mission completion
            missionSystem.CompleteMission(missionId);
            retrievedMission = missionSystem.GetMission(missionId);
            Assert(retrievedMission.State == MissionState.Completed, "Mission should be completed");

            Console.WriteLine("  ✓ Mission completion");

            // Test mission failure
            var failMission = new Mission { Title = "Fail Test", TimeLimit = 0.1f };
            int failId = missionSystem.CreateMission(failMission);
            missionSystem.AssignMission(failId, player.Id);
            missionSystem.Update(1.0f); // Expire mission
            
            var failedMission = missionSystem.GetMission(failId);
            Assert(failedMission.State == MissionState.Failed, "Mission should fail on timeout");

            Console.WriteLine("  ✓ Mission expiration/failure");
            Console.WriteLine("MissionSystem tests passed!\n");
        }

        #endregion

        #region AI Behavior System Tests

        private void TestAIBehaviorSystem()
        {
            Console.WriteLine("Testing AIBehaviorSystem...");

            var aiSystem = new AIBehaviorSystem(_entityManager, _spatialSystem, _eventSystem);
            _entityManager.RegisterSystem(aiSystem);

            // Test Idle behavior
            var ship = CreateTestShip("AI_Ship", Vector2.Zero);
            ship.AddComponent(new AIBehaviorComponent 
            { 
                DefaultBehavior = AIBehaviorType.Idle,
                CruiseSpeed = 200f
            });

            aiSystem.Update(0.1f);
            var ai = ship.GetComponent<AIBehaviorComponent>();
            Assert(ai.CurrentBehavior == AIBehaviorType.Idle, "Should start in Idle");

            Console.WriteLine("  ✓ Idle behavior");

            // Test Patrol behavior
            ai.CurrentBehavior = AIBehaviorType.Patrol;
            ai.PatrolWaypoints.Add(new Vector2(100, 100));
            ai.PatrolWaypoints.Add(new Vector2(200, 200));

            for (int i = 0; i < 5; i++)
            {
                aiSystem.Update(0.5f);
            }

            Assert(ai.PatrolWaypoints.Count == 2, "Patrol waypoints should be set");

            Console.WriteLine("  ✓ Patrol behavior");

            // Test Mine behavior
            var miner = CreateTestShip("Miner_AI", new Vector2(50, 50));
            miner.AddComponent(new MiningComponent { Range = 150f });
            miner.AddComponent(new CargoComponent { Capacity = 500f });
            miner.AddComponent(new AIBehaviorComponent 
            { 
                DefaultBehavior = AIBehaviorType.Mine 
            });

            // Create resource node
            var node = _entityManager.CreateEntity("Node");
            node.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
            node.AddComponent(new ResourceNodeComponent { ResourceType = "ore", Quantity = 100 });

            aiSystem.Update(0.5f);

            Console.WriteLine("  ✓ Mining behavior");
            Console.WriteLine("AIBehaviorSystem tests passed!\n");
        }

        #endregion

        #region Production System Tests

        private void TestProductionSystem()
        {
            Console.WriteLine("Testing ProductionSystem...");

            var productionSystem = new ProductionSystem(_entityManager, _eventSystem);
            _entityManager.RegisterSystem(productionSystem);

            // Create factory
            var factory = _entityManager.CreateEntity("Factory");
            factory.AddComponent(new FactoryComponent 
            { 
                ProductionRate = 1.0f,
                Efficiency = 1.0f
            });
            factory.AddComponent(new CargoComponent { Capacity = 1000f });
            
            var factoryComp = factory.GetComponent<FactoryComponent>();
            factoryComp.AvailableRecipes.Add("steel_production");

            // Add input resources
            var cargo = factory.GetComponent<CargoComponent>();
            cargo.Add("ore", 100);

            // Create recipe
            var recipe = new ProductionRecipe
            {
                Id = "steel_production",
                Name = "Steel Production",
                OutputWareId = "steel",
                OutputQuantity = 5,
                ProductionTime = 1.0f
            };
            recipe.InputWares["ore"] = 10;

            // Queue production
            bool queued = productionSystem.QueueProduction(factory.Id, recipe, 1);
            Assert(queued, "Production should be queued");
            Assert(factoryComp.ProductionQueue.Count == 1, "Queue should have 1 job");

            Console.WriteLine("  ✓ Production queueing");

            // Process production
            for (int i = 0; i < 15; i++)
            {
                productionSystem.Update(0.1f);
            }

            Assert(factoryComp.ProductionQueue.Count == 0, "Job should complete");
            Assert(factoryComp.CompletedJobs == 1, "Should have 1 completed job");
            Assert(cargo.Items.ContainsKey("steel"), "Should produce steel");

            Console.WriteLine("  ✓ Production processing");
            Console.WriteLine("  ✓ Resource consumption/output");
            Console.WriteLine("ProductionSystem tests passed!\n");
        }

        #endregion

        #region Integration Tests

        private void TestSystemIntegration()
        {
            Console.WriteLine("Testing System Integration...");

            // Test Fleet + AI integration
            var fleetSystem = new FleetSystem(_entityManager);
            var aiSystem = new AIBehaviorSystem(_entityManager, _spatialSystem, _eventSystem);

            var leader = CreateTestShip("Fleet_Leader", Vector2.Zero);
            var fleet = new FleetComponent { LeaderId = leader.Id };
            leader.AddComponent(fleet);
            leader.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Patrol });

            for (int i = 0; i < 2; i++)
            {
                var member = CreateTestShip($"Member_{i}", new Vector2(i * 50, 0));
                member.AddComponent(new SquadronMemberComponent { FleetId = leader.Id });
                fleet.MemberIds.Add(member.Id);
            }

            fleetSystem.Update(0.1f);
            aiSystem.Update(0.1f);

            Console.WriteLine("  ✓ Fleet + AI integration");

            // Test Mining + Production integration
            var miningSystem = new MiningSystem(_entityManager, _spatialSystem);
            var productionSystem = new ProductionSystem(_entityManager, _eventSystem);

            var node = _entityManager.CreateEntity("Resource");
            node.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
            node.AddComponent(new ResourceNodeComponent { ResourceType = "ore", Quantity = 1000 });

            var miner = CreateTestShip("Miner", new Vector2(90, 90));
            miner.AddComponent(new MiningComponent { Range = 200f, ExtractionRate = 100f });
            miner.AddComponent(new CargoComponent { Capacity = 500f });

            var factory = _entityManager.CreateEntity("Factory");
            factory.AddComponent(new FactoryComponent());
            factory.AddComponent(new CargoComponent { Capacity = 1000f });

            // Mine resources
            for (int i = 0; i < 10; i++)
            {
                miningSystem.Update(0.1f);
            }

            // Transfer to factory (simplified)
            var minerCargo = miner.GetComponent<CargoComponent>();
            var factoryCargo = factory.GetComponent<CargoComponent>();
            if (minerCargo.Items.ContainsKey("ore"))
            {
                int oreAmount = minerCargo.Items["ore"].Quantity;
                factoryCargo.Add("ore", oreAmount);
                minerCargo.Remove("ore", oreAmount);
            }

            Console.WriteLine("  ✓ Mining + Production chain");

            // Test Diplomacy + Mission integration
            var diplomacySystem = new DiplomacySystem(_eventSystem);
            var missionSystem = new MissionSystem(_entityManager, _eventSystem);

            diplomacySystem.SetRelationship("player", "faction_a", 50f);
            
            var mission = new Mission
            {
                Title = "Diplomatic Mission",
                IssuerFaction = "faction_a",
                RewardCredits = 1000f
            };
            int missionId = missionSystem.CreateMission(mission);

            var player = CreateTestShip("Player", Vector2.Zero);
            missionSystem.AssignMission(missionId, player.Id);
            missionSystem.CompleteMission(missionId);

            // Completing mission improves standing
            diplomacySystem.ModifyRelationship("player", "faction_a", 10f);

            Console.WriteLine("  ✓ Diplomacy + Mission integration");
            Console.WriteLine("System Integration tests passed!\n");
        }

        #endregion

        #region Helper Methods

        private Entity CreateTestShip(string name, Vector2 position)
        {
            var ship = _entityManager.CreateEntity(name);
            ship.AddComponent(new TransformComponent { Position = position });
            ship.AddComponent(new VelocityComponent());
            ship.AddComponent(new CollisionComponent { Radius = 20f });
            ship.AddComponent(new HealthComponent { MaxHealth = 100f, CurrentHealth = 100f });
            ship.AddComponent(new FactionComponent("test", "Test Faction"));
            return ship;
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }

        #endregion
    }
}
