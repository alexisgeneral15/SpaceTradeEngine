# Behavior Tree AI System - Complete Guide

## Overview

The **Behavior Tree AI System** brings your space game's NPCs to life with intelligent, reactive behaviors. Ships will hunt enemies, flee from danger, patrol routes, trade between stations, and adapt to changing situations - all through an elegant, hierarchical decision-making system.

## What Are Behavior Trees?

Behavior Trees are a hierarchical way to organize AI logic. Instead of giant `if-else` chains or complex state machines, you build trees of reusable behaviors:

```
Root (Selector)
├─ Flee (if damaged)
├─ Attack (if enemy nearby)
└─ Patrol (default)
```

The AI evaluates the tree top-to-bottom, executing the first behavior that succeeds.

---

## Architecture

```
┌────────────────────────────────────────────────┐
│         BehaviorTreeSystem (ECS)               │
│    Updates all AI entities each frame          │
└────────────────┬───────────────────────────────┘
                 │
                 ├──► Entity 1 (Fighter)
                 │    └─ BehaviorTreeComponent
                 │       └─ BehaviorTree
                 │          └─ Root Node
                 │
                 ├──► Entity 2 (Trader)
                 │    └─ BehaviorTreeComponent
                 │       └─ BehaviorTree
                 │          └─ Root Node
                 │
                 └──► Entity 3 (Patrol)
                      └─ BehaviorTreeComponent
                         └─ BehaviorTree
                            └─ Root Node
```

---

## Node Types

### 1. Composite Nodes (have children)

#### SequenceNode
Executes children in order until one **fails**.
```csharp
new SequenceNode("Attack Sequence")
    .AddChild(FindEnemy())      // If this fails, stop
    .AddChild(MoveToEnemy())    // Execute if above succeeded
    .AddChild(FireWeapon())     // Execute if above succeeded
```
**Use for**: Step-by-step actions that must happen in order.

#### SelectorNode  
Executes children in order until one **succeeds**.
```csharp
new SelectorNode("Choose Action")
    .AddChild(FleeIfDamaged())  // Try this first
    .AddChild(AttackEnemy())    // If flee failed, try attack
    .AddChild(Patrol())         // Default if nothing else worked
```
**Use for**: Priority lists, fallback behaviors.

#### ParallelNode
Executes **all** children simultaneously.
```csharp
new ParallelNode(ParallelNode.ParallelPolicy.RequireAll)
    .AddChild(MoveToTarget())
    .AddChild(RotateToFace())
    .AddChild(ChargeWeapons())
```
**Use for**: Multi-tasking, simultaneous actions.

### 2. Decorator Nodes (modify single child)

#### InverterNode
Flips Success ↔ Failure.
```csharp
new InverterNode(IsHealthLow()) // Succeeds if health is HIGH
```

#### RepeaterNode
Repeats child N times.
```csharp
new RepeaterNode(Patrol(), repeatCount: 5) // Patrol 5 times
```

#### TimeoutNode
Fails if child takes too long.
```csharp
new TimeoutNode(MoveToTarget(), timeoutSeconds: 10f)
```

#### RetryNode
Retries child until it succeeds.
```csharp
new RetryNode(FindEnemy(), maxAttempts: 3)
```

### 3. Leaf Nodes (do actual work)

#### ActionNode
Performs an action.
```csharp
new ActionNode(context => 
{
    var velocity = context.Entity.GetComponent<VelocityComponent>();
    velocity.LinearVelocity = new Vector2(100, 0);
    return NodeStatus.Success;
}, "MoveRight")
```

#### ConditionNode
Checks a condition.
```csharp
new ConditionNode(context =>
{
    var health = context.Entity.GetComponent<HealthComponent>();
    return health.HealthPercent < 0.3f;
}, "IsHealthLow")
```

#### WaitNode
Waits for duration.
```csharp
new WaitNode(3f, "Wait 3 seconds")
```

---

## Node Status

Every node returns one of three statuses:

- **Success**: Node completed successfully
- **Failure**: Node failed to complete
- **Running**: Node is still executing (e.g., movement, waiting)

---

## Pre-Built Behaviors

The `SpaceBehaviors` class provides ready-to-use behaviors:

