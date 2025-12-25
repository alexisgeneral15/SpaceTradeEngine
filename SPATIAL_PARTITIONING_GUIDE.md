# Spatial Partitioning System - Complete Guide

## Overview

The **Spatial Partitioning System** uses a **QuadTree** data structure to dramatically improve performance for spatial queries in your space trading game. Without spatial partitioning, finding nearby entities requires checking every entity against every other entity (O(n²) complexity). With QuadTree, this drops to O(log n) for most queries.

## Performance Benefits

### Before Spatial Partitioning
- **100 entities**: ~10,000 comparisons per frame
- **1,000 entities**: ~1,000,000 comparisons per frame  
- **10,000 entities**: Game crashes 💥

### After Spatial Partitioning
- **100 entities**: ~700 comparisons per frame
- **1,000 entities**: ~7,000 comparisons per frame
- **10,000 entities**: ~70,000 comparisons per frame ✅

**Result**: 100x+ performance improvement at scale!

---

## Architecture

```
┌─────────────────────────────────────────────┐
│     SpatialPartitioningSystem               │
│  (Main interface for spatial queries)       │
└─────────────────────────────────────────────┘
                    │
                    ├─► QuadTree (core data structure)
                    │
                    ├─► CollisionSystem (uses spatial queries)
                    │
                    ├─► TargetingSystem (finds targets efficiently)
                    │
                    └─► CullingSystem (renders only visible entities)
```

---

## Core Components

### 1. QuadTree (`Spatial/QuadTree.cs`)

The heart of the system. Recursively divides 2D space into quadrants.

**Key Methods:**
```csharp
// Insert entity into tree
void Insert(Entity entity)

// Find entities in rectangular area
List<Entity> Query(Rectangle bounds)

// Find entities within circular radius
List<Entity> QueryRadius(Vector2 center, float radius)

// Find nearest entity to a point
Entity FindNearest(Vector2 point, float maxRadius)

// Raycast for line-of-sight
List<Entity> Raycast(Vector2 origin, Vector2 direction, float maxDistance)
```

**Configuration:**
- `MAX_OBJECTS = 8` - Objects per node before splitting
- `MAX_LEVELS = 8` - Maximum tree depth
- World bounds defined at initialization

### 2. SpatialPartitioningSystem (`Systems/SpatialPartitioningSystem.cs`)

ECS System that manages the QuadTree and provides high-level queries.

**Features:**
- Automatic tree rebuild each frame (for dynamic entities)
- Camera frustum queries
- Collision pair detection
- Debug visualization
- Statistics tracking

**Usage:**
```csharp
// Initialize with world bounds
Rectangle worldBounds = new Rectangle(-5000, -5000, 10000, 10000);
var spatialSystem = new SpatialPartitioningSystem(worldBounds);
entityManager.RegisterSystem(spatialSystem);

// Query nearby entities
List<Entity> nearby = spatialSystem.QueryRadius(position, 500f);

// Find nearest enemy
Entity target = spatialSystem.FindNearestMatching(
    position, 
    entity => entity.HasComponent<FactionComponent>(), 
    1000f
);
```

### 3. CollisionSystem (`Systems/CollisionSystem.cs`)

Efficient collision detection using spatial partitioning.

**Features:**
- Broad phase (spatial queries) + Narrow phase (actual collision test)
- Physical collisions vs Trigger collisions
- Collision events
- Automatic collision response

**Events:**
```csharp
collisionSystem.OnCollision += (a, b) => 
{
    Console.WriteLine($"{a.Name} hit {b.Name}!");
};

collisionSystem.OnTriggerEnter += (trigger, other) =>
{
    Console.WriteLine($"{other.Name} entered trigger zone");
};
```

### 4. TargetingSystem (`Systems/TargetingSystem.cs`)

Weapon targeting with predictive aiming.

**Features:**
- Auto-targeting within range
- Target prioritization (distance, health, facing)
- Lead calculation for moving targets
- Line-of-sight checking
- Faction filtering

**Component:**
```csharp
ship.AddComponent(new TargetingComponent
{
    MaxRange = 500f,
    AutoTarget = true,
    PreferWeakTargets = true,
    RequireLineOfSight = true,
    ProjectileSpeed = 300f,
    TargetFilter = entity => 
        entity.HasComponent<HealthComponent>() &&
        entity.GetComponent<FactionComponent>()?.FactionId != "friendly"
});
```

