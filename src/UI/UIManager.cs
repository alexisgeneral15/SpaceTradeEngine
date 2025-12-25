using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Minimal UI framework: panel + button with click handling.
    /// </summary>
    public class UIManager
    {
        private readonly GraphicsDevice _graphics;
        private readonly Texture2D _pixel;
        private readonly List<IUIElement> _elements = new();

        public UIManager(GraphicsDevice graphics)
        {
            _graphics = graphics;
            _pixel = new Texture2D(_graphics, 1, 1);
            _pixel.SetData(new[] { Color.White });
            GlobalMemoryArena.Instance.Allocate("UIManager_Pixel", 1024);
        }

        public void Add(IUIElement element) => _elements.Add(element);

        public void Update(InputManager input)
        {
            var mouse = new Point(input.MouseX, input.MouseY);
            foreach (var e in _elements)
                e.Update(input, mouse);
        }

        public void Render(SpriteBatch spriteBatch, Systems.RenderingSystem renderer)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            foreach (var e in _elements)
                e.Render(spriteBatch, _pixel, renderer);
            spriteBatch.End();
        }
    }

    public interface IUIElement
    {
        void Update(InputManager input, Point mouse);
        void Render(SpriteBatch spriteBatch, Texture2D pixel, Systems.RenderingSystem renderer);
    }

    public class UIPanel : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public Color Background { get; set; } = new Color(0, 0, 0, 180);
        public List<IUIElement> Children { get; } = new();

        public UIPanel(Rectangle bounds)
        {
            Bounds = bounds;
        }

        public void Add(IUIElement child) => Children.Add(child);

        public void Update(InputManager input, Point mouse)
        {
            foreach (var c in Children) c.Update(input, mouse);
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, Systems.RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, Background);
            foreach (var c in Children) c.Render(spriteBatch, pixel, renderer);
        }
    }

    public class UIButton : IUIElement
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; set; }
        public Action OnClick { get; set; }
        public Color BgColor { get; set; } = new Color(30, 30, 30, 220);
        public Color HoverColor { get; set; } = new Color(60, 60, 60, 220);
        public Color TextColor { get; set; } = Color.White;
        private bool _hover;

        public UIButton(Rectangle bounds, string text, Action onClick)
        {
            Bounds = bounds;
            Text = text;
            OnClick = onClick;
        }

        public void Update(InputManager input, Point mouse)
        {
            _hover = Bounds.Contains(mouse);
            if (_hover && input.IsMouseLeftClicked)
            {
                OnClick?.Invoke();
            }
        }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, Systems.RenderingSystem renderer)
        {
            spriteBatch.Draw(pixel, Bounds, _hover ? HoverColor : BgColor);
            // Center text if renderer has a font
            var center = new Vector2(Bounds.X + Bounds.Width / 2f, Bounds.Y + Bounds.Height / 2f);
            // Use renderer.RenderText (may be no font; method is safe)
            renderer.RenderText(spriteBatch, Text, center - new Vector2(Text.Length * 3.5f, 8f), TextColor);
        }
    }

    /// <summary>
    /// Convenience factory for a simple top-right toolbar with Save/Load buttons.
    /// </summary>
    public static class UIFactory
    {
        public static UIPanel CreateTopRightToolbar(GraphicsDevice graphics, int viewportWidth, Action onSave, Action onLoad)
        {
            var panelRect = new Rectangle(viewportWidth - 220, 10, 210, 60);
            var panel = new UIPanel(panelRect);
            panel.Add(new UIButton(new Rectangle(panelRect.X + 10, panelRect.Y + 10, 90, 40), "Save (F6)", onSave));
            panel.Add(new UIButton(new Rectangle(panelRect.X + 110, panelRect.Y + 10, 90, 40), "Load (F7)", onLoad));
            return panel;
        }
    }
}
