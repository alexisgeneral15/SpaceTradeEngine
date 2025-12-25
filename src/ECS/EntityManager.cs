using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.ECS
{
    /// <summary>
    /// Manages all entities and systems
    /// </summary>
    public class EntityManager
    {
        private Dictionary<int, Entity> _entities = new(256); // pre-alloc para evitar resize
        private List<System> _systems = new(32); // pre-alloc para evitar resize
        private int _nextEntityId = 1;

        public Entity CreateEntity(string name = "Entity")
        {
            var entity = new Entity
            {
                Id = _nextEntityId++,
                Name = name
            };
            _entities[entity.Id] = entity;
            
            // Track entity allocation in memory arena (approx 1KB per entity)
            Core.GlobalMemoryArena.Instance.Allocate($"Entity_{entity.Id}", 1024);
            
            // Register with all systems
            foreach (var system in _systems)
                system.RegisterEntity(entity);
            
            return entity;
        }

        /// <summary>
        /// Re-register an entity with all systems after adding components so systems pick it up.
        /// </summary>
        public void RefreshEntity(Entity entity)
        {
            foreach (var system in _systems)
                system.RegisterEntity(entity);
        }

        public Entity GetEntity(int id)
        {
            _entities.TryGetValue(id, out var entity);
            return entity;
        }

        public void DestroyEntity(int id)
        {
            if (_entities.TryGetValue(id, out var entity))
            {
                foreach (var system in _systems)
                    system.UnregisterEntity(entity);
                
                _entities.Remove(id);
                
                // Track entity deallocation in memory arena
                Core.GlobalMemoryArena.Instance.Deallocate($"Entity_{id}", 1024);
            }
        }

        public void RegisterSystem<T>(T system) where T : System
        {
            _systems.Add(system);
            system.Initialize();
            
            // Track system allocation in memory arena (approx 16KB per system)
            Core.GlobalMemoryArena.Instance.Allocate($"System_{system.GetType().Name}", 16384);
            
            // Register existing entities
            foreach (var entity in _entities.Values)
                system.RegisterEntity(entity);
        }

        public void Update(float deltaTime)
        {
            // First update entities so movement/velocity applies before system logic (collisions, targeting, etc.)
            foreach (var entity in _entities.Values)
                entity.Update(deltaTime);

            // Then update systems that operate on the latest transforms/components
            foreach (var system in _systems)
                system.Update(deltaTime);
        }

        // Retorna referencia directa sin allocar - SOLO lectura
        public IReadOnlyCollection<Entity> GetAllEntities() => _entities.Values;

        public List<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            var result = new List<Entity>(_entities.Count / 4); // estimate capacity
            foreach (var entity in _entities.Values)
            {
                if (entity.HasComponent<T>())
                    result.Add(entity);
            }
            return result;
        }
    }
}
