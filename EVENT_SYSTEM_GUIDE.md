# Event System Guide

A lightweight, fast, and flexible event bus for SpaceTradeEngine. It keeps systems decoupled, code maintainable, and makes it easy to react to game state changes.

## Goals
- Typed pub/sub with minimal boilerplate
- Priorities and filters for fine control
- Sync/async dispatch
- Optional queued processing per frame
- Sticky events (deliver last known value to new subscribers)
- Easy ECS integration

## Core Types
- `IEvent`: Marker interface with `Timestamp`
- `EventBus`: Central pub/sub class
- `SubscriptionToken`: Opaque handle to unsubscribe
- `EventSystem`: ECS-facing wrapper for per-frame dispatch
- `GameEvents`: Common built-in game events

## Quick Start
```csharp
var events = new EventSystem();

// Subscribe
var token = events.Subscribe<EntityDamagedEvent>(e =>
{
    Console.WriteLine($"Entity {e.EntityId} took {e.Damage}");
}, priority: 5, filter: e => e.EntityId == playerId);

// Publish
events.Publish(new EntityDamagedEvent(playerId, 12f, 88f, 42, EventFactory.Now()));

// Unsubscribe
events.Unsubscribe(token);
```

## API Reference

### EventBus
- `Subscribe<T>(Action<T> handler, int priority = 0, Predicate<T>? filter = null, bool once = false, bool deliverLastSticky = false)`
- `Unsubscribe(SubscriptionToken token)`
- `Publish<T>(T evt, bool sticky = false)`
- `PublishAsync<T>(T evt, bool sticky = false, CancellationToken ct = default)`
- `Enqueue<T>(T evt, bool sticky = false)`
- `DispatchQueued(int maxEvents = int.MaxValue)`
- `Clear<T>()`, `ClearAll()`

### EventSystem
- `Update(float deltaTime, int maxEventsPerFrame = 256)`
- Convenience wrappers for `Publish`, `Enqueue`, `Subscribe`, `Unsubscribe`

## Patterns

### Prioritized Handling
Use priorities to ensure critical handlers run first:
```csharp
events.Subscribe<EntityDamagedEvent>(OnUIUpdate, priority: 0);
events.Subscribe<EntityDamagedEvent>(OnGameplayRules, priority: 10); // runs before UI
```

### Filters
Narrow handlers to relevant events:
```csharp
events.Subscribe<EntityDamagedEvent>(OnPlayerDamaged, filter: e => e.EntityId == playerId);
```

### Once Handlers
Useful for one-time transitions:
```csharp
events.Subscribe<EntityDestroyedEvent>(OnFirstKillAchieved, once: true);
```

### Sticky Events
Deliver latest state to late subscribers:
```csharp
events.Publish(new SelectionChangedEvent(null, playerId, EventFactory.Now()), sticky: true);
events.Subscribe<SelectionChangedEvent>(UpdateSelectionUI, deliverLastSticky: true);
```

### Queued Processing
Avoid frame spikes by limiting dispatch:
```csharp
events.Enqueue(new TradeCompletedEvent(trader.Id, from.Id, to.Id, ware.Id, qty, profit, EventFactory.Now()));
int processed = events.Update(deltaTime, maxEventsPerFrame: 128);
```

## Integration with ECS

1. Register `EventSystem` alongside other systems.
2. Prefer publishing domain events from systems/components rather than direct cross-calls.
3. Subscribe in systems that need to react; avoid tight coupling.

Example:
```csharp
// During engine setup
_eventSystem = new EventSystem();
_entityManager.RegisterSystem(_eventSystem); // if your ECS supports system registration

// In CollisionSystem
_eventSystem.Publish(new CollisionEvent(a.Id, b.Id, contactPoint, normal, EventFactory.Now()));

// In DamageSystem
_eventSystem.Publish(new EntityDamagedEvent(entity.Id, dmg, entity.Health, attackerId, EventFactory.Now()));
```

## Built-in Game Events
- `EntityDamagedEvent`
- `EntityDestroyedEvent`
- `CollisionEvent`
- `TargetAcquiredEvent`
- `TradeCompletedEvent`
- `AIStateChangedEvent`
- `SelectionChangedEvent`
- `TagAddedEvent` / `TagRemovedEvent`

Extend by adding your own record types in `GameEvents.cs`.

## Best Practices
- Keep event payloads small and serializable
- Use records for immutability and clarity
- Prefer filters over branching inside handlers
- Use `once` for one-time transitions
- Limit queued dispatch per frame to avoid hitches
- Consider logging or metrics around event volume if needed

## Demo
Run the example to see the system in action:
```csharp
await SpaceTradeEngine.Examples.EventSystemExample.RunDemo();
```

## FAQ
- **Synchronous or async?** Both are supported.
- **Memory leaks?** Unsubscribe when handlers are no longer needed; tokens help.
- **Thread safety?** Subscription operations are safe; dispatch is controlled.

## Roadmap
- Optional event tracing hooks (if logging system added)
- Event correlation IDs for complex workflows
- Performance counters via lightweight instrumentation
