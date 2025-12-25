using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Auto-ajustador de pantalla que detecta resolución del monitor
    /// y adapta ventana automáticamente con márgenes seguros
    /// </summary>
    public class ScreenAdapter
    {
        private GraphicsDeviceManager _graphics;
        private GameWindow _gameWindow;
        private int _screenWidth;
        private int _screenHeight;
        private int _safeWidth;
        private int _safeHeight;
        private float _safeAreaPercentage = 0.60f; // 60% del tamaño de pantalla para ahorrar memoria/VRAM

        public int ScreenWidth => _screenWidth;
        public int ScreenHeight => _screenHeight;
        public int SafeWidth => _safeWidth;
        public int SafeHeight => _safeHeight;

        public ScreenAdapter(GraphicsDeviceManager graphics, GameWindow gameWindow)
        {
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _gameWindow = gameWindow ?? throw new ArgumentNullException(nameof(gameWindow));
        }

        /// <summary>
        /// Detecta resolución del monitor principal y ajusta ventana automáticamente
        /// </summary>
        public void AutoDetectAndAdaptScreen()
        {
            try
            {
                // Obtener resolución del monitor principal
                var displayMode = _graphics.GraphicsDevice.Adapter.CurrentDisplayMode;
                _screenWidth = displayMode.Width;
                _screenHeight = displayMode.Height;

                // Calcular tamaño seguro (85% para evitar desbordamiento)
                _safeWidth = (int)(_screenWidth * _safeAreaPercentage);
                _safeHeight = (int)(_screenHeight * _safeAreaPercentage);

                // Aplicar tamaño seguro a la ventana
                _graphics.PreferredBackBufferWidth = _safeWidth;
                _graphics.PreferredBackBufferHeight = _safeHeight;
                _graphics.IsFullScreen = false; // Modo ventana para evitar problemas
                _graphics.ApplyChanges();

                Console.WriteLine($"[ScreenAdapter] Monitor: {_screenWidth}x{_screenHeight} → Window: {_safeWidth}x{_safeHeight} (85% safe area)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScreenAdapter] Error detecting screen: {ex.Message}");
                // Fallback a resolución conservadora
                ApplyFallbackResolution();
            }
        }

        /// <summary>
        /// Ajusta tamaño de ventana a porcentaje específico de pantalla
        /// </summary>
        public void SetScreenPercentage(float percentage)
        {
            if (percentage <= 0f || percentage > 1f)
            {
                Console.WriteLine($"[ScreenAdapter] Invalid percentage: {percentage}. Using 0.85 instead.");
                percentage = 0.85f;
            }

            _safeAreaPercentage = percentage;
            AutoDetectAndAdaptScreen();
        }

        /// <summary>
        /// Resolución de respaldo cuando falla detección
        /// </summary>
        private void ApplyFallbackResolution()
        {
            _safeWidth = 1280;
            _safeHeight = 720;
            
            _graphics.PreferredBackBufferWidth = _safeWidth;
            _graphics.PreferredBackBufferHeight = _safeHeight;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();

            Console.WriteLine($"[ScreenAdapter] Using fallback resolution: {_safeWidth}x{_safeHeight}");
        }

        /// <summary>
        /// Retorna información de adaptación para debug
        /// </summary>
        public string GetAdapterInfo()
        {
            return $"Monitor: {_screenWidth}x{_screenHeight} | Window: {_safeWidth}x{_safeHeight} ({(_safeAreaPercentage * 100):F0}%)";
        }

        /// <summary>
        /// Cambia a modo borderless fullscreen (ocupa toda la pantalla sin marcos)
        /// </summary>
        public void SetBorderlessFullscreen()
        {
            try
            {
                _graphics.PreferredBackBufferWidth = _screenWidth;
                _graphics.PreferredBackBufferHeight = _screenHeight;
                _graphics.IsFullScreen = false;
                _graphics.ApplyChanges();

                Console.WriteLine($"[ScreenAdapter] Borderless fullscreen: {_screenWidth}x{_screenHeight}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScreenAdapter] Error setting borderless: {ex.Message}");
            }
        }
    }
}
