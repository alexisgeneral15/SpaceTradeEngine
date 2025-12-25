using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.Spatial;

namespace SpaceTradeEngine.Examples
{
    /// <summary>
    /// Example showing how to use the Spatial Partitioning system
    /// with collision detection, targeting, and culling
    /// </summary>
    public class SpatialPartitioningExample
    {
        private EntityManager _entityManager;
        private SpatialPartitioningSystem _spatialSystem;
        private CollisionSystem _collisionSystem;
        private TargetingSystem _targetingSystem;
        private CullingSystem _cullingSystem;
        private WeaponSystem _weaponSystem;
        private ProjectileSystem _projectileSystem;
        private int _projectileHits = 0;
        private int _destroyed = 0;
        private readonly System.Collections.Generic.Dictionary<string, int> _lossesByFaction = new();
        private int _collisionLogsRemaining = 30;
        private int _dockingLogsRemaining = 5;
        private readonly HashSet<(int stationId, int shipId)> _dockingLogPairs = new();
        private int _projectileLogsRemaining = 30;

        public void Initialize()
        {
            _entityManager = new EntityManager();

            // Define world bounds (e.g., 10000x10000 space)
            Rectangle worldBounds = new Rectangle(-5000, -5000, 10000, 10000);

            // Create spatial partitioning system
            _spatialSystem = new SpatialPartitioningSystem(worldBounds);
            _entityManager.RegisterSystem(_spatialSystem);

            // Create related systems
            _collisionSystem = new CollisionSystem(_spatialSystem);
            _entityManager.RegisterSystem(_collisionSystem);

            _targetingSystem = new TargetingSystem(_spatialSystem);
            _entityManager.RegisterSystem(_targetingSystem);

            _cullingSystem = new CullingSystem(_spatialSystem);

            // Add combat systems so fleets auto-fire
            _weaponSystem = new WeaponSystem(_entityManager, _spatialSystem);
            _entityManager.RegisterSystem(_weaponSystem);
            _projectileSystem = new ProjectileSystem(_entityManager);
            _entityManager.RegisterSystem(_projectileSystem);

            // Subscribe to collision events
            _collisionSystem.OnCollision += OnEntitiesCollided;
            _collisionSystem.OnTriggerEnter += OnTriggerEntered;

            Console.WriteLine("Spatial Partitioning System initialized!");
            Console.WriteLine($"World Bounds: {worldBounds}");
        }

        /// <summary>
        /// Create a ship entity with all necessary components
        /// </summary>
        public Entity CreateShip(string name, Vector2 position, string factionId)
        {
            var ship = _entityManager.CreateEntity(name);

            // Transform
            ship.AddComponent(new TransformComponent
            {
                Position = position,
                Rotation = 0f,
                Scale = Vector2.One
            });

            // Collision
            ship.AddComponent(new CollisionComponent
            {
                Radius = 25f,
                IsTrigger = false
            });

            // Health
            ship.AddComponent(new HealthComponent
            {
                MaxHealth = 100f
            });

            // Velocity for movement
            ship.AddComponent(new VelocityComponent
            {
                LinearVelocity = Vector2.Zero
            });

            // Faction
            ship.AddComponent(new FactionComponent(factionId));

            // Tags
            ship.AddComponent(new TagComponent("ship", "combat"));

            // Targeting (for weapons)
            ship.AddComponent(new TargetingComponent
            {
                MaxRange = 500f,
                AutoTarget = true,
                PreferWeakTargets = true,
                RequireLineOfSight = false, // allow firing in cluttered demo
                ProjectileSpeed = 300f,
                TargetFilter = e =>
                {
                    var tag = e.GetComponent<TagComponent>();
                    // Avoid shooting asteroids/stations for cleaner demo
                    if (tag != null && tag.HasAnyTag("asteroid", "station")) return false;
                    return e.GetComponent<HealthComponent>() != null;
                }
            });

            // Weapon
            ship.AddComponent(new WeaponComponent
            {
                Damage = 6f,
                Range = 500f,
                Cooldown = 0.9f,
                ProjectileSpeed = 320f,
                ProjectileRadius = 5f,
                ProjectileTTL = 5f,
                AutoFire = true,
                FireWithoutTarget = true
            });

            // Selection
            ship.AddComponent(new SelectionComponent
            {
                IsSelectable = true,
                SelectionRadius = 30f
            });

            // Ensure systems pick up newly added components
            _entityManager.RefreshEntity(ship);

            return ship;
        }

