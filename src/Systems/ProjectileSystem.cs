using System;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages projectile lifetime and cleanup.
    /// </summary>
    public class ProjectileSystem : ECS.System
    {
        private readonly EntityManager _entityManager;

        public ProjectileSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<ProjectileComponent>();
        }

        public override void Update(float deltaTime)
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                var e = _entities[i];
                var proj = e.GetComponent<ProjectileComponent>();
                var t = e.GetComponent<TransformComponent>();
                if (proj == null || t == null)
                    continue;

                proj.TTL -= deltaTime;
                if (proj.TTL <= 0)
                {
                    _entityManager.DestroyEntity(e.Id);
                    continue;
                }

                // Remove if exceeded max range
                if (Vector2.DistanceSquared(proj.Origin, t.Position) > proj.MaxRange * proj.MaxRange)
                {
                    _entityManager.DestroyEntity(e.Id);
                }
            }
        }
    }
}
