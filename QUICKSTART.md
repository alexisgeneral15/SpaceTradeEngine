# Space Trade Engine - Quick Start Guide

## Overview
This is the C# + MonoGame implementation of the Space Trade Game Engine, inspired by Unending Galaxy.

## Project Structure

```
SpaceTradeEngine/
├── src/

## Developer Templates (copy/paste)

### New System (pattern)
```csharp
public class MySystem : System
{
    protected override bool ShouldProcess(Entity entity)
        => entity.HasComponent<MyComponent>();

    public override void Update(float deltaTime)
    {
        foreach (var e in _entities)
        {
            var c = e.GetComponent<MyComponent>();
            // system logic
        }
    }
}

// Register in GameEngine.Initialize():
_mySystem = new MySystem();
_entityManager.RegisterSystem(_mySystem);
```

### New Component (minimal)
```csharp
public class MyStatComponent : Component
{
    public float Value { get; set; } = 1f;
}
```

### Publish/Subscribe Event
```csharp
// Publish
_eventSystem.Publish(new MyEvent { EntityId = entity.Id });

// Subscribe
_eventSystem.Subscribe<MyEvent>(evt => {
    // handle event
});
```

### Data Template (ship JSON)
```json
{
  "id": "prototype_fighter",
  "name": "Prototype Fighter",
  "type": "fighter",
  "hull": { "hp": 220, "armor": 8 },
  "engines": { "max_speed": 280, "acceleration": 110 },
  "cost": 220000
}
```

### Quick Integration Steps
1) **Create component/system** with templates above.
2) **Register system** in `GameEngine.Initialize()` after creation.
3) **Hook data** (JSON) if content-driven; ensure files are under `assets/data`.
4) **Emit events** for UI/FX; subscribe where needed.
5) **Test** with hotkeys or demo spawns (F1-F4, F8-F12) to validate behavior.

### VS Code Snippets (ready to use)
- File: `tools/snippets/spaceTrade.code-snippets`
- Prefixes:
    - `sts-system` – ECS system template
    - `sts-component` – Component template
    - `sts-event` – Event publish/subscribe pattern
    - `sts-ship` – Ship JSON template
    - `sts-faction` – Faction JSON template
    - `sts-station` – Station JSON template
    - `sts-entity` – Spawn entity with common components
    - `sts-mission-accept` – Accept and query missions
Load the folder in VS Code; snippets auto-enable.
│   ├── Core/                 # Engine core (GameEngine, GameStateManager, InputManager)
│   ├── ECS/                  # Entity-Component System framework
│   ├── Systems/              # Game systems (Rendering, Physics, etc.)
│   ├── Data/                 # Data loading and serialization
│   └── UI/                   # UI framework (TODO)
├── assets/
│   ├── data/                 # Game data (JSON config files)
│   │   ├── ships/            # Ship templates
│   │   ├── factions/         # Faction definitions
│   │   └── items/            # Item templates
│   ├── sprites/              # Image assets
│   ├── sounds/               # Audio files
│   └── fonts/                # Font files
├── tools/                    # Editor tools (TODO)
├── tests/                    # Unit tests
└── SpaceTradeEngine.csproj   # Visual Studio project file
```

## Prerequisites

1. **.NET 6.0 SDK** - Download from https://dotnet.microsoft.com/
2. **Visual Studio 2022 Community** - Free version from https://visualstudio.microsoft.com/
   - Install with C# workload
3. **MonoGame** - Will be installed via NuGet automatically

## Installation & Setup

### Step 1: Clone or Open Project
```bash
cd SpaceTradeEngine
```

### Step 2: Restore Dependencies
```bash
dotnet restore
```

### Step 3: Build Project
```bash
dotnet build
```

### Step 4: Run Game
```bash
dotnet run
```

## If Using Visual Studio

1. Open `SpaceTradeEngine.csproj` in Visual Studio
2. Wait for NuGet packages to restore
3. Press F5 to run

## Game Controls (Current)

- **WASD** - Pan camera
- **Mouse Wheel** - Zoom in/out
- **Left Click** - Select (TODO)
- **Esc** - Exit
- **F12** - Toggle debug mode

## What's Implemented

✅ **Core Engine & ECS**
- Main loop, input, config, entity manager, system management, core components (Transform, Sprite, Velocity, Collision, Health)

✅ **Rendering**
- 2D sprites, camera pan/zoom, debug viz, layer sorting

✅ **Combat & Ranks**
- Damage/shields, projectiles, rank/XP (civilian, military, clan), tactical bonuses (accuracy/dodge/range/defense)

✅ **Economy & AI**
- Stations, trading, autonomous traders, shipyards, diplomacy-aware docking/trade

✅ **Missions & Events**
- Mission system, bounty/escort/patrol/delivery/destroy/salvage mission types
- Dynamic events: distress calls, pirate raids, merchant convoys, derelicts, faction skirmishes, anomalies, traveling merchants