### Movement
```csharp
SpaceBehaviors.MoveToPosition(Vector2 target, float speed)
SpaceBehaviors.MoveToEntity(string entityKey, float speed)
SpaceBehaviors.FleeFromEntity(string entityKey, float speed, float safeDistance)
SpaceBehaviors.Patrol(Vector2[] waypoints, float speed)
SpaceBehaviors.Stop()
```

### Combat
```csharp
SpaceBehaviors.AttackTarget(float range, float cooldown)
SpaceBehaviors.HasTarget()
SpaceBehaviors.IsTargetInRange()
SpaceBehaviors.HasLineOfSight()
```

### Detection
```csharp
SpaceBehaviors.FindNearestEnemy(spatialSystem, float radius)
SpaceBehaviors.AreEnemiesNearby(spatialSystem, float radius)
SpaceBehaviors.FindNearestAlly(spatialSystem, float radius)
```

### Health & Survival
```csharp
SpaceBehaviors.IsHealthLow(float threshold)
SpaceBehaviors.IsAlive()
SpaceBehaviors.SeekHealing(spatialSystem, float radius)
```

### Utility
```csharp
SpaceBehaviors.RandomWait(float min, float max)
SpaceBehaviors.HasTag(string tag)
SpaceBehaviors.Log(string message)
```

---

## Pre-Built AI Templates

The `AITemplates` class provides complete AI personalities:

### 1. Fighter AI
Aggressive combat AI. Hunts enemies, flees when damaged.
```csharp
var fighterAI = AITemplates.CreateFighterAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Finds and attacks enemies
- ✅ Maintains optimal weapon range
- ✅ Retreats when health low
- ✅ Patrols when no threats

**Best for:** Combat ships, interceptors, defense fleet

### 2. Trader AI
Peaceful trading AI. Travels between stations, avoids combat.
```csharp
var traderAI = AITemplates.CreateTraderAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Travels between trade stations
- ✅ Flees from danger
- ✅ Seeks repairs when damaged
- ✅ Docks at stations

**Best for:** Cargo ships, merchants, supply vessels

### 3. Patrol AI
Guards a specific area. Engages threats in patrol zone.
```csharp
Vector2[] route = { point1, point2, point3 };
var patrolAI = AITemplates.CreatePatrolAI(entity, spatialSystem, route);
```

**Behavior:**
- ✅ Follows patrol route
- ✅ Engages enemies in patrol zone
- ✅ Returns to patrol after combat
- ✅ Maintains zone security

**Best for:** Station guards, border patrol, perimeter defense

### 4. Berserker AI
Extremely aggressive. Attacks until destroyed.
```csharp
var berserkerAI = AITemplates.CreateBerserkerAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Seeks enemies aggressively
- ✅ Never retreats
- ✅ Ignores damage
- ✅ Fast attack rate

**Best for:** Elite enemies, boss ships, fanatic units

### 5. Coward AI
Flees from everything. Never fights.
```csharp
var cowardAI = AITemplates.CreateCowardAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Flees from all enemies
- ✅ Seeks allied protection
- ✅ Wanders when safe
- ✅ Never engages in combat

**Best for:** Civilian ships, damaged vessels, escape pods

