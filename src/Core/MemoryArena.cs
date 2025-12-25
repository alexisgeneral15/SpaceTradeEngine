using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Estadísticas de la arena de memoria.
    /// </summary>
    public class MemoryArenaStats
    {
        public long TotalAllocated { get; set; }
        public long MaxCapacity { get; set; }
        public float UsagePercent { get; set; }
        public int ActiveAllocations { get; set; }
        public int TotalDeallocations { get; set; }
        public List<(string name, long bytes, int count)> TopAllocations { get; set; } = new();
    }

    /// <summary>
    /// Memory arena allocator con límite configurable.
    /// Detecta asignaciones excesivas y permite inspección.
    /// </summary>
    public class MemoryArena
    {
        private long _totalAllocated = 0;
        private long _maxCapacity;
        private int _totalDeallocations = 0;
        private readonly List<(string name, long size, DateTime allocated)> _allocations = new();
        private readonly object _lockObj = new();

        public long TotalAllocated => _totalAllocated;
        public long MaxCapacity => _maxCapacity;
        public float UsagePercent => (float)_totalAllocated / _maxCapacity * 100f;

        public MemoryArena(long maxCapacityBytes = 1_000_000) // 1MB default
        {
            _maxCapacity = maxCapacityBytes;
        }

        /// <summary>
        /// Registra una asignación de memoria. Retorna true si está dentro del límite.
        /// </summary>
        public bool Allocate(string name, long bytes)
        {
            lock (_lockObj)
            {
                if (_totalAllocated + bytes > _maxCapacity)
                {
                    Console.WriteLine($"⚠️  MEMORY ARENA OVERFLOW: {name} solicita {bytes} bytes");
                    Console.WriteLine($"   Current: {_totalAllocated} / {_maxCapacity} ({UsagePercent:F1}%)");
                    return false; // NO asignar
                }

                _totalAllocated += bytes;
                _allocations.Add((name, bytes, DateTime.UtcNow));

                if (UsagePercent > 80f)
                {
                    Console.WriteLine($"⚠️  MEMORY ARENA WARNING: Usage {UsagePercent:F1}%");
                }

                return true;
            }
        }

        /// <summary>
        /// Desregistra una asignación de memoria.
        /// </summary>
        public void Deallocate(string name, long bytes)
        {
            lock (_lockObj)
            {
                _totalAllocated = Math.Max(0, _totalAllocated - bytes);
                _allocations.RemoveAll(x => x.name == name);
                _totalDeallocations++;
            }
        }

        /// <summary>
        /// Retorna estadísticas detalladas de la arena.
        /// </summary>
        public MemoryArenaStats GetStats()
        {
            lock (_lockObj)
            {
                var topAllocations = _allocations
                    .GroupBy(x => x.name)
                    .Select(g => (name: g.Key, bytes: g.Sum(x => x.size), count: g.Count()))
                    .OrderByDescending(x => x.bytes)
                    .Take(10)
                    .ToList();

                return new MemoryArenaStats
                {
                    TotalAllocated = _totalAllocated,
                    MaxCapacity = _maxCapacity,
                    UsagePercent = UsagePercent,
                    ActiveAllocations = _allocations.Count,
                    TotalDeallocations = _totalDeallocations,
                    TopAllocations = topAllocations
                };
            }
        }

        /// <summary>
        /// Cambia el límite máximo de la arena.
        /// </summary>
        public void SetMaxCapacity(long bytes)
        {
            lock (_lockObj)
            {
                if (_totalAllocated > bytes)
                {
                    Console.WriteLine($"⚠️  Warning: Nueva capacidad ({bytes / 1024}KB) es menor que uso actual ({_totalAllocated / 1024}KB)");
                }
                _maxCapacity = bytes;
            }
        }

        /// <summary>
        /// Limpia todas las asignaciones registradas.
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _totalAllocated = 0;
                _allocations.Clear();
            }
        }
    }

    /// <summary>
    /// Global memory arena singleton.
    /// </summary>
    public static class GlobalMemoryArena
    {
        private static readonly Lazy<MemoryArena> _instance = new(() => new MemoryArena(1_000_000));

        public static MemoryArena Instance => _instance.Value;

        public static void LogStats()
        {
            Console.WriteLine(Instance.GetStats());
        }
    }

    /// <summary>
    /// Allocation tracker para objetos grandes.
    /// </summary>
    public class ArenaAllocation : IDisposable
    {
        private string _name;
        private long _size;
        private bool _disposed;

        public ArenaAllocation(string name, long bytes)
        {
            _name = name;
            _size = bytes;

            if (!GlobalMemoryArena.Instance.Allocate(name, bytes))
            {
                throw new OutOfMemoryException($"Memory arena overflow for {name} ({bytes} bytes)");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                GlobalMemoryArena.Instance.Deallocate(_name, _size);
                _disposed = true;
            }
        }
    }
}
