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
    /// Displays fleet information and formation controls.
    /// </summary>
    public class FleetPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;
        
        private readonly FleetSystem _fleetSystem;
        private readonly ECS.EntityManager _entityManager;
        private Entity? _selectedFleet;
        private readonly List<Rectangle> _formationButtons = new();
        private readonly List<FormationType> _formations = new();

        public FleetPanel(Rectangle bounds, FleetSystem fleetSystem, ECS.EntityManager entityManager)
        {
            Bounds = bounds;
            _fleetSystem = fleetSystem;
            _entityManager = entityManager;
        }

        public void SetSelectedFleet(Entity? fleet)
        {
            _selectedFleet = fleet;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible || _selectedFleet == null) return;

            for (int i = 0; i < _formationButtons.Count; i++)
            {
                if (_formationButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    var fleet = _selectedFleet.GetComponent<FleetComponent>();
                    if (fleet != null)
                    {
                        fleet.Formation = _formations[i];
                    }
                }
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (!Visible) return;

            spriteBatch.Draw(pixel, Bounds, Background);
            
            _formationButtons.Clear();
            _formations.Clear();

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "=== FLEET ===", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            if (_selectedFleet == null || !_selectedFleet.IsActive)
            {
                renderer.RenderText(spriteBatch, "No fleet selected", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            var fleet = _selectedFleet.GetComponent<FleetComponent>();
            if (fleet == null)
            {
                renderer.RenderText(spriteBatch, "Selected entity is not a fleet", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            // Fleet info
            renderer.RenderText(spriteBatch, $"Fleet: {fleet.Name}", new Vector2(Bounds.X + 15, y), Color.White);
            y += 20;

            renderer.RenderText(spriteBatch, $"Ships: {fleet.MemberIds.Count + 1}", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
            y += 18;

            renderer.RenderText(spriteBatch, $"Formation: {fleet.Formation}", 
                new Vector2(Bounds.X + 20, y), Color.LightBlue);
            y += 18;

            renderer.RenderText(spriteBatch, $"Spacing: {fleet.FormationSpacing:F0}", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
            y += 25;

            // Formation buttons
            renderer.RenderText(spriteBatch, "Change Formation:", new Vector2(Bounds.X + 15, y), Color.Yellow);
            y += 20;

            var formationTypes = new[] 
            { 
                FormationType.Line, FormationType.Column, FormationType.Wedge, 
                FormationType.Box, FormationType.Circle, FormationType.Diamond 
            };

            int btnX = Bounds.X + 15;
            int btnY = (int)y;
            int btnWidth = 70;
            int btnHeight = 22;
            int btnSpacing = 5;
            int btnsPerRow = 2;

            for (int i = 0; i < formationTypes.Length; i++)
            {
                if (i > 0 && i % btnsPerRow == 0)
                {
                    btnY += btnHeight + btnSpacing;
                    btnX = Bounds.X + 15;
                }

                var btnRect = new Rectangle(btnX, btnY, btnWidth, btnHeight);
                var btnColor = fleet.Formation == formationTypes[i] 
                    ? new Color(80, 120, 80, 255) 
                    : new Color(60, 60, 60, 255);
                
                spriteBatch.Draw(pixel, btnRect, btnColor);
                renderer.RenderText(spriteBatch, formationTypes[i].ToString(), 
                    new Vector2(btnRect.X + 8, btnRect.Y + 4), Color.White);

                _formationButtons.Add(btnRect);
                _formations.Add(formationTypes[i]);

                btnX += btnWidth + btnSpacing;
            }

            y = btnY + btnHeight + 20;

            // Ship health summary
            renderer.RenderText(spriteBatch, "Fleet Health:", new Vector2(Bounds.X + 15, y), Color.Yellow);
            y += 18;

            float totalHealth = 0f;
            float maxHealth = 0f;
            int aliveCount = 0;

            var leader = _entityManager.GetEntity(fleet.LeaderId);
            if (leader != null)
            {
                var health = leader.GetComponent<ECS.Components.HealthComponent>();
                if (health != null)
                {
                    totalHealth += health.CurrentHealth;
                    maxHealth += health.MaxHealth;
                    if (health.IsAlive) aliveCount++;
                }
            }

            foreach (var memberId in fleet.MemberIds)
            {
                var member = _entityManager.GetEntity(memberId);
                if (member == null) continue;
                
                var health = member.GetComponent<ECS.Components.HealthComponent>();
                if (health != null)
                {
                    totalHealth += health.CurrentHealth;
                    maxHealth += health.MaxHealth;
                    if (health.IsAlive) aliveCount++;
                }
            }

            renderer.RenderText(spriteBatch, $"  Ships Alive: {aliveCount}/{fleet.MemberIds.Count + 1}", 
                new Vector2(Bounds.X + 20, y), aliveCount == fleet.MemberIds.Count + 1 ? Color.LightGreen : Color.Orange);
            y += 16;

            renderer.RenderText(spriteBatch, $"  Total HP: {totalHealth:F0}/{maxHealth:F0}", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
        }
    }
}