**Targeting Data (auto-updated):**
- `CurrentTarget` - Selected target entity
- `TargetDistance` - Distance to target
- `TargetDirection` - Unit vector to target
- `LeadPosition` - Where to aim for moving targets
- `IsInRange` - Target within weapon range
- `HasLineOfSight` - No obstacles blocking

### 5. CullingSystem (`Systems/CullingSystem.cs`)

Only render entities visible to the camera.

**Performance Impact:**
- Without culling: Render all 10,000 entities
- With culling: Render only ~200 visible entities (50x speedup)

**Usage:**
```csharp
var cullingSystem = new CullingSystem(spatialSystem);
var visibleEntities = cullingSystem.GetVisibleEntities(
    cameraPosition, 
    viewportSize, 
    zoom
);

// Render only visible entities
renderer.RenderWorld(spriteBatch, visibleEntities);
```

---

## New Components

### FactionComponent
```csharp
entity.AddComponent(new FactionComponent("human_federation", "Humans"));
```
Identifies entity faction for friend/foe detection.

### TagComponent
```csharp
entity.AddComponent(new TagComponent("ship", "combat", "player"));

// Query
if (tag.HasTag("ship")) { }
if (tag.HasAnyTag("station", "structure")) { }
```
Flexible categorization system.

### SelectionComponent
```csharp
entity.AddComponent(new SelectionComponent
{
    IsSelectable = true,
    SelectionRadius = 50f,
    SelectionColor = Color.Yellow
});

// Select entity
selection.IsSelected = true; // Shows selection indicator
```
Visual selection for RTS-style interfaces.

### TargetingComponent
Already covered above - weapon targeting.

---

## Integration Guide

### Step 1: Initialize Systems

```csharp
// In your game initialization
public void Initialize()
{
    _entityManager = new EntityManager();
    
    // Define your game world size
    Rectangle worldBounds = new Rectangle(-10000, -10000, 20000, 20000);
    
    // Create spatial system FIRST
    _spatialSystem = new SpatialPartitioningSystem(worldBounds);
    _entityManager.RegisterSystem(_spatialSystem);
    
    // Create dependent systems
    _collisionSystem = new CollisionSystem(_spatialSystem);
    _entityManager.RegisterSystem(_collisionSystem);
    
    _targetingSystem = new TargetingSystem(_spatialSystem);
    _entityManager.RegisterSystem(_targetingSystem);
    
    _cullingSystem = new CullingSystem(_spatialSystem);
    
    // Connect culling to renderer
    _renderingSystem.SetCullingSystem(_cullingSystem);
}
```

### Step 2: Create Entities with Spatial Components

```csharp
// Create a ship
var ship = _entityManager.CreateEntity("PlayerShip");

// Position (REQUIRED for spatial partitioning)
ship.AddComponent(new TransformComponent 
{ 
    Position = new Vector2(100, 200) 
});

// Collision (for collision detection)
ship.AddComponent(new CollisionComponent 
{ 
    Radius = 25f,
    IsTrigger = false 
});

// Targeting (for weapons)
ship.AddComponent(new TargetingComponent 
{ 
    MaxRange = 600f,
    AutoTarget = true 
});

// Faction (for friend/foe)
ship.AddComponent(new FactionComponent("player"));

// Tags (for categorization)
ship.AddComponent(new TagComponent("ship", "player", "combat"));
```

### Step 3: Use Spatial Queries

```csharp
public void FindAndAttackNearbyEnemies(Entity player)
{
    var transform = player.GetComponent<TransformComponent>();
    var faction = player.GetComponent<FactionComponent>();
    
    // Find all entities within 1000 units
    var nearbyEntities = _spatialSystem.QueryRadius(transform.Position, 1000f);
    
    foreach (var entity in nearbyEntities)
    {
        // Skip self
        if (entity.Id == player.Id) continue;
        
        // Check if enemy faction
        var otherFaction = entity.GetComponent<FactionComponent>();
        if (otherFaction?.FactionId == faction.FactionId) 
            continue;
        
        // Attack!
        AttackEntity(player, entity);
    }
}
```

### Step 4: Handle Collisions

