using System;
using System.Threading.Tasks;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.Examples
{
    public static class EventSystemExample
    {
        /// <summary>
        /// Demonstrates basic event usage: subscribe, priority, filters, once, sticky, async, queue.
        /// </summary>
        public static async Task RunDemo()
        {
            var events = new EventSystem();

            // Subscribe with different priorities
            events.Subscribe<EntityDamagedEvent>(e => Console.WriteLine($"[P10] Damaged: {e.EntityId} for {e.Damage}"), priority: 10);
            events.Subscribe<EntityDamagedEvent>(e => Console.WriteLine($"[P0] Damaged: {e.EntityId} newHP={e.NewHealth}"));

            // Filter to only player entity
            int playerId = 1;
            events.Subscribe<EntityDamagedEvent>(e => Console.WriteLine($"[FILTER player] Player took damage: {e.Damage}"),
                priority: 5,
                filter: e => e.EntityId == playerId);

            // One-time handler
            events.Subscribe<EntityDestroyedEvent>(e => Console.WriteLine($"Entity {e.EntityId} destroyed by {e.KillerId}"), once: true);

            // Sticky event: remember last selection change
            events.Publish(new SelectionChangedEvent(null, playerId, EventFactory.Now()), sticky: true);
            events.Subscribe<SelectionChangedEvent>(e => Console.WriteLine($"[Sticky delivered] Selected: {e.CurrentEntityId}"), deliverLastSticky: true);

            // Synchronous publish
            events.Publish(new EntityDamagedEvent(EntityId: playerId, Damage: 10f, NewHealth: 90f, AttackerId: 42, Timestamp: EventFactory.Now()));

            // Async publish
            await events.Bus.PublishAsync(new EntityDamagedEvent(EntityId: playerId, Damage: 20f, NewHealth: 70f, AttackerId: 42, Timestamp: EventFactory.Now()));

            // Queued publish processed in Update
            events.Enqueue(new EntityDestroyedEvent(EntityId: 99, KillerId: playerId, Timestamp: EventFactory.Now()));
            int processed = events.Update(deltaTime: 0.016f);
            Console.WriteLine($"Queued processed: {processed}");
        }

        /// <summary>
        /// Demonstrates collision event publishing via CollisionSystem and SpatialPartitioningSystem.
        /// </summary>
        public static void RunCollisionDemo()
        {
            var events = new EventSystem();
            events.Subscribe<CollisionEvent>(e =>
            {
                Console.WriteLine($"Collision: A={e.EntityAId} B={e.EntityBId} Point=({e.Point.X:F1},{e.Point.Y:F1}) Normal=({e.Normal.X:F2},{e.Normal.Y:F2})");
            });

            // Minimal ECS setup
            var entityManager = new SpaceTradeEngine.ECS.EntityManager();
            var spatial = new SpaceTradeEngine.Systems.SpatialPartitioningSystem(new Microsoft.Xna.Framework.Rectangle(-1000, -1000, 2000, 2000));
            var collision = new SpaceTradeEngine.Systems.CollisionSystem(spatial);
            collision.SetEventSystem(events);

            entityManager.RegisterSystem(spatial);
            entityManager.RegisterSystem(collision);

            // Create two entities on a collision course
            var e1 = entityManager.CreateEntity("A");
            e1.AddComponent(new SpaceTradeEngine.ECS.Components.TransformComponent { Position = new Microsoft.Xna.Framework.Vector2(-50, 0) });
            e1.AddComponent(new SpaceTradeEngine.ECS.Components.CollisionComponent { Radius = 20f });
            e1.AddComponent(new SpaceTradeEngine.ECS.Components.VelocityComponent { LinearVelocity = new Microsoft.Xna.Framework.Vector2(60, 0) });

            var e2 = entityManager.CreateEntity("B");
            e2.AddComponent(new SpaceTradeEngine.ECS.Components.TransformComponent { Position = new Microsoft.Xna.Framework.Vector2(50, 0) });
            e2.AddComponent(new SpaceTradeEngine.ECS.Components.CollisionComponent { Radius = 20f });
            e2.AddComponent(new SpaceTradeEngine.ECS.Components.VelocityComponent { LinearVelocity = new Microsoft.Xna.Framework.Vector2(-60, 0) });

            // Simulate a few frames
            for (int i = 0; i < 10; i++)
            {
                entityManager.Update(0.1f);
                events.Update(0.1f);
                var p1 = e1.GetComponent<SpaceTradeEngine.ECS.Components.TransformComponent>().Position;
                var p2 = e2.GetComponent<SpaceTradeEngine.ECS.Components.TransformComponent>().Position;
                Console.WriteLine($"Step {i+1}: A=({p1.X:F1},{p1.Y:F1}) B=({p2.X:F1},{p2.Y:F1}) Dist={Microsoft.Xna.Framework.Vector2.Distance(p1,p2):F1}");
                if (spatial.CheckCollision(e1, e2))
                {
                    Console.WriteLine("Direct check: collision true");
                    // Manually publish to demonstrate event flow (in case broad-phase pairs are empty)
                    var p = (p1 + p2) * 0.5f;
                    var n = p2 - p1; if (n != Microsoft.Xna.Framework.Vector2.Zero) n.Normalize();
                    events.Publish(new CollisionEvent(e1.Id, e2.Id, p, n, EventFactory.Now()));
                }
                var pairs = spatial.GetPotentialCollisions();
                Console.WriteLine($"Potential pairs: {pairs.Count}");
            }
        }

        /// <summary>
        /// Demonstrates damage events by applying damage to an entity via DamageSystem.
        /// </summary>
        public static void RunDamageDemo()
        {
            var events = new EventSystem();

            events.Subscribe<EntityDamagedEvent>(e =>
            {
                Console.WriteLine($"Damaged: Entity={e.EntityId} Amount={e.Damage} NewHP={e.NewHealth}");
            }, priority: 5);

            events.Subscribe<EntityDestroyedEvent>(e =>
            {
                Console.WriteLine($"Destroyed: Entity={e.EntityId} Killer={e.KillerId}");
            }, once: false);

            var entityManager = new SpaceTradeEngine.ECS.EntityManager();
            var damageSystem = new SpaceTradeEngine.Systems.DamageSystem(events);
            entityManager.RegisterSystem(damageSystem);

            var target = entityManager.CreateEntity("TargetShip");
            target.AddComponent(new SpaceTradeEngine.ECS.Components.TransformComponent { Position = new Microsoft.Xna.Framework.Vector2(0, 0) });
            target.AddComponent(new SpaceTradeEngine.ECS.Components.HealthComponent { MaxHealth = 100f });

            // Simulate damage over time
            damageSystem.ApplyDamage(target, 15f, attackerId: 42);
            damageSystem.ApplyDamage(target, 30f, attackerId: 42);
            damageSystem.ApplyDamage(target, 60f, attackerId: 42); // should destroy

            // Ensure any queued events are processed
            events.Update(0.016f);
        }
    }
}
