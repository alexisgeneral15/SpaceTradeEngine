using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.AI;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Systems;

#nullable enable
namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Displays clan hierarchies, sub-clans, and clan relationships.
    /// </summary>
    public class ClanPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;

        private readonly ClanSystem _clanSystem;
        private readonly string _playerFaction;
        private readonly List<Rectangle> _actionButtons = new();
        private readonly List<(string clan, string action)> _buttonActions = new();
        private string? _selectedClan = null;

        public ClanPanel(Rectangle bounds, ClanSystem clanSystem, string playerFaction)
        {
            Bounds = bounds;
            _clanSystem = clanSystem;
            _playerFaction = playerFaction;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible) return;

            for (int i = 0; i < _actionButtons.Count; i++)
            {
                if (_actionButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    var (clan, action) = _buttonActions[i];

                    if (action == "Select")
                    {
                        _selectedClan = clan;
                    }
                    else if (action == "War")
                    {
                        // Declare war between clans
                        var otherClans = _clanSystem.GetClansForFaction(_playerFaction);
                        if (otherClans.Count > 0)
                        {
                            var other = otherClans.FirstOrDefault(c => c.ClanId != clan);
                            if (other != null)
                                _clanSystem.DeclareClanWar(clan, other.ClanId, "Clan war initiated");
                        }
                    }
                    else if (action == "Alliance")
                    {
                        // Form alliance between clans
                        var otherClans = _clanSystem.GetClansForFaction(_playerFaction);
                        if (otherClans.Count > 0)
                        {
                            var other = otherClans.FirstOrDefault(c => c.ClanId != clan);
                            if (other != null)
                                _clanSystem.DeclareClanAlliance(clan, other.ClanId, "Alliance formed");
                        }
                    }
                    else if (action == "Peace")
                    {
                        // Improve relations
                        var otherClans = _clanSystem.GetClansForFaction(_playerFaction);
                        if (otherClans.Count > 0)
                        {
                            var other = otherClans.FirstOrDefault(c => c.ClanId != clan);
                            if (other != null)
                                _clanSystem.ModifyClanRelationship(clan, other.ClanId, 50f, "Peace negotiated");
                        }
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
            renderer.RenderText(spriteBatch, "=== CLAN HIERARCHY ===", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            // Get main clans for player faction
            var mainClans = _clanSystem.GetClansForFaction(_playerFaction);

            if (mainClans.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No clans in faction", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            // Display main clans
            foreach (var clan in mainClans.Take(4))
            {
                // Clan header
                renderer.RenderText(spriteBatch, $"> {clan.ClanName}", 
                    new Vector2(Bounds.X + 15, y), Color.LightYellow);
                y += 16;

                // Clan info
                renderer.RenderText(spriteBatch, 
                    $"  Members: {clan.Members.Count} | Strength: {clan.GetStrengthPercent():P0} | Rep: {clan.Reputation:F0}",
                    new Vector2(Bounds.X + 20, y), Color.LightGray);
                y += 14;

                // Sub-clans
                var subClans = _clanSystem.GetSubClans(clan.ClanId);
                if (subClans.Count > 0)
                {
                    renderer.RenderText(spriteBatch, $"  Sub-clans: {subClans.Count}",
                        new Vector2(Bounds.X + 25, y), Color.Gray);
                    y += 12;
                }

                // Select button
                var selectBtn = new Rectangle(Bounds.X + 20, (int)y, 80, 18);
                spriteBatch.Draw(pixel, selectBtn, new Color(60, 60, 100, 255));
                renderer.RenderText(spriteBatch, "Select", new Vector2(selectBtn.X + 5, selectBtn.Y + 2), Color.White);
                _actionButtons.Add(selectBtn);
                _buttonActions.Add((clan.ClanId, "Select"));
                y += 22;
            }

            // Display selected clan details
            if (!string.IsNullOrEmpty(_selectedClan))
            {
                var selectedClan = _clanSystem.GetClan(_selectedClan);
                if (selectedClan != null)
                {
                    y += 10;
                    renderer.RenderText(spriteBatch, $"SELECTED: {selectedClan.ClanName}", 
                        new Vector2(Bounds.X + 10, y), Color.Cyan);
                    y += 18;

                    // Show relationships
                    var relationships = _clanSystem.GetAllClanRelationships(_selectedClan);
                    foreach (var rel in relationships.Take(2))
                    {
                        string otherClan = rel.Clan1 == _selectedClan ? rel.Clan2 : rel.Clan1;
                        var state = _clanSystem.GetClanRelationship(_selectedClan, otherClan);
                        
                        renderer.RenderText(spriteBatch, $"{otherClan}: {state} ({rel.Standing:F0})",
                            new Vector2(Bounds.X + 15, y), GetStateColor(state));
                        y += 14;
                    }

                    // Action buttons
                    y += 8;
                    int btnX = Bounds.X + 20;
                    var warBtn = new Rectangle(btnX, (int)y, 60, 18);
                    spriteBatch.Draw(pixel, warBtn, new Color(200, 50, 50, 255));
                    renderer.RenderText(spriteBatch, "War", new Vector2(warBtn.X + 15, warBtn.Y + 2), Color.White);
                    _actionButtons.Add(warBtn);
                    _buttonActions.Add((_selectedClan, "War"));

                    btnX += 70;
                    var peaceBtn = new Rectangle(btnX, (int)y, 60, 18);
                    spriteBatch.Draw(pixel, peaceBtn, new Color(50, 150, 50, 255));
                    renderer.RenderText(spriteBatch, "Peace", new Vector2(peaceBtn.X + 10, peaceBtn.Y + 2), Color.White);
                    _actionButtons.Add(peaceBtn);
                    _buttonActions.Add((_selectedClan, "Peace"));

                    btnX += 70;
                    var allyBtn = new Rectangle(btnX, (int)y, 60, 18);
                    spriteBatch.Draw(pixel, allyBtn, new Color(50, 100, 200, 255));
                    renderer.RenderText(spriteBatch, "Alliance", new Vector2(allyBtn.X + 5, allyBtn.Y + 2), Color.White);
                    _actionButtons.Add(allyBtn);
                    _buttonActions.Add((_selectedClan, "Alliance"));
                }
            }
        }

        private Color GetStateColor(ClanRelationshipState state)
        {
            return state switch
            {
                ClanRelationshipState.Hostile => Color.Red,
                ClanRelationshipState.Unfriendly => Color.Orange,
                ClanRelationshipState.Neutral => Color.Gray,
                ClanRelationshipState.Friendly => Color.LightGreen,
                ClanRelationshipState.Allied => Color.Cyan,
                _ => Color.Gray
            };
        }
    }
}
