using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Collision detection and response system using spatial partitioning
    /// </summary>
    public class CollisionSystem : ECS.System
    {
        private SpatialPartitioningSystem _spatialSystem;
        private EventSystem _eventSystem;
        
        // Collision events
        public event Action<Entity, Entity> OnCollision;
        public event Action<Entity, Entity> OnTriggerEnter;

        public CollisionSystem(SpatialPartitioningSystem spatialSystem)
        {
            _spatialSystem = spatialSystem;
        }

        public void SetEventSystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
        }

        public override void Update(float deltaTime)
        {
            // Get all potential collision pairs from spatial partition (broad phase)
            var potentialPairs = _spatialSystem.GetPotentialCollisions();

            // Check actual collisions (narrow phase)
            foreach (var (entityA, entityB) in potentialPairs)
            {
                if (_spatialSystem.CheckCollision(entityA, entityB))
                {
                    HandleCollision(entityA, entityB);
                }
            }
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Process entities with collision components
            return entity.HasComponent<CollisionComponent>();
        }

        private void HandleCollision(Entity entityA, Entity entityB)
        {
            var collisionA = entityA.GetComponent<CollisionComponent>();
            var collisionB = entityB.GetComponent<CollisionComponent>();

            // Publish collision event (midpoint + normal)
            var tA = entityA.GetComponent<TransformComponent>();
            var tB = entityB.GetComponent<TransformComponent>();
            if (tA != null && tB != null && _eventSystem != null)
            {
                var midpoint = (tA.Position + tB.Position) * 0.5f;
                var normal = tB.Position - tA.Position;
                if (normal != Vector2.Zero)
                {
                    normal.Normalize();
                }
                _eventSystem.Publish(new CollisionEvent(entityA.Id, entityB.Id, midpoint, normal, EventFactory.Now()));
            }

            // Handle trigger collisions
            if (collisionA.IsTrigger || collisionB.IsTrigger)
            {
                OnTriggerEnter?.Invoke(entityA, entityB);
            }
            else
            {
                // Handle physical collision
                OnCollision?.Invoke(entityA, entityB);
                ResolveCollision(entityA, entityB);
            }
        }

        private void ResolveCollision(Entity entityA, Entity entityB)
        {
            var transformA = entityA.GetComponent<TransformComponent>();
            var transformB = entityB.GetComponent<TransformComponent>();
            var velocityA = entityA.GetComponent<VelocityComponent>();
            var velocityB = entityB.GetComponent<VelocityComponent>();

            if (transformA == null || transformB == null)
                return;

            // Calculate collision normal
            Vector2 delta = transformB.Position - transformA.Position;
            float distance = delta.Length();
            
            if (distance == 0)
                return; // Avoid division by zero

            delta.Normalize();

            // Simple collision response - separate objects
            var collisionA = entityA.GetComponent<CollisionComponent>();
            var collisionB = entityB.GetComponent<CollisionComponent>();
            
            float overlap = (collisionA.Radius + collisionB.Radius) - distance;
            
            if (overlap > 0)
            {
                // Move objects apart
                Vector2 separation = delta * (overlap / 2f);
                
                if (velocityA != null)
                    transformA.Position -= separation;
                if (velocityB != null)
                    transformB.Position += separation;

                // Apply velocity response (simple elastic collision)
                if (velocityA != null && velocityB != null)
                {
                    // Swap velocities along collision normal
                    float relativeVelocity = Vector2.Dot(velocityA.LinearVelocity - velocityB.LinearVelocity, delta);
                    
                    if (relativeVelocity < 0)
                    {
                        float impulse = relativeVelocity * 0.5f; // 0.5 = equal mass assumption
                        velocityA.LinearVelocity -= delta * impulse;
                        velocityB.LinearVelocity += delta * impulse;
                    }
                }
            }
        }
    }
}
