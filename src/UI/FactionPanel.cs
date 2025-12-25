using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Gameplay;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// UI Panel showing faction relationships, player reputation, and recent events.
    /// Sprint 2: Faction and diplomacy information.
    /// </summary>
    public class FactionPanel : IUIElement
    {
        private FactionManager _factionManager;
        private EventManager _eventManager;
        private Entity? _playerShip;
        private bool _isVisible = true;
        private Vector2 _position = new Vector2(10, 150);
        private SpriteFont? _font;
        
        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public FactionPanel(FactionManager factionManager, EventManager eventManager)
        {
            _factionManager = factionManager;
            _eventManager = eventManager;
        }

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetPlayerShip(Entity playerShip)
        {
            _playerShip = playerShip;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Update(InputManager input, Point mouse)
        {
            // No interactive elements yet
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (!_isVisible)
                return;

            // Get font from renderer if not set
            if (_font == null && renderer != null)
            {
                // FactionPanel will use the default font from rendering system
                // For now, skip rendering if no font available
                return;
            }

            if (_font == null)
                return;

            var y = _position.Y;
            var lineHeight = 20f;
            var indent = 15f;

            // Title
            spriteBatch.DrawString(_font, "=== FACTION STATUS ===", 
                new Vector2(_position.X, y), Color.Gold);
            y += lineHeight * 1.5f;

            // Player reputation
            if (_playerShip != null)
            {
                var reputation = _playerShip.GetComponent<ReputationComponent>();
                if (reputation != null)
                {
                    spriteBatch.DrawString(_font, "Your Reputation:", 
                        new Vector2(_position.X, y), Color.White);
                    y += lineHeight;

                    foreach (var faction in _factionManager.GetAllFactions())
                    {
                        var factionComp = faction.GetComponent<FactionComponent>();
                        if (factionComp == null) continue;

                        float rep = reputation.GetReputation(factionComp.FactionId);
                        string standing = reputation.GetStanding(factionComp.FactionId);
                        Color standingColor = GetStandingColor(standing);

                        string repText = $"  {factionComp.FactionName}: {rep:F0} ({standing})";
                        spriteBatch.DrawString(_font, repText, 
                            new Vector2(_position.X + indent, y), standingColor);
                        y += lineHeight;
                    }
                }
            }

            y += lineHeight * 0.5f;

            // Faction relationships
            spriteBatch.DrawString(_font, "Faction Relations:", 
                new Vector2(_position.X, y), Color.White);
            y += lineHeight;

            var relations = _factionManager.GetAllRelations().Take(5).ToList();
            foreach (var relation in relations)
            {
                var attitudeColor = GetAttitudeColor(relation.GetAttitude());
                string treaty = relation.Treaty != TreatyType.None ? $" [{relation.Treaty}]" : "";
                string relationText = $"  {relation.FactionA} <-> {relation.FactionB}: {relation.GetAttitude()}{treaty}";
                
                spriteBatch.DrawString(_font, relationText, 
                    new Vector2(_position.X + indent, y), attitudeColor);
                y += lineHeight;
            }

            y += lineHeight * 0.5f;

            // Recent events
            spriteBatch.DrawString(_font, "Recent Events:", 
                new Vector2(_position.X, y), Color.White);
            y += lineHeight;

            var events = _eventManager.GetRecentEvents(5).ToList();
            if (events.Count == 0)
            {
                spriteBatch.DrawString(_font, "  No recent events", 
                    new Vector2(_position.X + indent, y), Color.Gray);
                y += lineHeight;
            }
            else
            {
                foreach (var evt in events)
                {
                    string timeAgo = GetTimeAgo(evt.OccurredAt);
                    string eventText = $"  {evt.Title} ({timeAgo})";
                    Color eventColor = evt.IsActive ? Color.Yellow : Color.Gray;
                    
                    spriteBatch.DrawString(_font, eventText, 
                        new Vector2(_position.X + indent, y), eventColor);
                    y += lineHeight;
                }
            }

            // Controls hint
            y += lineHeight * 0.5f;
            spriteBatch.DrawString(_font, "[F3] Toggle Faction Panel", 
                new Vector2(_position.X, y), Color.DarkGray);
        }

        private Color GetStandingColor(string standing)
        {
            return standing switch
            {
                "Allied" => Color.LimeGreen,
                "Friendly" => Color.LightGreen,
                "Neutral" => Color.Yellow,
                "Unfriendly" => Color.Orange,
                "Hostile" => Color.Red,
                _ => Color.White
            };
        }

        private Color GetAttitudeColor(FactionAttitude attitude)
        {
            return attitude switch
            {
                FactionAttitude.Allied => Color.LimeGreen,
                FactionAttitude.Friendly => Color.LightGreen,
                FactionAttitude.Neutral => Color.Yellow,
                FactionAttitude.Hostile => Color.Orange,
                FactionAttitude.War => Color.Red,
                _ => Color.White
            };
        }

        private string GetTimeAgo(DateTime time)
        {
            var diff = DateTime.Now - time;
            if (diff.TotalSeconds < 60)
                return $"{(int)diff.TotalSeconds}s ago";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m ago";
            return $"{(int)diff.TotalHours}h ago";
        }
    }
}
