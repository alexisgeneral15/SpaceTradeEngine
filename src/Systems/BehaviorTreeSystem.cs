using System;
using System.Collections.Generic;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.AI;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Component that holds a behavior tree for an entity
    /// </summary>
    public class BehaviorTreeComponent : Component
    {
        public BehaviorTree Tree { get; set; }
        public float TickInterval { get; set; } = 0f; // 0 = every frame
        
        private float _timeSinceLastTick = 0f;

        public BehaviorTreeComponent(BehaviorTree tree)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
        }

        public override void Update(float deltaTime)
        {
            if (!IsEnabled || Tree == null)
                return;

            _timeSinceLastTick += deltaTime;

            if (_timeSinceLastTick >= TickInterval)
            {
                Tree.Tick(deltaTime);
                _timeSinceLastTick = 0f;
            }
        }
    }

    /// <summary>
    /// System that manages and updates all behavior trees
    /// </summary>
    public class BehaviorTreeSystem : ECS.System
    {
        private Dictionary<int, BehaviorTreeComponent> _behaviorTrees = new Dictionary<int, BehaviorTreeComponent>();
        private EntityManager _entityManager;

        public BehaviorTreeSystem()
        {
        }

        public BehaviorTreeSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public override void Update(float deltaTime)
        {
            // BehaviorTreeComponent.Update is already called via Entity.Update.
            // This system can remain lightweight; no additional ticking needed here.
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Track all entities; stats will filter by presence of BehaviorTreeComponent.
            return true;
        }

        /// <summary>
        /// Pause all behavior trees
        /// </summary>
        public void PauseAll()
        {
            var entities = _entityManager != null ? _entityManager.GetEntitiesWithComponent<BehaviorTreeComponent>() : _entities;
            foreach (var entity in entities)
            {
                var btComponent = entity.GetComponent<BehaviorTreeComponent>();
                if (btComponent != null)
                    btComponent.IsEnabled = false;
            }
        }

        /// <summary>
        /// Resume all behavior trees
        /// </summary>
        public void ResumeAll()
        {
            var entities = _entityManager != null ? _entityManager.GetEntitiesWithComponent<BehaviorTreeComponent>() : _entities;
            foreach (var entity in entities)
            {
                var btComponent = entity.GetComponent<BehaviorTreeComponent>();
                if (btComponent != null)
                    btComponent.IsEnabled = true;
            }
        }

        /// <summary>
        /// Get statistics about active behavior trees
        /// </summary>
        public BehaviorTreeStats GetStats()
        {
            int total = 0;
            int active = 0;

            var entities = _entityManager != null ? _entityManager.GetEntitiesWithComponent<BehaviorTreeComponent>() : _entities;
            foreach (var entity in entities)
            {
                var btComponent = entity.GetComponent<BehaviorTreeComponent>();
                if (btComponent != null)
                {
                    total++;
                    if (btComponent.IsEnabled)
                        active++;
                }
            }

            return new BehaviorTreeStats
            {
                TotalTrees = total,
                ActiveTrees = active
            };
        }
    }

    public struct BehaviorTreeStats
    {
        public int TotalTrees;
        public int ActiveTrees;

        public override string ToString()
        {
            return $"AI Trees: {ActiveTrees}/{TotalTrees} active";
        }
    }
}
