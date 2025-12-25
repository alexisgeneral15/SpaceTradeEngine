using System;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Physics system that integrates velocity into position.
    /// Updates transform.Position based on velocity.LinearVelocity for all moving entities.
    /// This is the CRITICAL system that makes entities actually move on screen.
    /// </summary>
    public class PhysicsSystem : ECS.System
    {
        public PhysicsSystem()
        {
            Console.WriteLine("[Physics] System initialized");
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Process all entities that have both transform and velocity
            return entity.HasComponent<TransformComponent>() && 
                   entity.HasComponent<VelocityComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                var transform = entity.GetComponent<TransformComponent>();
                var velocity = entity.GetComponent<VelocityComponent>();

                if (transform == null || velocity == null)
                    continue;

                // Integrate linear velocity into position
                transform.Position += velocity.LinearVelocity * deltaTime;

                // Integrate angular velocity into rotation
                transform.Rotation += velocity.AngularVelocity * deltaTime;

                // Normalize rotation to -π to π
                while (transform.Rotation > MathHelper.Pi)
                    transform.Rotation -= MathHelper.TwoPi;
                while (transform.Rotation < -MathHelper.Pi)
                    transform.Rotation += MathHelper.TwoPi;
            }
        }
    }
}
