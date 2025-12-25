# Space Trade Engine

A modular, data-driven game engine for space trading/combat games, inspired by **Unending Galaxy**.

Built with **C# + MonoGame**, designed for extensibility, ease of modding, and complete creative control.

## Quick Start

```bash
# Prerequisites: .NET 6.0 SDK installed

dotnet restore
dotnet build
dotnet run
```

See [QUICKSTART.md](QUICKSTART.md) for detailed setup instructions.

## Features

### Core Engine ✅
- **Game Loop** - Stable 60 FPS main loop
- **Input System** - Keyboard, mouse, and gamepad support
- **Game State Manager** - Easy state transitions
- **ECS Framework** - Flexible entity-component system
- **Data Loading** - JSON-based configuration system

### Rendering System ✅
- **2D Sprite Rendering** - Efficient sprite batch rendering
- **Camera System** - Pan and zoom with smooth controls
- **Layer Sorting** - Depth-based sprite ordering
- **Debug Visualization** - FPS counter, entity count, debug text

### Data System ✅
- **Ship Templates** - Fully customizable ship designs
- **Faction System** - Multiple factions with relationships
- **Item Database** - Trade goods and equipment
- **Configuration** - Centralized game settings

### In Development 🚧
- Physics & Collision system
- Combat mechanics
- Economy & trading
- NPC AI
- Quest system
- UI framework
- Audio system
- Save/load system

### Planned 📋
- Map editor
- Ship designer
- Faction editor
- Quest editor
- Asset manager

## Architecture

### Entity-Component System

All game objects (ships, stations, NPCs) are **Entities** composed of reusable **Components**:

```csharp
var ship = _entityManager.CreateEntity("My Ship");
ship.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
ship.AddComponent(new SpriteComponent { Texture = ... });
ship.AddComponent(new VelocityComponent());
ship.AddComponent(new HealthComponent { MaxHealth = 100 });
```

### Data-Driven Design

All game content defined in JSON, not hardcoded:

```json
{
  "id": "hornet_fighter",
  "name": "Hornet Fighter",
  "hull": { "hp": 150, "armor": 5 },
  "engines": { "max_speed": 250, "acceleration": 100 },
  "cost": 150000
}
```

### Modular Systems

Each game system (rendering, physics, AI) is independent and testable:

```csharp
_entityManager.RegisterSystem(new PhysicsSystem());
_entityManager.RegisterSystem(new RenderingSystem());
_entityManager.RegisterSystem(new CombatSystem());
```

## Project Structure

```
src/
├── Core/           # Engine core (GameEngine, StateManager, Input)
├── ECS/            # Entity-Component framework
├── Systems/        # Game systems (Rendering, Physics, etc.)
├── Data/           # Data loading and serialization
└── UI/             # UI framework (TODO)

assets/
├── data/           # Game content (JSON)
├── sprites/        # Image assets
├── sounds/         # Audio files
└── fonts/          # Font files
```

## Development

### Technologies
- **Language:** C# (.NET 6.0)
- **Graphics:** MonoGame 3.8
- **Data Format:** JSON (Newtonsoft.Json)
- **IDE:** Visual Studio 2022

### First Steps

1. **Run the game** - Verify everything works
2. **Study the code** - Understand ECS pattern
3. **Create an entity** - Practice adding components
4. **Add ship types** - Modify JSON data files
5. **Extend a system** - Add your own features

### Creating Custom Content

#### Add a Ship
Edit `assets/data/ships/ship_templates.json`:
```json
{
  "id": "my_ship",
  "name": "My Custom Ship",
  "type": "fighter",
  "hull": { "hp": 200, "armor": 8 },
  "engines": { "max_speed": 280, "acceleration": 120 },
  "cost": 200000
}
```

#### Add a Faction
Create `assets/data/factions/my_faction.json`:
```json
{
  "id": "my_faction",
  "name": "My Faction",
  "alignment": "neutral",
  "description": "Your faction description",
  "territories": ["sector1", "sector2"],
  "technologies": ["tech1"]
}
```

### Extending the Engine

#### Create a System
```csharp
public class MySystem : System
{
    protected override bool ShouldProcess(Entity entity)
    {
        return entity.HasComponent<MyComponent>();
    }

    public override void Update(float deltaTime)
    {
        foreach (var entity in _entities)
        {
            // Your logic here
        }
    }
}
```

#### Create a Component
```csharp
public class MyComponent : Component
{
    public override void Update(float deltaTime)
    {
        // Your logic here
    }
}
```

## Comparison with Unending Galaxy

### ✅ What We Emulate
- Data-driven design (factions, ships, items in JSON)
- Modular equipment system
- Multiple faction relationships
- Flexible NPC profiles
- Branching content

### 🚀 What We Improve
- Modern graphics pipeline (MonoGame)
- Better mod support built-in
- Cross-platform (Windows, Mac, Linux)
- Clean architecture (ECS pattern)
- Built-in editor tools (planned)
- Steam Workshop integration (planned)

## Performance Targets

- **Frame Rate:** 60 FPS consistently
- **Entity Limit:** 10,000+ entities
- **Memory:** <2GB RAM
- **Load Time:** <10 seconds per area

## Known Limitations

- **2D Only** - No 3D support (by design)
- **Early Alpha** - Many systems not yet implemented
- **No Audio** - Audio system coming soon
- **No UI** - UI framework in development

## Troubleshooting

**"MonoGame.Framework not found"**
```bash
dotnet restore
```

**Project won't build**
- Verify .NET 6.0 SDK: `dotnet --version`
- Clean: `dotnet clean`
- Rebuild: `dotnet build`

**Missing assets**
- Ensure `assets/` folder exists in project root
- Check file permissions
- Verify paths in data files

## Resources

- [MonoGame Docs](https://docs.monogame.net/)
- [ECS Pattern](https://en.wikipedia.org/wiki/Entity_component_system)
- [C# Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [Game Architecture](../GAME_ENGINE_ARCHITECTURE.md)

## Roadmap

See [ROADMAP.md](ROADMAP.md) for detailed development plan.

**Current Phase:** Foundation (Phase 1) ✅
**Next Phase:** Game Systems (Phase 2)

## Contributing

This is a learning project - feel free to:
- Extend systems
- Add new features
- Create tools
- Optimize performance
- Fix bugs

## License

Open for modification and learning. Use freely for personal projects.

---

**Status:** Early Alpha (v0.1.0)  
**Last Updated:** December 23, 2025

Start by reading [QUICKSTART.md](QUICKSTART.md) and running `dotnet run`!