        /// <summary>
        /// Create an asteroid entity
        /// </summary>
        public Entity CreateAsteroid(Vector2 position, float size)
        {
            var asteroid = _entityManager.CreateEntity("Asteroid");

            asteroid.AddComponent(new TransformComponent
            {
                Position = position,
                Scale = Vector2.One * size
            });

            asteroid.AddComponent(new CollisionComponent
            {
                Radius = 15f * size,
                IsTrigger = false
            });

            asteroid.AddComponent(new TagComponent("asteroid", "obstacle"));

            _entityManager.RefreshEntity(asteroid);

            return asteroid;
        }

        /// <summary>
        /// Create a space station
        /// </summary>
        public Entity CreateStation(string name, Vector2 position, string factionId)
        {
            var station = _entityManager.CreateEntity(name);

            station.AddComponent(new TransformComponent
            {
                Position = position,
                Scale = Vector2.One * 2f
            });

            station.AddComponent(new CollisionComponent
            {
                Radius = 50f,
                IsTrigger = true // Station uses trigger collisions
            });

            station.AddComponent(new HealthComponent
            {
                MaxHealth = 1000f
            });

            station.AddComponent(new FactionComponent(factionId));
            station.AddComponent(new TagComponent("station", "structure"));

            _entityManager.RefreshEntity(station);

            return station;
        }

        /// <summary>
        /// Example: Spawn a fleet of ships
        /// </summary>
        public void SpawnFleet(Vector2 center, string factionId, int count)
        {
            Random random = new Random();

            for (int i = 0; i < count; i++)
            {
                // Random position around center
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float distance = (float)(random.NextDouble() * 200 + 50);
                Vector2 position = center + new Vector2(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );

                var ship = CreateShip($"Ship_{factionId}_{i}", position, factionId);

                // Random velocity
                var velocity = ship.GetComponent<VelocityComponent>();
                velocity.LinearVelocity = new Vector2(
                    (float)(random.NextDouble() - 0.5) * 50f,
                    (float)(random.NextDouble() - 0.5) * 50f
                );
            }

            Console.WriteLine($"Spawned fleet of {count} ships for faction {factionId}");
        }

        /// <summary>
        /// Example: Find nearest enemy
        /// </summary>
        public Entity FindNearestEnemy(Entity sourceShip)
        {
            var transform = sourceShip.GetComponent<TransformComponent>();
            var faction = sourceShip.GetComponent<FactionComponent>();

            if (transform == null || faction == null)
                return null;

            // Use spatial query to find nearby entities
            var nearbyEntities = _spatialSystem.QueryRadius(transform.Position, 1000f);

            Entity nearestEnemy = null;
            float nearestDistance = float.MaxValue;

            foreach (var entity in nearbyEntities)
            {
                if (entity.Id == sourceShip.Id)
                    continue;

                // Check if enemy faction
                var otherFaction = entity.GetComponent<FactionComponent>();
                if (otherFaction == null || otherFaction.FactionId == faction.FactionId)
                    continue;

                // Check if alive
                var health = entity.GetComponent<HealthComponent>();
                if (health != null && !health.IsAlive)
                    continue;

                // Calculate distance
                var otherTransform = entity.GetComponent<TransformComponent>();
                if (otherTransform == null)
                    continue;

                float distance = Vector2.Distance(transform.Position, otherTransform.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = entity;
                }
            }

            return nearestEnemy;
        }

        /// <summary>
        /// Example: Area of Effect damage
        /// </summary>
        public void ApplyAOEDamage(Vector2 center, float radius, float damage, string sourceFaction)
        {
            // Use spatial query to find all entities in blast radius
            var entitiesInRange = _spatialSystem.QueryRadius(center, radius);

            int hitCount = 0;
            foreach (var entity in entitiesInRange)
            {
                // Don't damage friendly units
                var faction = entity.GetComponent<FactionComponent>();
                if (faction != null && faction.FactionId == sourceFaction)
                    continue;

                // Apply damage
                var health = entity.GetComponent<HealthComponent>();
                if (health != null && health.IsAlive)
                {
                    // Distance-based damage falloff
                    var transform = entity.GetComponent<TransformComponent>();
                    if (transform != null)
                    {
                        float distance = Vector2.Distance(center, transform.Position);
                        float damageMultiplier = 1.0f - (distance / radius);
                        float actualDamage = damage * Math.Max(0, damageMultiplier);

                        health.TakeDamage(actualDamage);
                        hitCount++;

                        Console.WriteLine($"AOE hit {entity.Name}: {actualDamage:F1} damage ({health.CurrentHealth:F0}/{health.MaxHealth:F0} HP)");
                    }
                }
            }

            Console.WriteLine($"AOE explosion hit {hitCount} entities");
        }

