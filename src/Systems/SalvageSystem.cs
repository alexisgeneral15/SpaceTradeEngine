using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Handles salvage spawning on ship death and collection by nearby ships.
    /// Sprint 3: Salvage system for combat rewards.
    /// </summary>
    public class SalvageSystem : ECS.System
    {
        private readonly EventSystem _eventSystem;
        private readonly EntityManager _entityManager;
        private readonly List<Entity> _salvageItems = new List<Entity>();
        private const float CollectionRange = 100f;

        public SalvageSystem(EventSystem eventSystem, EntityManager entityManager)
        {
            _eventSystem = eventSystem;
            _entityManager = entityManager;
            
            // Subscribe to entity destroyed events
            _eventSystem.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Process entities with salvage components (salvage items)
            return entity.HasComponent<SalvageComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Check for salvage collection
            CheckSalvageCollection();
        }

        /// <summary>
        /// When an entity is destroyed, spawn salvage if it has a salvage component.
        /// </summary>
        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            var destroyedEntity = _entityManager.GetEntity(evt.EntityId);
            if (destroyedEntity == null) return;

            var salvageComp = destroyedEntity.GetComponent<SalvageComponent>();
            if (salvageComp == null || salvageComp.IsCollected) return;

            var transform = destroyedEntity.GetComponent<TransformComponent>();
            if (transform == null) return;

            // Spawn salvage entity at destroyed ship's position
            var salvageEntity = _entityManager.CreateEntity($"Salvage_{destroyedEntity.Name}");
            
            salvageEntity.AddComponent(new TransformComponent
            {
                Position = transform.Position,
                Scale = new Vector2(0.5f, 0.5f)
            });

            salvageEntity.AddComponent(new SpriteComponent
            {
                Color = Color.Gold,
                Width = 16,
                Height = 16,
                LayerDepth = 0.3f,
                IsVisible = true
            });

            // Clone salvage component
            var newSalvage = new SalvageComponent
            {
                SalvageWares = new Dictionary<string, int>(salvageComp.SalvageWares),
                SalvageCredits = salvageComp.SalvageCredits,
                SalvageValueMultiplier = salvageComp.SalvageValueMultiplier,
                IsCollected = false
            };
            salvageEntity.AddComponent(newSalvage);

            _salvageItems.Add(salvageEntity);
            Console.WriteLine($"[Salvage] Spawned salvage from {destroyedEntity.Name}: {salvageComp.SalvageCredits:F0} credits");
        }

        /// <summary>
        /// Check if any ships are close enough to collect salvage.
        /// </summary>
        private void CheckSalvageCollection()
        {
            var collectors = _entityManager.GetAllEntities()
                .Where(e => e.HasComponent<CargoComponent>() && e.HasComponent<TransformComponent>())
                .ToList();

            foreach (var salvageEntity in _salvageItems.ToList())
            {
                var salvageTransform = salvageEntity.GetComponent<TransformComponent>();
                var salvageComp = salvageEntity.GetComponent<SalvageComponent>();
                
                if (salvageTransform == null || salvageComp == null || salvageComp.IsCollected)
                    continue;

                foreach (var collector in collectors)
                {
                    var collectorTransform = collector.GetComponent<TransformComponent>();
                    if (collectorTransform == null) continue;

                    float distance = Vector2.Distance(salvageTransform.Position, collectorTransform.Position);
                    
                    if (distance <= CollectionRange)
                    {
                        CollectSalvage(collector, salvageEntity, salvageComp);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Collect salvage into collector's cargo.
        /// </summary>
        private void CollectSalvage(Entity collector, Entity salvageEntity, SalvageComponent salvageComp)
        {
            var cargo = collector.GetComponent<CargoComponent>();
            if (cargo == null) return;

            // Add credits
            cargo.Credits += salvageComp.SalvageCredits;

            // Add wares (if there's space)
            foreach (var ware in salvageComp.SalvageWares)
            {
                // For now, just add credits value instead of actual wares (simplified)
                cargo.Credits += ware.Value * 10f; // 10 credits per ware unit
            }

            salvageComp.IsCollected = true;
            _salvageItems.Remove(salvageEntity);
            
            // Mark entity for destruction
            _entityManager.DestroyEntity(salvageEntity.Id);

            Console.WriteLine($"[Salvage] {collector.Name} collected salvage: +{salvageComp.SalvageCredits:F0} credits");
        }

        /// <summary>
        /// Get all active salvage items.
        /// </summary>
        public IEnumerable<Entity> GetActiveSalvage()
        {
            return _salvageItems.Where(s => s.HasComponent<SalvageComponent>() && 
                                            !s.GetComponent<SalvageComponent>()!.IsCollected);
        }
    }
}