### 6. Miner AI
Collects resources. Avoids combat.
```csharp
var minerAI = AITemplates.CreateMinerAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Finds and mines asteroids
- ✅ Returns to station when full
- ✅ Flees from threats
- ✅ Economic focus

**Best for:** Mining ships, resource gatherers, harvesters

### 7. Escort AI
Protects a specific target.
```csharp
var escortAI = AITemplates.CreateEscortAI(entity, protectedShip, spatialSystem);
```

**Behavior:**
- ✅ Stays near protected target
- ✅ Defends target from threats
- ✅ Maintains formation
- ✅ Prioritizes target safety

**Best for:** Bodyguards, convoy escorts, VIP protection

### 8. Kamikaze AI
Rams into enemies at high speed.
```csharp
var kamikazeAI = AITemplates.CreateKamikazeAI(entity, spatialSystem);
```

**Behavior:**
- ✅ Charges enemies at max speed
- ✅ No self-preservation
- ✅ Aggressive searching
- ✅ Suicide attack

**Best for:** Drones, missiles, suicide bombers

---

## Integration Guide

### Step 1: Initialize System

```csharp
public void Initialize()
{
    _entityManager = new EntityManager();
    
    // Spatial system (required for AI queries)
    _spatialSystem = new SpatialPartitioningSystem(worldBounds);
    _entityManager.RegisterSystem(_spatialSystem);
    
    // Behavior tree system
    _behaviorTreeSystem = new BehaviorTreeSystem();
    _entityManager.RegisterSystem(_behaviorTreeSystem);
}
```

### Step 2: Create Entity with AI

```csharp
// Create ship
var ship = _entityManager.CreateEntity("Fighter Ship");
ship.AddComponent(new TransformComponent { Position = new Vector2(100, 100) });
ship.AddComponent(new VelocityComponent());
ship.AddComponent(new HealthComponent { MaxHealth = 100f });
ship.AddComponent(new FactionComponent("human"));

// Add AI using template
var fighterAI = AITemplates.CreateFighterAI(ship, _spatialSystem);
ship.AddComponent(new BehaviorTreeComponent(fighterAI));

// That's it! Ship now has intelligent AI.
```

### Step 3: Update Each Frame

```csharp
public void Update(float deltaTime)
{
    _entityManager.Update(deltaTime); // Updates all systems including AI
}
```

---

## Custom AI Examples

### Example 1: Simple Patrol & Attack

```csharp
var behaviorTree = new BehaviorTree(entity,
    new SelectorNode("Patrol & Attack")
        .AddChild(
            // Priority 1: Attack if enemy nearby
            new SequenceNode("Combat")
                .AddChild(SpaceBehaviors.FindNearestEnemy(_spatialSystem, 800f))
                .AddChild(SpaceBehaviors.MoveToEntity("Enemy", 200f))
                .AddChild(SpaceBehaviors.AttackTarget(600f, 1f))
        )
        .AddChild(
            // Priority 2: Patrol
            SpaceBehaviors.Patrol(
                new Vector2[] { 
                    new Vector2(0, 0), 
                    new Vector2(500, 500) 
                },
                150f
            )
        )
);

entity.AddComponent(new BehaviorTreeComponent(behaviorTree));
```

### Example 2: Flee When Damaged

```csharp
var behaviorTree = new BehaviorTree(entity,
    new SelectorNode("Smart Combat")
        .AddChild(
            // If health low, flee
            new SequenceNode("Retreat")
                .AddChild(SpaceBehaviors.IsHealthLow(0.3f))
                .AddChild(SpaceBehaviors.Log("Retreating!"))
                .AddChild(SpaceBehaviors.FindNearestAlly(_spatialSystem, 2000f))
                .AddChild(SpaceBehaviors.MoveToEntity("Ally", 250f))
        )
        .AddChild(
            // Otherwise, attack
            new SequenceNode("Attack")
                .AddChild(SpaceBehaviors.FindNearestEnemy(_spatialSystem, 1000f))
                .AddChild(SpaceBehaviors.MoveToEntity("Enemy", 220f, 550f))
                .AddChild(SpaceBehaviors.AttackTarget(600f, 0.8f))
        )
);
```

### Example 3: Formation Flying

```csharp
// Leader ship does normal AI
var leaderAI = AITemplates.CreateFighterAI(leader, _spatialSystem);
leader.AddComponent(new BehaviorTreeComponent(leaderAI));

