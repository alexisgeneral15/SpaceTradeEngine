using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Gestiona ventana, resolución, fullscreen, VSync, y propiedades gráficas
    /// </summary>
    public class WindowManager
    {
        public enum Resolution
        {
            _1280x720 = 0,
            _1366x768 = 1,
            _1600x900 = 2,
            _1920x1080 = 3,
            _2560x1440 = 4,
            _3840x2160 = 5
        }

        private GraphicsDeviceManager _graphics;
        private GameWindow _gameWindow;
        private int _width = 1280;
        private int _height = 720;
        private bool _isFullscreen = false;
        private bool _vsyncEnabled = true;
        private Resolution _currentResolution = Resolution._1280x720;

        // Performance settings
        private int _maxDrawDistance = 5000;
        private int _maxPhysicsDistance = 3000;
        private int _maxParticles = 1000;
        private bool _enablePostProcessing = true;

        public int Width => _width;
        public int Height => _height;
        public bool IsFullscreen => _isFullscreen;
        public bool VsyncEnabled => _vsyncEnabled;
        public Vector2 Center => new Vector2(_width / 2f, _height / 2f);
        public Rectangle Bounds => new Rectangle(0, 0, _width, _height);
        public int MaxDrawDistance => _maxDrawDistance;
        public int MaxPhysicsDistance => _maxPhysicsDistance;
        public int MaxParticles => _maxParticles;

        public WindowManager(GraphicsDeviceManager graphics, GameWindow gameWindow)
        {
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _gameWindow = gameWindow ?? throw new ArgumentNullException(nameof(gameWindow));
            
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            SetResolution(_currentResolution);
            _graphics.PreferMultiSampling = false; // enforce no MSAA to save memory
            _graphics.IsFullScreen = false;        // enforce windowed-only
            _graphics.SynchronizeWithVerticalRetrace = _vsyncEnabled;
            _graphics.ApplyChanges();

            Console.WriteLine($"[Window] Initialized: {_width}x{_height} | Fullscreen: {_isFullscreen} | VSync: {_vsyncEnabled}");
        }

        /// <summary>
        /// Cambia la resolución de la ventana
        /// </summary>
        public void SetResolution(Resolution resolution)
        {
            _currentResolution = resolution;
            
            (_width, _height) = resolution switch
            {
                Resolution._1280x720 => (1280, 720),
                Resolution._1366x768 => (1366, 768),
                Resolution._1600x900 => (1600, 900),
                Resolution._1920x1080 => (1920, 1080),
                Resolution._2560x1440 => (2560, 1440),
                Resolution._3840x2160 => (3840, 2160),
                _ => (1920, 1080)
            };

            _graphics.PreferredBackBufferWidth = _width;
            _graphics.PreferredBackBufferHeight = _height;
            _graphics.ApplyChanges();

            Console.WriteLine($"[Window] Resolution changed to {_width}x{_height}");
        }

        /// <summary>
        /// Toggle fullscreen
        /// </summary>
        public void SetFullscreen(bool enabled)
        {
            // Windowed-only policy: ignore fullscreen requests
            if (enabled)
            {
                Console.WriteLine("[Window] Fullscreen request ignored (windowed enforced)");
            }

            _isFullscreen = false;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();
            Console.WriteLine("[Window] Fullscreen: OFF (enforced)");
        }

        /// <summary>
        /// Toggle VSync
        /// </summary>
        public void SetVSync(bool enabled)
        {
            if (_vsyncEnabled == enabled)
                return;

            _vsyncEnabled = enabled;
            _graphics.SynchronizeWithVerticalRetrace = _vsyncEnabled;
            _graphics.ApplyChanges();

            Console.WriteLine($"[Window] VSync: {(_vsyncEnabled ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Configura límites de calidad para carga pesada
        /// </summary>
        public void SetQualityPreset(QualityPreset preset)
        {
            (_maxDrawDistance, _maxPhysicsDistance, _maxParticles, _enablePostProcessing) = preset switch
            {
                QualityPreset.Low => (2000, 1000, 300, false),
                QualityPreset.Medium => (3500, 2000, 600, true),
                QualityPreset.High => (5000, 3000, 1000, true),
                QualityPreset.Ultra => (8000, 5000, 2000, true),
                _ => (5000, 3000, 1000, true)
            };

            Console.WriteLine($"[Quality] Preset: {preset} | Draw: {_maxDrawDistance} | Physics: {_maxPhysicsDistance} | Particles: {_maxParticles}");
        }

        /// <summary>
        /// Cambia tamaño de ventana sin fullscreen
        /// </summary>
        public void SetWindowSize(int width, int height)
        {
            _width = Math.Max(800, width);
            _height = Math.Max(600, height);
            
            _graphics.PreferredBackBufferWidth = _width;
            _graphics.PreferredBackBufferHeight = _height;
            _graphics.ApplyChanges();

            Console.WriteLine($"[Window] Size changed to {_width}x{_height}");
        }

        /// <summary>
        /// Retorna información de la ventana para debug
        /// </summary>
        public string GetWindowInfo()
        {
            return $"[{_width}x{_height}] FS:{(_isFullscreen ? "Y" : "N")} VS:{(_vsyncEnabled ? "Y" : "N")} Preset:High";
        }
    }

    public enum QualityPreset
    {
        Low,
        Medium,
        High,
        Ultra
    }
}
