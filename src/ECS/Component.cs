using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.ECS
{
    /// <summary>
    /// Base class for all components
    /// </summary>
    public abstract class Component
    {
        public Entity Entity { get; internal set; }
        public bool IsEnabled { get; set; } = true;

        public virtual void Initialize() { }
        public virtual void Update(float deltaTime) { }
        public virtual void OnEnabled() { }
        public virtual void OnDisabled() { }
        public virtual void OnDestroy() { }
    }

    /// <summary>
    /// Entity - container for components
    /// </summary>
    public class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Entity";
        public bool IsActive { get; set; } = true;

        // Pre-alloc: la mayoría de entities tienen 4-8 components
        private Dictionary<Type, Component> _components = new(8);

        public void AddComponent<T>(T component) where T : Component
        {
            var type = typeof(T);
            if (_components.ContainsKey(type))
                throw new InvalidOperationException($"Entity already has component of type {type.Name}");

            component.Entity = this;
            _components[type] = component;
            component.Initialize();
        }

        public T GetComponent<T>() where T : Component
        {
            var type = typeof(T);
            _components.TryGetValue(type, out var component);
            return component as T;
        }

        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T));
        }

        public void RemoveComponent<T>() where T : Component
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var component))
            {
                component.OnDestroy();
                _components.Remove(type);
            }
        }

        public void Update(float deltaTime)
        {
            if (!IsActive)
                return;

            foreach (var component in _components.Values)
            {
                if (component.IsEnabled)
                    component.Update(deltaTime);
            }
        }

        public IEnumerable<Component> GetAllComponents() => _components.Values;
    }

    /// <summary>
    /// Base class for systems that operate on entities with specific components
    /// </summary>
    public abstract class System
    {
        protected List<Entity> _entities = new(128); // pre-alloc para evitar resize
        private HashSet<int> _entityIds = new(128); // O(1) lookup en lugar de Contains O(n)

        public virtual void Initialize() { }
        public virtual void Update(float deltaTime) { }

        public void RegisterEntity(Entity entity)
        {
            if (ShouldProcess(entity) && _entityIds.Add(entity.Id))
                _entities.Add(entity);
        }

        public void UnregisterEntity(Entity entity)
        {
            if (_entityIds.Remove(entity.Id))
                _entities.Remove(entity);
        }

        protected abstract bool ShouldProcess(Entity entity);
    }
}