        private void TrackLoss(HealthComponent health, Entity victim)
        {
            var faction = victim.GetComponent<FactionComponent>()?.FactionId ?? "neutral";
            if (!_lossesByFaction.ContainsKey(faction)) _lossesByFaction[faction] = 0;
            _lossesByFaction[faction]++;
        }

        /// <summary>
        /// Example: Mouse selection (click to select ship)
        /// </summary>
        public Entity SelectEntityAtPosition(Vector2 worldPosition, float searchRadius = 50f)
        {
            var entity = _spatialSystem.FindNearest(worldPosition, searchRadius);

            if (entity != null)
            {
                var selection = entity.GetComponent<SelectionComponent>();
                if (selection != null && selection.IsSelectable)
                {
                    // Deselect all others
                    foreach (var e in _entityManager.GetAllEntities())
                    {
                        var s = e.GetComponent<SelectionComponent>();
                        if (s != null) s.IsSelected = false;
                    }

                    // Select this one
                    selection.IsSelected = true;
                    Console.WriteLine($"Selected: {entity.Name}");
                    return entity;
                }
            }

            return null;
        }

        /// <summary>
        /// Example: Get visible entities for rendering
        /// </summary>
        public List<Entity> GetVisibleEntities(Vector2 cameraPosition, Vector2 viewportSize, float zoom)
        {
            return _cullingSystem.GetVisibleEntities(cameraPosition, viewportSize, zoom);
        }

        /// <summary>
        /// Update all systems
        /// </summary>
        public void Update(float deltaTime)
        {
            _entityManager.Update(deltaTime);
        }

        /// <summary>
        /// Get performance statistics
        /// </summary>
        public void PrintStatistics()
        {
            var stats = _spatialSystem.GetStats();
            Console.WriteLine("=== Spatial System Statistics ===");
            Console.WriteLine(stats.ToString());

            var allEntities = _entityManager.GetAllEntities();
            Console.WriteLine($"Active Entities: {allEntities.Count}");
            
            // Count by type
            int ships = 0, stations = 0, asteroids = 0;
            foreach (var entity in allEntities)
            {
                var tag = entity.GetComponent<TagComponent>();
                if (tag != null)
                {
                    if (tag.HasTag("ship")) ships++;
                    if (tag.HasTag("station")) stations++;
                    if (tag.HasTag("asteroid")) asteroids++;
                }
            }
            Console.WriteLine($"Ships: {ships}, Stations: {stations}, Asteroids: {asteroids}");
        }

        #region Event Handlers

        private void OnEntitiesCollided(Entity a, Entity b)
        {
            var tagA = a.GetComponent<TagComponent>();
            var tagB = b.GetComponent<TagComponent>();
            // Trim spam: skip asteroid-involved collisions
            if (!(tagA != null && tagA.HasTag("asteroid")) && !(tagB != null && tagB.HasTag("asteroid")))
            {
                if (_collisionLogsRemaining-- > 0)
                {
                    Console.WriteLine($"Collision: {a.Name} <-> {b.Name}");
                    if (_collisionLogsRemaining == 0) Console.WriteLine("(collision logging throttled)");
                }
            }

            // Example: Deal collision damage
            var healthA = a.GetComponent<HealthComponent>();
            var healthB = b.GetComponent<HealthComponent>();
            var velocityA = a.GetComponent<VelocityComponent>();
            var velocityB = b.GetComponent<VelocityComponent>();

            if (healthA != null && velocityB != null)
            {
                float damage = velocityB.LinearVelocity.Length() * 0.1f;
                var pre = healthA.CurrentHealth;
                healthA.TakeDamage(damage);
                if (pre > 0 && healthA.CurrentHealth <= 0)
                {
                    _destroyed++;
                    TrackLoss(healthA, a);
                }
            }

            if (healthB != null && velocityA != null)
            {
                float damage = velocityA.LinearVelocity.Length() * 0.1f;
                var pre = healthB.CurrentHealth;
                healthB.TakeDamage(damage);
                if (pre > 0 && healthB.CurrentHealth <= 0)
                {
                    _destroyed++;
                    TrackLoss(healthB, b);
                }
            }
        }

