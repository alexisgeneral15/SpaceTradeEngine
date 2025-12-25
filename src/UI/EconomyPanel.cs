using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// UI panel for monitoring stations, shipyards, and civilian traders.
    /// Shows Unending Galaxy-style economic activity.
    /// </summary>
    public class EconomyPanel
    {
        private readonly StationSystem _stationSystem;
        private readonly TraderAISystem _traderSystem;
        private readonly ShipyardSystem _shipyardSystem;
        private Texture2D? _pixelCache; // OPTIMIZADO: cache el pixel en lugar de crear cada DrawBox
        
        private int? _selectedStationId;
        private Vector2 _position = new Vector2(10, 200);
        private bool _isVisible = true;

        public EconomyPanel(StationSystem stationSystem, TraderAISystem traderSystem, ShipyardSystem shipyardSystem)
        {
            _stationSystem = stationSystem;
            _traderSystem = traderSystem;
            _shipyardSystem = shipyardSystem;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!_isVisible) return;

            int yOffset = (int)_position.Y;
            var bgColor = new Color(0, 0, 0, 180);

            // Header
            DrawBox(spriteBatch, new Rectangle((int)_position.X, yOffset, 400, 30), bgColor);
            DrawText(spriteBatch, font, $"═══ ECONOMY MONITOR ═══", new Vector2(_position.X + 10, yOffset + 5), Color.Cyan);
            yOffset += 35;

            // Station Summary
            DrawBox(spriteBatch, new Rectangle((int)_position.X, yOffset, 400, 80), bgColor);
            DrawStationSummary(spriteBatch, font, ref yOffset);
            yOffset += 5;

            // Trader Summary
            DrawBox(spriteBatch, new Rectangle((int)_position.X, yOffset, 400, 80), bgColor);
            DrawTraderSummary(spriteBatch, font, ref yOffset);
            yOffset += 5;

            // Shipyard Summary
            DrawBox(spriteBatch, new Rectangle((int)_position.X, yOffset, 400, 80), bgColor);
            DrawShipyardSummary(spriteBatch, font, ref yOffset);
            yOffset += 5;

            // Selected Station Details
            if (_selectedStationId.HasValue)
            {
                DrawBox(spriteBatch, new Rectangle((int)_position.X, yOffset, 400, 200), bgColor);
                DrawStationDetails(spriteBatch, font, ref yOffset);
            }
        }

        private void DrawStationSummary(SpriteBatch spriteBatch, SpriteFont font, ref int yOffset)
        {
            var stations = _stationSystem.Stations.Values.ToList();
            int tradePosts = stations.Count(s => s.StationType == "TradePost");
            int factories = stations.Count(s => s.StationType == "Factory");
            int shipyards = stations.Count(s => s.StationType == "Shipyard");
            int totalDocked = stations.Sum(s => s.DockedShips.Count);

            DrawText(spriteBatch, font, "STATIONS:", new Vector2(_position.X + 10, yOffset + 5), Color.Yellow);
            DrawText(spriteBatch, font, $"  Total: {stations.Count}", new Vector2(_position.X + 15, yOffset + 25), Color.White);
            DrawText(spriteBatch, font, $"  Trade: {tradePosts} | Factory: {factories} | Shipyard: {shipyards}", 
                new Vector2(_position.X + 15, yOffset + 40), Color.LightGray);
            DrawText(spriteBatch, font, $"  Ships Docked: {totalDocked}", new Vector2(_position.X + 15, yOffset + 55), Color.LightGreen);

            yOffset += 80;
        }

        private void DrawTraderSummary(SpriteBatch spriteBatch, SpriteFont font, ref int yOffset)
        {
            var traders = _traderSystem.Traders.Values.ToList();
            int idle = traders.Count(t => t.CurrentState == TraderState.Idle);
            int trading = traders.Count(t => t.CurrentState == TraderState.Buying || t.CurrentState == TraderState.Selling);
            int traveling = traders.Count(t => t.CurrentState.ToString().Contains("Traveling"));
            float totalProfit = traders.Sum(t => t.TotalProfit);

            DrawText(spriteBatch, font, "CIVILIAN TRADERS:", new Vector2(_position.X + 10, yOffset + 5), Color.Yellow);
            DrawText(spriteBatch, font, $"  Active: {traders.Count}", new Vector2(_position.X + 15, yOffset + 25), Color.White);
            DrawText(spriteBatch, font, $"  Idle: {idle} | Trading: {trading} | Traveling: {traveling}", 
                new Vector2(_position.X + 15, yOffset + 40), Color.LightGray);
            DrawText(spriteBatch, font, $"  Total Profit: {totalProfit:F0} credits", new Vector2(_position.X + 15, yOffset + 55), Color.Gold);
            DrawText(spriteBatch, font, $"  Rank Bonuses: Buy/Sell margins & dodge +", new Vector2(_position.X + 15, yOffset + 70), Color.Cyan);

            yOffset += 95;
        }

        private void DrawShipyardSummary(SpriteBatch spriteBatch, SpriteFont font, ref int yOffset)
        {
            var shipyards = _shipyardSystem.Shipyards.Values.ToList();
            int activeOrders = shipyards.Sum(s => s.BuildQueue.Count);
            int totalBuilt = shipyards.Sum(s => s.ShipsBuilt);
            int autoProducing = shipyards.Count(s => s.AutoProduction);

            DrawText(spriteBatch, font, "SHIPYARDS:", new Vector2(_position.X + 10, yOffset + 5), Color.Yellow);
            DrawText(spriteBatch, font, $"  Active: {shipyards.Count}", new Vector2(_position.X + 15, yOffset + 25), Color.White);
            DrawText(spriteBatch, font, $"  Build Queue: {activeOrders} | Auto-Production: {autoProducing}", 
                new Vector2(_position.X + 15, yOffset + 40), Color.LightGray);
            DrawText(spriteBatch, font, $"  Ships Built: {totalBuilt}", new Vector2(_position.X + 15, yOffset + 55), Color.LightGreen);

            yOffset += 80;
        }

        private void DrawStationDetails(SpriteBatch spriteBatch, SpriteFont font, ref int yOffset)
        {
            var station = _stationSystem.GetStation(_selectedStationId.Value);
            if (station == null) return;

            DrawText(spriteBatch, font, $"STATION: {station.Name}", new Vector2(_position.X + 10, yOffset + 5), Color.Cyan);
            DrawText(spriteBatch, font, $"Type: {station.StationType}", new Vector2(_position.X + 15, yOffset + 25), Color.White);
            DrawText(spriteBatch, font, $"Faction: {station.Faction}", new Vector2(_position.X + 15, yOffset + 40), Color.White);
            DrawText(spriteBatch, font, $"Docked: {station.DockedShips.Count}/{station.MaxDockedShips}", 
                new Vector2(_position.X + 15, yOffset + 55), Color.LightGreen);

            yOffset += 75;

            // Market info
            if (station.Market != null)
            {
                DrawText(spriteBatch, font, "Market Goods:", new Vector2(_position.X + 15, yOffset), Color.Yellow);
                yOffset += 20;

                int count = 0;
                foreach (var (wareId, good) in station.Market.Goods.Take(4))
                {
                    DrawText(spriteBatch, font, $"  {wareId}: {good.StockLevel} @ {good.CurrentPrice:F0}cr", 
                        new Vector2(_position.X + 20, yOffset), Color.LightGray);
                    yOffset += 15;
                    count++;
                }

                if (station.Market.Goods.Count > 4)
                {
                    DrawText(spriteBatch, font, $"  ...+{station.Market.Goods.Count - 4} more", 
                        new Vector2(_position.X + 20, yOffset), Color.Gray);
                    yOffset += 15;
                }
            }

            yOffset += 10;
        }

        public void SelectStation(int stationId)
        {
            _selectedStationId = stationId;
        }

        public void ClearSelection()
        {
            _selectedStationId = null;
        }

        private void DrawBox(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            // OPTIMIZADO: cache el pixel en lugar de crear cada frame
            if (_pixelCache == null)
            {
                _pixelCache = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _pixelCache.SetData(new[] { Color.White });
                GlobalMemoryArena.Instance.Allocate("UI_EconomyPanel_Pixel", 1024);
            }
            spriteBatch.Draw(_pixelCache, rect, color);
        }

        private void DrawText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
