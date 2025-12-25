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
    /// Displays faction AI behavior, satellite factions, and clan management.
    /// </summary>
    public class FactionAIPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;

        private readonly FactionAISystem _factionAISystem;
        private readonly DiplomacySystem _diplomacySystem;
        private readonly ClanSystem _clanSystem;
        private readonly List<Rectangle> _actionButtons = new();
        private readonly List<(string factionId, string action)> _buttonActions = new();
        private string? _selectedFaction = null;

        public FactionAIPanel(Rectangle bounds, FactionAISystem factionAISystem, 
            DiplomacySystem diplomacySystem, ClanSystem clanSystem)
        {
            Bounds = bounds;
            _factionAISystem = factionAISystem;
            _diplomacySystem = diplomacySystem;
            _clanSystem = clanSystem;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible) return;

            for (int i = 0; i < _actionButtons.Count; i++)
            {
                if (_actionButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    var (factionId, action) = _buttonActions[i];

                    if (action == "Select")
                    {
                        _selectedFaction = factionId;
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
            renderer.RenderText(spriteBatch, "=== FACTION AI CONTROL ===", 
                new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            // Show faction list with basic info
            var knownFactions = GetKnownFactions();
            
            if (knownFactions.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No AI factions active", 
                    new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            // Display up to 5 factions
            foreach (var factionId in knownFactions.Take(5))
            {
                var controller = _factionAISystem.GetFactionController(factionId);
                if (controller == null) continue;

                bool isSatellite = controller.MotherFactionId != null;
                string prefix = isSatellite ? "  └─ " : "> ";
                Color nameColor = isSatellite ? Color.LightGray : Color.LightYellow;

                // Faction name
                renderer.RenderText(spriteBatch, $"{prefix}{controller.Profile.FactionName}", 
                    new Vector2(Bounds.X + 15, y), nameColor);
                y += 16;

                // Stats
                string stats = $"  Treasury: {controller.Treasury:F0} | " +
                               $"Clans: {controller.ManagedClans.Count} | " +
                               $"Satellites: {controller.SatelliteFactions.Count}";
                renderer.RenderText(spriteBatch, stats, 
                    new Vector2(Bounds.X + 20, y), Color.LightGray);
                y += 14;

                // Traits indicator
                string traits = $"  Aggr:{controller.Profile.Aggressiveness:F1} " +
                               $"Exp:{controller.Profile.Expansionist:F1} " +
                               $"Dipl:{(controller.Profile.Diplomatic ? "Y" : "N")}";
                renderer.RenderText(spriteBatch, traits, 
                    new Vector2(Bounds.X + 20, y), new Color(150, 150, 150));
                y += 14;

                // Select button
                var selectBtn = new Rectangle(Bounds.X + 20, (int)y, 80, 18);
                spriteBatch.Draw(pixel, selectBtn, new Color(60, 60, 100, 255));
                renderer.RenderText(spriteBatch, "Select", 
                    new Vector2(selectBtn.X + 20, selectBtn.Y + 2), Color.White);
                _actionButtons.Add(selectBtn);
                _buttonActions.Add((factionId, "Select"));
                y += 24;

                // Show satellites indented
                foreach (var satelliteId in controller.SatelliteFactions.Take(2))
                {
                    var satController = _factionAISystem.GetFactionController(satelliteId);
                    if (satController == null) continue;

                    renderer.RenderText(spriteBatch, $"    ├─ {satController.Profile.FactionName}", 
                        new Vector2(Bounds.X + 25, y), Color.Gray);
                    y += 14;
                }
            }

            // Selected faction details
            if (!string.IsNullOrEmpty(_selectedFaction))
            {
                var selected = _factionAISystem.GetFactionController(_selectedFaction);
                if (selected != null)
                {
                    y += 10;
                    renderer.RenderText(spriteBatch, $"SELECTED: {selected.Profile.FactionName}", 
                        new Vector2(Bounds.X + 10, y), Color.Cyan);
                    y += 18;

                    // Detailed stats
                    renderer.RenderText(spriteBatch, $"Treasury: {selected.Treasury:F0} credits", 
                        new Vector2(Bounds.X + 15, y), Color.White);
                    y += 14;

                    renderer.RenderText(spriteBatch, $"Income: {selected.Profile.IncomeRate:F0}/min", 
                        new Vector2(Bounds.X + 15, y), Color.White);
                    y += 14;

                    renderer.RenderText(spriteBatch, $"Expansions: {selected.ExpansionCount}", 
                        new Vector2(Bounds.X + 15, y), Color.White);
                    y += 14;

                    // Mother faction link
                    if (selected.MotherFactionId != null)
                    {
                        var mother = _factionAISystem.GetFactionController(selected.MotherFactionId);
                        string motherName = mother?.Profile.FactionName ?? "Unknown";
                        renderer.RenderText(spriteBatch, $"Mother: {motherName}", 
                            new Vector2(Bounds.X + 15, y), Color.Yellow);
                        y += 14;

                        float standing = _diplomacySystem.GetStanding(_selectedFaction, selected.MotherFactionId);
                        renderer.RenderText(spriteBatch, $"Loyalty: {standing:F0}", 
                            new Vector2(Bounds.X + 15, y), GetStandingColor(standing));
                        y += 14;
                    }

                    // Managed clans
                    if (selected.ManagedClans.Count > 0)
                    {
                        y += 8;
                        renderer.RenderText(spriteBatch, "Clans:", 
                            new Vector2(Bounds.X + 15, y), Color.LightBlue);
                        y += 14;

                        foreach (var clanId in selected.ManagedClans.Take(3))
                        {
                            var clan = _clanSystem.GetClan(clanId);
                            if (clan != null)
                            {
                                renderer.RenderText(spriteBatch, 
                                    $"  • {clan.ClanName} ({clan.Members.Count} ships)", 
                                    new Vector2(Bounds.X + 20, y), Color.LightGray);
                                y += 12;
                            }
                        }
                    }

                    // Satellite factions
                    if (selected.SatelliteFactions.Count > 0)
                    {
                        y += 8;
                        renderer.RenderText(spriteBatch, "Satellites:", 
                            new Vector2(Bounds.X + 15, y), Color.LightBlue);
                        y += 14;

                        foreach (var satId in selected.SatelliteFactions.Take(3))
                        {
                            var sat = _factionAISystem.GetFactionController(satId);
                            if (sat != null)
                            {
                                renderer.RenderText(spriteBatch, 
                                    $"  • {sat.Profile.FactionName}", 
                                    new Vector2(Bounds.X + 20, y), Color.LightGray);
                                y += 12;
                            }
                        }
                    }
                }
            }
        }

        private List<string> GetKnownFactions()
        {
            var factions = new List<string>();
            
            // Get all faction relationships to discover factions
            var allRelations = _diplomacySystem.GetAllRelationships("player");
            foreach (var rel in allRelations)
            {
                string factionId = rel.Faction1 == "player" ? rel.Faction2 : rel.Faction1;
                var controller = _factionAISystem.GetFactionController(factionId);
                if (controller != null && !factions.Contains(factionId))
                {
                    factions.Add(factionId);
                    
                    // Add satellites
                    foreach (var satId in controller.SatelliteFactions)
                    {
                        if (!factions.Contains(satId))
                            factions.Add(satId);
                    }
                }
            }

            return factions;
        }

        private Color GetStandingColor(float standing)
        {
            if (standing >= 75f) return Color.Cyan;
            if (standing >= 25f) return Color.LightGreen;
            if (standing >= -25f) return Color.Gray;
            if (standing >= -50f) return Color.Orange;
            return Color.Red;
        }
    }
}
