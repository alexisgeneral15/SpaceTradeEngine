using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.AI;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Shipyard system - builds ships from blueprints, manages build queues.
    /// Factions can auto-produce fleets at their shipyards.
    /// </summary>
    public class ShipyardSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly EventSystem _eventSystem;
        private readonly Dictionary<int, Shipyard> _shipyards = new();
        private readonly Dictionary<string, ShipBlueprint> _blueprints = new();

        public IReadOnlyDictionary<int, Shipyard> Shipyards => _shipyards;
        public IReadOnlyDictionary<string, ShipBlueprint> Blueprints => _blueprints;

        public ShipyardSystem(EntityManager entityManager, EventSystem eventSystem)
        {
            _entityManager = entityManager;
            _eventSystem = eventSystem;
            InitializeDefaultBlueprints();
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<ShipyardComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var shipyardComp = entity.GetComponent<ShipyardComponent>();
                if (shipyardComp == null) continue;

                if (!_shipyards.TryGetValue(shipyardComp.ShipyardId, out var shipyard))
                    continue;

                // Process build queue
                ProcessBuildQueue(shipyard, deltaTime);

                // Auto-production for factions
                if (shipyard.AutoProduction && shipyard.BuildQueue.Count < shipyard.MaxQueueSize)
                {
                    TryAutoQueueShip(shipyard);
                }
            }
        }

        /// <summary>
        /// Register a shipyard for ship production.
        /// </summary>
        public int RegisterShipyard(Entity entity, string faction, ShipyardClass shipyardClass)
        {
            var shipyardComp = entity.GetComponent<ShipyardComponent>();
            int shipyardId;

            if (shipyardComp != null)
            {
                shipyardId = shipyardComp.ShipyardId;
            }
            else
            {
                shipyardId = _shipyards.Count + 1;
                shipyardComp = new ShipyardComponent { ShipyardId = shipyardId };
                entity.AddComponent(shipyardComp);
            }

            var shipyard = new Shipyard
            {
                ShipyardId = shipyardId,
                EntityId = entity.Id,
                Faction = faction,
                ShipyardClass = shipyardClass,
                BuildSpeed = GetBuildSpeed(shipyardClass),
                MaxQueueSize = GetMaxQueueSize(shipyardClass)
            };

            _shipyards[shipyardId] = shipyard;

            _eventSystem.Publish(new ShipyardRegisteredEvent
            {
                ShipyardId = shipyardId,
                Faction = faction,
                ShipyardClass = shipyardClass
            });

            return shipyardId;
        }

        /// <summary>
        /// Queue a ship for construction.
        /// </summary>
        public bool QueueShip(int shipyardId, string blueprintId, int priority = 0)
        {
            if (!_shipyards.TryGetValue(shipyardId, out var shipyard))
                return false;

            if (!_blueprints.TryGetValue(blueprintId, out var blueprint))
                return false;

            if (shipyard.BuildQueue.Count >= shipyard.MaxQueueSize)
                return false;

            // Check if shipyard can build this class
            if (!CanBuildShipClass(shipyard.ShipyardClass, blueprint.ShipClass))
                return false;

            var buildOrder = new ShipBuildOrder
            {
                OrderId = Guid.NewGuid().ToString(),
                BlueprintId = blueprintId,
                Progress = 0f,
                BuildTime = blueprint.BuildTime / shipyard.BuildSpeed,
                Priority = priority,
                QueuedTime = DateTime.UtcNow
            };

            shipyard.BuildQueue.Add(buildOrder);
            shipyard.BuildQueue = shipyard.BuildQueue.OrderByDescending(o => o.Priority).ToList();

            _eventSystem.Publish(new ShipQueuedEvent
            {
                ShipyardId = shipyardId,
                OrderId = buildOrder.OrderId,
                BlueprintId = blueprintId
            });

            return true;
        }

        /// <summary>
        /// Cancel a build order.
        /// </summary>
        public bool CancelOrder(int shipyardId, string orderId)
        {
            if (!_shipyards.TryGetValue(shipyardId, out var shipyard))
                return false;

            var order = shipyard.BuildQueue.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
                return false;

            shipyard.BuildQueue.Remove(order);

            _eventSystem.Publish(new ShipBuildCancelledEvent
            {
                ShipyardId = shipyardId,
                OrderId = orderId
            });

            return true;
        }

        /// <summary>
        /// Enable/disable auto-production for a shipyard.
        /// </summary>
        public void SetAutoProduction(int shipyardId, bool enabled, string? defaultBlueprintId = null)
        {
            if (_shipyards.TryGetValue(shipyardId, out var shipyard))
            {
                shipyard.AutoProduction = enabled;
                if (defaultBlueprintId != null)
                    shipyard.AutoProductionBlueprint = defaultBlueprintId;
            }
        }

        /// <summary>
        /// Register a ship blueprint.
        /// </summary>
        public void RegisterBlueprint(ShipBlueprint blueprint)
        {
            _blueprints[blueprint.BlueprintId] = blueprint;
        }

        private void ProcessBuildQueue(Shipyard shipyard, float deltaTime)
        {
            if (shipyard.BuildQueue.Count == 0)
                return;

            var currentOrder = shipyard.BuildQueue[0];
            currentOrder.Progress += deltaTime;

            if (currentOrder.Progress >= currentOrder.BuildTime)
            {
                // Ship complete!
                CompleteShipBuild(shipyard, currentOrder);
                shipyard.BuildQueue.RemoveAt(0);
                shipyard.ShipsBuilt++;
            }
        }

        private void CompleteShipBuild(Shipyard shipyard, ShipBuildOrder order)
        {
            var blueprint = _blueprints.GetValueOrDefault(order.BlueprintId);
            if (blueprint == null)
                return;

            // Get shipyard entity position
            var shipyardEntity = _entityManager.GetEntity(shipyard.EntityId);
            var shipyardTransform = shipyardEntity?.GetComponent<TransformComponent>();
            Vector2 spawnPos = shipyardTransform?.Position ?? Vector2.Zero;

            // Offset spawn slightly
            spawnPos += new Vector2(100f, 0f);

            // Create the ship entity
            var ship = _entityManager.CreateEntity($"{blueprint.ShipName}_{Guid.NewGuid().ToString().Substring(0, 8)}");

            ship.AddComponent(new TransformComponent { Position = spawnPos });
            ship.AddComponent(new VelocityComponent());
            ship.AddComponent(new FactionComponent(shipyard.Faction));
            
            if (blueprint.CargoCapacity > 0)
            {
                ship.AddComponent(new CargoComponent 
                { 
                    MaxVolume = blueprint.CargoCapacity,
                    Credits = 5000f 
                });
            }

            ship.AddComponent(new SpriteComponent
            {
                Tint = Microsoft.Xna.Framework.Color.White,
                LayerDepth = 0.5f
            });

            // Add AI behavior if specified
            if (!string.IsNullOrEmpty(blueprint.DefaultAIBehavior))
            {
                AddShipAI(ship, blueprint.DefaultAIBehavior);
            }

            _eventSystem.Publish(new ShipBuiltEvent
            {
                ShipyardId = shipyard.ShipyardId,
                ShipId = ship.Id,
                BlueprintId = order.BlueprintId,
                Faction = shipyard.Faction,
                BuildDuration = order.Progress
            });
        }

        private void TryAutoQueueShip(Shipyard shipyard)
        {
            if (string.IsNullOrEmpty(shipyard.AutoProductionBlueprint))
                return;

            if (shipyard.TimeSinceLastAutoQueue < 10f)
                return;

            QueueShip(shipyard.ShipyardId, shipyard.AutoProductionBlueprint, priority: 1);
            shipyard.TimeSinceLastAutoQueue = 0f;
        }

        private void AddShipAI(Entity ship, string aiType)
        {
            switch (aiType)
            {
                case "Trader":
                    ship.AddComponent(new TraderAIComponent
                    {
                        MinProfitMargin = 0.15f,
                        TraderType = "Merchant"
                    });
                    break;

                case "Fighter":
                    ship.AddComponent(new AIBehaviorComponent
                    {
                        Aggressiveness = 0.8f
                        // PreferredRange = 300f // Property not available
                    });
                    break;

                case "Patrol":
                    ship.AddComponent(new AIBehaviorComponent
                    {
                        Aggressiveness = 0.5f
                        // PreferredRange = 400f // Property not available
                    });
                    break;
            }
        }

        private bool CanBuildShipClass(ShipyardClass shipyardClass, ShipClass shipClass)
        {
            return shipyardClass switch
            {
                ShipyardClass.Small => shipClass == ShipClass.Fighter || shipClass == ShipClass.Corvette,
                ShipyardClass.Medium => shipClass != ShipClass.Capital && shipClass != ShipClass.Station,
                ShipyardClass.Large => true, // Can build anything
                ShipyardClass.Military => shipClass != ShipClass.Freighter && shipClass != ShipClass.Station,
                ShipyardClass.Civilian => shipClass == ShipClass.Freighter || shipClass == ShipClass.Transport || shipClass == ShipClass.Miner,
                _ => false
            };
        }

        private float GetBuildSpeed(ShipyardClass shipyardClass)
        {
            return shipyardClass switch
            {
                ShipyardClass.Small => 0.8f,
                ShipyardClass.Medium => 1.0f,
                ShipyardClass.Large => 1.5f,
                ShipyardClass.Military => 1.2f,
                ShipyardClass.Civilian => 1.0f,
                _ => 1.0f
            };
        }

        private int GetMaxQueueSize(ShipyardClass shipyardClass)
        {
            return shipyardClass switch
            {
                ShipyardClass.Small => 3,
                ShipyardClass.Medium => 6,
                ShipyardClass.Large => 12,
                ShipyardClass.Military => 8,
                ShipyardClass.Civilian => 5,
                _ => 4
            };
        }

        private void InitializeDefaultBlueprints()
        {
            // Fighter
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_fighter",
                ShipName = "Fighter",
                ShipClass = ShipClass.Fighter,
                BuildTime = 15f,
                CargoCapacity = 0,
                DefaultAIBehavior = "Fighter"
            });

            // Trader
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_trader",
                ShipName = "Trader",
                ShipClass = ShipClass.Freighter,
                BuildTime = 20f,
                CargoCapacity = 100,
                DefaultAIBehavior = "Trader"
            });

            // Corvette
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_corvette",
                ShipName = "Corvette",
                ShipClass = ShipClass.Corvette,
                BuildTime = 25f,
                CargoCapacity = 20,
                DefaultAIBehavior = "Patrol"
            });

            // Freighter
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_freighter",
                ShipName = "Freighter",
                ShipClass = ShipClass.Freighter,
                BuildTime = 30f,
                CargoCapacity = 200,
                DefaultAIBehavior = "Trader"
            });

            // Frigate
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_frigate",
                ShipName = "Frigate",
                ShipClass = ShipClass.Frigate,
                BuildTime = 40f,
                CargoCapacity = 30,
                DefaultAIBehavior = "Fighter"
            });

            // Miner
            RegisterBlueprint(new ShipBlueprint
            {
                BlueprintId = "bp_miner",
                ShipName = "Miner",
                ShipClass = ShipClass.Miner,
                BuildTime = 25f,
                CargoCapacity = 150,
                DefaultAIBehavior = ""
            });
        }

        public Shipyard? GetShipyard(int shipyardId)
        {
            return _shipyards.GetValueOrDefault(shipyardId);
        }

        public List<Shipyard> GetShipyardsByFaction(string faction)
        {
            return _shipyards.Values.Where(s => s.Faction == faction).ToList();
        }
    }

    /// <summary>
    /// Component marking entity as a shipyard.
    /// </summary>
    public class ShipyardComponent : Component
    {
        public int ShipyardId { get; set; }
    }

    /// <summary>
    /// Shipyard facility data.
    /// </summary>
    public class Shipyard
    {
        public int ShipyardId { get; set; }
        public int EntityId { get; set; }
        public string Faction { get; set; } = "neutral";
        public ShipyardClass ShipyardClass { get; set; } = ShipyardClass.Medium;
        public float BuildSpeed { get; set; } = 1.0f;
        public int MaxQueueSize { get; set; } = 5;
        
        public List<ShipBuildOrder> BuildQueue { get; set; } = new();
        public bool AutoProduction { get; set; } = false;
        public string AutoProductionBlueprint { get; set; } = string.Empty;
        public float TimeSinceLastAutoQueue { get; set; } = 0f;

        // Statistics
        public int ShipsBuilt { get; set; }
    }

    public class ShipBuildOrder
    {
        public string OrderId { get; set; } = string.Empty;
        public string BlueprintId { get; set; } = string.Empty;
        public float Progress { get; set; }
        public float BuildTime { get; set; }
        public int Priority { get; set; }
        public DateTime QueuedTime { get; set; }
    }

    public class ShipBlueprint
    {
        public string BlueprintId { get; set; } = string.Empty;
        public string ShipName { get; set; } = "Ship";
        public ShipClass ShipClass { get; set; } = ShipClass.Fighter;
        public float BuildTime { get; set; } = 10f;
        public int CargoCapacity { get; set; } = 0;
        public string DefaultAIBehavior { get; set; } = string.Empty;
    }

    public enum ShipyardClass
    {
        Small,      // Fighters, corvettes
        Medium,     // Up to frigates
        Large,      // All ships
        Military,   // Combat ships only
        Civilian    // Freighters, miners, transports
    }

    public enum ShipClass
    {
        Fighter,
        Corvette,
        Frigate,
        Destroyer,
        Cruiser,
        Capital,
        Freighter,
        Transport,
        Miner,
        Station
    }

    // Events
    public class ShipyardRegisteredEvent
    {
        public int ShipyardId { get; set; }
        public string Faction { get; set; } = string.Empty;
        public ShipyardClass ShipyardClass { get; set; }
    }

    public class ShipQueuedEvent
    {
        public int ShipyardId { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string BlueprintId { get; set; } = string.Empty;
    }

    public class ShipBuiltEvent
    {
        public int ShipyardId { get; set; }
        public int ShipId { get; set; }
        public string BlueprintId { get; set; } = string.Empty;
        public string Faction { get; set; } = string.Empty;
        public float BuildDuration { get; set; }
    }

    public class ShipBuildCancelledEvent
    {
        public int ShipyardId { get; set; }
        public string OrderId { get; set; } = string.Empty;
    }
}
