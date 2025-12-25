using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Simplified campaign manager focusing on core playable first steps.
    /// Spawns player ship, enemies, and trading stations.
    /// </summary>
    public class CampaignManager
    {
        private readonly EntityManager _entityManager;
        private Entity? _playerShip;
        private bool _campaignStarted;
        private readonly List<Entity> _enemies = new();
        private readonly List<Entity> _stations = new();

        public Entity? PlayerShip => _playerShip;
        public bool CampaignStarted => _campaignStarted;

        public CampaignManager(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public void StartNewCampaign()
        {
            if (_campaignStarted)
                return;

            _campaignStarted = true;
            Console.WriteLine("=== CAMPAIGN STARTED ===");

            SpawnPlayerShip();
            SpawnInitialEnemies();
            SpawnTradeNetwork();

            Console.WriteLine("✓ Campaign initialized with player ship, 3 enemy patrols, 3 trading stations");
        }

        private void SpawnPlayerShip()
        {
            var startPos = Vector2.Zero;
            _playerShip = _entityManager.CreateEntity("Player_Cobra");

            _playerShip.AddComponent(new TransformComponent
            {
                Position = startPos,
                Rotation = 0f,
                Scale = Vector2.One
            });

            _playerShip.AddComponent(new SpriteComponent
            {
                Color = Color.LimeGreen,
                Width = 32,
                Height = 24,
                LayerDepth = 0.5f,
                IsVisible = true
            });

            _playerShip.AddComponent(new VelocityComponent
            {
                LinearVelocity = Vector2.Zero,
                AngularVelocity = 0f
            });

            _playerShip.AddComponent(new CollisionComponent
            {
                Radius = 16f,
                IsTrigger = false
            });

            _playerShip.AddComponent(new FactionComponent("human", "Human Federation"));

            _playerShip.AddComponent(new HealthComponent
            {
                MaxHealth = 200f,
                CurrentHealth = 200f
            });

            _playerShip.AddComponent(new WeaponComponent
            {
                Id = "laser_cannon",
                Damage = 15f,
                Cooldown = 0.3f,
                CooldownRemaining = 0f,
                Range = 800f,
                ProjectileSpeed = 450f,
                AutoFire = false,
                FireWithoutTarget = true
            });

            _playerShip.AddComponent(new CargoComponent
            {
                MaxVolume = 1000f,
                Credits = 10000f
            });

            _playerShip.AddComponent(new SelectionComponent
            {
                IsSelectable = true,
                IsSelected = true
            });

            _playerShip.AddComponent(new PlayerControlComponent());

            // CRITICAL: Re-register with systems AFTER adding all components
            // so systems like PlayerInputSystem can see it has PlayerControlComponent
            _entityManager.RefreshEntity(_playerShip);

            Console.WriteLine($"✓ Player ship spawned at {startPos}");
        }

        private void SpawnTradeNetwork()
        {
            var stations = new[]
            {
                ("Alpha_Station", new Vector2(-400, -400), Color.CornflowerBlue),
                ("Beta_Outpost", new Vector2(400, -400), Color.SteelBlue),
                ("Gamma_Hub", new Vector2(0, 400), Color.DodgerBlue),
            };

            foreach (var (name, pos, color) in stations)
            {
                var station = _entityManager.CreateEntity(name);

                station.AddComponent(new TransformComponent
                {
                    Position = pos,
                    Scale = Vector2.One
                });

                station.AddComponent(new SpriteComponent
                {
                    Color = color,
                    Width = 40,
                    Height = 40,
                    LayerDepth = 0.3f,
                    IsVisible = true
                });

                station.AddComponent(new CargoComponent
                {
                    MaxVolume = 5000f,
                    Credits = 50000f
                });

                _stations.Add(station);
                Console.WriteLine($"✓ Trading station {name} spawned at {pos}");
            }
        }

        private void SpawnInitialEnemies()
        {
            var enemies = new[]
            {
                ("Hornet_Patrol_1", new Vector2(-300, -300), Color.Red),
                ("Viper_Patrol_2", new Vector2(300, -300), Color.OrangeRed),
                ("Reaper_Patrol_3", new Vector2(0, 300), Color.DarkRed),
            };

            foreach (var (name, pos, color) in enemies)
            {
                var enemy = _entityManager.CreateEntity(name);

                enemy.AddComponent(new TransformComponent
                {
                    Position = pos,
                    Rotation = 0f,
                    Scale = Vector2.One
                });

                enemy.AddComponent(new SpriteComponent
                {
                    Color = color,
                    Width = 30,
                    Height = 20,
                    LayerDepth = 0.5f,
                    IsVisible = true
                });

                enemy.AddComponent(new VelocityComponent
                {
                    LinearVelocity = Vector2.Zero,
                    AngularVelocity = 0f
                });

                enemy.AddComponent(new CollisionComponent
                {
                    Radius = 15f,
                    IsTrigger = false
                });

                enemy.AddComponent(new FactionComponent("pirate", "Pirates"));

                enemy.AddComponent(new HealthComponent
                {
                    MaxHealth = 100f,
                    CurrentHealth = 100f
                });

                enemy.AddComponent(new WeaponComponent
                {
                    Id = "cannon",
                    Damage = 10f,
                    Cooldown = 0.5f,
                    CooldownRemaining = 0f,
                    Range = 600f,
                    ProjectileSpeed = 400f,
                    AutoFire = true,
                    FireWithoutTarget = false
                });

                _enemies.Add(enemy);
                // Re-register with systems after adding all components
                _entityManager.RefreshEntity(enemy);
                Console.WriteLine($"✓ Enemy patrol {name} spawned at {pos}");
            }
        }

        public void Update(float deltaTime)
        {
            // Simple placeholder update
        }

        public void EndCampaign()
        {
            _campaignStarted = false;
            _playerShip = null;
            _enemies.Clear();
            _stations.Clear();
            Console.WriteLine("Campaign ended");
        }
    }

    /// <summary>
    /// Component marking an entity as player-controlled
    /// </summary>
    public class PlayerControlComponent : Component
    {
        public bool IsControlled { get; set; } = true;
        public float RotationSpeed { get; set; } = 4f;
        public float Acceleration { get; set; } = 140f;
        public float MaxSpeed { get; set; } = 180f;
    }
}
