using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Simple minimap rendering entities as dots scaled to world bounds.
    /// </summary>
    public class Minimap : IUIElement
    {
        private readonly SpatialPartitioningSystem _spatial;
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 180);
        public Color DotColor { get; set; } = Color.LightGray;

        public Minimap(SpatialPartitioningSystem spatial, Rectangle bounds)
        {
            _spatial = spatial;
            Bounds = bounds;
        }

        public void Update(InputManager input, Point mouse)
        {
            // No interaction for now
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, Background);
            var stats = _spatial.GetStats();
            var world = stats.WorldBounds;

            // Query visible entities using camera frustum for efficiency
            var vp = renderer.CameraZoom;
            List<Entity> entities = _spatial.QueryArea(world);

            foreach (var e in entities)
            {
                var t = e.GetComponent<SpaceTradeEngine.ECS.Components.TransformComponent>();
                if (t == null) continue;

                // Map world position to minimap bounds
                float xNorm = (t.Position.X - world.X) / (float)world.Width;
                float yNorm = (t.Position.Y - world.Y) / (float)world.Height;
                int x = Bounds.X + (int)(xNorm * Bounds.Width);
                int y = Bounds.Y + (int)(yNorm * Bounds.Height);
                var dotRect = new Rectangle(x, y, 2, 2);

                var color = DotColor;
                var faction = e.GetComponent<SpaceTradeEngine.ECS.Components.FactionComponent>();
                if (faction != null)
                {
                    color = faction.FactionId switch
                    {
                        "human" => Color.LightBlue,
                        "alien" => Color.MediumPurple,
                        _ => DotColor
                    };
                }

                spriteBatch.Draw(pixel, dotRect, color);
            }
        }
    }
}
