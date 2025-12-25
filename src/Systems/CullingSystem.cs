using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Optimized rendering system with frustum culling using spatial partitioning
    /// </summary>
    public class CullingSystem
    {
        private SpatialPartitioningSystem _spatialSystem;
        private List<Entity> _visibleEntities = new List<Entity>();

        public CullingSystem(SpatialPartitioningSystem spatialSystem)
        {
            _spatialSystem = spatialSystem;
        }

        /// <summary>
        /// Get only entities visible to the camera
        /// </summary>
        public List<Entity> GetVisibleEntities(Vector2 cameraPosition, Vector2 viewportSize, float zoom)
        {
            _visibleEntities.Clear();
            
            // Query spatial partition for entities in camera view
            var candidates = _spatialSystem.QueryCameraView(cameraPosition, viewportSize, zoom);

            // Additional filtering if needed
            foreach (var entity in candidates)
            {
                if (!entity.IsActive)
                    continue;

                // Must have sprite to be visible
                if (!entity.HasComponent<SpriteComponent>())
                    continue;

                _visibleEntities.Add(entity);
            }

            return _visibleEntities;
        }

        /// <summary>
        /// Get culling statistics
        /// </summary>
        public CullingStats GetStats(int totalEntities, int visibleEntities)
        {
            return new CullingStats
            {
                TotalEntities = totalEntities,
                VisibleEntities = visibleEntities,
                CulledEntities = totalEntities - visibleEntities,
                CullPercentage = totalEntities > 0 ? (float)(totalEntities - visibleEntities) / totalEntities * 100f : 0f
            };
        }
    }

    public struct CullingStats
    {
        public int TotalEntities;
        public int VisibleEntities;
        public int CulledEntities;
        public float CullPercentage;

        public override string ToString()
        {
            return $"Visible: {VisibleEntities}/{TotalEntities} ({CullPercentage:F1}% culled)";
        }
    }
}
