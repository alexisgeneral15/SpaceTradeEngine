using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Handles all rendering with spatial culling
    /// </summary>
    public class RenderingSystem
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private ContentManager _content;
        
        // Camera
        private Vector2 _cameraPosition = Vector2.Zero;
        private float _cameraZoom = 1.0f;
        
        // Debug
        private SpriteFont _debugFont;
        private Texture2D _pixelTexture;

        // Culling
        private CullingSystem _cullingSystem;
        private bool _useCulling = true;

        public RenderingSystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _content = content;
            
            // Create a 1x1 white pixel for debug rendering
            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            GlobalMemoryArena.Instance.Allocate("Rendering_PixelTexture", 1024); // track tiny GPU staging alloc
            
            // Load debug font - using Arial as fallback
            try
            {
                _debugFont = _content.Load<SpriteFont>("Arial");
            }
            catch
            {
                // If font not found, we'll skip debug text
            }
        }

        public void SetCullingSystem(CullingSystem cullingSystem)
        {
            _cullingSystem = cullingSystem;
        }

        public void ToggleCulling()
        {
            _useCulling = !_useCulling;
        }

        public void UpdateCamera(InputManager input)
        {
            // Zoom with mouse scroll
            var scrollDelta = input.MouseScrollDelta;
            if (scrollDelta != 0)
            {
                float zoomAmount = scrollDelta > 0 ? 1.1f : 0.9f;
                _cameraZoom = Math.Max(0.5f, Math.Min(3.0f, _cameraZoom * zoomAmount));
            }
        }

        public void RenderWorld(SpriteBatch spriteBatch, IEnumerable<Entity> entities)
        {
            // NOTE: NO camera matrix for now - render in screen space for debugging
            // This makes entities visible immediately
            
            // Get visible entities using culling
            IEnumerable<Entity> entitiesToRender = entities;
            if (_useCulling && _cullingSystem != null)
            {
                Vector2 viewportSize = new Vector2(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
                entitiesToRender = _cullingSystem.GetVisibleEntities(_cameraPosition, viewportSize, _cameraZoom);
            }

            // NOTE: SpriteBatch.Begin() is now called by GameEngine.Draw()
            // Just render the entities within the active batch
            
            // Center offset to make (0,0) be screen center
            Vector2 screenCenter = new Vector2(_graphicsDevice.Viewport.Width / 2f, _graphicsDevice.Viewport.Height / 2f);
            
            // Render all visible entities with sprites
            foreach (var entity in entitiesToRender)
            {
                if (!entity.IsActive)
                    continue;

                var transform = entity.GetComponent<TransformComponent>();
                var sprite = entity.GetComponent<SpriteComponent>();

                if (transform == null)
                    continue;

                // Convert world position to screen position
                Vector2 screenPos = screenCenter + transform.Position * _cameraZoom;

                if (sprite?.Texture != null)
                {
                    var origin = sprite.GetOrigin();
                    spriteBatch.Draw(
                        sprite.Texture,
                        screenPos,
                        null,
                        sprite.Tint,
                        transform.Rotation,
                        origin,
                        transform.Scale * _cameraZoom,
                        SpriteEffects.None,
                        sprite.LayerDepth
                    );
                }
                else
                {
                    // Fallback: draw COLORED SQUARES with rotation for entities without a sprite
                    var collision = entity.GetComponent<CollisionComponent>();
                    float radius = collision?.Radius ?? 15f;

                    var color = Color.White;
                    var faction = entity.GetComponent<FactionComponent>();
                    if (faction != null)
                    {
                        // quick faction tint heuristic
                        color = faction.FactionId switch
                        {
                            "human" => Color.LightBlue,
                            "pirate" => Color.Red,
                            "alien" => Color.MediumPurple,
                            _ => Color.White
                        };
                    }

                    // Rotated rectangle using 1x1 pixel texture scaled to size
                    var origin = new Vector2(0.5f, 0.5f);
                    var scale = new Vector2(radius * 2f, radius * 2f) * _cameraZoom;
                    spriteBatch.Draw(_pixelTexture,
                        position: screenPos,
                        sourceRectangle: null,
                        color: color,
                        rotation: transform.Rotation,
                        origin: origin,
                        scale: scale,
                        effects: SpriteEffects.None,
                        layerDepth: 0.5f);

                    // Heading line to show forward direction
                    var headingScale = new Vector2(radius * 2.5f, 2f) * _cameraZoom; // length x thickness
                    var headingColor = Color.Black * 0.8f;
                    spriteBatch.Draw(_pixelTexture,
                        position: screenPos,
                        sourceRectangle: null,
                        color: headingColor,
                        rotation: transform.Rotation,
                        origin: new Vector2(0f, 0.5f),
                        scale: headingScale,
                        effects: SpriteEffects.None,
                        layerDepth: 0.49f);

                    // Draw selection ring if selected
                    var selection = entity.GetComponent<SelectionComponent>();
                    if (selection != null && selection.IsSelected)
                    {
                        DrawCircle(spriteBatch, screenPos, selection.SelectionRadius * _cameraZoom, selection.SelectionColor, 2f);
                    }
                }
            }
            
            // NOTE: SpriteBatch.End() is now called by GameEngine.Draw()
        }

        public void RenderHUD(SpriteBatch spriteBatch)
        {
            // HUD rendering is driven by GameEngine (overlay texts, etc.).
        }

        public void RenderDebugText(SpriteBatch spriteBatch, string[] debugLines)
        {
            // Render debug text with simple rectangles if no font available
            Vector2 pos = new Vector2(10, _graphicsDevice.Viewport.Height - (debugLines.Length * 20) - 10);
            
            if (_debugFont != null)
            {
                // Use actual font if available
                foreach (var line in debugLines)
                {
                    spriteBatch.DrawString(_debugFont, line, pos + Vector2.One, Color.Black);
                    spriteBatch.DrawString(_debugFont, line, pos, Color.LightGreen);
                    pos.Y += 20;
                }
            }
            else
            {
                // Fallback: render colored bars to show debug is working
                foreach (var line in debugLines)
                {
                    Rectangle background = new Rectangle((int)pos.X, (int)pos.Y, 600, 18);
                    spriteBatch.Draw(_pixelTexture, background, Color.Black * 0.7f);
                    
                    // Draw colored indicator bar
                    Rectangle indicator = new Rectangle((int)pos.X + 2, (int)pos.Y + 2, 596, 14);
                    spriteBatch.Draw(_pixelTexture, indicator, Color.LightGreen * 0.5f);
                    
                    pos.Y += 20;
                }
            }
        }

        public void RenderText(SpriteBatch spriteBatch, string text, Vector2 screenPosition, Color color)
        {
            // DON'T call Begin/End here - GameEngine already has an active batch
            if (_debugFont != null)
            {
                spriteBatch.DrawString(_debugFont, text, screenPosition + Vector2.One, Color.Black);
                spriteBatch.DrawString(_debugFont, text, screenPosition, color);
            }
            else
            {
                // Fallback: draw colored rectangles
                Rectangle bar = new Rectangle((int)screenPosition.X, (int)screenPosition.Y, text.Length * 8, 16);
                spriteBatch.Draw(_pixelTexture, bar, color * 0.6f);
            }
        }

        public void RenderDebugQuadTree(SpriteBatch spriteBatch, List<Rectangle> quadTreeBounds)
        {
            if (quadTreeBounds == null || quadTreeBounds.Count == 0)
                return;

            var cameraTransform = Matrix.CreateTranslation(-_cameraPosition.X, -_cameraPosition.Y, 0) *
                                 Matrix.CreateScale(_cameraZoom, _cameraZoom, 1f) *
                                 Matrix.CreateTranslation(_graphicsDevice.Viewport.Width / 2f, 
                                                         _graphicsDevice.Viewport.Height / 2f, 0);

            spriteBatch.Begin(transformMatrix: cameraTransform);

            foreach (var bounds in quadTreeBounds)
            {
                DrawRectangle(spriteBatch, bounds, Color.Cyan * 0.3f, 1f);
            }

            spriteBatch.End();
        }

        private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, float thickness)
        {
            // Top
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, (int)thickness), color);
            // Bottom
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - (int)thickness, rect.Width, (int)thickness), color);
            // Left
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, (int)thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - (int)thickness, rect.Y, (int)thickness, rect.Height), color);
        }

        private void DrawCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness)
        {
            // OPTIMIZADO: 16 segmentos en lugar de 32 (mitad de draw calls, sigue siendo suave)
            const int segments = 16;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;

                DrawLine(spriteBatch, p1, p2, color, thickness);
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            spriteBatch.Draw(_pixelTexture,
                new Rectangle((int)start.X, (int)start.Y, (int)length, (int)thickness),
                null,
                color,
                angle,
                Vector2.Zero,
                SpriteEffects.None,
                0);
        }

        public Vector2 CameraPosition => _cameraPosition;
        public float CameraZoom => _cameraZoom;

        public void SetCameraPosition(Vector2 position)
        {
            _cameraPosition = position;
        }

        public void SetCameraZoom(float zoom)
        {
            _cameraZoom = MathHelper.Clamp(zoom, 0.5f, 3.0f);
        }

        public Texture2D GetPixelTexture() => _pixelTexture;

        public Vector2 WorldToScreen(Vector2 world)
        {
            var vp = _graphicsDevice.Viewport;
            var x = (world.X - _cameraPosition.X) * _cameraZoom + vp.Width / 2f;
            var y = (world.Y - _cameraPosition.Y) * _cameraZoom + vp.Height / 2f;
            return new Vector2(x, y);
        }

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            var vp = _graphicsDevice.Viewport;
            float worldX = (screen.X - vp.Width / 2f) / _cameraZoom + _cameraPosition.X;
            float worldY = (screen.Y - vp.Height / 2f) / _cameraZoom + _cameraPosition.Y;
            return new Vector2(worldX, worldY);
        }
    }
}