// Wingmen follow leader
for (int i = 0; i < 3; i++)
{
    var wingman = CreateShip($"Wingman_{i}", position, "human");
    
    var formationAI = new BehaviorTree(wingman,
        new SelectorNode("Formation")
            .AddChild(
                // Help leader if in combat
                new SequenceNode("Support Leader")
                    .AddChild(SpaceBehaviors.AreEnemiesNearby(_spatialSystem, 500f))
                    .AddChild(SpaceBehaviors.FindNearestEnemy(_spatialSystem, 800f))
                    .AddChild(SpaceBehaviors.AttackTarget(600f, 1f))
            )
            .AddChild(
                // Stay in formation
                new ActionNode(ctx => {
                    var leaderPos = leader.GetComponent<TransformComponent>().Position;
                    Vector2 offset = new Vector2(100 * i, 50);
                    Vector2 formationPos = leaderPos + offset;
                    
                    var transform = ctx.Entity.GetComponent<TransformComponent>();
                    var velocity = ctx.Entity.GetComponent<VelocityComponent>();
                    
                    Vector2 toFormation = formationPos - transform.Position;
                    velocity.LinearVelocity = toFormation * 0.5f;
                    
                    return NodeStatus.Running;
                }, "MaintainFormation")
            )
    );
    
    wingman.AddComponent(new BehaviorTreeComponent(formationAI));
}
```

### Example 4: State Machine Style

```csharp
var behaviorTree = new BehaviorTree(entity,
    new SelectorNode("State Machine")
        .AddChild(
            // State: Idle
            new SequenceNode("Idle State")
                .AddChild(new ConditionNode(ctx => 
                    ctx.GetValue<string>("State") == "Idle", "IsIdle"))
                .AddChild(SpaceBehaviors.Stop())
                .AddChild(new WaitNode(5f))
                .AddChild(new ActionNode(ctx => {
                    ctx.SetValue("State", "Patrol");
                    return NodeStatus.Success;
                }, "SwitchToPatrol"))
        )
        .AddChild(
            // State: Patrol
            new SequenceNode("Patrol State")
                .AddChild(new ConditionNode(ctx => 
                    ctx.GetValue<string>("State") == "Patrol", "IsPatrol"))
                .AddChild(SpaceBehaviors.Patrol(waypoints, 150f))
        )
        .AddChild(
            // State: Combat
            new SequenceNode("Combat State")
                .AddChild(new ConditionNode(ctx => 
                    ctx.GetValue<string>("State") == "Combat", "IsCombat"))
                .AddChild(SpaceBehaviors.AttackTarget(600f, 1f))
        )
);
```

---

## Blackboard System

The **blackboard** is a shared memory for storing data during behavior execution.

### Store Data
```csharp
context.SetValue("Enemy", enemyEntity);
context.SetValue("PatrolCount", 5);
context.SetValue("LastAttackTime", 10.5f);
```

### Retrieve Data
```csharp
Entity enemy = context.GetValue<Entity>("Enemy");
int count = context.GetValue<int>("PatrolCount");
bool hasEnemy = context.HasValue("Enemy");
```

### Clear Data
```csharp
context.ClearValue("Enemy");
```

### Use Case: Persistent State
```csharp
new ActionNode(context =>
{
    // Count how many times we've patrolled
    int count = context.GetValue<int>("PatrolCount");
    count++;
    context.SetValue("PatrolCount", count);
    
    if (count >= 5)
    {
        Console.WriteLine("Patrolled 5 times, taking a break!");
        return NodeStatus.Success;
    }
    
    return NodeStatus.Running;
}, "CountPatrols")
```

---

## Performance & Optimization

### Tick Intervals

By default, AI updates every frame. For performance, you can reduce update frequency:

```csharp
var btComponent = entity.GetComponent<BehaviorTreeComponent>();
btComponent.TickInterval = 0.1f; // Update every 0.1 seconds instead of every frame
```

**Guidelines:**
- Combat AI: Every frame (0f)
- Patrol AI: Every 0.1s
- Trading AI: Every 0.5s
- Background NPCs: Every 1s

### Pause/Resume AI

```csharp
// Pause all AI
_behaviorTreeSystem.PauseAll();

// Resume all AI
_behaviorTreeSystem.ResumeAll();

// Pause individual AI
entity.GetComponent<BehaviorTreeComponent>().IsEnabled = false;
```

### Statistics

```csharp
var stats = _behaviorTreeSystem.GetStats();
Console.WriteLine(stats.ToString());
// Output: AI Trees: 45/50 active
```

---

## Common Patterns

### Pattern 1: Priority-Based Behavior

```csharp
new SelectorNode("Priorities")
    .AddChild(Emergency())      // Highest priority
    .AddChild(Combat())         // Medium priority
    .AddChild(Trade())          // Low priority
    .AddChild(Idle())           // Lowest (default)
