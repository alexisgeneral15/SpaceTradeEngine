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
    /// Displays factory production queue and controls.
    /// </summary>
    public class ProductionPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 200);
        public bool Visible { get; set; } = false;
        
        private readonly ProductionSystem _productionSystem;
        private readonly ECS.EntityManager _entityManager;
        private Entity? _selectedFactory;
        private readonly List<Rectangle> _cancelButtons = new();
        private readonly List<int> _jobIndices = new();

        public ProductionPanel(Rectangle bounds, ProductionSystem productionSystem, ECS.EntityManager entityManager)
        {
            Bounds = bounds;
            _productionSystem = productionSystem;
            _entityManager = entityManager;
        }

        public void SetSelectedFactory(Entity? factory)
        {
            _selectedFactory = factory;
        }

        public void Update(InputManager input, Point mouse)
        {
            if (!Visible || _selectedFactory == null) return;

            for (int i = 0; i < _cancelButtons.Count; i++)
            {
                if (_cancelButtons[i].Contains(mouse) && input.IsMouseLeftClicked)
                {
                    _productionSystem.CancelProduction(_selectedFactory.Id, _jobIndices[i]);
                }
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, RenderingSystem renderer)
        {
            if (!Visible) return;

            spriteBatch.Draw(pixel, Bounds, Background);
            
            _cancelButtons.Clear();
            _jobIndices.Clear();

            float y = Bounds.Y + 10;
            renderer.RenderText(spriteBatch, "=== PRODUCTION ===", new Vector2(Bounds.X + 10, y), Color.Cyan);
            y += 25;

            if (_selectedFactory == null || !_selectedFactory.IsActive)
            {
                renderer.RenderText(spriteBatch, "No factory selected", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            var factory = _selectedFactory.GetComponent<FactoryComponent>();
            if (factory == null)
            {
                renderer.RenderText(spriteBatch, "Selected entity is not a factory", new Vector2(Bounds.X + 10, y), Color.Gray);
                return;
            }

            // Factory status
            var statusColor = factory.IsActive ? Color.LightGreen : Color.Red;
            renderer.RenderText(spriteBatch, $"Status: {(factory.IsActive ? "Active" : "Inactive")}", 
                new Vector2(Bounds.X + 15, y), statusColor);
            y += 20;

            renderer.RenderText(spriteBatch, $"Efficiency: {factory.Efficiency * 100:F0}%", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
            y += 18;

            renderer.RenderText(spriteBatch, $"Rate: {factory.ProductionRate}x", 
                new Vector2(Bounds.X + 20, y), Color.LightGray);
            y += 18;

            renderer.RenderText(spriteBatch, $"Completed: {factory.CompletedJobs}", 
                new Vector2(Bounds.X + 20, y), Color.Gold);
            y += 25;

            // Production queue
            if (factory.ProductionQueue.Count == 0)
            {
                renderer.RenderText(spriteBatch, "Production queue empty", new Vector2(Bounds.X + 15, y), Color.Gray);
                return;
            }

            renderer.RenderText(spriteBatch, $"Queue ({factory.ProductionQueue.Count}):", 
                new Vector2(Bounds.X + 15, y), Color.Yellow);
            y += 20;

            for (int i = 0; i < Math.Min(factory.ProductionQueue.Count, 4); i++)
            {
                var job = factory.ProductionQueue[i];
                
                // Job info
                renderer.RenderText(spriteBatch, $"{i + 1}. {job.RecipeId}", 
                    new Vector2(Bounds.X + 20, y), Color.White);
                y += 18;

                // Progress bar
                float progress = job.Progress / job.ProductionTime;
                float barWidth = 160f;
                float fillWidth = progress * barWidth;
                
                var barRect = new Rectangle(Bounds.X + 25, (int)y, (int)barWidth, 10);
                var fillRect = new Rectangle(Bounds.X + 25, (int)y, (int)fillWidth, 10);
                
                spriteBatch.Draw(pixel, barRect, new Color(40, 40, 40, 255));
                spriteBatch.Draw(pixel, fillRect, new Color(80, 150, 80, 255));
                
                renderer.RenderText(spriteBatch, $"{progress * 100:F0}%", 
                    new Vector2(Bounds.X + 195, y - 3), Color.White);
                y += 14;

                // Output
                renderer.RenderText(spriteBatch, $"   → {job.OutputWareId} x{job.OutputQuantity}", 
                    new Vector2(Bounds.X + 25, y), Color.LightBlue);
                y += 16;

                // Cancel button (except for current job)
                if (i > 0)
                {
                    var cancelBtn = new Rectangle(Bounds.X + Bounds.Width - 70, (int)y - 40, 60, 18);
                    spriteBatch.Draw(pixel, cancelBtn, new Color(120, 40, 40, 255));
                    renderer.RenderText(spriteBatch, "Cancel", 
                        new Vector2(cancelBtn.X + 12, cancelBtn.Y + 2), Color.White);
                    
                    _cancelButtons.Add(cancelBtn);
                    _jobIndices.Add(i);
                }

                y += 8;
            }

            if (factory.ProductionQueue.Count > 4)
            {
                renderer.RenderText(spriteBatch, $"   ... and {factory.ProductionQueue.Count - 4} more", 
                    new Vector2(Bounds.X + 25, y), Color.Gray);
            }
        }
    }
}
