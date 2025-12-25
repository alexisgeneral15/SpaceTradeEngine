using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Displays available contracts and allows player to accept them.
    /// </summary>
    public class ContractPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 180);
        
        private readonly ContractSystem _contractSystem;
        private readonly Entity? _playerShip;
        private readonly List<Rectangle> _acceptButtonRects = new();
        private const int MAX_VISIBLE_CONTRACTS = 4;

        public ContractPanel(Rectangle bounds, ContractSystem contractSystem, Entity? playerShip)
        {
            Bounds = bounds;
            _contractSystem = contractSystem;
            _playerShip = playerShip;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (_playerShip == null) return;

            // Check if any accept button was clicked
            var contracts = _contractSystem.GetAvailableContracts();
            for (int i = 0; i < _acceptButtonRects.Count && i < contracts.Count; i++)
            {
                if (_acceptButtonRects[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    _contractSystem.AcceptContract(contracts[i].Id, _playerShip.Id);
                    Console.WriteLine($"[Contract] Accepted: {contracts[i].GetDescription()}");
                    break;
                }
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, Background);
            _acceptButtonRects.Clear();

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "═══ AVAILABLE CONTRACTS ═══", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            var contracts = _contractSystem.GetAvailableContracts();
            if (contracts.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No contracts available", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            int displayed = 0;
            foreach (var contract in contracts)
            {
                if (displayed >= MAX_VISIBLE_CONTRACTS) break;

                // Contract title (use contract type)
                string title = $"[{contract.Type}] Contract #{contract.Id}";
                renderer.RenderText(spriteBatch, title, new Vector2(Bounds.X + 10, y), Color.Yellow);
                y += 20;

                // Contract details
                string details = $"Deliver {contract.Quantity}x {contract.WareId}";
                renderer.RenderText(spriteBatch, details, new Vector2(Bounds.X + 20, y), Color.White);
                y += 18;

                string route = $"From Station #{contract.SourceStationId} → #{contract.DestinationStationId}";
                renderer.RenderText(spriteBatch, route, new Vector2(Bounds.X + 20, y), Color.LightGray);
                y += 18;

                string reward = $"Reward: {contract.Reward:F0} credits | Time: {contract.TimeLimit:F0}s";
                renderer.RenderText(spriteBatch, reward, new Vector2(Bounds.X + 20, y), Color.LightGreen);
                y += 20;

                // Accept button
                var buttonRect = new Rectangle(Bounds.X + 20, (int)y, 100, 20);
                _acceptButtonRects.Add(buttonRect);
                spriteBatch.Draw(pixel, buttonRect, new Color(40, 100, 40, 220));
                renderer.RenderText(spriteBatch, "Accept", new Vector2(buttonRect.X + 25, buttonRect.Y + 2), Color.White);
                y += 30;

                displayed++;
            }

            // Show active contracts count
            var activeContracts = _contractSystem.GetActiveContracts();
            if (activeContracts.Count > 0)
            {
                y += 10;
                renderer.RenderText(spriteBatch, $"Active Contracts: {activeContracts.Count}", new Vector2(Bounds.X + 10, y), Color.Orange);
            }
        }
    }
}
