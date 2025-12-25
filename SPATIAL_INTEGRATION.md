# Quick Integration Guide - Spatial Partitioning

## Add to Your Existing GameEngine.cs

### Step 1: Add System Fields

```csharp
// In your GameEngine class, add these fields:
private SpatialPartitioningSystem _spatialSystem;
private CollisionSystem _collisionSystem;
private TargetingSystem _targetingSystem;
private CullingSystem _cullingSystem;
```

### Step 2: Initialize Systems (in Initialize method)

```csharp
protected override void Initialize()
{
    base.Initialize();
    
    // ... existing initialization code ...
    
    // Initialize Spatial Systems
    InitializeSpatialSystems();
}

private void InitializeSpatialSystems()
{
    // Define your game world bounds
    // For Unending Galaxy-style game, this should be large!
    Rectangle worldBounds = new Rectangle(-10000, -10000, 20000, 20000);
    
    // Create spatial partitioning system (MUST BE FIRST)
    _spatialSystem = new SpatialPartitioningSystem(worldBounds);
    _entityManager.RegisterSystem(_spatialSystem);
    
    // Create systems that depend on spatial system
    _collisionSystem = new CollisionSystem(_spatialSystem);
    _entityManager.RegisterSystem(_collisionSystem);
    
    _targetingSystem = new TargetingSystem(_spatialSystem);
    _entityManager.RegisterSystem(_targetingSystem);
    
    _cullingSystem = new CullingSystem(_spatialSystem);
    
    // Connect culling to rendering system
    _renderingSystem.SetCullingSystem(_cullingSystem);
    
    // Subscribe to collision events
    _collisionSystem.OnCollision += OnEntitiesCollided;
    _collisionSystem.OnTriggerEnter += OnTriggerEntered;
    
    Console.WriteLine("Spatial Partitioning System initialized!");
}

private void OnEntitiesCollided(Entity a, Entity b)
{
    // Handle physical collisions
    Debug.WriteLine($"Collision: {a.Name} <-> {b.Name}");
    
    // Example: Deal collision damage based on velocity
    var healthA = a.GetComponent<HealthComponent>();
    var healthB = b.GetComponent<HealthComponent>();
    var velocityA = a.GetComponent<VelocityComponent>();
    var velocityB = b.GetComponent<VelocityComponent>();
    
    if (healthA != null && velocityB != null)
    {
        float damage = velocityB.LinearVelocity.Length() * 0.1f;
        healthA.TakeDamage(damage);
    }
    
    if (healthB != null && velocityA != null)
    {
        float damage = velocityA.LinearVelocity.Length() * 0.1f;
        healthB.TakeDamage(damage);
    }
}

private void OnTriggerEntered(Entity trigger, Entity other)
{
    // Handle trigger zones (stations, pickups, etc.)
    Debug.WriteLine($"Trigger: {other.Name} entered {trigger.Name}");
}
```

### Step 3: Update Debug Rendering

```csharp
protected override void Draw(GameTime gameTime)
{
    GraphicsDevice.Clear(Color.Black);
    
    // ... existing drawing code ...
    
    // Render debug info (Press F3 to toggle)
    if (_debugMode)
    {
        var debugLines = new List<string>
        {
            $"FPS: {_fps}",
            $"Entities: {_entityManager.GetAllEntities().Count}",
            
            // Add spatial stats
            $"Spatial: {_spatialSystem.GetStats()}",
        };
        
        _renderingSystem.RenderDebugText(_spriteBatch, debugLines.ToArray());
        
        // Visualize QuadTree (Press F4 to toggle)
        if (_showQuadTree)
        {
            var quadTreeBounds = _spatialSystem.GetDebugBounds();
            _renderingSystem.RenderDebugQuadTree(_spriteBatch, quadTreeBounds);
        }
    }
    
    base.Draw(gameTime);
}
```

### Step 4: Add Debug Hotkeys

```csharp
protected override void Update(GameTime gameTime)
{
    _input.Update();
    
    // ... existing input handling ...
    
    // F3 - Toggle debug info
    if (_input.IsKeyPressed(Keys.F3))
    {
        _debugMode = !_debugMode;
    }
    
    // F4 - Toggle QuadTree visualization
    if (_input.IsKeyPressed(Keys.F4))
    {
        _spatialSystem.ToggleDebugVisualization();
    }
    
    // F5 - Toggle culling (useful for debugging)
    if (_input.IsKeyPressed(Keys.F5))
    {
        _renderingSystem.ToggleCulling();
    }
    
    // ... rest of update code ...
    
    base.Update(gameTime);
}
```

### Step 5: Update Entity Creation

When creating ships, stations, etc., add the new components:

