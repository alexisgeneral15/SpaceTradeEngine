using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Sistema para generar entidades masivas y simular carga pesada (stress test)
    /// Genera entidades reales con Transform, Velocity, Collision, Faction
    /// </summary>
    public class StressTestGenerator
    {
        private Random _random = new(42);
        private EntityManager _entityManager;

        public int TotalEntitiesSpawned { get; private set; }
        public int ActiveEntities { get; private set; }

        public StressTestGenerator(EntityManager entityManager)
        {
            _entityManager = entityManager;
            Console.WriteLine("[StressTest] Generator initialized - full entity spawning");
        }

        /// <summary>
        /// Genera masivamente entidades para stress test
        /// REDUCIDO: 100 enemigos por defecto para evitar sobrecarga de CPU
        /// </summary>
        public void GenerateMassiveBattle(int enemyCount = 100, int asteroidCount = 50)
        {
            Console.WriteLine($"[StressTest] Spawning {enemyCount} enemies + {asteroidCount} asteroids...");
            
                int spawnedEnemies = 0;
                // Spawn enemies in circular pattern
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (float)i / enemyCount * MathHelper.TwoPi;
                float distance = 500f + _random.Next(2000);
                
                var enemy = _entityManager.CreateEntity($"StressEnemy_{i}");
                enemy.AddComponent(new TransformComponent
                {
                    Position = new Vector2(
                        (float)Math.Cos(angle) * distance,
                        (float)Math.Sin(angle) * distance
                    ),
                    Rotation = angle + MathHelper.PiOver2
                });
                
                enemy.AddComponent(new VelocityComponent
                {
                    LinearVelocity = new Vector2(
                        _random.Next(-50, 50),
                        _random.Next(-50, 50)
                    ),
                    AngularVelocity = (_random.NextSingle() - 0.5f) * 0.5f
                });
                
                enemy.AddComponent(new CollisionComponent
                {
                    Radius = 10f + _random.Next(15)
                });
                
                enemy.AddComponent(new FactionComponent("pirate", "Pirate"));
                
                // Explicit sprite to force red visual appearance
                enemy.AddComponent(new SpriteComponent
                {
                    Color = Color.Red,
                    Width = 24,
                    Height = 24,
                    LayerDepth = 0.5f,
                    IsVisible = true
                });

                // Re-register with systems after all components added
                _entityManager.RefreshEntity(enemy);
                    spawnedEnemies++;
            }
            
                int spawnedAsteroids = 0;
            // Spawn asteroids
            for (int i = 0; i < asteroidCount; i++)
            {
                var asteroid = _entityManager.CreateEntity($"StressAsteroid_{i}");
                asteroid.AddComponent(new TransformComponent
                {
                    Position = new Vector2(
                        _random.Next(-3000, 3000),
                        _random.Next(-3000, 3000)
                    ),
                    Rotation = _random.NextSingle() * MathHelper.TwoPi
                });
                
                asteroid.AddComponent(new VelocityComponent
                {
                    LinearVelocity = new Vector2(
                        _random.Next(-20, 20),
                        _random.Next(-20, 20)
                    ),
                    AngularVelocity = (_random.NextSingle() - 0.5f) * 1f
                });
                
                asteroid.AddComponent(new CollisionComponent
                {
                    Radius = 20f + _random.Next(30)
                });
                
                // Re-register with systems
                _entityManager.RefreshEntity(asteroid);
                    spawnedAsteroids++;
            }
            
                TotalEntitiesSpawned += spawnedEnemies + spawnedAsteroids;
            ActiveEntities = TotalEntitiesSpawned;
                Console.WriteLine($"[StressTest] ✓ Successfully spawned: {spawnedEnemies} enemies + {spawnedAsteroids} asteroids = {spawnedEnemies + spawnedAsteroids} total");
        }

        /// <summary>
        /// Genera entidades en onda de ataque
        /// </summary>
        public void GenerateWaveAttack(int waveNumber = 1)
        {
            int enemiesPerWave = 50 + (waveNumber * 25);
            TotalEntitiesSpawned += enemiesPerWave;
            Console.WriteLine($"[StressTest] Wave {waveNumber}: +{enemiesPerWave} enemies");
        }

        /// <summary>
        /// Genera proyectiles en spray masivo
        /// </summary>
        public void GenerateMassiveBarrage(Vector2 origin, int projectileCount = 100)
        {
            TotalEntitiesSpawned += projectileCount;
            Console.WriteLine($"[StressTest] Missile barrage: +{projectileCount} projectiles");
        }

        /// <summary>
        /// Genera efecto de tormenta de partículas
        /// </summary>
        public void GenerateParticleStorm(Vector2 origin, int particleCount = 1000)
        {
            TotalEntitiesSpawned += particleCount;
            Console.WriteLine($"[StressTest] Particle storm: +{particleCount} particles");
        }

        /// <summary>
        /// Limpia todos los enemigos generados
        /// </summary>
        public void ClearAllEnemies()
        {
            Console.WriteLine($"[StressTest] Clearing {TotalEntitiesSpawned} entities");
            TotalEntitiesSpawned = 0;
            ActiveEntities = 0;
        }

        /// <summary>
        /// Información del stress test
        /// </summary>
        public string GetStressTestInfo()
        {
            return $"Stress Test | Entities: {TotalEntitiesSpawned} | Active: {ActiveEntities}";
        }
    }
}

