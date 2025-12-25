using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Economy;
using SpaceTradeEngine.AI;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Autonomous trader AI system - NPCs buy low, sell high, transport goods.
    /// Creates living economy like Unending Galaxy's civilian traders.
    /// </summary>
    public class TraderAISystem : ECS.System
    {
        private readonly MarketManager _markets;
        private readonly StationSystem _stationSystem;
        private readonly EntityManager _entityManager;
        private readonly DiplomacySystem? _diplomacySystem;
        private RankSystem? _rankSystem;
        private readonly Dictionary<int, TraderAI> _traders = new();
        private readonly Random _random = new();

        public IReadOnlyDictionary<int, TraderAI> Traders => _traders;

        public TraderAISystem(MarketManager markets, StationSystem stationSystem, EntityManager entityManager, DiplomacySystem? diplomacySystem = null)
        {
            _markets = markets;
            _stationSystem = stationSystem;
            _entityManager = entityManager;
            _diplomacySystem = diplomacySystem;
        }

        public void SetRankSystem(RankSystem rankSystem)
        {
            _rankSystem = rankSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<TraderAIComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var traderComp = entity.GetComponent<TraderAIComponent>();
                if (traderComp == null) continue;

                if (!_traders.TryGetValue(entity.Id, out var traderAI))
                {
                    traderAI = new TraderAI { EntityId = entity.Id };
                    _traders[entity.Id] = traderAI;
                }

                traderAI.TimeSinceLastDecision += deltaTime;

                // Make decisions every 3 seconds
                if (traderAI.TimeSinceLastDecision >= 3f)
                {
                    UpdateTraderBehavior(entity, traderAI, traderComp);
                    traderAI.TimeSinceLastDecision = 0f;
                }
            }
        }

        private void UpdateTraderBehavior(Entity entity, TraderAI traderAI, TraderAIComponent traderComp)
        {
            var transform = entity.GetComponent<TransformComponent>();
            var cargo = entity.GetComponent<CargoComponent>();
            
            if (transform == null || cargo == null)
                return;

            switch (traderAI.CurrentState)
            {
                case TraderState.Idle:
                    HandleIdleState(entity, traderAI, traderComp, cargo, transform);
                    break;

                case TraderState.SeekingBuyStation:
                    HandleSeekingBuyState(entity, traderAI, transform);
                    break;

                case TraderState.TravelingToBuy:
                    HandleTravelingToBuyState(entity, traderAI, transform);
                    break;

                case TraderState.Buying:
                    HandleBuyingState(entity, traderAI, cargo, traderComp);
                    break;

                case TraderState.SeekingSellStation:
                    HandleSeekingSellState(entity, traderAI, cargo);
                    break;

                case TraderState.TravelingToSell:
                    HandleTravelingToSellState(entity, traderAI, transform);
                    break;

                case TraderState.Selling:
                    HandleSellingState(entity, traderAI, cargo);
                    break;
            }
        }

        private void HandleIdleState(Entity entity, TraderAI traderAI, TraderAIComponent traderComp, 
            CargoComponent cargo, TransformComponent transform)
        {
            // Find profitable trade opportunity
            var tradeRoute = FindBestTradeRoute(transform.Position, (int)cargo.Capacity, traderComp.MinProfitMargin);

            if (tradeRoute != null)
            {
                traderAI.CurrentRoute = tradeRoute;
                traderAI.CurrentState = TraderState.SeekingBuyStation;
                traderAI.TradeRouteStartTime = DateTime.UtcNow;
            }
            else
            {
                // No profitable routes, wander or wait
                WanderRandomly(entity, transform);
            }
        }

        private void HandleSeekingBuyState(Entity entity, TraderAI traderAI, TransformComponent transform)
        {
            if (traderAI.CurrentRoute == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var buyStation = _stationSystem.GetStation(traderAI.CurrentRoute.BuyStationId);
            if (buyStation == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                traderAI.CurrentRoute = null;
                return;
            }

            // Navigate to buy station
            var distance = Vector2.Distance(transform.Position, buyStation.Position);
            
            if (distance < 50f)
            {
                // Check if we can dock based on faction relations
                if (!CanDockAt(entity, buyStation))
                {
                    // Hostile station, abort route
                    traderAI.CurrentState = TraderState.Idle;
                    traderAI.CurrentRoute = null;
                    return;
                }

                // Arrived, try to dock
                if (_stationSystem.DockShip(buyStation.StationId, entity))
                {
                    traderAI.CurrentState = TraderState.Buying;
                    traderAI.CurrentStationId = buyStation.StationId;
                }
            }
            else
            {
                traderAI.CurrentState = TraderState.TravelingToBuy;
                MoveTowards(entity, transform, buyStation.Position);
            }
        }

        private void HandleTravelingToBuyState(Entity entity, TraderAI traderAI, TransformComponent transform)
        {
            if (traderAI.CurrentRoute == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var buyStation = _stationSystem.GetStation(traderAI.CurrentRoute.BuyStationId);
            if (buyStation == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var distance = Vector2.Distance(transform.Position, buyStation.Position);
            
            if (distance < 50f)
            {
                traderAI.CurrentState = TraderState.SeekingBuyStation;
            }
            else
            {
                MoveTowards(entity, transform, buyStation.Position);
            }
        }

        private void HandleBuyingState(Entity entity, TraderAI traderAI, CargoComponent cargo, TraderAIComponent traderComp)
        {
            if (traderAI.CurrentRoute == null || traderAI.CurrentStationId == null)
            {
                _stationSystem.UndockShip(traderAI.CurrentStationId ?? 0, entity.Id);
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var route = traderAI.CurrentRoute;
            var rank = entity.GetComponent<RankComponent>();
            
            // Apply trader rank bonus to buy price (experienced traders negotiate better)
            float buyPriceModifier = 1f;
            if (rank != null && rank.EntityType == EntityType.Civilian)
            {
                // Rookie: pays 5% more, Elite: pays 60% less
                buyPriceModifier = 1f - rank.TradeMarginBonus;
            }
            
            // Buy goods
            int availableCapacity = (int)(cargo.Capacity - cargo.UsedCapacity);
            int quantityToBuy = Math.Min(route.Quantity, availableCapacity);
            if (quantityToBuy > 0)
            {
                bool success = _stationSystem.TradeWithStation(
                    traderAI.CurrentStationId.Value,
                    entity,
                    route.WareId,
                    quantityToBuy,
                    buying: true
                );

                if (success)
                {
                    // Track actual cost with rank discount
                    float actualCost = route.BuyPrice * quantityToBuy * buyPriceModifier;
                    traderAI.TotalProfit -= actualCost;
                    traderAI.LastPurchaseCost = actualCost; // Store for XP calculation
                    traderAI.TradeCount++;
                }
            }

            // Undock and head to sell station
            if (traderAI.CurrentStationId.HasValue)
                _stationSystem.UndockShip(traderAI.CurrentStationId.Value, entity.Id);
            traderAI.CurrentState = TraderState.SeekingSellStation;
            traderAI.CurrentStationId = null;
        }

        private void HandleSeekingSellState(Entity entity, TraderAI traderAI, CargoComponent cargo)
        {
            if (traderAI.CurrentRoute == null)
            {
                if (traderAI.CurrentStationId.HasValue)
                    _stationSystem.UndockShip(traderAI.CurrentStationId.Value, entity.Id);
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            // Check if we have goods to sell
            if (!cargo.Contains(traderAI.CurrentRoute.WareId, 1))
            {
                if (traderAI.CurrentStationId.HasValue)
                    _stationSystem.UndockShip(traderAI.CurrentStationId.Value, entity.Id);
                traderAI.CurrentState = TraderState.Idle;
                traderAI.CurrentRoute = null;
                return;
            }

            traderAI.CurrentState = TraderState.TravelingToSell;
        }

        private void HandleTravelingToSellState(Entity entity, TraderAI traderAI, TransformComponent transform)
        {
            if (traderAI.CurrentRoute == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var sellStation = _stationSystem.GetStation(traderAI.CurrentRoute.SellStationId);
            if (sellStation == null)
            {
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var distance = Vector2.Distance(transform.Position, sellStation.Position);
            
            if (distance < 50f)
            {
                // Check faction relations before docking
                if (!CanDockAt(entity, sellStation))
                {
                    // Can't sell here, abort
                    traderAI.CurrentState = TraderState.Idle;
                    traderAI.CurrentRoute = null;
                    return;
                }

                // Arrived, try to dock
                if (_stationSystem.DockShip(sellStation.StationId, entity))
                {
                    traderAI.CurrentState = TraderState.Selling;
                    traderAI.CurrentStationId = sellStation.StationId;
                }
            }
            else
            {
                MoveTowards(entity, transform, sellStation.Position);
            }
        }

        private void HandleSellingState(Entity entity, TraderAI traderAI, CargoComponent cargo)
        {
            if (traderAI.CurrentRoute == null || traderAI.CurrentStationId == null)
            {
                _stationSystem.UndockShip(traderAI.CurrentStationId ?? 0, entity.Id);
                traderAI.CurrentState = TraderState.Idle;
                return;
            }

            var route = traderAI.CurrentRoute;
            var rank = entity.GetComponent<RankComponent>();
            
            // Apply trader rank bonus to sell price (experienced traders negotiate better)
            float sellPriceModifier = 1f;
            if (rank != null && rank.EntityType == EntityType.Civilian)
            {
                // Rookie: sells 5% lower, Elite: sells 60% higher
                sellPriceModifier = 1f + rank.TradeMarginBonus;
            }
            
            // Sell all goods of this type
            var cargoItem = cargo.Items.GetValueOrDefault(route.WareId);
            if (cargoItem != null && cargoItem.Quantity > 0)
            {
                bool success = _stationSystem.TradeWithStation(
                    traderAI.CurrentStationId.Value,
                    entity,
                    route.WareId,
                    cargoItem.Quantity,
                    buying: false
                );

                if (success)
                {
                    // Track actual revenue with rank bonus
                    float actualRevenue = route.SellPrice * cargoItem.Quantity * sellPriceModifier;
                    traderAI.TotalProfit += actualRevenue;
                    traderAI.SuccessfulTrades++;
                    
                    // Award trade XP based on profit margin
                    if (_rankSystem != null && rank != null && traderAI.LastPurchaseCost > 0)
                    {
                        float profitMargin = (actualRevenue - traderAI.LastPurchaseCost) / traderAI.LastPurchaseCost;
                        _rankSystem.AwardTradeExperience(entity, profitMargin, cargoItem.Quantity);
                    }
                }
            }

            // Complete route
            traderAI.CompletedRoutes++;
            traderAI.CurrentRoute = null;

            // Undock and return to idle
            if (traderAI.CurrentStationId.HasValue)
                _stationSystem.UndockShip(traderAI.CurrentStationId.Value, entity.Id);
            traderAI.CurrentState = TraderState.Idle;
            traderAI.CurrentStationId = null;
        }

        /// <summary>
        /// Find most profitable trade route within range.
        /// Respects faction relations - avoids hostile factions, prefers friendly/allied ones.
        /// </summary>
        private TradeRoute? FindBestTradeRoute(Vector2 position, int cargoCapacity, float minMargin)
        {
            var nearbyStations = _stationSystem.GetStationsInRange(position, 2000f);
            if (nearbyStations.Count < 2)
                return null;

            TradeRoute? bestRoute = null;
            float bestProfit = 0f;

            foreach (var buyStation in nearbyStations)
            {
                if (buyStation.Market == null) continue;

                foreach (var (wareId, good) in buyStation.Market.Goods)
                {
                    if (good.StockLevel < 5) continue; // Skip low stock

                    foreach (var sellStation in nearbyStations)
                    {
                        if (sellStation.StationId == buyStation.StationId) continue;
                        if (sellStation.Market == null) continue;

                        var sellPrice = _markets.GetPrice(sellStation.StationId, wareId);
                        if (sellPrice == null) continue;

                        // Check diplomacy relations
                        float relationBonus = GetRelationBonus(buyStation.Faction, sellStation.Faction);
                        if (relationBonus < 0) continue; // Skip hostile stations

                        float margin = (sellPrice.Value - good.CurrentPrice) / good.CurrentPrice;
                        if (margin < minMargin) continue;

                        int quantity = Math.Min(good.StockLevel, cargoCapacity);
                        float profit = quantity * (sellPrice.Value - good.CurrentPrice);
                        
                        // Apply relation bonus to profit (prefer friendly/allied factions)
                        profit *= (1f + relationBonus);

                        if (profit > bestProfit)
                        {
                            bestProfit = profit;
                            bestRoute = new TradeRoute
                            {
                                BuyStationId = buyStation.StationId,
                                SellStationId = sellStation.StationId,
                                WareId = wareId,
                                Quantity = quantity,
                                BuyPrice = good.CurrentPrice,
                                SellPrice = sellPrice.Value,
                                ExpectedProfit = profit
                            };
                        }
                    }
                }
            }

            return bestRoute;
        }

        private void MoveTowards(Entity entity, TransformComponent transform, Vector2 target)
        {
            var velocity = entity.GetComponent<VelocityComponent>();
            if (velocity == null) return;

            var direction = target - transform.Position;
            if (direction.LengthSquared() > 0.1f)
            {
                direction.Normalize();
                velocity.LinearVelocity = direction * 150f; // Trader speed
            }
        }

        private void WanderRandomly(Entity entity, TransformComponent transform)
        {
            var velocity = entity.GetComponent<VelocityComponent>();
            if (velocity == null) return;

            // Random wandering
            if (_random.Next(100) < 5) // 5% chance to change direction
            {
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                velocity.LinearVelocity = new Vector2(
                    (float)Math.Cos(angle) * 50f,
                    (float)Math.Sin(angle) * 50f
                );
            }
        }

        public TraderAI? GetTraderAI(int entityId)
        {
            return _traders.GetValueOrDefault(entityId);
        }

        /// <summary>
        /// Get relation bonus multiplier for trade profitability.
        /// Returns -1 for hostile (blocks trade), 0 for neutral, up to +0.5 for allied.
        /// </summary>
        private float GetRelationBonus(string faction1, string faction2)
        {
            if (_diplomacySystem == null) return 0f; // No diplomacy system, allow all trades
            if (string.IsNullOrEmpty(faction1) || string.IsNullOrEmpty(faction2)) return 0f;
            if (faction1 == faction2) return 0.2f; // Same faction bonus

            var standing = _diplomacySystem.GetStanding(faction1, faction2);
            
            // Hostile factions refuse trade
            if (standing < -50f) return -1f;
            
            // Unfriendly factions reduce profitability
            if (standing < -25f) return -0.1f;
            
            // Neutral
            if (standing < 25f) return 0f;
            
            // Friendly factions get bonus
            if (standing < 75f) return 0.2f;
            
            // Allied factions get best bonus
            return 0.5f;
        }

        /// <summary>
        /// Check if trader can dock at station based on faction relations.
        /// </summary>
        private bool CanDockAt(Entity trader, Station station)
        {
            if (_diplomacySystem == null) return true; // No diplomacy, allow all
            
            var traderFaction = trader.GetComponent<FactionComponent>();
            if (traderFaction == null) return true; // No faction, allow
            if (string.IsNullOrEmpty(station.Faction)) return true; // Neutral station
            
            // Check if hostile
            return !_diplomacySystem.AreHostile(traderFaction.FactionId, station.Faction);
        }
    }

    /// <summary>
    /// Component marking entity as autonomous trader.
    /// </summary>
    public class TraderAIComponent : Component
    {
        public float MinProfitMargin { get; set; } = 0.15f; // 15% minimum
        public string TraderType { get; set; } = "Merchant"; // Merchant, Smuggler, etc.
        public float RiskTolerance { get; set; } = 0.5f;
    }

    /// <summary>
    /// Trader AI state and memory.
    /// </summary>
    public class TraderAI
    {
        public int EntityId { get; set; }
        public TraderState CurrentState { get; set; } = TraderState.Idle;
        public TradeRoute? CurrentRoute { get; set; }
        public int? CurrentStationId { get; set; }
        
        public float TimeSinceLastDecision { get; set; }
        public DateTime TradeRouteStartTime { get; set; }
        
        // Statistics
        public int CompletedRoutes { get; set; }
        public int TradeCount { get; set; }
        public int SuccessfulTrades { get; set; }
        public float TotalProfit { get; set; }
        public float LastPurchaseCost { get; set; } // Track for XP calculation
    }

    public enum TraderState
    {
        Idle,
        SeekingBuyStation,
        TravelingToBuy,
        Buying,
        SeekingSellStation,
        TravelingToSell,
        Selling
    }

    public class TradeRoute
    {
        public int BuyStationId { get; set; }
        public int SellStationId { get; set; }
        public string WareId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public float BuyPrice { get; set; }
        public float SellPrice { get; set; }
        public float ExpectedProfit { get; set; }
    }
}
