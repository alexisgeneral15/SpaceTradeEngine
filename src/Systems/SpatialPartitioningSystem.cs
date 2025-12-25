using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Spatial;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// System that manages spatial partitioning and provides spatial queries
    /// </summary>
    public class SpatialPartitioningSystem : ECS.System
    {
        private QuadTree _quadTree;
        private Rectangle _worldBounds;
        private bool _debugVisualization = false;
        private int _frameCounter = 0;
        private int _rebuildInterval = 10; // rebuild quadtree every 10 frames to reduce CPU load

        public SpatialPartitioningSystem(Rectangle worldBounds)
        {
            _worldBounds = worldBounds;
            _quadTree = new QuadTree(worldBounds);
        }

        public override void Initialize()
        {
            Console.WriteLine($"Spatial Partitioning System initialized with world bounds: {_worldBounds}");
        }

        public override void Update(float deltaTime)
        {
            _frameCounter++;
            if (_frameCounter % _rebuildInterval != 0)
                return; // skip rebuild this frame to save CPU

            // Rebuild the quadtree periodically
            _quadTree.Clear();

            foreach (var entity in _entities)
            {
                if (!entity.IsActive)
                    continue;

                _quadTree.Insert(entity);
            }
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Track all entities; queries will use components when present.
            return true;
        }

        #region Spatial Queries

        /// <summary>
        /// Query entities within a rectangular area
        /// </summary>
        public List<Entity> QueryArea(Rectangle area)
        {
            return _quadTree.Query(area);
        }

        /// <summary>
        /// Query entities within a circular radius
        /// </summary>
        public List<Entity> QueryRadius(Vector2 center, float radius)
        {
            return _quadTree.QueryRadius(center, radius);
        }

        /// <summary>
        /// Find the nearest entity to a point
        /// </summary>
        public Entity? FindNearest(Vector2 point, float maxRadius = float.MaxValue)
        {
            return _quadTree.FindNearest(point, maxRadius);
        }

        /// <summary>
        /// Find the nearest entity of a specific type using a predicate
        /// </summary>
        public Entity? FindNearestMatching(Vector2 point, Predicate<Entity> predicate, float maxRadius = float.MaxValue)
        {
            var candidates = _quadTree.QueryRadius(point, maxRadius);
            
            Entity? nearest = null;
            float nearestDistance = maxRadius;

            foreach (var entity in candidates)
            {
                if (!predicate(entity))
                    continue;

                var transform = entity.GetComponent<TransformComponent>();
                if (transform == null)
                    continue;

                float distance = Vector2.Distance(point, transform.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Perform a raycast to find entities along a line
        /// </summary>
        public List<Entity> Raycast(Vector2 origin, Vector2 direction, float maxDistance)
        {
            return _quadTree.Raycast(origin, direction, maxDistance);
        }

        /// <summary>
        /// Find all entities within camera frustum (for culling)
        /// </summary>
        public List<Entity> QueryCameraView(Vector2 cameraPosition, Vector2 viewportSize, float zoom)
        {
            // Calculate the visible area
            Vector2 viewSize = viewportSize / zoom;
            Rectangle visibleArea = new Rectangle(
                (int)(cameraPosition.X - viewSize.X / 2),
                (int)(cameraPosition.Y - viewSize.Y / 2),
                (int)viewSize.X,
                (int)viewSize.Y
            );

            return QueryArea(visibleArea);
        }

        #endregion

        #region Collision Detection

        /// <summary>
        /// Get all potential collision pairs (broad phase)
        /// </summary>
        public List<(Entity, Entity)> GetPotentialCollisions()
        {
            var pairs = new List<(Entity, Entity)>();
            var processed = new HashSet<(int, int)>();

            foreach (var entity in _entities)
            {
                if (!entity.IsActive)
                    continue;

                var collision = entity.GetComponent<CollisionComponent>();
                if (collision == null)
                    continue;

                var bounds = collision.GetBounds();
                var candidates = _quadTree.Query(bounds);

                foreach (var other in candidates)
                {
                    if (entity.Id >= other.Id)
                        continue; // Skip self and already processed pairs

                    var otherCollision = other.GetComponent<CollisionComponent>();
                    if (otherCollision == null)
                        continue;

                    // Create a unique pair ID
                    var pairId = (Math.Min(entity.Id, other.Id), Math.Max(entity.Id, other.Id));
                    if (processed.Contains(pairId))
                        continue;

                    processed.Add(pairId);
                    pairs.Add((entity, other));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Check if two entities are colliding (narrow phase)
        /// </summary>
        public bool CheckCollision(Entity a, Entity b)
        {
            var collisionA = a.GetComponent<CollisionComponent>();
            var collisionB = b.GetComponent<CollisionComponent>();

            if (collisionA == null || collisionB == null)
                return false;

            return collisionA.Intersects(collisionB);
        }

        #endregion

        #region Debug and Statistics

        /// <summary>
        /// Toggle debug visualization
        /// </summary>
        public void ToggleDebugVisualization()
        {
            _debugVisualization = !_debugVisualization;
        }

        /// <summary>
        /// Get debug visualization bounds
        /// </summary>
        public List<Rectangle> GetDebugBounds()
        {
            return _debugVisualization ? _quadTree.GetDebugBounds() : new List<Rectangle>();
        }

        /// <summary>
        /// Get statistics about the spatial partition
        /// </summary>
        public SpatialStats GetStats()
        {
            return new SpatialStats
            {
                TotalEntities = _entities.Count,
                TotalObjects = _quadTree.GetTotalObjects(),
                WorldBounds = _worldBounds
            };
        }

        #endregion

        /// <summary>
        /// Update world bounds if game world changes size
        /// </summary>
        public void UpdateWorldBounds(Rectangle newBounds)
        {
            _worldBounds = newBounds;
            _quadTree.UpdateWorldBounds(newBounds);
        }
    }

    /// <summary>
    /// Statistics about the spatial partition system
    /// </summary>
    public struct SpatialStats
    {
        public int TotalEntities;
        public int TotalObjects;
        public Rectangle WorldBounds;

        public override string ToString()
        {
            return $"Entities: {TotalEntities}, Objects: {TotalObjects}, World: {WorldBounds}";
        }
    }
}
