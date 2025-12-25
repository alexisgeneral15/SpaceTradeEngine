using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages cargo, trading, and economy state for entities.
    /// </summary>
    public class EconomySystem : ECS.System
    {
        private readonly Economy.MarketManager _markets;

        public EconomySystem(Economy.MarketManager markets)
        {
            _markets = markets;
        }

        public Economy.MarketManager GetMarketManager()
        {
            return _markets;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<CargoComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Update market prices globally
            _markets.UpdatePrices(deltaTime);

            // Update entity cargo/trade states
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;
                var cargo = entity.GetComponent<CargoComponent>();
                if (cargo == null) continue;

                // Decay cargo if needed (spoilage, etc.)
                foreach (var item in cargo.Items.Values)
                {
                    item.DaysStored += deltaTime / 86400f;
                }
            }
        }

        public bool Trade(Entity trader, int stationId, string wareId, int quantity, bool isSelling)
        {
            var cargo = trader.GetComponent<CargoComponent>();
            if (cargo == null) return false;

            if (isSelling)
            {
                if (!cargo.Contains(wareId, quantity)) return false;
                float revenue = _markets.Sell(stationId, wareId, quantity);
                if (revenue <= 0f) return false;
                cargo.Remove(wareId, quantity);
                cargo.Credits += revenue;
                return true;
            }
            else
            {
                if (!_markets.CanBuy(stationId, wareId, quantity)) return false;
                float cost = _markets.Buy(stationId, wareId, quantity);
                if (cost > cargo.Credits) return false;
                cargo.Add(wareId, quantity);
                cargo.Credits -= cost;
                return true;
            }
        }
    }

    /// <summary>
    /// Cargo hold for trading entities (ships, traders).
    /// </summary>
    public class CargoComponent : Component
    {
        public float Credits { get; set; } = 0f;
        public float Capacity { get; set; } = 1000f;
        public Dictionary<string, CargoItem> Items { get; } = new();

        public void Add(string wareId, int quantity)
        {
            if (!Items.TryGetValue(wareId, out var item))
            {
                item = new CargoItem { WareId = wareId, Quantity = quantity, DaysStored = 0f };
                Items[wareId] = item;
            }
            else
            {
                item.Quantity += quantity;
            }
        }

        public void Remove(string wareId, int quantity)
        {
            if (Items.TryGetValue(wareId, out var item))
            {
                item.Quantity = Math.Max(0, item.Quantity - quantity);
                if (item.Quantity <= 0)
                    Items.Remove(wareId);
            }
        }

        public bool Contains(string wareId, int quantity)
        {
            return Items.TryGetValue(wareId, out var item) && item.Quantity >= quantity;
        }

        public int TotalQuantity => Items.Values.Sum(i => i.Quantity);
        public float UsedCapacity => Items.Values.Sum(i => i.Quantity);
    }

    /// <summary>
    /// Single cargo item entry.
    /// </summary>
    public class CargoItem
    {
        public string WareId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public float DaysStored { get; set; }
    }
}
