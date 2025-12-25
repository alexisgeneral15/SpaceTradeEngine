using System;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Game timing and clock system
    /// </summary>
    public class GameClock
    {
        public float DeltaTime { get; private set; }
        public float TotalTime { get; private set; }
        public int FrameCount { get; private set; }
        
        // Game time (days, seasons)
        public int GameDay { get; private set; } = 1;
        public int GameMonth { get; private set; } = 1;
        public int GameYear { get; private set; } = 2500;
        
        // Time scaling
        private float _timeScale = 1.0f;
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0.1f, value);
        }

        public void Update(float deltaTime)
        {
            DeltaTime = deltaTime * TimeScale;
            TotalTime += DeltaTime;
            FrameCount++;
            
            // Update game time (simple: 1 real second = 1 game hour)
            float gameHours = TotalTime / 3600f;
            GameDay = 1 + (int)(gameHours / 24f);
            GameMonth = 1 + ((GameDay - 1) / 30);
            GameYear = 2500 + ((GameMonth - 1) / 12);
        }

        public void Reset()
        {
            DeltaTime = 0;
            TotalTime = 0;
            FrameCount = 0;
            GameDay = 1;
            GameMonth = 1;
            GameYear = 2500;
        }

        public string GetGameTimeString()
        {
            return $"Year {GameYear}, Day {GameDay % 30}";
        }
    }
}
