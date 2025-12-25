using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Profiler de rendimiento en tiempo real con recolección de métricas
    /// </summary>
    public class PerformanceProfiler
    {
        private readonly Stopwatch _frameStopwatch = new();
        private readonly Stopwatch _updateStopwatch = new();
        private readonly Stopwatch _renderStopwatch = new();

        // Historial de métricas (reducido a 10 frames para ahorrar memoria)
        private readonly Queue<FrameMetrics> _frameHistory = new(10);
        private const int MaxHistoryFrames = 10;

        // Contadores
        private int _drawCallsThisFrame;
        private int _verticesThisFrame;
        private int _entitiesUpdatedThisFrame;
        private int _physicsBodiesThisFrame;
        private int _collisionsThisFrame;
        private int _projectilesThisFrame;

        // Métricas acumuladas
        public double AverageFrameTime { get; private set; }
        public double MaxFrameTime { get; private set; }
        public double MinFrameTime { get; private set; }
        public double AverageUpdateTime { get; private set; }
        public double AverageRenderTime { get; private set; }
        public double CurrentFrameTime { get; private set; }
        public double CurrentUpdateTime { get; private set; }
        public double CurrentRenderTime { get; private set; }
        public double CurrentFPS { get; private set; }
        public long MemoryUsageMB { get; private set; }

        public int DrawCallsThisFrame => _drawCallsThisFrame;
        public int VerticesThisFrame => _verticesThisFrame;
        public int EntitiesUpdatedThisFrame => _entitiesUpdatedThisFrame;
        public int PhysicsBodiesThisFrame => _physicsBodiesThisFrame;
        public int CollisionsThisFrame => _collisionsThisFrame;
        public int ProjectilesThisFrame => _projectilesThisFrame;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PerformanceProfiler()
        {
            Console.WriteLine("[Profiler] Initialized");
        }

        /// <summary>
        /// Inicia medición de frame completo
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFrame()
        {
            _frameStopwatch.Restart();
            _drawCallsThisFrame = 0;
            _verticesThisFrame = 0;
            _entitiesUpdatedThisFrame = 0;
            _physicsBodiesThisFrame = 0;
            _collisionsThisFrame = 0;
            _projectilesThisFrame = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginUpdate()
        {
            _updateStopwatch.Restart();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndUpdate()
        {
            _updateStopwatch.Stop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRender()
        {
            _renderStopwatch.Restart();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRender()
        {
            _renderStopwatch.Stop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndFrame()
        {
            _frameStopwatch.Stop();

            CurrentFrameTime = _frameStopwatch.Elapsed.TotalMilliseconds;
            CurrentUpdateTime = _updateStopwatch.Elapsed.TotalMilliseconds;
            CurrentRenderTime = _renderStopwatch.Elapsed.TotalMilliseconds;
            CurrentFPS = CurrentFrameTime > 0 ? 1000.0 / CurrentFrameTime : 0;
            MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);

            // Guardar en historial
            var metrics = new FrameMetrics
            {
                FrameTime = CurrentFrameTime,
                UpdateTime = CurrentUpdateTime,
                RenderTime = CurrentRenderTime,
                FPS = CurrentFPS,
                Memory = MemoryUsageMB,
                DrawCalls = _drawCallsThisFrame,
                Vertices = _verticesThisFrame,
                Entities = _entitiesUpdatedThisFrame,
                Collisions = _collisionsThisFrame,
                Timestamp = DateTime.UtcNow
            };

            _frameHistory.Enqueue(metrics);
            if (_frameHistory.Count > MaxHistoryFrames)
                _frameHistory.Dequeue();

            UpdateAverages();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordDrawCall() => _drawCallsThisFrame++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordVertices(int count) => _verticesThisFrame += count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordEntityUpdate() => _entitiesUpdatedThisFrame++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordPhysicsBody() => _physicsBodiesThisFrame++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordCollision() => _collisionsThisFrame++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordProjectile() => _projectilesThisFrame++;

        private void UpdateAverages()
        {
            if (_frameHistory.Count == 0) return;

            double totalFrameTime = 0;
            double totalUpdateTime = 0;
            double totalRenderTime = 0;
            MaxFrameTime = 0;
            MinFrameTime = double.MaxValue;

            foreach (var frame in _frameHistory)
            {
                totalFrameTime += frame.FrameTime;
                totalUpdateTime += frame.UpdateTime;
                totalRenderTime += frame.RenderTime;
                MaxFrameTime = Math.Max(MaxFrameTime, frame.FrameTime);
                MinFrameTime = Math.Min(MinFrameTime, frame.FrameTime);
            }

            int count = _frameHistory.Count;
            AverageFrameTime = totalFrameTime / count;
            AverageUpdateTime = totalUpdateTime / count;
            AverageRenderTime = totalRenderTime / count;
        }

        /// <summary>
        /// Retorna informe de rendimiento detallado
        /// </summary>
        public string GetDetailedReport()
        {
            return $@"
╔════════════════════════════════════════╗
║      PERFORMANCE PROFILER REPORT      ║
╠════════════════════════════════════════╣
║ FPS:          {CurrentFPS:F1} ({AverageUpdateTime:F2}ms avg)
║ Frame Time:   {CurrentFrameTime:F2}ms (Min: {MinFrameTime:F2}ms, Max: {MaxFrameTime:F2}ms)
║ Update Time:  {CurrentUpdateTime:F2}ms ({(CurrentUpdateTime / CurrentFrameTime * 100):F1}%)
║ Render Time:  {CurrentRenderTime:F2}ms ({(CurrentRenderTime / CurrentFrameTime * 100):F1}%)
║ Memory:       {MemoryUsageMB}MB
╠════════════════════════════════════════╣
║ Draw Calls:   {_drawCallsThisFrame}
║ Vertices:     {_verticesThisFrame:N0}
║ Entities:     {_entitiesUpdatedThisFrame}
║ Physics:      {_physicsBodiesThisFrame}
║ Collisions:   {_collisionsThisFrame}
║ Projectiles:  {_projectilesThisFrame}
╚════════════════════════════════════════╝";
        }

        /// <summary>
        /// Retorna string corto para HUD
        /// </summary>
        public string GetHUDString()
        {
            return $"FPS: {CurrentFPS:F1} | Frame: {CurrentFrameTime:F2}ms | Upd: {CurrentUpdateTime:F2}ms | Rnd: {CurrentRenderTime:F2}ms | Mem: {MemoryUsageMB}MB | Draws: {_drawCallsThisFrame}";
        }
    }

    public struct FrameMetrics
    {
        public double FrameTime;
        public double UpdateTime;
        public double RenderTime;
        public double FPS;
        public long Memory;
        public int DrawCalls;
        public int Vertices;
        public int Entities;
        public int Collisions;
        public DateTime Timestamp;
    }
}