```

### Pattern 2: Multi-Stage Attack

```csharp
new SequenceNode("Attack Sequence")
    .AddChild(FindTarget())
    .AddChild(MoveIntoRange())
    .AddChild(AimWeapon())
    .AddChild(WaitForLock())
    .AddChild(Fire())
```

### Pattern 3: Reactive Behavior

```csharp
new ParallelNode(ParallelNode.ParallelPolicy.RequireOne)
    .AddChild(PatrolNormally())
    .AddChild(ReactToThreats())  // Interrupts patrol if threat detected
```

### Pattern 4: Timeout Behavior

```csharp
new TimeoutNode(
    new SequenceNode()
        .AddChild(FindEnemy())
        .AddChild(ChaseEnemy()),
    timeoutSeconds: 15f  // Give up after 15 seconds
)
```

---

## Debugging Tips

### 1. Add Log Nodes

```csharp
new SequenceNode("Debug Combat")
    .AddChild(SpaceBehaviors.Log("Looking for enemy..."))
    .AddChild(FindEnemy())
    .AddChild(SpaceBehaviors.Log("Enemy found! Attacking..."))
    .AddChild(AttackEnemy())
    .AddChild(SpaceBehaviors.Log("Attack complete!"))
```

### 2. Name Your Nodes

```csharp
new SequenceNode("Fighter Combat Sequence")  // Clear name!
new ActionNode(attack, "Fire Main Weapon")   // Clear name!
```

### 3. Use Success/Failure Nodes for Testing

```csharp
new SelectorNode("Test")
    .AddChild(new SuccessNode("Always Succeeds"))
    .AddChild(new FailureNode("Always Fails"))
```

### 4. Check Blackboard Values

```csharp
new ActionNode(context =>
{
    Console.WriteLine("Blackboard contents:");
    foreach (var key in context.Blackboard.Keys)
    {
        Console.WriteLine($"  {key}: {context.Blackboard[key]}");
    }
    return NodeStatus.Success;
}, "DebugBlackboard")
```

---

## Best Practices

### ✅ DO:
- Use `SelectorNode` for priority lists
- Use `SequenceNode` for step-by-step actions
- Name all nodes clearly
- Use pre-built behaviors when possible
- Store entities in blackboard by name
- Use decorators for retry/timeout logic

### ❌ DON'T:
- Create deeply nested trees (keep under 5 levels)
- Put heavy computation in conditions
- Forget to return `NodeStatus.Running` for ongoing actions
- Modify entity components directly in conditions
- Create circular references in blackboard

---

## Migration from Other AI Systems

### From State Machines

**Before (State Machine):**
```csharp
switch (currentState)
{
    case State.Patrol:
        Patrol();
        if (EnemyNearby()) currentState = State.Combat;
        break;
    case State.Combat:
        Attack();
        if (!EnemyNearby()) currentState = State.Patrol;
        break;
}
```

**After (Behavior Tree):**
```csharp
new SelectorNode()
    .AddChild(CombatBehavior())  // Auto-switches when enemy found
    .AddChild(PatrolBehavior())  // Auto-switches when no enemy
```

### From If-Else Chains

**Before:**
```csharp
if (health < 0.3f)
    Flee();
else if (enemyNearby)
    Attack();
else
    Patrol();
```

**After:**
```csharp
new SelectorNode()
    .AddChild(FleeIfDamaged())
    .AddChild(AttackIfEnemy())
    .AddChild(Patrol())
```

---

## API Reference

See source files for complete documentation:
- `AI/BehaviorTree.cs` - Core behavior tree classes
- `AI/SpaceBehaviors.cs` - Pre-built behaviors
- `AI/AITemplates.cs` - Complete AI personalities
- `Systems/BehaviorTreeSystem.cs` - ECS system
- `Examples/BehaviorTreeExample.cs` - Working examples

---

## Next Steps

1. **Read**: `BehaviorTreeExample.cs` for complete examples
2. **Run**: `BehaviorTreeDemo.RunDemo()` to see AI in action
3. **Experiment**: Try different AI templates
4. **Build**: Create your own custom behaviors
5. **Integrate**: Add AI to your existing entities

---

**Your NPCs are now ALIVE! 🤖🚀**
