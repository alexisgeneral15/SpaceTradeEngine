using System;
using System.Collections.Generic;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Pool genérico de objetos para reducir allocaciones y GC
    /// </summary>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _available = new Stack<T>(64);
        private readonly Action<T> _resetAction;
        private int _totalCreated;

        public int TotalCreated => _totalCreated;
        public int Available => _available.Count;

        public ObjectPool(Action<T> resetAction = null, int prewarmCount = 0)
        {
            _resetAction = resetAction;
            
            // Pre-crear objetos si se especifica
            for (int i = 0; i < prewarmCount; i++)
            {
                var obj = new T();
                _available.Push(obj);
                _totalCreated++;

                // Track estimated memory per pooled object (~256 bytes heuristic)
                GlobalMemoryArena.Instance.Allocate($"Pool_{typeof(T).Name}", 256);
            }
        }

        /// <summary>
        /// Obtener un objeto del pool (o crear uno nuevo si está vacío)
        /// </summary>
        public T Get()
        {
            if (_available.Count > 0)
            {
                return _available.Pop();
            }
            
            _totalCreated++;
            // Track estimated memory per new instance created
            GlobalMemoryArena.Instance.Allocate($"Pool_{typeof(T).Name}", 256);
            return new T();
        }

        /// <summary>
        /// Devolver un objeto al pool para reutilización
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
                return;
                
            _resetAction?.Invoke(obj);
            _available.Push(obj);
        }

        /// <summary>
        /// Devolver múltiples objetos al pool
        /// </summary>
        public void ReturnRange(IEnumerable<T> objects)
        {
            foreach (var obj in objects)
                Return(obj);
        }

        /// <summary>
        /// Limpiar pool completamente
        /// </summary>
        public void Clear()
        {
            _available.Clear();
            _totalCreated = 0;
        }
    }
}
