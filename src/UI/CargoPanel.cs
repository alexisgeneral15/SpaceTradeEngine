using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Displays player cargo inventory with volume bar.
    /// </summary>
    public class CargoPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 180);
        
        private readonly Entity? _playerShip;

        public CargoPanel(Rectangle bounds, Entity? playerShip)
        {
            Bounds = bounds;
            _playerShip = playerShip;
        }

        public void Update(InputManager input, Point mouse)
        {
            // No interactive elements for now
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, Background);

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "═══ CARGO HOLD ═══", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            if (_playerShip == null)
            {
                renderer.RenderText(spriteBatch, "No ship", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            var cargo = _playerShip.GetComponent<CargoComponent>();
            if (cargo == null)
            {
                renderer.RenderText(spriteBatch, "No cargo component", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            // Credits
            renderer.RenderText(spriteBatch, $"Credits: {cargo.Credits:F0}", new Vector2(Bounds.X + 10, y), Color.Gold);
            y += 25;

            // Volume bar
            float usedPercent = cargo.GetUsedPercent();
            renderer.RenderText(spriteBatch, $"Volume: {cargo.CurrentVolume:F1}/{cargo.MaxVolume:F1} ({usedPercent * 100:F0}%)", 
                new Vector2(Bounds.X + 10, y), Color.White);
            y += 20;

            // Volume bar visualization
            int barWidth = Bounds.Width - 30;
            int barHeight = 20;
            var barBackRect = new Rectangle(Bounds.X + 15, (int)y, barWidth, barHeight);
            var barFillRect = new Rectangle(Bounds.X + 15, (int)y, (int)(barWidth * usedPercent), barHeight);
            
            spriteBatch.Draw(pixel, barBackRect, new Color(40, 40, 40, 220));
            spriteBatch.Draw(pixel, barFillRect, new Color(100, 150, 255, 220));
            y += 30;

            // Inventory items
            renderer.RenderText(spriteBatch, "Inventory:", new Vector2(Bounds.X + 10, y), Color.LightGray);
            y += 20;

            if (cargo.Inventory.Count == 0)
            {
                renderer.RenderText(spriteBatch, "  (empty)", new Vector2(Bounds.X + 15, y), Color.Gray);
            }
            else
            {
                int displayed = 0;
                foreach (var item in cargo.Inventory)
                {
                    if (displayed >= 8) // Max 8 items visible
                    {
                        renderer.RenderText(spriteBatch, $"  ... +{cargo.Inventory.Count - displayed} more", 
                            new Vector2(Bounds.X + 15, y), Color.Gray);
                        break;
                    }

                    renderer.RenderText(spriteBatch, $"  {item.Key}: {item.Value}", 
                        new Vector2(Bounds.X + 15, y), Color.LightYellow);
                    y += 18;
                    displayed++;
                }
            }
        }
    }
}