✅ **Diplomacy & Clans**
- Faction standings, trade bonuses/blocks, clan hierarchies, faction AI profiles

## What's TODO

- [ ] Physics engine polish
- [ ] UI framework & screens
- [ ] Audio system
- [ ] Save/load system
- [ ] Editor tools (map, ship, faction editors)
- [ ] Procedural map generation

## Creating Your First Entity

```csharp
// In your game code:
var ship = _entityManager.CreateEntity("Player Ship");

// Add components
ship.AddComponent(new TransformComponent 
{ 
    Position = new Vector2(640, 360) 
});

ship.AddComponent(new SpriteComponent 
{ 
    Texture = _content.Load<Texture2D>("sprites/ship")
});

ship.AddComponent(new VelocityComponent());

ship.AddComponent(new HealthComponent 
{ 
    MaxHealth = 100 
});
```

## Adding New Ship Types

1. Edit `assets/data/ships/ship_templates.json`
2. Add new ship definition:
```json
{
  "id": "my_ship",
  "name": "My Ship",
  "type": "fighter",
  "hull": { "hp": 150, "armor": 5 },
  "engines": { "max_speed": 250, "acceleration": 100 },
  "cost": 150000
}
```
3. Reload game - ship data is loaded automatically

## Adding New Factions

1. Create `assets/data/factions/your_faction.json`
2. Define faction data:
```json
{
  "id": "your_faction",
  "name": "Your Faction Name",
  "alignment": "neutral",
  "description": "Your faction description",
  "territories": ["sector1", "sector2"],
  "technologies": ["tech1", "tech2"]
}
```

## Architecture Notes

### Entity-Component System (ECS)
- **Entities**: Containers for components (ships, stations, NPCs)
- **Components**: Data containers (Transform, Sprite, Health)
- **Systems**: Logic that operates on entities with specific components

Benefits:
- Flexible composition over inheritance
- Easy to add new entity types
- Reusable components
- Data-driven behavior

### Data-Driven Design
All game content is in JSON files, not hardcoded:
- Easy to create mods
- Game balance adjustable without recompiling
- Non-programmers can create content

### Game Loop
```
While running:
  1. Handle input
  2. Update game state
  3. Render world
  4. Render UI
```

## Extending the Engine

### Adding a New System

1. Create class inheriting from `System`
2. Override `ShouldProcess()` to filter entities
3. Implement `Update()` with your logic
4. Register in GameEngine

Example:
```csharp
public class PhysicsSystem : System
{
    protected override bool ShouldProcess(Entity entity)
    {
        return entity.HasComponent<VelocityComponent>();
    }

    public override void Update(float deltaTime)
    {
        foreach (var entity in _entities)
        {
            var velocity = entity.GetComponent<VelocityComponent>();
            // Your physics logic here
        }
    }
}
```

### Adding a New Component

1. Create class inheriting from `Component`
2. Override desired methods:
   - `Initialize()` - Called when added to entity
   - `Update(deltaTime)` - Called every frame
   - `OnEnabled()` - When component enabled
   - `OnDisabled()` - When component disabled
   - `OnDestroy()` - When component removed

Example:
```csharp
public class MyComponent : Component
{
    public override void Update(float deltaTime)
    {
        // Your logic here
    }
}
```

## Performance Tips

1. Use object pooling for frequently created/destroyed entities
2. Cull off-screen entities before rendering
3. Use spatial partitioning (quadtree) for collision detection
4. Cache component references instead of getting them every frame
5. Profile with built-in FPS counter (press F12)

## Known Limitations

- 2D only (no 3D support)
- No audio system yet
- No UI framework yet
- No physics engine yet
- No save/load system yet

## Next Steps

1. **Implement Physics System** - Handle acceleration, velocity, collisions
2. **Create Combat System** - Weapons, damage, shields
3. **Build Economy System** - Supply/demand, trading
4. **Add NPC AI** - Behavior trees, decision making
5. **Create Editor Tools** - Map editor, ship designer
6. **Polish & Optimize** - Performance profiling, balancing

## Troubleshooting

### "MonoGame.Framework not found"
```bash
dotnet restore
```

### Project won't build
- Make sure you have .NET 6.0 SDK installed: `dotnet --version`
- Clean and rebuild: `dotnet clean && dotnet build`

### Missing assets
- Ensure `assets` folder is in the root project directory
- Check that `Copy to Output Directory` is set to `PreserveNewest` for asset files

## Resources

- **MonoGame Documentation**: https://docs.monogame.net/
- **ECS Pattern**: https://en.wikipedia.org/wiki/Entity_component_system
- **C# Reference**: https://docs.microsoft.com/en-us/dotnet/csharp/

## License

This project is a learning implementation. Modify and extend as needed for your game.

---

**Version:** 0.1.0  
**Last Updated:** 2025-12-23

Happy coding! Start by running the game and seeing the debug output, then begin implementing the systems you need.
