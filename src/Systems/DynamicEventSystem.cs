using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Dynamic Events System - generates random encounters, distress calls, pirate raids, etc.
    /// Creates a living, unpredictable universe with emergent gameplay.
    /// </summary>
    public class DynamicEventSystem : ECS.System
    {
        private readonly EventSystem _eventSystem;
        private readonly EntityManager _entityManager;
        private readonly Random _random = new();
        
        private float _eventTimer = 0f;
        private const float EVENT_CHECK_INTERVAL = 45f; // Check for events every 45 seconds
        private const float EVENT_CHANCE = 0.4f; // 40% chance per check

        private readonly List<DynamicEvent> _activeEvents = new();

        public DynamicEventSystem(EventSystem eventSystem, EntityManager entityManager)
        {
            _eventSystem = eventSystem;
            _entityManager = entityManager;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return false; // System-level only, no per-entity processing
        }

        public override void Update(float deltaTime)
        {
            _eventTimer += deltaTime;

            if (_eventTimer >= EVENT_CHECK_INTERVAL)
            {
                _eventTimer = 0f;

                // Roll for random event
                if (_random.NextDouble() < EVENT_CHANCE)
                {
                    TriggerRandomEvent();
                }
            }

            // Update active event timers
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var evt = _activeEvents[i];
                evt.TimeRemaining -= deltaTime;

                if (evt.TimeRemaining <= 0)
                {
                    ExpireEvent(evt);
                    _activeEvents.RemoveAt(i);
                }
            }
        }

        private void TriggerRandomEvent()
        {
            var eventType = (DynamicEventType)_random.Next(0, 8);

            switch (eventType)
            {
                case DynamicEventType.DistressCall:
                    SpawnDistressCall();
                    break;
                case DynamicEventType.PirateRaid:
                    SpawnPirateRaid();
                    break;
                case DynamicEventType.MerchantConvoy:
                    SpawnMerchantConvoy();
                    break;
                case DynamicEventType.AsteroidField:
                    SpawnAsteroidField();
                    break;
                case DynamicEventType.AncientDerelict:
                    SpawnAncientDerelict();
                    break;
                case DynamicEventType.FactionSkirmish:
                    SpawnFactionSkirmish();
                    break;
                case DynamicEventType.SpacePhenomenon:
                    SpawnSpacePhenomenon();
                    break;
                case DynamicEventType.TravelingMerchant:
                    SpawnTravelingMerchant();
                    break;
            }
        }

        private void SpawnDistressCall()
        {
            var position = GetRandomSpacePosition();
            
            // Spawn damaged trader ship
            var distressShip = _entityManager.CreateEntity($"Distress_Ship_{Guid.NewGuid().ToString().Substring(0, 6)}");
            distressShip.AddComponent(new TransformComponent { Position = position });
            distressShip.AddComponent(new VelocityComponent { LinearVelocity = Vector2.Zero });
            distressShip.AddComponent(new CollisionComponent { Radius = 22f });
            distressShip.AddComponent(new HealthComponent { MaxHealth = 80f, CurrentHealth = 20f }); // Damaged!
            distressShip.AddComponent(new FactionComponent("civilian", "Civilian"));
            distressShip.AddComponent(new TagComponent("distress"));
            distressShip.AddComponent(new CargoComponent { MaxVolume = 50 });
            
            // Add valuable cargo as reward
            var cargo = distressShip.GetComponent<CargoComponent>();
            cargo.Add("rare_artifacts", 5, 1.0f);

            // Spawn pirates attacking them
            for (int i = 0; i < 2; i++)
            {
                var pirate = _entityManager.CreateEntity($"Pirate_Raider_{Guid.NewGuid().ToString().Substring(0, 6)}");
                var offset = new Vector2(_random.Next(-150, 150), _random.Next(-150, 150));
                pirate.AddComponent(new TransformComponent { Position = position + offset });
                pirate.AddComponent(new VelocityComponent());
                pirate.AddComponent(new CollisionComponent { Radius = 18f });
                pirate.AddComponent(new HealthComponent { MaxHealth = 100f });
                pirate.AddComponent(new FactionComponent("pirates", "Pirates"));
                pirate.AddComponent(new TagComponent("hostile"));
                pirate.AddComponent(new WeaponComponent { Damage = 10f, Range = 700f, Cooldown = 0.6f });
                pirate.AddComponent(new RankComponent { CurrentRank = Rank.Regular, EntityType = EntityType.ClanMember });
                pirate.AddComponent(new SpaceTradeEngine.AI.AIBehaviorComponent 
                { 
                    DefaultBehavior = SpaceTradeEngine.AI.AIBehaviorType.Attack, 
                    Aggressiveness = 0.8f 
                });
            }

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.DistressCall,
                Location = position,
                Title = "Distress Call Detected",
                Description = "A civilian vessel is under attack by pirates!",
                TimeRemaining = 180f,
                Reward = 1500f,
                InvolvedEntities = new List<int> { distressShip.Id }
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnPirateRaid()
        {
            var position = GetRandomSpacePosition();
            int pirateCount = _random.Next(3, 6);

            var raiders = new List<int>();

            for (int i = 0; i < pirateCount; i++)
            {
                var pirate = _entityManager.CreateEntity($"Pirate_Squadron_{Guid.NewGuid().ToString().Substring(0, 6)}");
                var offset = new Vector2(_random.Next(-200, 200), _random.Next(-200, 200));
                pirate.AddComponent(new TransformComponent { Position = position + offset });
                pirate.AddComponent(new VelocityComponent());
                pirate.AddComponent(new CollisionComponent { Radius = 20f });
                pirate.AddComponent(new HealthComponent { MaxHealth = 120f });
                pirate.AddComponent(new FactionComponent("pirates", "Red Corsairs"));
                pirate.AddComponent(new TagComponent("hostile"));
                pirate.AddComponent(new WeaponComponent { Damage = 12f, Range = 800f, Cooldown = 0.5f });
                
                var rank = i == 0 ? Rank.Veteran : (i < 2 ? Rank.Experienced : Rank.Regular);
                pirate.AddComponent(new RankComponent { CurrentRank = rank, EntityType = EntityType.ClanMember });
                
                pirate.AddComponent(new SpaceTradeEngine.AI.AIBehaviorComponent 
                { 
                    DefaultBehavior = SpaceTradeEngine.AI.AIBehaviorType.Attack, 
                    Aggressiveness = 0.9f 
                });

                raiders.Add(pirate.Id);
            }

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.PirateRaid,
                Location = position,
                Title = "Pirate Squadron Detected",
                Description = $"A dangerous pirate squadron of {pirateCount} ships has been spotted!",
                TimeRemaining = 240f,
                Reward = 3000f + (pirateCount * 500f),
                InvolvedEntities = raiders
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnMerchantConvoy()
        {
            var position = GetRandomSpacePosition();
            int shipCount = _random.Next(3, 5);

            for (int i = 0; i < shipCount; i++)
            {
                var merchant = _entityManager.CreateEntity($"Merchant_Convoy_{Guid.NewGuid().ToString().Substring(0, 6)}");
                var offset = new Vector2(i * 100, i * 50);
                merchant.AddComponent(new TransformComponent { Position = position + offset });
                merchant.AddComponent(new VelocityComponent());
                merchant.AddComponent(new CollisionComponent { Radius = 24f });
                merchant.AddComponent(new HealthComponent { MaxHealth = 150f });
                merchant.AddComponent(new FactionComponent("trade_guild", "Trade Guild"));
                merchant.AddComponent(new TagComponent("trader"));
                merchant.AddComponent(new CargoComponent { MaxVolume = 100 });
                merchant.AddComponent(new RankComponent { CurrentRank = Rank.Experienced, EntityType = EntityType.Civilian });
                merchant.AddComponent(new TraderAIComponent { MinProfitMargin = 0.12f });
            }

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.MerchantConvoy,
                Location = position,
                Title = "Merchant Convoy Passing",
                Description = "A wealthy merchant convoy is traveling through the sector.",
                TimeRemaining = 120f,
                Reward = 0f // Trade opportunity, not combat
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnAsteroidField()
        {
            var position = GetRandomSpacePosition();
            int asteroidCount = _random.Next(8, 15);

            for (int i = 0; i < asteroidCount; i++)
            {
                var angle = (i / (float)asteroidCount) * MathHelper.TwoPi;
                var radius = _random.Next(100, 300);
                var offset = new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius
                );

                var asteroid = _entityManager.CreateEntity($"Asteroid_{Guid.NewGuid().ToString().Substring(0, 6)}");
                asteroid.AddComponent(new TransformComponent { Position = position + offset });
                asteroid.AddComponent(new VelocityComponent { LinearVelocity = new Vector2(_random.Next(-20, 20), _random.Next(-20, 20)) });
                asteroid.AddComponent(new CollisionComponent { Radius = 30f });
                asteroid.AddComponent(new HealthComponent { MaxHealth = 200f });
                asteroid.AddComponent(new TagComponent("asteroid"));
                asteroid.AddComponent(new ResourceNodeComponent 
                { 
                    ResourceType = "ore", 
                    Quantity = _random.Next(500, 1000),
                    MaxQuantity = 1000
                });
            }

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.AsteroidField,
                Location = position,
                Title = "Asteroid Field Detected",
                Description = "Rich mineral deposits detected in nearby asteroids!",
                TimeRemaining = 300f,
                Reward = 0f // Mining opportunity
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnAncientDerelict()
        {
            var position = GetRandomSpacePosition();

            var derelict = _entityManager.CreateEntity($"Ancient_Derelict_{Guid.NewGuid().ToString().Substring(0, 6)}");
            derelict.AddComponent(new TransformComponent { Position = position });
            derelict.AddComponent(new VelocityComponent { LinearVelocity = Vector2.Zero });
            derelict.AddComponent(new CollisionComponent { Radius = 40f, IsTrigger = true });
            derelict.AddComponent(new TagComponent("derelict"));
            derelict.AddComponent(new CargoComponent { MaxVolume = 200 });
            
            // Load with valuable loot
            var cargo = derelict.GetComponent<CargoComponent>();
            cargo.Add("ancient_tech", 3, 1.0f);
            cargo.Add("rare_artifacts", 5, 1.0f);
            cargo.Credits = _random.Next(5000, 15000);

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.AncientDerelict,
                Location = position,
                Title = "Ancient Derelict Found",
                Description = "An ancient ship drifts silently... who knows what treasures it holds?",
                TimeRemaining = 200f,
                Reward = 0f, // Loot is the reward
                InvolvedEntities = new List<int> { derelict.Id }
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnFactionSkirmish()
        {
            var position = GetRandomSpacePosition();
            
            // Two factions fighting
            string faction1 = "human_federation";
            string faction2 = "sirak_empire";

            for (int i = 0; i < 3; i++)
            {
                // Faction 1 ships
                var ship1 = _entityManager.CreateEntity($"Faction1_Fighter_{i}");
                ship1.AddComponent(new TransformComponent { Position = position + new Vector2(-200 + i * 80, -100) });
                ship1.AddComponent(new VelocityComponent());
                ship1.AddComponent(new CollisionComponent { Radius = 18f });
                ship1.AddComponent(new HealthComponent { MaxHealth = 100f });
                ship1.AddComponent(new FactionComponent(faction1, "Human Federation"));
                ship1.AddComponent(new WeaponComponent { Damage = 10f, Range = 750f });
                ship1.AddComponent(new RankComponent { CurrentRank = Rank.Regular, EntityType = EntityType.Military });

                // Faction 2 ships
                var ship2 = _entityManager.CreateEntity($"Faction2_Fighter_{i}");
                ship2.AddComponent(new TransformComponent { Position = position + new Vector2(-200 + i * 80, 100) });
                ship2.AddComponent(new VelocityComponent());
                ship2.AddComponent(new CollisionComponent { Radius = 18f });
                ship2.AddComponent(new HealthComponent { MaxHealth = 100f });
                ship2.AddComponent(new FactionComponent(faction2, "Sirak Empire"));
                ship2.AddComponent(new WeaponComponent { Damage = 10f, Range = 750f });
                ship2.AddComponent(new RankComponent { CurrentRank = Rank.Regular, EntityType = EntityType.Military });
            }

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.FactionSkirmish,
                Location = position,
                Title = "Faction Battle in Progress",
                Description = $"{faction1} and {faction2} forces are engaged in combat!",
                TimeRemaining = 180f,
                Reward = 2000f // Help either side
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnSpacePhenomenon()
        {
            var position = GetRandomSpacePosition();

            // Create dangerous/rewarding space anomaly
            var anomaly = _entityManager.CreateEntity($"Anomaly_{Guid.NewGuid().ToString().Substring(0, 6)}");
            anomaly.AddComponent(new TransformComponent { Position = position });
            anomaly.AddComponent(new CollisionComponent { Radius = 80f, IsTrigger = true });
            anomaly.AddComponent(new TagComponent("anomaly"));
            // Could add damage-over-time or buff effects here

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.SpacePhenomenon,
                Location = position,
                Title = "Space Anomaly Detected",
                Description = "Strange energy readings detected. Approach with caution...",
                TimeRemaining = 150f,
                Reward = 0f,
                InvolvedEntities = new List<int> { anomaly.Id }
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void SpawnTravelingMerchant()
        {
            var position = GetRandomSpacePosition();

            var merchant = _entityManager.CreateEntity($"Special_Merchant_{Guid.NewGuid().ToString().Substring(0, 6)}");
            merchant.AddComponent(new TransformComponent { Position = position });
            merchant.AddComponent(new VelocityComponent { LinearVelocity = new Vector2(50, 30) });
            merchant.AddComponent(new CollisionComponent { Radius = 25f });
            merchant.AddComponent(new HealthComponent { MaxHealth = 200f });
            merchant.AddComponent(new FactionComponent("neutral", "Wandering Merchant"));
            merchant.AddComponent(new TagComponent("special_trader"));
            merchant.AddComponent(new CargoComponent { MaxVolume = 150 });
            merchant.AddComponent(new RankComponent { CurrentRank = Rank.Elite, EntityType = EntityType.Civilian });
            
            // Add rare goods
            var cargo = merchant.GetComponent<CargoComponent>();
            cargo.Add("legendary_weapon", 1, 1.0f);
            cargo.Add("shield_booster", 2, 1.0f);

            var evt = new DynamicEvent
            {
                Type = DynamicEventType.TravelingMerchant,
                Location = position,
                Title = "Mysterious Merchant",
                Description = "A wandering merchant with rare goods has been spotted!",
                TimeRemaining = 120f,
                Reward = 0f,
                InvolvedEntities = new List<int> { merchant.Id }
            };

            _activeEvents.Add(evt);

            _eventSystem.Publish(new DynamicEventTriggeredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private void ExpireEvent(DynamicEvent evt)
        {
            // Clean up event-related entities
            foreach (var entityId in evt.InvolvedEntities)
            {
                var entity = _entityManager.GetEntity(entityId);
                if (entity != null && entity.HasComponent<TagComponent>())
                {
                    var tagComp = entity.GetComponent<TagComponent>();
                    if (tagComp.Tags.Contains("distress") || tagComp.Tags.Contains("derelict") || tagComp.Tags.Contains("anomaly"))
                    {
                        _entityManager.DestroyEntity(entityId);
                    }
                }
            }

            _eventSystem.Publish(new DynamicEventExpiredEvent
            {
                Event = evt,
                Timestamp = DateTime.UtcNow
            });
        }

        private Vector2 GetRandomSpacePosition()
        {
            return new Vector2(
                _random.Next(-2500, 2500),
                _random.Next(-2500, 2500)
            );
        }

        public List<DynamicEvent> GetActiveEvents() => new List<DynamicEvent>(_activeEvents);
    }

    public class DynamicEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DynamicEventType Type { get; set; }
        public Vector2 Location { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float TimeRemaining { get; set; }
        public float Reward { get; set; }
        public List<int> InvolvedEntities { get; set; } = new();
    }

    public enum DynamicEventType
    {
        DistressCall,       // Save civilian ship from pirates
        PirateRaid,         // Large pirate attack
        MerchantConvoy,     // Trading opportunity
        AsteroidField,      // Mining opportunity
        AncientDerelict,    // Loot opportunity
        FactionSkirmish,    // Two factions fighting
        SpacePhenomenon,    // Dangerous anomaly
        TravelingMerchant   // Rare goods vendor
    }

    public class DynamicEventTriggeredEvent
    {
        public DynamicEvent Event { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DynamicEventExpiredEvent
    {
        public DynamicEvent Event { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