```csharp
// Subscribe to collision events
_collisionSystem.OnCollision += (a, b) =>
{
    // Physical collision - deal damage
    var healthA = a.GetComponent<HealthComponent>();
    var healthB = b.GetComponent<HealthComponent>();
    
    healthA?.TakeDamage(10f);
    healthB?.TakeDamage(10f);
};

_collisionSystem.OnTriggerEnter += (trigger, other) =>
{
    // Trigger collision - docking, pickups, etc.
    if (trigger.HasComponent<TagComponent>() && 
        trigger.GetComponent<TagComponent>().HasTag("station"))
    {
        // Ship entered station docking zone
        DockShipAtStation(other, trigger);
    }
};
```

### Step 5: Implement Weapon Targeting

```csharp
public void FireWeapons(Entity ship)
{
    var targeting = ship.GetComponent<TargetingComponent>();
    
    if (targeting.CurrentTarget == null)
        return; // No target
    
    if (!targeting.IsInRange)
        return; // Out of range
    
    if (!targeting.HasLineOfSight)
        return; // Blocked by obstacle
    
    // Fire at lead position (compensates for target movement)
    Vector2 aimPosition = targeting.LeadPosition;
    SpawnProjectile(ship, aimPosition);
}
```

---

## Common Use Cases

### 1. Area of Effect (AOE) Attacks

```csharp
public void ExplosionDamage(Vector2 center, float radius, float damage)
{
    var entities = _spatialSystem.QueryRadius(center, radius);
    
    foreach (var entity in entities)
    {
        var health = entity.GetComponent<HealthComponent>();
        if (health == null) continue;
        
        var transform = entity.GetComponent<TransformComponent>();
        float distance = Vector2.Distance(center, transform.Position);
        
        // Damage falloff with distance
        float damageMultiplier = 1.0f - (distance / radius);
        health.TakeDamage(damage * damageMultiplier);
    }
}
```

### 2. Formation Flying

```csharp
public void MaintainFormation(Entity leader, List<Entity> wingmen)
{
    var leaderTransform = leader.GetComponent<TransformComponent>();
    
    for (int i = 0; i < wingmen.Count; i++)
    {
        // Calculate formation position
        Vector2 offset = new Vector2(100, 100 * i);
        Vector2 formationPos = leaderTransform.Position + offset;
        
        // Move wingman to formation position
        var wingmanVelocity = wingmen[i].GetComponent<VelocityComponent>();
        Vector2 toFormation = formationPos - wingmen[i].GetComponent<TransformComponent>().Position;
        wingmanVelocity.LinearVelocity = toFormation * 0.5f;
    }
}
```

### 3. Radar / Minimap

```csharp
public List<Entity> GetRadarContacts(Entity ship, float radarRange)
{
    var transform = ship.GetComponent<TransformComponent>();
    return _spatialSystem.QueryRadius(transform.Position, radarRange);
}
```

### 4. Mouse Click Selection

```csharp
public void OnMouseClick(Vector2 worldPosition)
{
    // Find entity closest to click
    Entity clicked = _spatialSystem.FindNearest(worldPosition, 50f);
    
    if (clicked != null)
    {
        var selection = clicked.GetComponent<SelectionComponent>();
        if (selection?.IsSelectable == true)
        {
            // Deselect all
            foreach (var entity in _entityManager.GetAllEntities())
            {
                entity.GetComponent<SelectionComponent>()?.SetSelected(false);
            }
            
            // Select clicked entity
            selection.IsSelected = true;
        }
    }
}
```

### 5. NPC Flee Behavior

```csharp
public void FleeFromDanger(Entity npc)
{
    var transform = npc.GetComponent<TransformComponent>();
    
    // Find all enemies within 300 units
    var threats = _spatialSystem.QueryRadius(transform.Position, 300f);
    
    Vector2 fleeDirection = Vector2.Zero;
    
    foreach (var threat in threats)
    {
        if (IsEnemy(npc, threat))
        {
            // Add vector away from threat
            Vector2 away = transform.Position - threat.GetComponent<TransformComponent>().Position;
            away.Normalize();
            fleeDirection += away;
        }
    }
    
    // Move away from combined threat direction
    var velocity = npc.GetComponent<VelocityComponent>();
    velocity.LinearVelocity = fleeDirection * 200f;
}
```

---

## Performance Tuning

### QuadTree Configuration

```csharp
// In QuadTree.cs - adjust these constants:
private const int MAX_OBJECTS = 8;   // Objects per node before split
private const int MAX_LEVELS = 8;    // Maximum tree depth
```

**Guidelines:**
- **Small world, few entities**: Increase `MAX_OBJECTS` to 16
- **Large world, many entities**: Keep at 8 or reduce to 4
- **Very deep worlds**: Increase `MAX_LEVELS` to 10

