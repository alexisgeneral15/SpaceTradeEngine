using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Data;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.AI;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Main game engine class - handles initialization, game loop, and system management
    /// </summary>
    public class GameEngine : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        // Core systems
        private GameStateManager _stateManager;
        private InputManager _inputManager;
        private RenderingSystem _renderingSystem;
        private EntityManager _entityManager;
        private ConfigManager _configManager;
        
        // Spatial systems
        private SpatialPartitioningSystem _spatialSystem;
        private CollisionSystem _collisionSystem;
        private TargetingSystem _targetingSystem;
        private CullingSystem _cullingSystem;
        private EventSystem _eventSystem;
        private SaveLoadSystem _saveLoadSystem;
        private UI.UIManager _uiManager;
        private WeaponSystem _weaponSystem;
        private ProjectileSystem _projectileSystem;
        private CameraFollowSystem _cameraFollowSystem;
        private UI.SelectionPanel _selectionPanel;
        private UI.SelectionBoxOverlay _selectionBox;
        private bool _isDraggingSelection = false;
        private Vector2 _dragStartScreen;
        private Rectangle _dragRectScreen;
        private List<int> _selectedIds; // lazy init
        
        // Gameplay systems (activos)
        private FleetSystem _fleetSystem;
        private MiningSystem _miningSystem;
        private DiplomacySystem _diplomacySystem;
        private MissionSystem _missionSystem;
        private AIBehaviorSystem _aiBehaviorSystem;
        private ProductionSystem _productionSystem;
        // ELIMINADOS para ahorrar memoria: _economySystem, _audioSystem, _clanSystem, _factionAISystem, _stationSystem, _traderAISystem, _shipyardSystem, _economyPanel
        private RankSystem _rankSystem;
        private DamageSystem _damageSystem;
        private CombatSystem _combatSystem;
        // ELIMINADO: _economyPanel
        
        // Campaign and player systems
        private CampaignManager _campaignManager;
        private PlayerInputSystem _playerInputSystem;
        private PhysicsSystem _physicsSystem;
        private UI.GameHUD _gameHUD;
        
        // Overlay texts for events (damage, collisions) - lazy init
        private class OverlayText { public Vector2 WorldPos; public string Text; public Color Color; public float TTL; }
        private List<OverlayText> _overlays;
        
        // Time acceleration
        private float _timeScale = 1.0f;

        // UI hints
        private bool _showControlsHint = true;
        
        // Game timing - NUEVO: Sistema optimizado
        private GameClock _clock;
        private OptimizedGameLoop _optimizedLoop;
        private InputBuffer _inputBuffer;
        
        // Nueva infraestructura (solo lo esencial)
        private WindowManager _windowManager;
        // ELIMINADOS: _advancedInput, _profiler (desactivados)
        private StressTestGenerator _stressTestGenerator;
        private ScreenAdapter _screenAdapter;
        
        // Debug mode
        public bool DebugMode { get; set; } = true;
        
        // Performance metrics
        private float _targetFPS = 60f;
        private bool _vsyncEnabled = true;
        // ELIMINADO: _showPerformanceMetrics (nunca usado)
        // QuadTree visualization toggled via F4 through spatial system

        public GameEngine()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "assets";
            IsMouseVisible = true;
            
            // Set up window con tamaño reducido para menos memoria
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;
            
            // Configurar VSync y frame rate
            _graphics.SynchronizeWithVerticalRetrace = _vsyncEnabled;
            IsFixedTimeStep = false; // Usamos nuestro loop optimizado
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / _targetFPS);
            
            _graphics.ApplyChanges();
        }

        /// <summary>
        /// Initialize game engine and all systems
        /// </summary>
        protected override void Initialize()
        {
            // Initialize memory arena FIRST (1MB default pool)
            Console.WriteLine($"=== MEMORY ARENA INITIALIZED ===");
            GlobalMemoryArena.Instance.SetMaxCapacity(1_000_000); // 1MB
            Console.WriteLine($"Memory Arena: {GlobalMemoryArena.Instance.MaxCapacity / 1024}KB max");
            
            // Load configuration first
            _configManager = new ConfigManager();
            _configManager.LoadConfig("assets/data/config.json");
            
            // Inicializar sistemas optimizados (mínimos)
            _clock = new GameClock();
            _optimizedLoop = new OptimizedGameLoop();
            _inputBuffer = new InputBuffer();
            
            // Auto-detectar y adaptar pantalla PRIMERO
            _screenAdapter = new ScreenAdapter(_graphics, Window);
            _screenAdapter.AutoDetectAndAdaptScreen();
            
            _windowManager = new WindowManager(_graphics, Window);
            // ELIMINADOS: _advancedInput, _profiler → ahorran ~5-10MB
            
            Console.WriteLine($"=== OPTIMIZED GAME ENGINE INITIALIZED ===");
            Console.WriteLine($"Target FPS: {_targetFPS} | VSync: {_vsyncEnabled}");
            Console.WriteLine($"Fixed Time Step: {_optimizedLoop.FixedDelta:F4}s");
            
            // Initialize core systems (sin duplicados)
            _inputManager = new InputManager();
            _entityManager = new EntityManager();
            _stateManager = new GameStateManager();
            
            // Stress test generator
            _stressTestGenerator = new StressTestGenerator(_entityManager);
            
            base.Initialize();
        }

        /// <summary>
        /// Load game content (textures, fonts, etc.)
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Initialize rendering system
            _renderingSystem = new RenderingSystem(GraphicsDevice, _spriteBatch, Content);
            
            // Initialize overlays early (prevents lazy-init issues)
            _overlays ??= new List<OverlayText>(32);
            
            // Load all game data
#pragma warning disable IL2026 // Data loader uses JSON reflection; acceptable for trimming here
            DataLoader.LoadAllData("assets/data", _entityManager, _configManager);
