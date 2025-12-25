using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.Core;

namespace SpaceTradeEngine.UI
{
    /// <summary>
    /// Renders the current drag selection rectangle in screen space.
    /// </summary>
    public class SelectionBoxOverlay : IUIElement
    {
        public Rectangle Rect { get; set; }
        public bool Visible { get; set; }

        public void Update(InputManager input, Point mouse) { }

        public void Render(SpriteBatch spriteBatch, Texture2D pixel, Systems.RenderingSystem renderer)
        {
            if (!Visible || Rect.Width <= 0 || Rect.Height <= 0) return;
            // Semi-transparent fill
            spriteBatch.Draw(pixel, Rect, new Color(0, 120, 255, 50));
            // Border
            spriteBatch.Draw(pixel, new Rectangle(Rect.X, Rect.Y, Rect.Width, 2), Color.CornflowerBlue);
            spriteBatch.Draw(pixel, new Rectangle(Rect.X, Rect.Bottom - 2, Rect.Width, 2), Color.CornflowerBlue);
            spriteBatch.Draw(pixel, new Rectangle(Rect.X, Rect.Y, 2, Rect.Height), Color.CornflowerBlue);
            spriteBatch.Draw(pixel, new Rectangle(Rect.Right - 2, Rect.Y, 2, Rect.Height), Color.CornflowerBlue);
        }
    }
}
