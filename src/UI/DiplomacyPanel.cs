using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Displays faction relationships and diplomatic actions.
    /// </summary>
    public class DiplomacyPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;
        
        private readonly DiplomacySystem _diplomacySystem;
        private readonly string _playerFaction;
        private readonly List<Rectangle> _actionButtons = new();
        private readonly List<(string faction, string action)> _buttonActions = new();

        public DiplomacyPanel(Rectangle bounds, DiplomacySystem diplomacySystem, string playerFaction)
        {
            Bounds = bounds;
            _diplomacySystem = diplomacySystem;
            _playerFaction = playerFaction;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible) return;

            for (int i = 0; i < _actionButtons.Count; i++)
            {
                if (_actionButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    var (faction, action) = _buttonActions[i];
                    
                    if (action == "War")
                    {
                        _diplomacySystem.DeclareWar(_playerFaction, faction);
                    }
                    else if (action == "Peace")
                    {
                        _diplomacySystem.ModifyRelationship(_playerFaction, faction, 50f);
                    }
                    else if (action == "Alliance")
                    {
                        _diplomacySystem.DeclareAlliance(_playerFaction, faction);
                    }
                }
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (!Visible) return;

            spriteBatch.Draw(pixel, Bounds, Background);
            
            _actionButtons.Clear();
            _buttonActions.Clear();

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "=== DIPLOMACY ===", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            var relationships = _diplomacySystem.GetAllRelationships(_playerFaction);
            
            if (relationships.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No known factions", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            foreach (var rel in relationships.Take(5))
            {
                string otherFaction = rel.Faction1 == _playerFaction ? rel.Faction2 : rel.Faction1;
                
                // Faction name
                renderer.RenderText(spriteBatch, otherFaction, new Vector2(Bounds.X + 15, y), Color.White);
                y += 20;

                // Standing bar
                float barWidth = 180f;
                float fillWidth = (rel.Standing + 100f) / 200f * barWidth;
                var barRect = new Rectangle(Bounds.X + 20, (int)y, (int)barWidth, 12);
                var fillRect = new Rectangle(Bounds.X + 20, (int)y, (int)fillWidth, 12);
                
                spriteBatch.Draw(pixel, barRect, new Color(40, 40, 40, 255));
                
                var state = _diplomacySystem.GetRelationship(_playerFaction, otherFaction);
                Color barColor = state switch
                {
                    RelationshipState.Hostile => Color.Red,
                    RelationshipState.Unfriendly => Color.Orange,
                    RelationshipState.Neutral => Color.Gray,
                    RelationshipState.Friendly => Color.LightGreen,
                    RelationshipState.Allied => Color.Cyan,
                    _ => Color.Gray
                };
                spriteBatch.Draw(pixel, fillRect, barColor);
                
                renderer.RenderText(spriteBatch, $"{rel.Standing:F0}", 
                    new Vector2(Bounds.X + 210, y - 2), Color.White);
                y += 16;

                // State indicator
                bool atWar = rel.Standing <= -75f;
                string stateText = state.ToString();
                if (atWar) stateText += " [WAR]";
                renderer.RenderText(spriteBatch, $"  Status: {stateText}", 
                    new Vector2(Bounds.X + 20, y), atWar ? Color.Red : Color.LightGray);
                y += 18;

                // Action buttons
                int btnX = Bounds.X + 20;
                if (!atWar)
                {
                    var warBtn = new Rectangle(btnX, (int)y, 60, 18);
                    spriteBatch.Draw(pixel, warBtn, new Color(120, 40, 40, 255));
                    renderer.RenderText(spriteBatch, "War", new Vector2(warBtn.X + 15, warBtn.Y + 2), Color.White);
                    _actionButtons.Add(warBtn);
                    _buttonActions.Add((otherFaction, "War"));
                    btnX += 65;
                }
                else
                {
                    var peaceBtn = new Rectangle(btnX, (int)y, 60, 18);
                    spriteBatch.Draw(pixel, peaceBtn, new Color(40, 120, 40, 255));
                    renderer.RenderText(spriteBatch, "Peace", new Vector2(peaceBtn.X + 10, peaceBtn.Y + 2), Color.White);
                    btnX += 65;
                }

                if (state == RelationshipState.Friendly || state == RelationshipState.Allied)
                {
                    var allyBtn = new Rectangle(btnX, (int)y, 70, 18);
                    spriteBatch.Draw(pixel, allyBtn, new Color(40, 80, 120, 255));
                    renderer.RenderText(spriteBatch, "Alliance", new Vector2(allyBtn.X + 8, allyBtn.Y + 2), Color.White);
                    _actionButtons.Add(allyBtn);
                    _buttonActions.Add((otherFaction, "Alliance"));
                }

                y += 25;
            }
        }
    }
}