        private void OnTriggerEntered(Entity a, Entity b)
        {
            // Normalize so projEntity is the projectile and target is the other entity
            Entity projEntity = null;
            Entity target = null;

            var projA = a.GetComponent<ProjectileComponent>();
            var projB = b.GetComponent<ProjectileComponent>();
            if (projA != null)
            {
                projEntity = a;
                target = b;
            }
            else if (projB != null)
            {
                projEntity = b;
                target = a;
            }

            if (projEntity != null)
            {
                var proj = projEntity.GetComponent<ProjectileComponent>();
                var health = target.GetComponent<HealthComponent>();
                if (health != null && health.IsAlive)
                {
                    var pre = health.CurrentHealth;
                    health.TakeDamage(proj.Damage);
                    if (_projectileLogsRemaining-- > 0)
                    {
                        Console.WriteLine($"{target.Name} took {proj.Damage:F1} dmg from projectile");
                        if (_projectileLogsRemaining == 0) Console.WriteLine("(projectile damage logging throttled)");
                    }
                    _projectileHits++;
                    if (pre > 0 && health.CurrentHealth <= 0)
                    {
                        _destroyed++;
                        TrackLoss(health, target);
                    }
                }

                // Destroy projectile
                _entityManager.DestroyEntity(projEntity.Id);
                return;
            }

            // Station docking (keep demo behavior)
            var triggerTag = a.GetComponent<TagComponent>();
            var otherTag = b.GetComponent<TagComponent>();
            if (triggerTag != null && triggerTag.HasTag("station") && otherTag != null && otherTag.HasTag("ship"))
            {
                LogDocking(a, b);
            }
            else if (otherTag != null && otherTag.HasTag("station") && triggerTag != null && triggerTag.HasTag("ship"))
            {
                // Handle inverted order for clarity
                LogDocking(b, a);
            }
        }

        private void LogDocking(Entity station, Entity ship)
        {
            var pair = (station.Id, ship.Id);
            if (!_dockingLogPairs.Add(pair)) return; // already logged this ship/station pair

            if (_dockingLogsRemaining-- > 0)
            {
                Console.WriteLine($"Ship {ship.Name} docking at station {station.Name}");
                if (_dockingLogsRemaining == 0) Console.WriteLine("(docking logging throttled)");
            }
        }

        #endregion
    }

    /// <summary>
    /// Quick test/demo
    /// </summary>
    public class SpatialPartitioningDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== Spatial Partitioning System Demo ===\n");

            var example = new SpatialPartitioningExample();
            example.Initialize();

            Console.WriteLine("\n--- Creating Game World ---");

            // Create stations
            var station1 = example.CreateStation("Station Alpha", new Vector2(0, 0), "human");
            var station2 = example.CreateStation("Station Beta", new Vector2(1000, 500), "alien");

            // Create asteroid field
            Random random = new Random();
            for (int i = 0; i < 50; i++)
            {
                Vector2 pos = new Vector2(
                    (float)(random.NextDouble() * 2000 - 1000),
                    (float)(random.NextDouble() * 2000 - 1000)
                );
                example.CreateAsteroid(pos, (float)(random.NextDouble() * 2 + 0.5));
            }

            // Spawn fleets
            example.SpawnFleet(new Vector2(-300, 0), "human", 20);
            example.SpawnFleet(new Vector2(300, 0), "alien", 20);

            Console.WriteLine("\n--- Simulating 600 frames ---");
            for (int frame = 0; frame < 600; frame++)
            {
                example.Update(1/60f);
                if ((frame + 1) % 120 == 0) Console.WriteLine($"Simulated {frame + 1} frames...");
            }

            Console.WriteLine("\n--- Testing Queries ---");

            // Test AOE damage
            example.ApplyAOEDamage(new Vector2(0, 0), 200f, 50f, "neutral");

            // Print statistics
            Console.WriteLine("\n--- Final Statistics ---");
            example.PrintStatistics();
            // Summary
            var summaryHitsField = typeof(SpatialPartitioningExample).GetField("_projectileHits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var summaryDestroyedField = typeof(SpatialPartitioningExample).GetField("_destroyed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int hits = (int)(summaryHitsField?.GetValue(example) ?? 0);
            int destroyed = (int)(summaryDestroyedField?.GetValue(example) ?? 0);
            Console.WriteLine($"Projectile hits: {hits}");
            Console.WriteLine($"Destroyed entities (approx): {destroyed}");
            // Per-faction losses
            var lossesField = typeof(SpatialPartitioningExample).GetField("_lossesByFaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var losses = lossesField?.GetValue(example) as System.Collections.Generic.Dictionary<string, int> ?? new System.Collections.Generic.Dictionary<string, int>();
            Console.WriteLine("Faction losses:");
            foreach (var kvp in losses)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("\n=== Demo Complete ===");
        }
    }
}
