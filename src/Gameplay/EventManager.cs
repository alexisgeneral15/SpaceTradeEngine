using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Economy;

namespace SpaceTradeEngine.Gameplay
{
    /// <summary>
    /// Types of dynamic events that can occur in the game.
    /// </summary>
    public enum GameEventType
    {
        Shortage,        // Ware shortage at station - increases prices
        Surplus,         // Ware surplus at station - decreases prices
        Embargo,         // Trade ban between factions
        War,             // Conflict between factions
        Peace,           // War ends, relations improve
        Raid,            // Pirate attack on station/sector
        Discovery        // New trade route or station discovered
    }

    /// <summary>
    /// Represents a dynamic game event.
    /// </summary>
    public class GameEvent
    {
        public int Id { get; set; }
        public GameEventType Type { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime OccurredAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Event targets
        public string? TargetFactionA { get; set; }
        public string? TargetFactionB { get; set; }
        public int? TargetStationId { get; set; }
        public string? TargetWareId { get; set; }
        
        // Event parameters
        public float Magnitude { get; set; } = 1.0f; // Intensity multiplier
    }

    /// <summary>
    /// Manages dynamic events that affect the game world.
    /// Sprint 2: Events affect markets, factions, and player experience.
    /// </summary>
    public class EventManager
    {
        private EntityManager _entityManager;
        private MarketManager _marketManager;
        private FactionManager _factionManager;
        private List<GameEvent> _events = new List<GameEvent>();
        private int _nextEventId = 1;
        private Random _random = new Random();
        
        // Event generation timers
        private float _timeSinceLastEvent = 0f;
        private const float MinEventInterval = 60f; // 1 minute between events
        private const float MaxEventInterval = 180f; // 3 minutes max

        public EventManager(EntityManager entityManager, MarketManager marketManager, FactionManager factionManager)
        {
            _entityManager = entityManager;
            _marketManager = marketManager;
            _factionManager = factionManager;
        }

        /// <summary>
        /// Get all active events.
        /// </summary>
        public IEnumerable<GameEvent> GetActiveEvents()
        {
            return _events.Where(e => e.IsActive);
        }

        /// <summary>
        /// Get recent events (last 10).
        /// </summary>
        public IEnumerable<GameEvent> GetRecentEvents(int count = 10)
        {
            return _events.OrderByDescending(e => e.OccurredAt).Take(count);
        }

        /// <summary>
        /// Trigger a new event manually.
        /// </summary>
        public GameEvent TriggerEvent(GameEventType type, float magnitude = 1.0f, 
            string? factionA = null, string? factionB = null, int? stationId = null, string? wareId = null)
        {
            var gameEvent = new GameEvent
            {
                Id = _nextEventId++,
                Type = type,
                OccurredAt = DateTime.Now,
                Magnitude = magnitude,
                TargetFactionA = factionA,
                TargetFactionB = factionB,
                TargetStationId = stationId,
                TargetWareId = wareId
            };

            // Set title and description based on type
            switch (type)
            {
                case GameEventType.Shortage:
                    var wareName = _marketManager.GetWareTemplate(wareId ?? "")?.Name ?? "goods";
                    gameEvent.Title = $"{wareName} Shortage";
                    gameEvent.Description = $"Critical shortage of {wareName} causing price spike";
                    gameEvent.ExpiresAt = DateTime.Now.AddMinutes(5);
                    break;
                    
                case GameEventType.Embargo:
                    gameEvent.Title = $"Trade Embargo";
                    gameEvent.Description = $"{factionA} has imposed trade restrictions on {factionB}";
                    gameEvent.ExpiresAt = DateTime.Now.AddMinutes(10);
                    break;
                    
                case GameEventType.War:
                    gameEvent.Title = $"War Declared";
                    gameEvent.Description = $"{factionA} and {factionB} are now at war!";
                    gameEvent.ExpiresAt = DateTime.Now.AddMinutes(15);
                    break;
                    
                case GameEventType.Raid:
                    gameEvent.Title = "Pirate Raid";
                    gameEvent.Description = "Pirates attacking trade routes in the sector";
                    gameEvent.ExpiresAt = DateTime.Now.AddMinutes(3);
                    break;
                    
                case GameEventType.Surplus:
                    var surplusWare = _marketManager.GetWareTemplate(wareId ?? "")?.Name ?? "goods";
                    gameEvent.Title = $"{surplusWare} Surplus";
                    gameEvent.Description = $"Abundant {surplusWare} supply causing price drops";
                    gameEvent.ExpiresAt = DateTime.Now.AddMinutes(5);
                    break;
            }

            _events.Add(gameEvent);
            ApplyEventEffects(gameEvent);
            
            Console.WriteLine($"[Event] {gameEvent.Title}: {gameEvent.Description}");
            return gameEvent;
        }

