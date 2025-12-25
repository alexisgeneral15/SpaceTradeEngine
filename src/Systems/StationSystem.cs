using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Economy;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages space stations: docking, trading, services, and station operations.
    /// Stations act as economic hubs, production facilities, and ship services.
    /// </summary>
    public class StationSystem : ECS.System
    {
        private readonly MarketManager _markets;
        private readonly EventSystem _eventSystem;
        private readonly DiplomacySystem? _diplomacySystem;
        private readonly Dictionary<int, Station> _stations = new();
        private int _nextStationId = 1;

        public IReadOnlyDictionary<int, Station> Stations => _stations;

        public StationSystem(MarketManager markets, EventSystem eventSystem, DiplomacySystem? diplomacySystem = null)
        {
            _markets = markets;
            _eventSystem = eventSystem;
            _diplomacySystem = diplomacySystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<StationComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var stationComp = entity.GetComponent<StationComponent>();
                if (stationComp == null) continue;

                if (!_stations.TryGetValue(stationComp.StationId, out var station))
                    continue;

                // Update docking cooldowns
                foreach (var dockedShip in station.DockedShips.ToList())
                {
                    dockedShip.DockingTime += deltaTime;
                    
                    // Auto-undock after max time
                    if (dockedShip.DockingTime >= station.MaxDockingTime)
                    {
                        UndockShip(stationComp.StationId, dockedShip.EntityId);
                    }
                }

                // Process service queue
                ProcessServices(station, deltaTime);

                // Update market prices
                if (station.Market != null)
                {
                    station.Market.Update(deltaTime);
                }
            }
        }

        /// <summary>
        /// Create a new station at specified position.
        /// </summary>
        public int CreateStation(Entity entity, string stationType, string name, string faction)
        {
            var stationId = _nextStationId++;
            
            var station = new Station
            {
                StationId = stationId,
                EntityId = entity.Id,
                Name = name,
                StationType = stationType,
                Faction = faction,
                Position = entity.GetComponent<TransformComponent>()?.Position ?? Vector2.Zero
            };

            // Initialize market for trade stations
            if (stationType == "TradePost" || stationType == "Factory")
            {
                var market = new StationMarket { StationId = stationId };
                station.Market = market;
                _markets.RegisterStationMarket(stationId, market);
            }

            _stations[stationId] = station;

            var stationComp = new StationComponent
            {
                StationId = stationId,
                StationType = stationType,
                Faction = faction,
                MaxDockedShips = GetMaxDockedShips(stationType)
            };

            entity.AddComponent(stationComp);

            _eventSystem.Publish(new StationCreatedEvent
            {
                StationId = stationId,
                StationType = stationType,
                Faction = faction,
                Position = station.Position
            });

            return stationId;
        }

        /// <summary>
        /// Request docking at a station.
        /// </summary>
        public bool DockShip(int stationId, Entity ship)
        {
            if (!_stations.TryGetValue(stationId, out var station))
                return false;

            if (station.DockedShips.Count >= station.MaxDockedShips)
                return false;

            var shipFaction = ship.GetComponent<FactionComponent>()?.FactionId;
            if (shipFaction != null && !CanDock(station.Faction, shipFaction))
                return false;

            if (station.DockedShips.Any(d => d.EntityId == ship.Id))
                return false;

            var dockedShip = new DockedShip
            {
                EntityId = ship.Id,
                DockingTime = 0f,
                ShipName = ship.Name
            };

            station.DockedShips.Add(dockedShip);

            // Disable ship movement while docked
            var movement = ship.GetComponent<VelocityComponent>();
            if (movement != null)
            {
                movement.LinearVelocity = Vector2.Zero;
            }

            _eventSystem.Publish(new ShipDockedEvent
            {
                StationId = stationId,
                ShipId = ship.Id,
                Timestamp = DateTime.UtcNow
            });

            return true;
        }

        /// <summary>
        /// Undock a ship from station.
        /// </summary>
        public bool UndockShip(int stationId, int shipId)
        {
            if (!_stations.TryGetValue(stationId, out var station))
                return false;

            var docked = station.DockedShips.FirstOrDefault(d => d.EntityId == shipId);
            if (docked == null)
                return false;

            station.DockedShips.Remove(docked);

            _eventSystem.Publish(new ShipUndockedEvent
            {
                StationId = stationId,
                ShipId = shipId,
                DockingDuration = docked.DockingTime
            });

            return true;
        }

        /// <summary>
        /// Trade goods between ship and station.
        /// </summary>
        public bool TradeWithStation(int stationId, Entity ship, string wareId, int quantity, bool buying)
        {
            if (!_stations.TryGetValue(stationId, out var station))
                return false;

            if (!station.DockedShips.Any(d => d.EntityId == ship.Id))
                return false; // Must be docked

            var cargo = ship.GetComponent<CargoComponent>();
            if (cargo == null)
                return false;

            if (station.Market == null)
                return false;

            if (buying)
            {
                // Buy from station
                if (!_markets.CanBuy(stationId, wareId, quantity))
                    return false;

                float cost = _markets.Buy(stationId, wareId, quantity);
                if (cost > cargo.Credits)
                    return false;

                cargo.Add(wareId, quantity);
                cargo.Credits -= cost;

                _eventSystem.Publish(new TradeCompletedEvent
                {
                    StationId = stationId,
                    TraderId = ship.Id,
                    WareId = wareId,
                    Quantity = quantity,
                    Price = cost,
                    IsBuying = true
                });

                return true;
            }
            else
            {
                // Sell to station
                if (!cargo.Contains(wareId, quantity))
                    return false;

                if (!_markets.CanSell(stationId, wareId, quantity))
                    return false;

                float revenue = _markets.Sell(stationId, wareId, quantity);
                cargo.Remove(wareId, quantity);
                cargo.Credits += revenue;

                _eventSystem.Publish(new TradeCompletedEvent
                {
                    StationId = stationId,
                    TraderId = ship.Id,
                    WareId = wareId,
                    Quantity = quantity,
                    Price = revenue,
                    IsBuying = false
                });

                return true;
            }
        }

        /// <summary>
        /// Request station service (repair, refuel, rearm).
        /// </summary>
        public bool RequestService(int stationId, Entity ship, StationServiceType serviceType)
        {
            if (!_stations.TryGetValue(stationId, out var station))
                return false;

            if (!station.DockedShips.Any(d => d.EntityId == ship.Id))
                return false;

            if (!station.AvailableServices.Contains(serviceType))
                return false;

            var service = new StationService
            {
                ServiceType = serviceType,
                TargetShipId = ship.Id,
                Progress = 0f,
                Duration = GetServiceDuration(serviceType)
            };

            station.ServiceQueue.Add(service);

            return true;
        }

        /// <summary>
        /// Add goods to station inventory (for factories/production).
        /// </summary>
        public void AddStationInventory(int stationId, string wareId, int quantity)
        {
            if (_stations.TryGetValue(stationId, out var station) && station.Market != null)
            {
                var good = station.Market.Goods.GetValueOrDefault(wareId);
                if (good != null)
                {
                    good.StockLevel = Math.Min(good.StockLevel + quantity, good.MaxStock);
                }
            }
        }

        /// <summary>
        /// Set station market prices and inventory.
        /// </summary>
        public void ConfigureMarket(int stationId, List<(string wareId, float basePrice, int stock, int maxStock)> goods)
        {
            if (!_stations.TryGetValue(stationId, out var station) || station.Market == null)
                return;

            foreach (var (wareId, basePrice, stock, maxStock) in goods)
            {
                var good = new MarketGood
                {
                    WareId = wareId,
                    BasePrice = basePrice,
                    CurrentPrice = basePrice,
                    StockLevel = stock,
                    MaxStock = maxStock,
                    Demand = 1f,
                    Supply = 1f,
                    PriceFriction = 0.3f
                };

                station.Market.AddGood(wareId, good);
            }
        }

        public Station? GetStation(int stationId)
        {
            return _stations.GetValueOrDefault(stationId);
        }

        public List<Station> GetStationsByFaction(string factionId)
        {
            return _stations.Values.Where(s => s.Faction == factionId).ToList();
        }

        public List<Station> GetStationsInRange(Vector2 position, float range)
        {
            return _stations.Values
                .Where(s => Vector2.Distance(s.Position, position) <= range)
                .ToList();
        }

        private void ProcessServices(Station station, float deltaTime)
        {
            if (station.ServiceQueue.Count == 0)
                return;

            var service = station.ServiceQueue[0];
            service.Progress += deltaTime;

            if (service.Progress >= service.Duration)
            {
                // Service complete
                CompleteService(station, service);
                station.ServiceQueue.RemoveAt(0);
            }
        }

        private void CompleteService(Station station, StationService service)
        {
            _eventSystem.Publish(new StationServiceCompletedEvent
            {
                StationId = station.StationId,
                ShipId = service.TargetShipId,
                ServiceType = service.ServiceType
            });
        }

        private bool CanDock(string stationFaction, string shipFaction)
        {
            // No diplomacy system or neutral stations allow all
            if (_diplomacySystem == null || string.IsNullOrEmpty(stationFaction))
                return true;

            // Same faction always allowed
            if (stationFaction == shipFaction)
                return true;

            // Check diplomatic relations - hostile factions cannot dock
            return !_diplomacySystem.AreHostile(stationFaction, shipFaction);
        }

        private int GetMaxDockedShips(string stationType)
        {
            return stationType switch
            {
                "TradePost" => 8,
                "Shipyard" => 4,
                "Factory" => 6,
                "Military" => 10,
                _ => 4
            };
        }

        private float GetServiceDuration(StationServiceType serviceType)
        {
            return serviceType switch
            {
                StationServiceType.Repair => 5f,
                StationServiceType.Refuel => 2f,
                StationServiceType.Rearm => 3f,
                _ => 2f
            };
        }
    }

    /// <summary>
    /// Component marking entity as a station.
    /// </summary>
    public class StationComponent : Component
    {
        public int StationId { get; set; }
        public string StationType { get; set; } = "TradePost";
        public string Faction { get; set; } = "neutral";
        public int MaxDockedShips { get; set; } = 4;
    }

    /// <summary>
    /// Station data structure.
    /// </summary>
    public class Station
    {
        public int StationId { get; set; }
        public int EntityId { get; set; }
        public string Name { get; set; } = "Station";
        public string StationType { get; set; } = "TradePost";
        public string Faction { get; set; } = "neutral";
        public Vector2 Position { get; set; }
        public int MaxDockedShips { get; set; } = 4;
        public float MaxDockingTime { get; set; } = 60f; // Auto-undock after 60s

        public List<DockedShip> DockedShips { get; set; } = new();
        public List<StationService> ServiceQueue { get; set; } = new();
        public List<StationServiceType> AvailableServices { get; set; } = new()
        {
            StationServiceType.Repair,
            StationServiceType.Refuel,
            StationServiceType.Rearm
        };

        public StationMarket? Market { get; set; }
    }

    public class DockedShip
    {
        public int EntityId { get; set; }
        public string ShipName { get; set; } = string.Empty;
        public float DockingTime { get; set; }
    }

    public class StationService
    {
        public StationServiceType ServiceType { get; set; }
        public int TargetShipId { get; set; }
        public float Progress { get; set; }
        public float Duration { get; set; }
    }

    public enum StationServiceType
    {
        Repair,
        Refuel,
        Rearm,
        Upgrade
    }

    // Events
    public class StationCreatedEvent
    {
        public int StationId { get; set; }
        public string StationType { get; set; } = string.Empty;
        public string Faction { get; set; } = string.Empty;
        public Vector2 Position { get; set; }
    }

    public class ShipDockedEvent
    {
        public int StationId { get; set; }
        public int ShipId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ShipUndockedEvent
    {
        public int StationId { get; set; }
        public int ShipId { get; set; }
        public float DockingDuration { get; set; }
    }

    public class TradeCompletedEvent
    {
        public int StationId { get; set; }
        public int TraderId { get; set; }
        public string WareId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public float Price { get; set; }
        public bool IsBuying { get; set; }
    }

    public class StationServiceCompletedEvent
    {
        public int StationId { get; set; }
        public int ShipId { get; set; }
        public StationServiceType ServiceType { get; set; }
    }
}