```csharp
public Entity CreateShip(string name, Vector2 position, string factionId)
{
    var ship = _entityManager.CreateEntity(name);
    
    // Position (REQUIRED for spatial system)
    ship.AddComponent(new TransformComponent 
    { 
        Position = position 
    });
    
    // Collision detection
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
    
    // Movement
    ship.AddComponent(new VelocityComponent());
    
    // NEW: Faction identification
    ship.AddComponent(new FactionComponent(factionId));
    
    // NEW: Entity tags
    ship.AddComponent(new TagComponent("ship", "combat"));
    
    // NEW: Weapon targeting
    ship.AddComponent(new TargetingComponent 
    {
        MaxRange = 500f,
        AutoTarget = true,
        PreferWeakTargets = true
    });
    
    // NEW: Selection support
    ship.AddComponent(new SelectionComponent 
    {
        IsSelectable = true,
        SelectionRadius = 30f
    });
    
    // Sprite (if you have textures loaded)
    // ship.AddComponent(new SpriteComponent { Texture = _shipTexture });
    
    return ship;
}
```

### Step 6: Example Usage - Find Nearest Enemy

```csharp
public void UpdateAI(Entity npcShip, float deltaTime)
{
    var transform = npcShip.GetComponent<TransformComponent>();
    var faction = npcShip.GetComponent<FactionComponent>();
    var targeting = npcShip.GetComponent<TargetingComponent>();
    
    if (transform == null || faction == null)
        return;
    
    // Targeting system automatically finds best target!
    // Just check if we have one
    if (targeting.CurrentTarget != null && targeting.IsInRange)
    {
        // Fire weapons at target
        FireWeapon(npcShip, targeting.LeadPosition);
    }
    else
    {
        // Move towards patrol point or search for enemies
        Patrol(npcShip, deltaTime);
    }
}
```

### Step 7: Example Usage - AOE Explosion

```csharp
public void CreateExplosion(Vector2 position, float radius, float damage, string sourceFaction)
{
    // Find all entities in blast radius (super fast with spatial queries!)
    var entitiesInRange = _spatialSystem.QueryRadius(position, radius);
    
    foreach (var entity in entitiesInRange)
    {
        // Don't damage friendly units
        var faction = entity.GetComponent<FactionComponent>();
        if (faction?.FactionId == sourceFaction)
            continue;
        
        var health = entity.GetComponent<HealthComponent>();
        if (health != null && health.IsAlive)
        {
            // Calculate distance-based damage falloff
            var transform = entity.GetComponent<TransformComponent>();
            float distance = Vector2.Distance(position, transform.Position);
            float damageMultiplier = 1.0f - (distance / radius);
            float actualDamage = damage * Math.Max(0, damageMultiplier);
            
            health.TakeDamage(actualDamage);
        }
    }
    
    // Spawn explosion visual effect
    CreateExplosionEffect(position, radius);
}
```

---

## Quick Test

Add this to test the system is working:

```csharp
private void TestSpatialSystem()
{
    Console.WriteLine("=== Testing Spatial System ===");
    
    // Create some test ships
    var ship1 = CreateShip("Ship A", new Vector2(0, 0), "human");
    var ship2 = CreateShip("Ship B", new Vector2(100, 100), "human");
    var ship3 = CreateShip("Ship C", new Vector2(500, 500), "alien");
    
    // Query nearby entities
    var nearby = _spatialSystem.QueryRadius(Vector2.Zero, 200f);
    Console.WriteLine($"Found {nearby.Count} entities within 200 units of origin");
    
    // Find nearest entity
    var nearest = _spatialSystem.FindNearest(Vector2.Zero);
    Console.WriteLine($"Nearest entity: {nearest?.Name}");
    
    // Get stats
    Console.WriteLine(_spatialSystem.GetStats().ToString());
    
    Console.WriteLine("=== Test Complete ===");
}
```

Call this from your `Initialize()` method to verify everything works!

---

## Performance Before/After

Test with this code:

```csharp
private void BenchmarkSpatialSystem()
{
    // Create 1000 ships
    var random = new Random();
    for (int i = 0; i < 1000; i++)
    {
        Vector2 pos = new Vector2(
            random.Next(-5000, 5000),
            random.Next(-5000, 5000)
        );
        CreateShip($"Ship_{i}", pos, i % 2 == 0 ? "human" : "alien");
    }
    
    // Benchmark: Find all entities within 500 units of origin
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    for (int i = 0; i < 100; i++)
    {
        var results = _spatialSystem.QueryRadius(Vector2.Zero, 500f);
    }
    
    stopwatch.Stop();
    Console.WriteLine($"100 radius queries took {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"Average: {stopwatch.ElapsedMilliseconds / 100.0}ms per query");
}
```

**Expected Results:**
- Without spatial partitioning: ~50ms per query (unusable!)
- With spatial partitioning: ~0.5ms per query (smooth!)

---

## Troubleshooting

### "No entities found in spatial queries"
- Check that entities have `TransformComponent`
- Verify world bounds contain your entities
- Ensure `_spatialSystem` is registered before other systems

### "Collisions not detected"
- Entities need both `TransformComponent` AND `CollisionComponent`
- Check collision radius is appropriate
- Verify `_collisionSystem` is registered and updated

### "Targeting not working"
- Ensure `TargetingComponent` added to entities
- Check `MaxRange` is set appropriately
- Verify faction filtering if used

---

## Next Steps

1. ✅ Integrate these changes into your `GameEngine.cs`
2. ✅ Update your entity creation methods
3. ✅ Run the test/benchmark
4. ✅ Start using spatial queries in your gameplay code!

See `SPATIAL_PARTITIONING_GUIDE.md` for comprehensive documentation.
