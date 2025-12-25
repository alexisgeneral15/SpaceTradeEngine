using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// In-game HUD overlay displaying player ship status and objectives.
    /// </summary>
    public class GameHUD : IUIElement
    {
        private Entity? _playerShip;
        private Vector2 _screenSize;

        public GameHUD(Vector2 screenSize)
        {
            _screenSize = screenSize;
        }

        public void SetPlayerShip(Entity? ship)
        {
            _playerShip = ship;
        }

        public void Update(InputManager input, Point mouse)
        {
            // HUD is passive
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (_playerShip == null)
                return;

            RenderPlayerStatus(spriteBatch, renderer, pixel);
            RenderWeaponInfo(spriteBatch, renderer, pixel);
            RenderObjectives(spriteBatch, renderer, pixel);
        }

        private void RenderPlayerStatus(SpriteBatch spriteBatch, RenderingSystem renderer, Texture2D pixel)
        {
            var health = _playerShip?.GetComponent<HealthComponent>();
            var cargo = _playerShip?.GetComponent<CargoComponent>();
            var transform = _playerShip?.GetComponent<TransformComponent>();

            if (health == null || transform == null)
                return;

            float padding = 10f;
            float y = 10f;

            // Health status
            string healthText = $"HULL: {health.CurrentHealth:F0}/{health.MaxHealth:F0}";
            var healthColor = health.CurrentHealth > health.MaxHealth * 0.5f ? Color.Lime : (health.CurrentHealth > health.MaxHealth * 0.25f ? Color.Yellow : Color.Red);
            renderer.RenderText(spriteBatch, healthText, new Vector2(padding, y), healthColor);

            y += 30f;

            // Cargo
            if (cargo != null)
            {
                int usedCapacity = cargo.Items.Values.Sum(item => item.Quantity);
                string cargoText = $"CARGO: {usedCapacity}/{cargo.Capacity:F0}";
                renderer.RenderText(spriteBatch, cargoText, new Vector2(padding, y), Color.Cyan);
                y += 30f;
            }

            // Credits
            if (cargo != null)
            {
                string creditsText = $"CREDITS: {cargo.Credits:F0}";
                renderer.RenderText(spriteBatch, creditsText, new Vector2(padding, y), Color.Gold);
                y += 30f;
            }

            // Position
            string posText = $"X:{transform.Position.X:F0} Y:{transform.Position.Y:F0}";
            renderer.RenderText(spriteBatch, posText, new Vector2(padding, y), Color.White);
        }

        private void RenderWeaponInfo(SpriteBatch spriteBatch, RenderingSystem renderer, Texture2D pixel)
        {
            var weapon = _playerShip?.GetComponent<WeaponComponent>();
            if (weapon == null)
                return;

            float padding = 10f;
            float y = _screenSize.Y - 80f;

            string weaponText = $"WEAPON: {weapon.Id}";
            renderer.RenderText(spriteBatch, weaponText, new Vector2(padding, y), Color.Yellow);

            y += 25f;

            string cooldownText = weapon.CooldownRemaining > 0
                ? $"COOLDOWN: {weapon.CooldownRemaining:F2}s"
                : "READY";
            Color cooldownColor = weapon.CooldownRemaining <= 0 ? Color.Lime : Color.Orange;
            renderer.RenderText(spriteBatch, cooldownText, new Vector2(padding, y), cooldownColor);
        }

        private void RenderObjectives(SpriteBatch spriteBatch, RenderingSystem renderer, Texture2D pixel)
        {
            float x = _screenSize.X - 350f;
            float y = 10f;

            renderer.RenderText(spriteBatch, "== CURRENT OBJECTIVES ==", new Vector2(x, y), Color.Yellow);

            y += 25f;

            string[] objectives =
            {
                "• Reach Alpha Station",
                "• Defeat 2 pirates",
                "• Collect salvage"
            };

            foreach (var obj in objectives)
            {
                renderer.RenderText(spriteBatch, obj, new Vector2(x, y), Color.LimeGreen);
                y += 20f;
            }
        }
    }
}
