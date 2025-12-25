using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Displays selected entity info (name, faction, health) in a fixed panel.
    /// </summary>
    public class SelectionPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 180);
        public System.Action<System.Collections.Generic.List<Entity>> OnFocusTarget { get; set; }
        public System.Collections.Generic.List<Entity> SelectedEntities { get; set; } = new System.Collections.Generic.List<Entity>();
        private Rectangle _focusBtnRect;

        public SelectionPanel(Rectangle bounds)
        {
            Bounds = bounds;
        }

        public void Update(InputManager input, Point mouse)
        {
            // Simple button click detection
            if (_focusBtnRect.Contains(mouse) && input.IsMouseLeftClicked && SelectedEntities != null && SelectedEntities.Count > 0)
            {
                OnFocusTarget?.Invoke(SelectedEntities);
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, Systems.RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, Background);

            if (SelectedEntities == null || SelectedEntities.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No selection", new Vector2(Bounds.X + 10, Bounds.Y + 10), Color.LightGray);
                return;
            }
            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, $"Selected ({SelectedEntities.Count})", new Vector2(Bounds.X + 10, y), Color.White);
            y += 20;

            // Show list (cap to 6 items for space)
            int shown = 0;
            foreach (var ent in SelectedEntities)
            {
                if (shown >= 6) break;
                var faction = ent.GetComponent<FactionComponent>()?.FactionName ?? "Neutral";
                var health = ent.GetComponent<HealthComponent>();
                string hp = health != null ? $"{health.CurrentHealth:F0}/{health.MaxHealth:F0}" : "n/a";
                renderer.RenderText(spriteBatch, $"- {ent.Name} [{faction}] HP {hp}", new Vector2(Bounds.X + 10, y), Color.LightGray);
                y += 18; shown++;
            }

            // If single selection, show weapon/cooldown and targeting info
            if (SelectedEntities.Count == 1)
            {
                var ent = SelectedEntities[0];
                var weapon = ent.GetComponent<SpaceTradeEngine.ECS.Components.WeaponComponent>();
                var targeting = ent.GetComponent<SpaceTradeEngine.Systems.TargetingComponent>();
                if (weapon != null)
                {
                    renderer.RenderText(spriteBatch, $"Weapon Dmg {weapon.Damage:F0} Cd {weapon.CooldownRemaining:F1}/{weapon.Cooldown:F1}", new Vector2(Bounds.X + 10, y), Color.Orange);
                    y += 18;
                }
                if (targeting != null)
                {
                    renderer.RenderText(spriteBatch, $"Range {targeting.MaxRange:F0} LOS {(targeting.HasLineOfSight ? "Y" : "N")}", new Vector2(Bounds.X + 10, y), Color.LightYellow);
                    y += 18;
                }
            }

            // Focus Target button at bottom of panel
            _focusBtnRect = new Rectangle(Bounds.X + Bounds.Width - 120, Bounds.Y + Bounds.Height - 35, 110, 25);
            spriteBatch.Draw(pixel, _focusBtnRect, new Color(60, 60, 60, 220));
            renderer.RenderText(spriteBatch, "Focus Target", new Vector2(_focusBtnRect.X + 8, _focusBtnRect.Y + 4), Color.White);
        }
    }
}