#pragma warning restore IL2026
            
            // Set initial game state - AUTO-START CAMPAIGN
            _stateManager.ChangeState(GameState.Playing);

            // Initialize spatial systems after rendering is available
            InitializeSpatialSystems();

            // Initialize Save/Load system
            _saveLoadSystem = new SaveLoadSystem(_entityManager);

            // Initialize minimal UI (Save/Load toolbar)
            _uiManager = new UI.UIManager(GraphicsDevice);
            var vp = GraphicsDevice.Viewport;
            _uiManager.Add(UI.UIFactory.CreateTopRightToolbar(GraphicsDevice, vp.Width,
                onSave: () => TrySaveGame(),
                onLoad: () => TryLoadGame()
            ));
            
            // Game HUD for player info display
            _gameHUD = new UI.GameHUD(new Vector2(vp.Width, vp.Height));
            _uiManager.Add(_gameHUD);
            
            // DESACTIVADO: Minimap y Economy panel para ahorrar memoria
            // Add minimap bottom-left
            // _uiManager.Add(new UI.Minimap(_spatialSystem, new Rectangle(10, vp.Height - 110, 180, 100)));
            
            // Add selection panel bottom-right
            _selectionPanel = new UI.SelectionPanel(new Rectangle(vp.Width - 260, vp.Height - 140, 250, 130));
            _selectionPanel.OnFocusTarget = FocusSelectedOnNearestEnemy;
            _uiManager.Add(_selectionPanel);
            _selectionBox = new UI.SelectionBoxOverlay();
            _uiManager.Add(_selectionBox);

            // Subscribe to gameplay events for UI overlays
            _eventSystem.Subscribe<Events.EntityDamagedEvent>(evt =>
            {
                var entity = _entityManager.GetEntity(evt.EntityId);
                var t = entity?.GetComponent<ECS.Components.TransformComponent>();
                if (t != null)
                {
                    _overlays ??= new List<OverlayText>(32);
                    _overlays.Add(new OverlayText
                    {
                        WorldPos = t.Position,
                        Text = $"-{evt.Damage:F0}",
                        Color = Color.Red,
                        TTL = 1.5f
                    });
                }
            });

            _eventSystem.Subscribe<Events.CollisionEvent>(evt =>
            {
                _overlays.Add(new OverlayText
                {
                    WorldPos = evt.Point,
                    Text = "Impact",
                    Color = Color.Orange,
                    TTL = 0.8f
                });
            });
            
            // AUTO-START CAMPAIGN
            Console.WriteLine("[Engine] Auto-starting campaign...");
            StartNewCampaign();
            
            // Log memory arena stats at startup
                Console.WriteLine("\n[STARTUP] Logging memory arena statistics:");
                var arena = GlobalMemoryArena.Instance;
                var stats = arena.GetStats();
                Console.WriteLine($"Max Capacity: {arena.MaxCapacity / 1024}KB");
                Console.WriteLine($"Total Allocated: {stats.TotalAllocated / 1024}KB");
                Console.WriteLine($"Usage: {stats.UsagePercent:F1}%");
                Console.WriteLine($"Active Allocations: {stats.ActiveAllocations}");
                Console.WriteLine($"Top 5 allocations:");
                  foreach (var item in stats.TopAllocations.Take(5))
                {
                     Console.WriteLine($"  {item.name}: {item.bytes / 1024}KB (count: {item.count})");
                }
        }

        /// <summary>
        /// Main game loop update - CICLO OPTIMIZADO
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            // PASO 0: Iniciar profiler (DESACTIVADO para ahorrar CPU/RAM)
            // _profiler.BeginFrame();
            // _profiler.BeginUpdate();
            
            // PASO 1: Iniciar frame y calcular delta time preciso
            _optimizedLoop.BeginFrame();
            float rawDelta = _optimizedLoop.GetSmoothedDeltaTime();
            float deltaTime = rawDelta * _timeScale;
            _clock.Update(deltaTime);

            // PASO 2: Capturar input crudo (sin pérdida)
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();
            var gamepad = GamePad.GetState(PlayerIndex.One);
            _inputBuffer.CaptureInput(keyboard, mouse, gamepad);
            
            // PASO 3: Procesar buffer de entrada
            _inputBuffer.ProcessBuffer();

            // PASO 4: Actualizar InputManager legacy para compatibilidad (una sola vez por frame)
            _inputManager.Update();
            if (_inputManager.IsKeyPressed(Keys.Escape))
                Exit();
            
            // Update current game state
            switch (_stateManager.CurrentState)
            {
                case GameState.Playing:
                    UpdateGameplay(deltaTime);
                    break;
                case GameState.Paused:
                    UpdatePaused(deltaTime);
                    break;
                case GameState.MainMenu:
                    UpdateMainMenu(deltaTime);
                    break;
            }
            
            // Toggle debug mode
            if (_inputManager.IsKeyPressed(Keys.F12))
                DebugMode = !DebugMode;
            // F3 - toggle debug info
            if (_inputManager.IsKeyPressed(Keys.F3))
                DebugMode = !DebugMode;
            // F4 - toggle QuadTree visualization
            if (_inputManager.IsKeyPressed(Keys.F4))
                _spatialSystem?.ToggleDebugVisualization();
            // F5 - toggle culling in rendering
            if (_inputManager.IsKeyPressed(Keys.F5))
                _renderingSystem?.ToggleCulling();

            // Time acceleration hotkeys (1x/2x/4x/8x)
            if (_inputManager.IsKeyPressed(Keys.D1)) _timeScale = 1f;
            if (_inputManager.IsKeyPressed(Keys.D2)) _timeScale = 2f;
            if (_inputManager.IsKeyPressed(Keys.D3)) _timeScale = 4f;
            if (_inputManager.IsKeyPressed(Keys.D4)) _timeScale = 8f;
            if (_inputManager.IsKeyPressed(Keys.H)) _showControlsHint = !_showControlsHint;

            // Campaign hotkeys
            if (_inputManager.IsKeyPressed(Keys.N)) StartNewCampaign();

            // Save/Load hotkeys
            if (_inputManager.IsKeyPressed(Keys.F6)) TrySaveGame();
            if (_inputManager.IsKeyPressed(Keys.F7)) TryLoadGame();

            // Quick demo helpers (varios eliminados por falta de sistemas)
            // if (_inputManager.IsKeyPressed(Keys.F1)) SpawnFactionAIDemo();  // ELIMINADO: usa _factionAISystem
            // if (_inputManager.IsKeyPressed(Keys.F2)) SpawnEconomyDemo();     // ELIMINADO: usa _stationSystem, _shipyardSystem
            if (_inputManager.IsKeyPressed(Keys.F3)) SpawnMilitaryRankDemo();
            if (_inputManager.IsKeyPressed(Keys.F4)) SpawnDynamicEventDemo();
            if (_inputManager.IsKeyPressed(Keys.F7)) PrintMemoryArenaStats();
            if (_inputManager.IsKeyPressed(Keys.F8)) ArmEntitiesWithWeapons();
            if (_inputManager.IsKeyPressed(Keys.F9)) SpawnDemoDuel();
            if (_inputManager.IsKeyPressed(Keys.F10)) SpawnFleetDemo();
            if (_inputManager.IsKeyPressed(Keys.F11)) SpawnMiningDemo();
            // if (_inputManager.IsKeyPressed(Keys.F12)) SpawnClanDemo();       // ELIMINADO: usa _clanSystem

            // Fleet formation hotkeys for selected ships
            if (_inputManager.IsKeyPressed(Keys.Q)) SetSelectedFleetFormation(FormationType.Line);
            if (_inputManager.IsKeyPressed(Keys.W)) SetSelectedFleetFormation(FormationType.Wedge);
            if (_inputManager.IsKeyPressed(Keys.E)) SetSelectedFleetFormation(FormationType.Box);

            // Stress Test hotkeys
            if (_inputManager.IsKeyPressed(Keys.NumPad0)) 
            {
                _stressTestGenerator?.ClearAllEnemies();
                Console.WriteLine("[Input] Stress test cleared");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad1)) 
            {
                _stressTestGenerator?.GenerateMassiveBattle(50, 10);
                Console.WriteLine("[Input] Small battle generated");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad2)) 
            {
                _stressTestGenerator?.GenerateMassiveBattle(100, 20);
                Console.WriteLine("[Input] Medium battle generated");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad3) || _inputManager.IsKeyPressed(Keys.PageDown))
            {
                Console.WriteLine("[DEBUG] NumPad3/PageDown pressed - spawning stress battle");
                _stressTestGenerator?.GenerateMassiveBattle(150, 30);
                // On-screen overlay confirmation
                var vp = GraphicsDevice.Viewport;
                _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Stress: 150 enemies spawned", Color = Color.Red, TTL = 1.5f });
                Console.WriteLine("[Input] Large battle generated");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad4)) 
            {
                _stressTestGenerator?.GenerateMassiveBarrage(Vector2.Zero, 200);
                Console.WriteLine("[Input] Missile barrage generated");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad5)) 
            {
                _stressTestGenerator?.GenerateParticleStorm(Vector2.Zero, 500);
                Console.WriteLine("[Input] Particle storm generated");
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad6)) 
            {
                _windowManager?.SetQualityPreset(QualityPreset.Low);
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad7)) 
            {
                _windowManager?.SetQualityPreset(QualityPreset.Medium);
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad8)) 
            {
                _windowManager?.SetQualityPreset(QualityPreset.High);
            }
            if (_inputManager.IsKeyPressed(Keys.NumPad9)) 
            {
                _windowManager?.SetQualityPreset(QualityPreset.Ultra);
            }

            // Camera follow hotkeys
            if (_inputManager.IsKeyPressed(Keys.F)) FollowSelectedEntity();
            if (_inputManager.IsKeyPressed(Keys.C)) _cameraFollowSystem?.ToggleFollow();
            if (_inputManager.IsKeyPressed(Keys.OemPlus)) _cameraFollowSystem?.SetZoomTarget(_renderingSystem.CameraZoom * 1.2f);
            if (_inputManager.IsKeyPressed(Keys.OemMinus)) _cameraFollowSystem?.SetZoomTarget(_renderingSystem.CameraZoom / 1.2f);

            // UI update
            _uiManager?.Update(_inputManager);

            // Handle selection on left-click
            // Begin drag
            if (!_isDraggingSelection && _inputManager.IsMouseLeftDown)
            {
                _isDraggingSelection = true;
                _dragStartScreen = new Vector2(_inputManager.MouseX, _inputManager.MouseY);
            }

            // Update drag rect
            if (_isDraggingSelection && _inputManager.IsMouseLeftDown)
            {
                Vector2 current = new Vector2(_inputManager.MouseX, _inputManager.MouseY);
                int x = (int)Math.Min(_dragStartScreen.X, current.X);
                int y = (int)Math.Min(_dragStartScreen.Y, current.Y);
                int w = (int)Math.Abs(current.X - _dragStartScreen.X);
                int h = (int)Math.Abs(current.Y - _dragStartScreen.Y);
                _dragRectScreen = new Rectangle(x, y, w, h);
                _selectionBox.Rect = _dragRectScreen;
                _selectionBox.Visible = w > 4 && h > 4;
            }

            // End drag on mouse release: select area or click
            if (_isDraggingSelection && !_inputManager.IsMouseLeftDown)
            {
                // Decide if it's area selection or click (small rect)
                bool area = _selectionBox.Visible;
                _isDraggingSelection = false;
                _selectionBox.Visible = false;
                _selectionBox.Rect = Rectangle.Empty;

                List<Entity> selectedEntities = new List<Entity>();
                if (area)
                {
                    // Convert rect corners to world rect
                    Vector2 tl = _renderingSystem.ScreenToWorld(new Vector2(_dragRectScreen.X, _dragRectScreen.Y));
                    Vector2 br = _renderingSystem.ScreenToWorld(new Vector2(_dragRectScreen.Right, _dragRectScreen.Bottom));
                    int rx = (int)Math.Min(tl.X, br.X);
                    int ry = (int)Math.Min(tl.Y, br.Y);
                    int rw = (int)Math.Abs(br.X - tl.X);
                    int rh = (int)Math.Abs(br.Y - tl.Y);
                    var worldRect = new Rectangle(rx, ry, rw, rh);
                    var entitiesInRect = _spatialSystem.QueryArea(worldRect);
                    foreach (var e in entitiesInRect)
                    {
                        var sc = e.GetComponent<ECS.Components.SelectionComponent>();
                        if (sc != null && sc.IsSelectable)
                            selectedEntities.Add(e);
                    }
                }
                else
                {
                    var mouseScreen = new Vector2(_inputManager.MouseX, _inputManager.MouseY);
                    var world = _renderingSystem.ScreenToWorld(mouseScreen);
                    var nearest = _spatialSystem.FindNearest(world, 40f);
                    if (nearest != null)
                    {
                        var sc = nearest.GetComponent<ECS.Components.SelectionComponent>();
                        if (sc != null && sc.IsSelectable)
                            selectedEntities.Add(nearest);
                    }
                }

                // Apply selection
                foreach (var e in _entityManager.GetAllEntities())
                {
                    var sc = e.GetComponent<ECS.Components.SelectionComponent>();
                    if (sc != null) sc.IsSelected = false;
                }
                _selectedIds ??= new List<int>(32); // lazy init con capacity
                _selectedIds.Clear();
                foreach (var e in selectedEntities)
                {
                    var sc = e.GetComponent<ECS.Components.SelectionComponent>();
                    if (sc != null) sc.IsSelected = true;
                    _selectedIds.Add(e.Id);
                }
                _selectionPanel.SelectedEntities = selectedEntities;
            }
            
            // Actualizar profiler al final del update (DESACTIVADO)
            // _profiler.EndUpdate();
            
            base.Update(gameTime);
        }

        /// <summary>
        /// Render the game
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            // _profiler.BeginRender(); // DESACTIVADO
            
            GraphicsDevice.Clear(Color.Black);
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            
            // Render current game state
            switch (_stateManager.CurrentState)
            {
                case GameState.Playing:
                    RenderGameplay();
                    break;
                case GameState.MainMenu:
                    RenderMainMenu();
                    break;
            }
            
            // Optional controls hint overlay (toggle with H)
            if (_showControlsHint)
                RenderControlsHints();

            // Debug rendering
            if (DebugMode)
                RenderDebugInfo();
            
            _spriteBatch.End();

            // Render UI overlay last
            _uiManager?.Render(_spriteBatch, _renderingSystem);
            
            // _profiler.EndRender(); // DESACTIVADO
            // _profiler.EndFrame();  // DESACTIVADO
            
            base.Draw(gameTime);
        }

        private void UpdateGameplay(float deltaTime)
        {
            // FÍSICA CON PASO FIJO para consistencia
            while (_optimizedLoop.ShouldUpdatePhysics())
            {
                float fixedDelta = (float)_optimizedLoop.FixedDelta * _timeScale;
                
                // Actualizar sistemas críticos con paso fijo
                _physicsSystem?.Update(fixedDelta);  // PHYSICS FIRST: velocity → position
                _spatialSystem?.Update(fixedDelta);
                _collisionSystem?.Update(fixedDelta);
                _projectileSystem?.Update(fixedDelta);
            }
            
            // LÓGICA DE JUEGO con delta variable suavizado
            _entityManager.Update(deltaTime);
            
            // Update rendering system camera
            _renderingSystem.UpdateCamera(_inputManager);

            // Update campaign
            _campaignManager?.Update(deltaTime);

            // Update HUD with player ship
            if (_campaignManager?.PlayerShip != null)
            {
                _gameHUD?.SetPlayerShip(_campaignManager.PlayerShip);
            }
        }

        private void UpdatePaused(float deltaTime)
        {
            if (_inputManager.IsKeyPressed(Keys.Space))
                _stateManager.ChangeState(GameState.Playing);
        }

        private void UpdateMainMenu(float deltaTime)
        {
            if (_inputManager.IsKeyPressed(Keys.Enter))
                _stateManager.ChangeState(GameState.Playing);
        }

        private void RenderGameplay()
        {
            _renderingSystem.RenderWorld(_spriteBatch, _entityManager.GetAllEntities());
            _renderingSystem.RenderHUD(_spriteBatch);
        }

        private void RenderMainMenu()
        {
            // TODO: Implement main menu rendering
        }

        private void RenderDebugInfo()
        {
            var debugInfo = new[]
            {
                $"═══ PERFORMANCE ═══",
                $"FPS: {_optimizedLoop.FPS:F1} (Avg: {_optimizedLoop.AverageFrameTime * 1000:F2}ms)",
                $"Frame: {_optimizedLoop.DeltaTime * 1000:F2}ms | Fixed: {_optimizedLoop.FixedDelta * 1000:F2}ms",
                $"Running Slowly: {(_optimizedLoop.IsRunningSlowly ? "YES ⚠️" : "NO ✓")}",
                // $"Profiler: {_profiler.GetHUDString()}",  // ELIMINADO
                $"",
                $"═══ SYSTEM ═══",
                $"Entities: {_entityManager.GetAllEntities().Count}",
                $"Game State: {_stateManager.CurrentState}",
                $"Time Scale: {_timeScale}x",
                $"Window: {_windowManager?.GetWindowInfo()}",
                $"",
                $"═══ STRESS TEST ═══",
                $"{_stressTestGenerator?.GetStressTestInfo()}",
                $"",
                $"═══ INPUT ═══",
                $"Buffer: {(_inputBuffer.IsMouseLeftDown ? "LMB" : "---")} Pos: ({_inputBuffer.MousePosition.X:F0}, {_inputBuffer.MousePosition.Y:F0})",
                $"",
                $"═══ SPATIAL ═══",
                _spatialSystem != null ? $"Spatial: {_spatialSystem.GetStats()}" : "Spatial: (not initialized)",
                $"",
                $"═══ HOTKEYS ═══",
                $"NumPad: 0=Clear | 1-5=Tests | 6-9=Quality | N=Campaign",
                $"F3=Debug | F4=Spatial | F12=DebugToggle"
            };
            
            _renderingSystem.RenderDebugText(_spriteBatch, debugInfo);

            // Visualize QuadTree
            if (_spatialSystem != null)
            {
                var quadBounds = _spatialSystem.GetDebugBounds();
                _renderingSystem.RenderDebugQuadTree(_spriteBatch, quadBounds);
            }

            // Render overlay texts (screen-space)
            RenderOverlays();

            // TODO: Economy panel needs font access
            // if (_economyPanel != null) _economyPanel.Draw(_spriteBatch, font);
        }

        private void RenderControlsHints()
        {
            // Compact instructions in top-left
            var lines = new[]
            {
                "═══ CONTROLS ═══",
                "Demo: F1 Faction | F2 Economy | F8 Arm | F9 Duel | F10 Fleet | F11 Mining | F12 Clans",
                "UI: F3 Debug | F4 QuadTree | F5 Culling | F6 Save | F7 Load",
                "Fleet: Q/W/E Formations (Line/Wedge/Box)",
                "Time: 1-4 Scale (1x/2x/4x/8x) | N New Campaign",
                "",
                "═══ STRESS TEST ═══",
                "NumPad 0: Clear All | 1: 100 Enemies | 2: 300 Enemies | 3: 500 Enemies",
                "NumPad 4: Missile Barrage | 5: Particle Storm",
                "",
                "═══ QUALITY ═══",
                "NumPad 6: Low | 7: Medium | 8: High | 9: Ultra",
                "",
                $"Current Time: {_timeScale}x | Press H to toggle this help"
            };

            // Draw stacked lines
            Vector2 pos = new Vector2(10, 10);
            foreach (var line in lines)
            {
                _renderingSystem.RenderText(_spriteBatch, line, pos, Color.LightGray);
                pos.Y += 16;
            }
        }

        private string GetDefaultSavePath()
        {
            try
            {
                var root = AppDomain.CurrentDomain.BaseDirectory;
                var savesDir = System.IO.Path.Combine(root, "saves");
                return System.IO.Path.Combine(savesDir, "autosave.json");
            }
            catch
            {
                return "saves\\autosave.json";
            }
        }

        private void TrySaveGame()
        {
            var path = GetDefaultSavePath();
            try
            {
#pragma warning disable IL2026 // Save system uses JSON serialization; acceptable for trimming
                _saveLoadSystem?.Save(path);
#pragma warning restore IL2026
                if (_renderingSystem != null && _overlays != null)
                {
                    _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Saved", Color = Color.LightGreen, TTL = 1.2f });
                }
            }
            catch (Exception ex)
            {
                if (_renderingSystem != null && _overlays != null)
                {
                    _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Save Failed", Color = Color.Red, TTL = 1.5f });
                }
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void TryLoadGame()
        {
            var path = GetDefaultSavePath();
            try
            {
#pragma warning disable IL2026 // Load system uses JSON deserialization; acceptable for trimming
                _saveLoadSystem?.Load(path);
#pragma warning restore IL2026
                if (_renderingSystem != null && _overlays != null)
                {
                    _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Loaded", Color = Color.LightBlue, TTL = 1.2f });
                }
            }
            catch (Exception ex)
            {
                if (_renderingSystem != null && _overlays != null)
                {
                    _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Load Failed", Color = Color.Red, TTL = 1.5f });
                }
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ArmEntitiesWithWeapons()
        {
            foreach (var e in _entityManager.GetAllEntities())
            {
                var health = e.GetComponent<ECS.Components.HealthComponent>();
                var transform = e.GetComponent<ECS.Components.TransformComponent>();
                if (health != null && transform != null && !e.HasComponent<ECS.Components.WeaponComponent>())
                {
                    e.AddComponent(new ECS.Components.WeaponComponent());
                    if (!e.HasComponent<SpaceTradeEngine.Systems.TargetingComponent>())
                        e.AddComponent(new SpaceTradeEngine.Systems.TargetingComponent { MaxRange = 600f, ProjectileSpeed = 450f, RequireLineOfSight = true });
                }
            }
            _overlays.Add(new OverlayText { WorldPos = _renderingSystem.CameraPosition, Text = "Armed", Color = Color.Yellow, TTL = 1.2f });
        }

        private void SpawnDemoDuel()
        {
            Vector2 center = _renderingSystem.CameraPosition;
            
            // Rookie vs Elite comparison for maximum contrast
            var rookie = _entityManager.CreateEntity("Rookie_Fighter");
            rookie.AddComponent(new ECS.Components.TransformComponent { Position = center + new Vector2(-250, 0) });
            rookie.AddComponent(new ECS.Components.VelocityComponent());
            rookie.AddComponent(new ECS.Components.CollisionComponent { Radius = 20f });
            rookie.AddComponent(new ECS.Components.HealthComponent { MaxHealth = 100f });
            rookie.AddComponent(new ECS.Components.FactionComponent("human", "Human") );
            rookie.AddComponent(new ECS.Components.TagComponent("ship"));
            rookie.AddComponent(new SpaceTradeEngine.Systems.TargetingComponent { MaxRange = 800f, ProjectileSpeed = 450f });
            rookie.AddComponent(new ECS.Components.WeaponComponent { Damage = 10f, Cooldown = 0.5f, Range = 800f });
            rookie.AddComponent(new RankComponent { CurrentRank = Rank.Rookie, EntityType = EntityType.Military });
            rookie.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Attack, Aggressiveness = 0.4f });
            _rankSystem.AwardExperience(rookie, 0, "Initial");

            var elite = _entityManager.CreateEntity("Elite_Fighter");
            elite.AddComponent(new ECS.Components.TransformComponent { Position = center + new Vector2(250, 0) });
            elite.AddComponent(new ECS.Components.VelocityComponent());
            elite.AddComponent(new ECS.Components.CollisionComponent { Radius = 20f });
            elite.AddComponent(new ECS.Components.HealthComponent { MaxHealth = 100f });
            elite.AddComponent(new ECS.Components.FactionComponent("alien", "Alien") );
            elite.AddComponent(new ECS.Components.TagComponent("ship"));
            elite.AddComponent(new SpaceTradeEngine.Systems.TargetingComponent { MaxRange = 800f, ProjectileSpeed = 450f });
            elite.AddComponent(new ECS.Components.WeaponComponent { Damage = 10f, Cooldown = 0.5f, Range = 800f });
            elite.AddComponent(new RankComponent { CurrentRank = Rank.Elite, EntityType = EntityType.Military });
            elite.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Attack, Aggressiveness = 0.95f, UseAdvancedTactics = true });
            _rankSystem.AwardExperience(elite, 0, "Initial");

            // Show detailed stats comparison
            var rookieRank = rookie.GetComponent<RankComponent>();
            var eliteRank = elite.GetComponent<RankComponent>();
            
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -100), 
                Text = $"ROOKIE vs ELITE DUEL", Color = Color.Yellow, TTL = 5f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -80), 
                Text = $"Damage: {rookieRank.DamageMultiplier:P0} vs {eliteRank.DamageMultiplier:P0}", Color = Color.Orange, TTL = 5f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -60), 
                Text = $"Accuracy: {rookieRank.AccuracyBonus:P1} vs {eliteRank.AccuracyBonus:P1}", Color = Color.Cyan, TTL = 5f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -40), 
                Text = $"Dodge: {rookieRank.EvasionBonus:P1} vs {eliteRank.EvasionBonus:P1}", Color = Color.LightGreen, TTL = 5f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -20), 
                Text = $"Range: {rookieRank.RangeBonus:P1} vs {eliteRank.RangeBonus:P1}", Color = Color.Magenta, TTL = 5f });
        }

        private void SpawnMilitaryRankDemo()
        {
            Vector2 center = _renderingSystem.CameraPosition;
            
            // Create a rank progression showcase: Rookie, Regular, Veteran, Elite (both Military and Clan)
            var ranks = new[] { Rank.Rookie, Rank.Regular, Rank.Veteran, Rank.Elite };
            
            // Military squadron (top row)
            for (int i = 0; i < ranks.Length; i++)
            {
                var rank = ranks[i];
                var fighter = _entityManager.CreateEntity($"Military_{rank}");
                fighter.AddComponent(new ECS.Components.TransformComponent { Position = center + new Vector2(i * 200 - 300, -150) });
                fighter.AddComponent(new ECS.Components.VelocityComponent());
                fighter.AddComponent(new ECS.Components.CollisionComponent { Radius = 18f });
                fighter.AddComponent(new ECS.Components.HealthComponent { MaxHealth = 120f });
                fighter.AddComponent(new ECS.Components.FactionComponent("military", "Military"));
                fighter.AddComponent(new ECS.Components.TagComponent("ship"));
                fighter.AddComponent(new SpaceTradeEngine.Systems.TargetingComponent { MaxRange = 900f, ProjectileSpeed = 500f });
                fighter.AddComponent(new ECS.Components.WeaponComponent { Damage = 12f, Cooldown = 0.6f, Range = 900f });
                fighter.AddComponent(new RankComponent { CurrentRank = rank, EntityType = EntityType.Military });
                fighter.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Patrol, CruiseSpeed = 180f });
                _rankSystem.AwardExperience(fighter, 0, "Initial");
                
                var rankComp = fighter.GetComponent<RankComponent>();
                _overlays.Add(new OverlayText { 
                    WorldPos = center + new Vector2(i * 200 - 300, -190), 
                    Text = $"{rank}\nAcc:{rankComp.AccuracyBonus:P0} Rng:{rankComp.RangeBonus:P0}", 
                    Color = Color.Cyan, TTL = 8f 
                });
            }
            
            // Clan members (bottom row) - slightly better combat stats
            for (int i = 0; i < ranks.Length; i++)
            {
                var rank = ranks[i];
                var raider = _entityManager.CreateEntity($"Clan_{rank}");
                raider.AddComponent(new ECS.Components.TransformComponent { Position = center + new Vector2(i * 200 - 300, 150) });
                raider.AddComponent(new ECS.Components.VelocityComponent());
                raider.AddComponent(new ECS.Components.CollisionComponent { Radius = 18f });
                raider.AddComponent(new ECS.Components.HealthComponent { MaxHealth = 110f });
                raider.AddComponent(new ECS.Components.FactionComponent("red_corsairs", "Red Corsairs"));
                raider.AddComponent(new ECS.Components.TagComponent("ship"));
                raider.AddComponent(new SpaceTradeEngine.Systems.TargetingComponent { MaxRange = 900f, ProjectileSpeed = 500f });
                raider.AddComponent(new ECS.Components.WeaponComponent { Damage = 12f, Cooldown = 0.6f, Range = 900f });
                raider.AddComponent(new RankComponent { CurrentRank = rank, EntityType = EntityType.ClanMember });
                raider.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Attack, Aggressiveness = 0.75f });
                _rankSystem.AwardExperience(raider, 0, "Initial");
                
                var rankComp = raider.GetComponent<RankComponent>();
                _overlays.Add(new OverlayText { 
                    WorldPos = center + new Vector2(i * 200 - 300, 190), 
                    Text = $"{rank} Clan\nAcc:{rankComp.AccuracyBonus:P0} Def:{rankComp.DefenseBonus:P0}", 
                    Color = Color.Red, TTL = 8f 
                });
            }
            
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -250), 
                Text = "MILITARY vs CLAN RANK COMPARISON", Color = Color.Yellow, TTL = 8f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, -230), 
                Text = "Top: Military (balanced) | Bottom: Clan (aggressive)", Color = Color.White, TTL = 8f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, 250), 
                Text = "Elite: 25% acc/dodge/range | Clan Elite: 28% acc/31% def", Color = Color.LightGreen, TTL = 8f });
        }

        private void SpawnDynamicEventDemo()
        {
            Vector2 center = _renderingSystem.CameraPosition;
            
            _overlays.Add(new OverlayText { WorldPos = center, 
                Text = "DYNAMIC EVENTS SHOWCASE", Color = Color.Yellow, TTL = 6f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, 20), 
                Text = "Wait 45s between events or explore space!", Color = Color.White, TTL = 6f });
            _overlays.Add(new OverlayText { WorldPos = center + new Vector2(0, 40), 
                Text = "Events: Distress Calls, Pirate Raids, Derelicts, Merchants", Color = Color.Cyan, TTL = 6f });
            
            // Spawn a sample distress call immediately
            var distressPos = center + new Vector2(800, 400);
            var distressShip = _entityManager.CreateEntity("Demo_Distress_Ship");
            distressShip.AddComponent(new TransformComponent { Position = distressPos });
            distressShip.AddComponent(new VelocityComponent());
            distressShip.AddComponent(new CollisionComponent { Radius = 22f });
            distressShip.AddComponent(new HealthComponent { MaxHealth = 80f, CurrentHealth = 25f });
            distressShip.AddComponent(new FactionComponent("civilian", "Civilian"));
            distressShip.AddComponent(new TagComponent("distress"));
            
            // Pirates attacking
            for (int i = 0; i < 2; i++)
            {
                var pirate = _entityManager.CreateEntity($"Demo_Pirate_{i}");
                var offset = new Vector2(i * 100 - 50, -100);
                pirate.AddComponent(new TransformComponent { Position = distressPos + offset });
                pirate.AddComponent(new VelocityComponent());
                pirate.AddComponent(new CollisionComponent { Radius = 18f });
                pirate.AddComponent(new HealthComponent { MaxHealth = 100f });
                pirate.AddComponent(new FactionComponent("pirates", "Pirates"));
                pirate.AddComponent(new WeaponComponent { Damage = 10f, Range = 700f });
                pirate.AddComponent(new RankComponent { CurrentRank = Rank.Regular, EntityType = EntityType.ClanMember });
            }
            
            _overlays.Add(new OverlayText { WorldPos = distressPos + new Vector2(0, -80), 
                Text = "DISTRESS CALL", Color = Color.Red, TTL = 8f });
            _overlays.Add(new OverlayText { WorldPos = distressPos + new Vector2(0, -60), 
                Text = "Save civilian from pirates!", Color = Color.Orange, TTL = 8f });
        }

        private void RenderOverlays()
        {
            if (_overlays == null || _overlays.Count == 0)
                return;

            // Decay TTL and remove expired
            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                _overlays[i].TTL -= _clock.DeltaTime;
                if (_overlays[i].TTL <= 0)
                    _overlays.RemoveAt(i);
            }

            // Draw texts
            if (_overlays.Count == 0)
                return;

            foreach (var o in _overlays)
            {
                var screen = _renderingSystem.WorldToScreen(o.WorldPos) + new Vector2(0, -20);
                _renderingSystem.RenderText(_spriteBatch, o.Text, screen, o.Color);
            }
        }

        private void InitializeSpatialSystems()
        {
            // Large world bounds for space gameplay
            var worldBounds = new Rectangle(-10000, -10000, 20000, 20000);

            // Partitioning first
            _spatialSystem = new SpatialPartitioningSystem(worldBounds);
            _entityManager.RegisterSystem(_spatialSystem);

            // Event system for publishing collision events
            _eventSystem = new EventSystem();

            // Dependent systems
            _collisionSystem = new CollisionSystem(_spatialSystem);
            _collisionSystem.SetEventSystem(_eventSystem);
            _entityManager.RegisterSystem(_collisionSystem);

            _targetingSystem = new TargetingSystem(_spatialSystem);
            _entityManager.RegisterSystem(_targetingSystem);

            _cullingSystem = new CullingSystem(_spatialSystem);
            _renderingSystem.SetCullingSystem(_cullingSystem);

            // Camera follow system for smooth player tracking
            _cameraFollowSystem = new CameraFollowSystem(_renderingSystem, _inputManager);
            _entityManager.RegisterSystem(_cameraFollowSystem);

            // Physics system for velocity integration
            _physicsSystem = new PhysicsSystem();
            _entityManager.RegisterSystem(_physicsSystem);

            // Subscribe to collision callbacks
            _collisionSystem.OnCollision += OnEntitiesCollided;
            _collisionSystem.OnTriggerEnter += OnTriggerEntered;

            // Combat systems
            _weaponSystem = new WeaponSystem(_entityManager, _spatialSystem);
            _entityManager.RegisterSystem(_weaponSystem);
            _projectileSystem = new ProjectileSystem(_entityManager);
            _entityManager.RegisterSystem(_projectileSystem);

            // Damage system uses events for UI overlays
            var damageSystem = new DamageSystem(_eventSystem);
            _entityManager.RegisterSystem(damageSystem);

            // Gameplay systems
            _fleetSystem = new FleetSystem(_entityManager);
            _entityManager.RegisterSystem(_fleetSystem);

            _miningSystem = new MiningSystem(_entityManager, _spatialSystem);
            _entityManager.RegisterSystem(_miningSystem);

            _diplomacySystem = new DiplomacySystem(_eventSystem);
            _entityManager.RegisterSystem(_diplomacySystem);

            _missionSystem = new MissionSystem(_entityManager, _eventSystem);
            _entityManager.RegisterSystem(_missionSystem);

            _aiBehaviorSystem = new AIBehaviorSystem(_entityManager, _spatialSystem, _eventSystem);
            _entityManager.RegisterSystem(_aiBehaviorSystem);

            _productionSystem = new ProductionSystem(_entityManager, _eventSystem);
            _entityManager.RegisterSystem(_productionSystem);

            // LAZY LOAD: Sistemas pesados solo cuando se necesiten (ahorra ~40MB en skeleton)
            // _economySystem = new EconomySystem(new Economy.MarketManager());
            // _entityManager.RegisterSystem(_economySystem);

            // _audioSystem = new AudioSystem(_eventSystem);
            // _entityManager.RegisterSystem(_audioSystem);

            // Clan system for hierarchies and sub-clans
            // _clanSystem = new ClanSystem(_eventSystem);
            // _entityManager.RegisterSystem(_clanSystem);

            // Faction AI system for autonomous faction behavior
            // _factionAISystem = new FactionAISystem(_entityManager, _eventSystem, _diplomacySystem, _clanSystem);
            // _entityManager.RegisterSystem(_factionAISystem);m);

            // LAZY LOAD: Station/Trader/Shipyard systems (ahorra ~20MB)
            // Station system for docking and trading
            // var marketManager = (_economySystem as EconomySystem)?.GetMarketManager() ?? new Economy.MarketManager();
            // _stationSystem = new StationSystem(marketManager, _eventSystem, _diplomacySystem);
            // _entityManager.RegisterSystem(_stationSystem);

            // Trader AI system for civilian economy
            // _traderAISystem = new TraderAISystem(marketManager, _stationSystem, _entityManager, _diplomacySystem);
            // _entityManager.RegisterSystem(_traderAISystem);

            // Shipyard system for ship production
            // _shipyardSystem = new ShipyardSystem(_entityManager, _eventSystem);
            // _entityManager.RegisterSystem(_shipyardSystem);

            // Rank and experience system
            _rankSystem = new RankSystem(_eventSystem);
            _entityManager.RegisterSystem(_rankSystem);

            // Combat system for rank-based combat bonuses
            _combatSystem = new CombatSystem();
            _entityManager.RegisterSystem(_combatSystem);

            // Damage system with rank integration
            _damageSystem = new DamageSystem(_eventSystem);
            _damageSystem.SetRankSystem(_rankSystem);
            _entityManager.RegisterSystem(_damageSystem);
            
            // Link trader AI system with rank system for XP rewards (disabled in skeleton)
            // _traderAISystem.SetRankSystem(_rankSystem);
            
            // Dynamic events system for random encounters
            var dynamicEventSystem = new DynamicEventSystem(_eventSystem, _entityManager);
            _entityManager.RegisterSystem(dynamicEventSystem);

            // Economy panel UI (disabled in skeleton mode)
            // _economyPanel = new UI.EconomyPanel(_stationSystem, _traderAISystem, _shipyardSystem);

            // Player input system for ship control
            _playerInputSystem = new PlayerInputSystem(_inputManager, _renderingSystem);
            _entityManager.RegisterSystem(_playerInputSystem);

            // Campaign manager for story and progression (don't add HUD yet, UIManager not initialized)
            _campaignManager = new CampaignManager(_entityManager);
        }

        private void OnEntitiesCollided(Entity a, Entity b)
        {
            // Example collision handling: apply velocity-based damage
            var healthA = a.GetComponent<HealthComponent>();
            var healthB = b.GetComponent<HealthComponent>();
            var velA = a.GetComponent<VelocityComponent>();
            var velB = b.GetComponent<VelocityComponent>();

            if (healthA != null && velB != null)
            {
                var dmg = velB.LinearVelocity.Length() * 0.1f;
                var pre = healthA.CurrentHealth;
                healthA.TakeDamage(dmg);
                // (UI overlay added via damage system; no local counter here)
            }
            if (healthB != null && velA != null)
            {
                var dmg = velA.LinearVelocity.Length() * 0.1f;
                var pre = healthB.CurrentHealth;
                healthB.TakeDamage(dmg);
            }
        }

        private void OnTriggerEntered(Entity a, Entity b)
        {
            // Apply projectile damage regardless of argument order
            Entity projEntity = null;
            Entity target = null;

            var projA = a.GetComponent<ECS.Components.ProjectileComponent>();
            var projB = b.GetComponent<ECS.Components.ProjectileComponent>();
            if (projA != null) { projEntity = a; target = b; }
            else if (projB != null) { projEntity = b; target = a; }

            if (projEntity != null)
            {
                var proj = projEntity.GetComponent<ECS.Components.ProjectileComponent>();
                var health = target.GetComponent<ECS.Components.HealthComponent>();
                if (health != null && health.IsAlive)
                {
                    // Get owner entity for rank damage bonus
                    var owner = _entityManager.GetEntity(proj.OwnerId);
                    var ownerRank = owner?.GetComponent<RankComponent>();
                    
                    float damage = proj.Damage;
                    if (ownerRank != null)
                    {
                        damage *= ownerRank.DamageMultiplier; // Apply rank damage bonus
                    }

                    // Use damage system for proper rank integration
                    _damageSystem.ApplyDamage(target, damage, proj.OwnerId);
                    _overlays.Add(new OverlayText { WorldPos = target.GetComponent<ECS.Components.TransformComponent>()?.Position ?? Vector2.Zero, Text = $"-{damage:F0}", Color = Color.Red, TTL = 1.0f });
                }

                // Destroy projectile
                _entityManager.DestroyEntity(projEntity.Id);
            }
        }

        private void FocusSelectedOnNearestEnemy(List<Entity> selected)
        {
            foreach (var src in selected)
            {
                var targeting = src.GetComponent<SpaceTradeEngine.Systems.TargetingComponent>();
                var t = src.GetComponent<ECS.Components.TransformComponent>();
                var f = src.GetComponent<ECS.Components.FactionComponent>();
                if (targeting == null || t == null) continue;
                var candidates = _spatialSystem.QueryRadius(t.Position, targeting.MaxRange);
                Entity best = null; float bestDist = float.MaxValue;
                foreach (var c in candidates)
                {
                    if (c.Id == src.Id) continue;
                    var cf = c.GetComponent<ECS.Components.FactionComponent>();
                    if (cf != null && f != null && cf.FactionId == f.FactionId) continue; // skip friendlies
                    var ct = c.GetComponent<ECS.Components.TransformComponent>();
                    var ch = c.GetComponent<ECS.Components.HealthComponent>();
                    if (ct == null || (ch != null && !ch.IsAlive)) continue;
                    float d = Vector2.Distance(t.Position, ct.Position);
                    if (d < bestDist)
                    {
                        bestDist = d; best = c;
                    }
                }
                if (best != null)
                    _targetingSystem.SetTarget(src, best);
            }
        }

        /// <summary>
        /// Start following the first selected entity with smooth camera interpolation.
        /// </summary>
        private void FollowSelectedEntity()
        {
            if (_selectedIds.Count == 0)
            {
                _cameraFollowSystem?.SetTarget(null);
                return;
            }

            var firstSelected = _entityManager.GetEntity(_selectedIds[0]);
            if (firstSelected != null)
            {
                _cameraFollowSystem?.SetTarget(firstSelected);
            }
        }

        /// <summary>
        /// Start a new player campaign with initial ship, missions, and enemy spawns.
        /// </summary>
        private void StartNewCampaign()
        {
            Console.WriteLine("\n═══════════════════════════════════════════");
            Console.WriteLine("◆ STARTING NEW CAMPAIGN ◆");
            Console.WriteLine("═══════════════════════════════════════════\n");

            _campaignManager?.StartNewCampaign();
            _stateManager.ChangeState(GameState.Playing);

            // Auto-follow player ship with camera
            if (_campaignManager?.PlayerShip != null)
            {
                _cameraFollowSystem?.SetTarget(_campaignManager.PlayerShip);
                Console.WriteLine("\u2713 Camera following player ship");
            }

            Console.WriteLine("\n[CAMPAIGN STARTED]");
            Console.WriteLine("Controls:");
            Console.WriteLine("  W/S - Throttle forward/reverse");
            Console.WriteLine("  A/D - Rotate left/right (or use mouse)");
            Console.WriteLine("  SPACE/LMB - Fire weapon");
            Console.WriteLine("  F - Follow selected ship with camera");
            Console.WriteLine("  Click/Drag - Select entities\n");
        }

        private void SpawnFleetDemo()
        {
            Vector2 center = _renderingSystem.CameraPosition;
            
            // Create fleet leader
            var leader = _entityManager.CreateEntity("Fleet_Leader");
            leader.AddComponent(new TransformComponent { Position = center });
            leader.AddComponent(new VelocityComponent());
            leader.AddComponent(new CollisionComponent { Radius = 25f });
            leader.AddComponent(new HealthComponent { MaxHealth = 150f, CurrentHealth = 150f });
            leader.AddComponent(new FactionComponent("human", "Human Fleet"));
            leader.AddComponent(new TagComponent("ship"));
            leader.AddComponent(new SelectionComponent { IsSelectable = true });
            
            var fleet = new FleetComponent { LeaderId = leader.Id, Formation = FormationType.Wedge, FormationSpacing = 100f };
            leader.AddComponent(fleet);

            // Create squadron members
            for (int i = 0; i < 5; i++)
            {
                var wingman = _entityManager.CreateEntity($"Wingman_{i}");
                wingman.AddComponent(new TransformComponent { Position = center + new Vector2(i * 50, i * 30) });
                wingman.AddComponent(new VelocityComponent());
                wingman.AddComponent(new CollisionComponent { Radius = 20f });
                wingman.AddComponent(new HealthComponent { MaxHealth = 100f, CurrentHealth = 100f });
                wingman.AddComponent(new FactionComponent("human", "Human Fleet"));
                wingman.AddComponent(new TagComponent("ship"));
                wingman.AddComponent(new SelectionComponent { IsSelectable = true });
                wingman.AddComponent(new SquadronMemberComponent { FleetId = leader.Id });
                
                fleet.MemberIds.Add(wingman.Id);
            }

            _overlays.Add(new OverlayText { WorldPos = center, Text = "Fleet Spawned (Q/W/E for formations)", Color = Color.Cyan, TTL = 2f });
        }

        private void SpawnMiningDemo()
        {
            Vector2 center = _renderingSystem.CameraPosition;
            
            // Create resource nodes
            for (int i = 0; i < 3; i++)
            {
                var node = _entityManager.CreateEntity($"ResourceNode_{i}");
                var angle = (i / 3f) * MathHelper.TwoPi;
                var pos = center + new Vector2((float)Math.Cos(angle) * 300f, (float)Math.Sin(angle) * 300f);
                
                node.AddComponent(new TransformComponent { Position = pos });
                node.AddComponent(new CollisionComponent { Radius = 40f, IsTrigger = true });
                node.AddComponent(new ResourceNodeComponent 
                { 
                    ResourceType = "ore", 
                    Quantity = 1000, 
                    MaxQuantity = 1000,
                    RespawnTime = 30f 
                });
                node.AddComponent(new TagComponent("resource"));
            }

            // Create mining ship
            var miner = _entityManager.CreateEntity("Miner");
            miner.AddComponent(new TransformComponent { Position = center + new Vector2(-200, 0) });
            miner.AddComponent(new VelocityComponent());
            miner.AddComponent(new CollisionComponent { Radius = 20f });
            miner.AddComponent(new HealthComponent { MaxHealth = 80f, CurrentHealth = 80f });
            miner.AddComponent(new FactionComponent("human", "Mining Corp"));
            miner.AddComponent(new TagComponent("ship"));
            miner.AddComponent(new SelectionComponent { IsSelectable = true });
            miner.AddComponent(new MiningComponent { ExtractionRate = 20f, Range = 150f });
            miner.AddComponent(new CargoComponent { MaxVolume = 500f });
            miner.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Mine, CruiseSpeed = 150f });

            _overlays.Add(new OverlayText { WorldPos = center, Text = "Mining Op Spawned", Color = Color.Gold, TTL = 2f });
        }

        private void SetSelectedFleetFormation(FormationType formation)
        {
            bool anySet = false;
            foreach (var id in _selectedIds)
            {
                var entity = _entityManager.GetEntity(id);
                var fleet = entity?.GetComponent<FleetComponent>();
                if (fleet != null)
                {
                    fleet.Formation = formation;
                    anySet = true;
                }
            }
            
            if (anySet)
            {
                var formationName = formation.ToString();
                _overlays.Add(new OverlayText 
                { 
                    WorldPos = _renderingSystem.CameraPosition, 
                    Text = $"Formation: {formationName}", 
                    Color = Color.LightBlue, 
                    TTL = 1.5f 
                });
            }
        }

        // ELIMINADO: SpawnClanDemo usa _clanSystem que fue removido
        /*
        private void SpawnClanDemo()
        {
            // Create sample clans
            var militaryClan = _clanSystem.CreateClan("demo_military_clan", "Military Command", "human_federation", null);
            var tradeClan = _clanSystem.CreateClan("demo_trade_clan", "Merchant Guild", "human_federation", null);
            var pirateClan = _clanSystem.CreateClan("demo_pirate_clan", "Corsair Fleet", "pirates", null);

            // Set initial relationships
            _clanSystem.SetClanRelationship(militaryClan.ClanId, tradeClan.ClanId, 60f); // Friendly
            _clanSystem.SetClanRelationship(militaryClan.ClanId, pirateClan.ClanId, -95f); // Hostile
            _clanSystem.SetClanRelationship(tradeClan.ClanId, pirateClan.ClanId, -70f); // Hostile

            // Create members for clans
            var center = new Vector2(0, 0);
            for (int i = 0; i < 3; i++)
            {
                var militaryShip = _entityManager.CreateEntity($"MilitaryShip_{i}");
                var pos = center + new Vector2(i * 150, 0);
                militaryShip.AddComponent(new TransformComponent { Position = pos });
                militaryShip.AddComponent(new VelocityComponent());
                militaryShip.AddComponent(new HealthComponent { MaxHealth = 200, CurrentHealth = 200 });
                militaryShip.AddComponent(new FactionComponent("human_federation"));
                militaryShip.AddComponent(new ClanComponent { ClanId = militaryClan.ClanId, ClanRole = i == 0 ? "leader" : "member" });
                militaryShip.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Patrol, CruiseSpeed = 180f });
                _clanSystem.AddMemberToClan(militaryClan.ClanId, militaryShip.Id.ToString(), 1.5f);
            }

            for (int i = 0; i < 2; i++)
            {
                var tradeShip = _entityManager.CreateEntity($"TradeShip_{i}");
                var pos = center + new Vector2(i * 150, 200);
                tradeShip.AddComponent(new TransformComponent { Position = pos });
                tradeShip.AddComponent(new VelocityComponent());
                tradeShip.AddComponent(new HealthComponent { MaxHealth = 150, CurrentHealth = 150 });
                tradeShip.AddComponent(new FactionComponent("human_federation"));
                tradeShip.AddComponent(new ClanComponent { ClanId = tradeClan.ClanId, ClanRole = i == 0 ? "leader" : "member" });
                tradeShip.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Trade, CruiseSpeed = 160f });
                _clanSystem.AddMemberToClan(tradeClan.ClanId, tradeShip.Id.ToString(), 1.0f);
            }

            for (int i = 0; i < 2; i++)
            {
                var pirateShip = _entityManager.CreateEntity($"PirateShip_{i}");
                var pos = center + new Vector2(i * 150, -200);
                pirateShip.AddComponent(new TransformComponent { Position = pos });
                pirateShip.AddComponent(new VelocityComponent());
                pirateShip.AddComponent(new HealthComponent { MaxHealth = 180, CurrentHealth = 180 });
                pirateShip.AddComponent(new FactionComponent("pirates"));
                pirateShip.AddComponent(new ClanComponent { ClanId = pirateClan.ClanId, ClanRole = i == 0 ? "leader" : "member" });
                pirateShip.AddComponent(new AIBehaviorComponent { DefaultBehavior = AIBehaviorType.Attack, Aggressiveness = 0.9f, CruiseSpeed = 200f });
                _clanSystem.AddMemberToClan(pirateClan.ClanId, pirateShip.Id.ToString(), 1.5f);
            }

            _overlays.Add(new OverlayText { WorldPos = center, Text = "Clan Demo Spawned (3 clans, 7 ships)", Color = Color.Cyan, TTL = 3f });
        }
        */

        // ELIMINADO: SpawnFactionAIDemo usa _factionAISystem que fue removido
        /*
        private void SpawnFactionAIDemo()
        {
            // Register AI-controlled factions with different profiles
            
            // 1. Aggressive pirate faction
            _factionAISystem.RegisterFaction("ai_pirates", new FactionAIProfile
            {
                FactionName = "Red Corsairs",
                StartingTreasury = 15000f,
                IncomeRate = 250f,
                Aggressiveness = 0.9f,
                Expansionist = 0.7f,
                Diplomatic = false,
                AllowInternalRivalry = true,
                ExpansionCost = 1500f,
                ClanCreationCost = 1000f,
                SatelliteFactionCost = 4000f,
                MaxClans = 4,
                MaxSatelliteFactions = 2,
                DecisionCooldown = 15f,
                SatelliteAutonomy = 0.9f
            });

            // 2. Diplomatic trading faction
            _factionAISystem.RegisterFaction("ai_traders", new FactionAIProfile
            {
                FactionName = "Merchant Collective",
                StartingTreasury = 30000f,
                IncomeRate = 500f,
                Aggressiveness = 0.2f,
                Expansionist = 0.6f,
                Diplomatic = true,
                AllowInternalRivalry = false,
                ExpansionCost = 2000f,
                ClanCreationCost = 1500f,
                SatelliteFactionCost = 6000f,
                MaxClans = 3,
                MaxSatelliteFactions = 3,
                DecisionCooldown = 20f,
                SatelliteAutonomy = 0.6f
            });

            // 3. Expansionist colonizers
            _factionAISystem.RegisterFaction("ai_colonists", new FactionAIProfile
            {
                FactionName = "Frontier Explorers",
                StartingTreasury = 20000f,
                IncomeRate = 300f,
                Aggressiveness = 0.4f,
                Expansionist = 0.9f,
                Diplomatic = true,
                AllowInternalRivalry = false,
                ExpansionCost = 1800f,
                ClanCreationCost = 1200f,
                SatelliteFactionCost = 5000f,
                MaxClans = 2,
                MaxSatelliteFactions = 4,
                DecisionCooldown = 18f,
                SatelliteAutonomy = 0.75f
            });

            // Set initial diplomacy
            _diplomacySystem.SetRelationship("ai_pirates", "ai_traders", -80f);
            _diplomacySystem.SetRelationship("ai_traders", "ai_colonists", 60f);
            _diplomacySystem.SetRelationship("ai_pirates", "ai_colonists", -40f);

            // Set relationships with player
            _diplomacySystem.SetRelationship("player", "ai_pirates", -60f);
            _diplomacySystem.SetRelationship("player", "ai_traders", 40f);
            _diplomacySystem.SetRelationship("player", "ai_colonists", 30f);

            var center = _renderingSystem.CameraPosition;
            _overlays.Add(new OverlayText 
            { 
                WorldPos = center, 
                Text = "Faction AI Demo: 3 autonomous factions spawned", 
                Color = Color.Gold, 
                TTL = 4f 
            });
        }
        */

        // ELIMINADO: SpawnEconomyDemo usa _stationSystem, _shipyardSystem, _economyPanel que fueron removidos
        /*
        /// <summary>
        /// F2: Spawn economy demo - stations, traders, shipyards
        /// Creates living economy like Unending Galaxy
        /// </summary>
        private void SpawnEconomyDemo()
        {
            var center = _renderingSystem.CameraPosition;
            var random = new Random();

            // 1. Create 5 stations in a ring around center
            var stationPositions = new[]
            {
                center + new Vector2(-600, -400),  // Top-left trade post
                center + new Vector2(600, -400),   // Top-right factory
                center + new Vector2(-700, 0),     // Left shipyard
                center + new Vector2(700, 0),      // Right trade post
                center + new Vector2(0, 500)       // Bottom factory
            };

            var stationTypes = new[] { "TradePost", "Factory", "Shipyard", "TradePost", "Factory" };
            var stationNames = new[] { "Alpha Station", "Beta Factory", "Gamma Shipyard", "Delta Trade Hub", "Epsilon Manufactory" };
            var stationFactions = new[] { "human_federation", "drath_empire", "neutral", "human_federation", "sirak_collective" };
            var stationIds = new List<int>();

            for (int i = 0; i < stationPositions.Length; i++)
            {
                var stationEntity = _entityManager.CreateEntity($"Station_{stationNames[i]}");
                stationEntity.AddComponent(new TransformComponent { Position = stationPositions[i] });
                stationEntity.AddComponent(new FactionComponent(stationFactions[i]));
                // Note: SpriteComponent needs texture setup

                var stationId = _stationSystem.CreateStation(stationEntity, stationTypes[i], stationNames[i], stationFactions[i]);
                stationIds.Add(stationId);

                // Configure market with goods
                var goods = new List<(string, float, int, int)>
                {
                    ("ore_iron", 50f, random.Next(50, 200), 500),
                    ("ore_copper", 75f, random.Next(40, 150), 400),
                    ("food_wheat", 30f, random.Next(100, 300), 600),
                    ("tech_electronics", 200f, random.Next(20, 80), 200),
                    ("fuel_cells", 100f, random.Next(50, 150), 400)
                };

                _stationSystem.ConfigureMarket(stationId, goods);

                // Setup shipyard for ship production
                if (stationTypes[i] == "Shipyard")
                {
                    var shipyardId = _shipyardSystem.RegisterShipyard(stationEntity, "neutral", ShipyardClass.Civilian);
                    _shipyardSystem.SetAutoProduction(shipyardId, true, "bp_trader");
                }
            }

            // 2. Spawn 12 civilian trader ships with diverse factions and ranks
            var traderFactions = new[] { "human_federation", "drath_empire", "sirak_collective", "neutral" };
            var traderRanks = new[] { Rank.Rookie, Rank.Regular, Rank.Experienced, Rank.Veteran };
            
            for (int i = 0; i < 12; i++)
            {
                var angle = (i / 12f) * MathHelper.TwoPi;
                var radius = 800f;
                var pos = center + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius
                );

                var trader = _entityManager.CreateEntity($"Trader_{i + 1}");
                var traderFaction = traderFactions[i % traderFactions.Length];
                var traderRank = traderRanks[i % traderRanks.Length];
                
                trader.AddComponent(new TransformComponent { Position = pos });
                trader.AddComponent(new VelocityComponent());
                trader.AddComponent(new FactionComponent(traderFaction));
                // Note: SpriteComponent needs texture setup
                trader.AddComponent(new CargoComponent 
                { 
                    MaxVolume = 100, 
                    Credits = 5000f 
                });
                trader.AddComponent(new TraderAIComponent 
                { 
                    MinProfitMargin = 0.12f,
                    TraderType = "Merchant" 
                });
                trader.AddComponent(new RankComponent 
                { 
                    CurrentRank = traderRank, 
                    EntityType = EntityType.Civilian 
                });
                _rankSystem.AwardExperience(trader, 0, "Initial");
            }

            // 3. Setup faction relations for economy demo
            // Human Federation vs Drath Empire rivalry
            _diplomacySystem.SetRelationship("human_federation", "drath_empire", -75f); // Hostile
            _diplomacySystem.SetRelationship("drath_empire", "human_federation", -75f);
            
            // Sirak Collective friendly with humans, neutral with Drath
            _diplomacySystem.SetRelationship("human_federation", "sirak_collective", 50f); // Friendly
            _diplomacySystem.SetRelationship("sirak_collective", "human_federation", 50f);
            _diplomacySystem.SetRelationship("drath_empire", "sirak_collective", 0f);  // Neutral
            _diplomacySystem.SetRelationship("sirak_collective", "drath_empire", 0f);

            // Show economy panel
            _economyPanel?.Toggle();

            _overlays.Add(new OverlayText
            {
                WorldPos = center,
                Text = "Economy Demo: 5 stations (3 factions) + 12 traders\nHumans vs Drath rivalry - traders avoid hostile stations!",
                Color = Color.Gold,
                TTL = 5f
            });
        }
        */

        private void PrintMemoryArenaStats()
        {
            var arena = GlobalMemoryArena.Instance;
            var stats = arena.GetStats();
            SimpleLogger.LogMemoryStats(stats);
        }
    }
}

