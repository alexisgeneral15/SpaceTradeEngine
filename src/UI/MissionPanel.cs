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
    /// Displays available and active missions with accept/abandon buttons.
    /// </summary>
    public class MissionPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;
        
        private readonly MissionSystem _missionSystem;
        private readonly int _playerEntityId;
        private readonly List<Rectangle> _missionButtons = new();
        private readonly List<int> _missionIds = new();

        public MissionPanel(Rectangle bounds, MissionSystem missionSystem, int playerEntityId)
        {
            Bounds = bounds;
            _missionSystem = missionSystem;
            _playerEntityId = playerEntityId;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible) return;

            for (int i = 0; i < _missionButtons.Count; i++)
            {
                if (_missionButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    var mission = _missionSystem.GetMission(_missionIds[i]);
                    if (mission != null)
                    {
                        if (mission.State == MissionState.Available)
                        {
                            _missionSystem.AssignMission(mission.Id, _playerEntityId);
                        }
                        else if (mission.State == MissionState.Active)
                        {
                            _missionSystem.FailMission(mission.Id, "Abandoned by player");
                        }
                    }
                }
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (!Visible) return;

            spriteBatch.Draw(pixel, Bounds, Background);
            
            _missionButtons.Clear();
            _missionIds.Clear();

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "=== MISSIONS ===", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            // Available missions
            var available = _missionSystem.GetAvailableMissions();
            if (available.Count > 0)
            {
                renderer.RenderText(spriteBatch, "Available:", new Vector2(Bounds.X + 10, y), Color.Yellow);
                y += 20;

                foreach (var mission in available.Take(3))
                {
                    RenderMission(spriteBatch, pixel, renderer, mission, ref y, "Accept");
                }
            }

            y += 10;

            // Active missions
            var active = _missionSystem.GetActiveMissionsForEntity(_playerEntityId);
            if (active.Count > 0)
            {
                renderer.RenderText(spriteBatch, "Active:", new Vector2(Bounds.X + 10, y), Color.LightGreen);
                y += 20;

                foreach (var mission in active.Take(3))
                {
                    RenderMission(spriteBatch, pixel, renderer, mission, ref y, "Abandon");
                }
            }

            if (available.Count == 0 && active.Count == 0)
            {
                renderer.RenderText(spriteBatch, "No missions available", new Vector2(Bounds.X + 10, y), Color.Gray);
            }
        }

        private void RenderMission(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer, 
            Mission mission, ref float y, string buttonText)
        {
            // Mission title
            renderer.RenderText(spriteBatch, mission.Title, new Vector2(Bounds.X + 15, y), Color.White);
            y += 18;

            // Rewards
            renderer.RenderText(spriteBatch, $"  Reward: {mission.RewardCredits:F0} credits", 
                new Vector2(Bounds.X + 20, y), Color.Gold);
            y += 16;

            // Objectives
            int completed = mission.Objectives.Count(o => o.Completed);
            renderer.RenderText(spriteBatch, $"  Objectives: {completed}/{mission.Objectives.Count}", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
            y += 16;

            // Time remaining
            if (mission.TimeLimit > 0)
            {
                var timeColor = mission.TimeRemaining < 60 ? Color.Red : Color.LightBlue;
                renderer.RenderText(spriteBatch, $"  Time: {mission.TimeRemaining:F0}s", 
                    new Vector2(Bounds.X + 20, y), timeColor);
                y += 16;
            }

            // Button
            var btnRect = new Rectangle(Bounds.X + Bounds.Width - 90, (int)y - 5, 80, 20);
            spriteBatch.Draw(pixel, btnRect, new Color(80, 80, 80, 255));
            renderer.RenderText(spriteBatch, buttonText, new Vector2(btnRect.X + 10, btnRect.Y + 3), Color.White);
            
            _missionButtons.Add(btnRect);
            _missionIds.Add(mission.Id);

            y += 25;
        }
    }
}