        /// <summary>
        /// Apply immediate effects of an event.
        /// </summary>
        private void ApplyEventEffects(GameEvent gameEvent)
        {
            switch (gameEvent.Type)
            {
                case GameEventType.Shortage:
                    if (gameEvent.TargetStationId.HasValue && gameEvent.TargetWareId != null)
                    {
                        var market = _marketManager.GetMarket(gameEvent.TargetStationId.Value);
                        if (market != null && market.Goods.ContainsKey(gameEvent.TargetWareId))
                        {
                            var good = market.Goods[gameEvent.TargetWareId];
                            good.StockLevel = Math.Max(0, good.StockLevel - 50);
                            good.BasePrice *= (1.5f + gameEvent.Magnitude * 0.5f);
                            Console.WriteLine($"  → Market shock: {gameEvent.TargetWareId} price now {good.BasePrice:F0}");
                        }
                    }
                    break;

                case GameEventType.Embargo:
                    if (gameEvent.TargetFactionA != null && gameEvent.TargetFactionB != null)
                    {
                        _factionManager.SetRelation(gameEvent.TargetFactionA, gameEvent.TargetFactionB, 
                            -60f, TreatyType.Embargo);
                        Console.WriteLine($"  → Trade blocked between {gameEvent.TargetFactionA} and {gameEvent.TargetFactionB}");
                    }
                    break;

                case GameEventType.War:
                    if (gameEvent.TargetFactionA != null && gameEvent.TargetFactionB != null)
                    {
                        _factionManager.SetRelation(gameEvent.TargetFactionA, gameEvent.TargetFactionB, -90f);
                        Console.WriteLine($"  → Hostilities between {gameEvent.TargetFactionA} and {gameEvent.TargetFactionB}");
                    }
                    break;

                case GameEventType.Surplus:
                    if (gameEvent.TargetStationId.HasValue && gameEvent.TargetWareId != null)
                    {
                        var market = _marketManager.GetMarket(gameEvent.TargetStationId.Value);
                        if (market != null && market.Goods.ContainsKey(gameEvent.TargetWareId))
                        {
                            var good = market.Goods[gameEvent.TargetWareId];
                            good.StockLevel += 100;
                            good.BasePrice *= (0.7f - gameEvent.Magnitude * 0.1f);
                            Console.WriteLine($"  → Market glut: {gameEvent.TargetWareId} price now {good.BasePrice:F0}");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Update event system - expire old events, generate new ones.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _timeSinceLastEvent += deltaTime;

            // Expire old events
            var now = DateTime.Now;
            foreach (var evt in _events.Where(e => e.IsActive && e.ExpiresAt.HasValue))
            {
                if (now > evt.ExpiresAt.Value)
                {
                    evt.IsActive = false;
                    Console.WriteLine($"[Event] Expired: {evt.Title}");
                }
            }

            // Generate random events periodically
            if (_timeSinceLastEvent > MinEventInterval && _random.NextDouble() > 0.95)
            {
                GenerateRandomEvent();
                _timeSinceLastEvent = 0f;
            }
        }

        /// <summary>
        /// Generate a random event based on game state.
        /// </summary>
        private void GenerateRandomEvent()
        {
            var eventTypes = new[] { 
                GameEventType.Shortage, 
                GameEventType.Surplus, 
                GameEventType.Raid 
            };
            var type = eventTypes[_random.Next(eventTypes.Length)];

            // Get random station and ware
            var markets = _marketManager.Markets.ToList();
            if (markets.Count == 0) return;

            var randomMarket = markets[_random.Next(markets.Count)];
            var randomWare = randomMarket.Value.Goods.Keys.ToList();
            if (randomWare.Count == 0) return;

            var wareId = randomWare[_random.Next(randomWare.Count)];

            TriggerEvent(type, 
                magnitude: 0.5f + (float)_random.NextDouble() * 1.5f,
                stationId: randomMarket.Key,
                wareId: wareId);
        }
    }
}
