using System;
using System.Collections.Generic;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Manages configuration settings loaded from JSON
    /// </summary>
    public class ConfigManager
    {
        private Dictionary<string, object> _settings = new();

        public void LoadConfig(string filePath)
        {
            // TODO: Load from JSON
            // For now, set defaults
            SetDefault("game_width", 1280);
            SetDefault("game_height", 720);
            SetDefault("target_fps", 60);
            SetDefault("enable_vsync", true);
            SetDefault("debug_mode", false);
        }

        public void SetDefault(string key, object value)
        {
            if (!_settings.ContainsKey(key))
                _settings[key] = value;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_settings.TryGetValue(key, out var value))
                return (T)Convert.ChangeType(value, typeof(T));
            return defaultValue;
        }

        public void Set(string key, object value)
        {
            _settings[key] = value;
        }
    }
}
