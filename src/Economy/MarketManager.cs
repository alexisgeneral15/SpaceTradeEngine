using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

#nullable enable
namespace SpaceTradeEngine.Economy
{
    /// <summary>
    /// Manages trade goods, prices, and market state across stations.
    /// </summary>
    public class MarketManager
    {
        public Dictionary<int, StationMarket> Markets { get; } = new();
        private readonly Dictionary<string, WareTemplate> _wareTemplates = new();

        public void RegisterWareTemplate(WareTemplate template)
        {
            if (template != null)
                _wareTemplates[template.Id] = template;
        }

        public WareTemplate? GetWareTemplate(string wareId)
        {
            return _wareTemplates.TryGetValue(wareId, out var t) ? t : null;
        }

        public void RegisterStationMarket(int stationId, StationMarket market)
        {
            if (market != null)
                Markets[stationId] = market;
        }

        public StationMarket? GetMarket(int stationId)
        {
            return Markets.TryGetValue(stationId, out var m) ? m : null;
        }

        public void UpdatePrices(float deltaSeconds)
        {
            foreach (var market in Markets.Values)
            {
                market.Update(deltaSeconds);
            }
        }

        public float? GetPrice(int stationId, string wareId)
        {
            var market = GetMarket(stationId);
            return market?.GetPrice(wareId);
        }

        public bool CanBuy(int stationId, string wareId, int quantity)
        {
            var market = GetMarket(stationId);
            return market != null && market.GetAvailable(wareId) >= quantity;
        }

        public bool CanSell(int stationId, string wareId, int quantity)
        {
            var market = GetMarket(stationId);
            return market != null && market.CanAccept(wareId, quantity);
        }

        public float Buy(int stationId, string wareId, int quantity)
        {
            var market = GetMarket(stationId);
            if (market == null) return 0f;
            return market.Buy(wareId, quantity);
        }

        public float Sell(int stationId, string wareId, int quantity)
        {
            var market = GetMarket(stationId);
            if (market == null) return 0f;
            return market.Sell(wareId, quantity);
        }
    }

    /// <summary>
    /// Per-station market state: inventory, prices, supply/demand.
    /// </summary>
    public class StationMarket
    {
        public int StationId { get; set; }
        public Dictionary<string, MarketGood> Goods { get; } = new();

        public void AddGood(string wareId, MarketGood good)
        {
            Goods[wareId] = good;
        }

        public void Update(float deltaSeconds)
        {
            foreach (var good in Goods.Values)
            {
                good.Update(deltaSeconds);
            }
        }

        public float GetPrice(string wareId)
        {
            return Goods.TryGetValue(wareId, out var good) ? good.CurrentPrice : 0f;
        }

        public int GetAvailable(string wareId)
        {
            return Goods.TryGetValue(wareId, out var good) ? good.StockLevel : 0;
        }

        public bool CanAccept(string wareId, int quantity)
        {
            if (!Goods.TryGetValue(wareId, out var good)) return false;
            return good.StockLevel + quantity <= good.MaxStock;
        }

        public float Buy(string wareId, int quantity)
        {
            if (!Goods.TryGetValue(wareId, out var good)) return 0f;
            int actual = Math.Min(quantity, good.StockLevel);
            if (actual <= 0) return 0f;
            float cost = actual * good.CurrentPrice;
            good.StockLevel -= actual;
            return cost;
        }

        public float Sell(string wareId, int quantity)
        {
            if (!Goods.TryGetValue(wareId, out var good)) return 0f;
            if (good.StockLevel + quantity > good.MaxStock) return 0f;
            float revenue = quantity * good.CurrentPrice;
            good.StockLevel += quantity;
            return revenue;
        }
    }

    /// <summary>
    /// Single good in a station market with price dynamics.
    /// </summary>
    public class MarketGood
    {
        public string WareId { get; set; } = string.Empty;
        public int StockLevel { get; set; }
        public int MaxStock { get; set; } = 1000;
        public int MinStock { get; set; } = 0;
        public float BasePrice { get; set; }
        public float CurrentPrice { get; set; }
        public float Demand { get; set; } = 1f;
        public float Supply { get; set; } = 1f;
        public float PriceFriction { get; set; } = 0.5f;
        
        // Supply/demand simulation
        public float ConsumptionRate { get; set; } = 1f; // units per minute
        public float ProductionRate { get; set; } = 1f;  // units per minute
        public bool IsProduced { get; set; } = false;
        public bool IsConsumed { get; set; } = true;
        
        private float _updateAccumulator = 0f;
        private const float UPDATE_INTERVAL = 5f; // seconds between supply/demand updates

        public void Update(float deltaSeconds)
        {
            _updateAccumulator += deltaSeconds;
            
            // Simulate consumption/production
            if (_updateAccumulator >= UPDATE_INTERVAL)
            {
                float minutesPassed = _updateAccumulator / 60f;
                
                if (IsConsumed)
                {
                    int consumed = (int)(ConsumptionRate * minutesPassed);
                    StockLevel = Math.Max(MinStock, StockLevel - consumed);
                }
                
                if (IsProduced)
                {
                    int produced = (int)(ProductionRate * minutesPassed);
                    StockLevel = Math.Min(MaxStock, StockLevel + produced);
                }
                
                _updateAccumulator = 0f;
            }
            
            // Calculate supply ratio (0 = empty, 1 = full)
            float stockRatio = (float)StockLevel / Math.Max(1, MaxStock);
            
            // Supply increases price when low, decreases when high
            Supply = 0.5f + stockRatio; // range: 0.5 to 1.5
            
            // Demand increases when stock is low (shortage drives demand)
            Demand = 2f - stockRatio; // range: 1 to 2
            
            // Calculate target price based on supply/demand
            float targetPrice = BasePrice * (Demand / Math.Max(0.1f, Supply));
            
            // Smooth price changes
            CurrentPrice = Lerp(CurrentPrice, targetPrice, PriceFriction * deltaSeconds);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }

    /// <summary>
    /// Ware/commodity definition.
    /// </summary>
    public class WareTemplate
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = "commodity";
        [JsonPropertyName("base_price")] public float BasePrice { get; set; } = 100f;
        [JsonPropertyName("volume")] public float Volume { get; set; } = 1f;
        [JsonPropertyName("illegal_factions")] public List<string> IllegalFactions { get; set; } = new();
    }

    /// <summary>
    /// Station definition for market setup.
    /// </summary>
    public class StationTemplate
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("faction")] public string Faction { get; set; } = string.Empty;
        [JsonPropertyName("inventory")] public Dictionary<string, int> Inventory { get; set; } = new();
        [JsonPropertyName("buy_wares")] public List<string> BuyWares { get; set; } = new();
        [JsonPropertyName("sell_wares")] public List<string> SellWares { get; set; } = new();
    }
}
