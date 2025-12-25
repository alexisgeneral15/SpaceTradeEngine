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

            // Update entity cargo/trade states handled by individual systems
        }

        public bool Trade(Entity trader, int stationId, string wareId, int quantity, bool isSelling)
        {
            var cargo = trader.GetComponent<CargoComponent>();
            if (cargo == null) return false;

            var wareTemplate = _markets.GetWareTemplate(wareId);
            if (wareTemplate == null) return false;

            if (isSelling)
            {
                if (!cargo.Contains(wareId, quantity)) return false;
                float revenue = _markets.Sell(stationId, wareId, quantity);
                if (revenue <= 0f) return false;
                cargo.Remove(wareId, quantity, wareTemplate.Volume);
                cargo.Credits += revenue;
                return true;
            }
            else
            {
                if (!_markets.CanBuy(stationId, wareId, quantity)) return false;
                float cost = _markets.Buy(stationId, wareId, quantity);
                if (cost > cargo.Credits) return false;
                
                if (!cargo.CanAdd(wareId, quantity, wareTemplate.Volume))
                    return false;

                cargo.Add(wareId, quantity, wareTemplate.Volume);
                cargo.Credits -= cost;
                return true;
            }
        }
    }
}