### World Bounds

```csharp
// Match your actual game world size
Rectangle worldBounds = new Rectangle(
    -gameWorldWidth / 2, 
    -gameWorldHeight / 2,
    gameWorldWidth, 
    gameWorldHeight
);
```

**Too small**: Entities outside bounds won't be in tree  
**Too large**: Wasted memory and slower queries

### Culling Optimization

```csharp
// Toggle culling on/off
_renderingSystem.ToggleCulling();

// Useful for debugging - see all entities even off-screen
```

---

## Debug Tools

### Visualize QuadTree

```csharp
// In your render loop
if (debugMode)
{
    var bounds = _spatialSystem.GetDebugBounds();
    _renderingSystem.RenderDebugQuadTree(spriteBatch, bounds);
}
```

Shows quadtree subdivision lines in cyan.

### Statistics

```csharp
var stats = _spatialSystem.GetStats();
Console.WriteLine(stats.ToString());
// Output: Entities: 1234, Objects: 1234, World: {X:-5000 Y:-5000 Width:10000 Height:10000}

var cullStats = _cullingSystem.GetStats(totalEntities, visibleEntities);
Console.WriteLine(cullStats.ToString());
// Output: Visible: 234/1234 (81.0% culled)
```

---

## Migration Guide

### From Old Collision System

**Before:**
```csharp
// O(n²) brute force
foreach (var a in entities)
{
    foreach (var b in entities)
    {
        if (CheckCollision(a, b))
            HandleCollision(a, b);
    }
}
```

**After:**
```csharp
// O(n log n) with spatial partitioning
var pairs = _spatialSystem.GetPotentialCollisions();
foreach (var (a, b) in pairs)
{
    if (_spatialSystem.CheckCollision(a, b))
        HandleCollision(a, b);
}
```

### From Manual Targeting

**Before:**
```csharp
Entity FindTarget(Entity ship)
{
    Entity closest = null;
    float closestDist = float.MaxValue;
    
    foreach (var entity in allEntities) // Checks ALL entities
    {
        float dist = Vector2.Distance(ship.Position, entity.Position);
        if (dist < closestDist)
        {
            closest = entity;
            closestDist = dist;
        }
    }
    return closest;
}
```

**After:**
```csharp
Entity FindTarget(Entity ship)
{
    var targeting = ship.GetComponent<TargetingComponent>();
    return targeting.CurrentTarget; // Auto-updated by TargetingSystem
}
```

---

## Common Pitfalls

### 1. Forgetting TransformComponent
❌ **Wrong:**
```csharp
var entity = _entityManager.CreateEntity("Ship");
entity.AddComponent(new CollisionComponent()); // No transform!
```

✅ **Correct:**
```csharp
var entity = _entityManager.CreateEntity("Ship");
entity.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
entity.AddComponent(new CollisionComponent());
```

### 2. Wrong World Bounds
❌ **Wrong:**
```csharp
Rectangle worldBounds = new Rectangle(0, 0, 1000, 1000);
// But entities at (-500, -500) won't be found!
```

✅ **Correct:**
```csharp
Rectangle worldBounds = new Rectangle(-5000, -5000, 10000, 10000);
// Centers world at (0, 0) with 10000x10000 size
```

### 3. Not Registering Systems in Order
❌ **Wrong:**
```csharp
_entityManager.RegisterSystem(_collisionSystem); // Needs spatial system!
_entityManager.RegisterSystem(_spatialSystem);
```

✅ **Correct:**
```csharp
_entityManager.RegisterSystem(_spatialSystem); // First
_entityManager.RegisterSystem(_collisionSystem); // Then dependents
```

---

## Next Steps

1. **Read the example**: `Examples/SpatialPartitioningExample.cs`
2. **Run the demo**: `SpatialPartitioningDemo.RunDemo()`
3. **Integrate into your game loop**
4. **Profile performance** before/after
5. **Tune MAX_OBJECTS and MAX_LEVELS** for your game

---

## API Reference

See individual file documentation:
- `Spatial/QuadTree.cs` - Core quadtree data structure
- `Systems/SpatialPartitioningSystem.cs` - Main ECS system
- `Systems/CollisionSystem.cs` - Collision detection
- `Systems/TargetingSystem.cs` - Weapon targeting
- `Systems/CullingSystem.cs` - Rendering optimization

---

**Questions?** Check `Examples/SpatialPartitioningExample.cs` for complete working code!
