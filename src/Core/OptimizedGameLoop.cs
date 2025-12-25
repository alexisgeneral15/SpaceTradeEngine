using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Ciclo de juego optimizado con delta time preciso y limitador de FPS.
    /// Garantiza velocidad consistente en hardware rápido y lento.
    /// </summary>
    public class OptimizedGameLoop
    {
        private readonly Stopwatch _stopwatch;
        private double _accumulator;
        private double _previousTime;
        private const double FixedTimeStep = 1.0 / 60.0; // 60 FPS fijo para física
        private const double MaxFrameTime = 0.25; // Evitar "espiral de muerte"
        
        // Métricas de rendimiento
        private double _fps;
        private double _averageFrameTime;
        private int _frameCount;
        private double _fpsTimer;
        
        // Suavizado de delta time (reducido a 4 frames para menos memoria)
        private readonly double[] _deltaHistory = new double[4];
        private int _deltaHistoryIndex;
        private double _smoothedDelta;

        public double DeltaTime { get; private set; }
        public double FixedDelta => FixedTimeStep;
        public double FPS => _fps;
        public double AverageFrameTime => _averageFrameTime;
        public bool IsRunningSlowly { get; private set; }

        public OptimizedGameLoop()
        {
            _stopwatch = Stopwatch.StartNew();
            _previousTime = 0;
            _accumulator = 0;
            _fps = 60.0;
            _smoothedDelta = FixedTimeStep;
        }

        /// <summary>
        /// Inicia el ciclo, debe llamarse al inicio del Update
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFrame()
        {
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double frameTime = currentTime - _previousTime;
            
            // Limitar frame time para evitar grandes saltos
            if (frameTime > MaxFrameTime)
                frameTime = MaxFrameTime;
            
            _previousTime = currentTime;
            DeltaTime = frameTime;
            
            // Suavizar delta time usando promedio móvil
            _deltaHistory[_deltaHistoryIndex] = frameTime;
            _deltaHistoryIndex = (_deltaHistoryIndex + 1) % _deltaHistory.Length;
            
            double sum = 0;
            for (int i = 0; i < _deltaHistory.Length; i++)
                sum += _deltaHistory[i];
            _smoothedDelta = sum / _deltaHistory.Length;
            
            // Acumular para pasos fijos
            _accumulator += frameTime;
            
            // Calcular FPS
            _frameCount++;
            _fpsTimer += frameTime;
            if (_fpsTimer >= 1.0)
            {
                _fps = _frameCount / _fpsTimer;
                _averageFrameTime = _fpsTimer / _frameCount;
                _frameCount = 0;
                _fpsTimer = 0;
            }
            
            // Detectar si va lento
            IsRunningSlowly = frameTime > FixedTimeStep * 1.5;
        }

        /// <summary>
        /// Ejecuta actualizaciones de física con paso fijo.
        /// Retorna true mientras haya pasos pendientes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldUpdatePhysics()
        {
            if (_accumulator >= FixedTimeStep)
            {
                _accumulator -= FixedTimeStep;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Factor de interpolación para renderizado suave entre pasos fijos
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetInterpolationAlpha()
        {
            return (float)(_accumulator / FixedTimeStep);
        }

        /// <summary>
        /// Delta time suavizado para lógica de juego variable
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSmoothedDeltaTime()
        {
            return (float)_smoothedDelta;
        }

        /// <summary>
        /// Resetea las métricas (útil al cambiar de escena)
        /// </summary>
        public void Reset()
        {
            _accumulator = 0;
            _previousTime = _stopwatch.Elapsed.TotalSeconds;
            Array.Clear(_deltaHistory, 0, _deltaHistory.Length);
            _deltaHistoryIndex = 0;
        }
    }
}
