# Camera Follow System - Feature Guide

## Overview
The **Camera Follow System** provides smooth, RTS-style camera tracking for selected entities. When you follow a ship, the camera interpolates smoothly toward the target's position while allowing zoom adjustments.

## Implementation Details

### Files Created/Modified:
- **New:** `src/Systems/CameraFollowSystem.cs` - Core follow system with smooth interpolation
- **Modified:** `src/Systems/RenderingSystem.cs` - Added `SetCameraPosition()` and `SetCameraZoom()` public methods
- **Modified:** `src/Core/GameEngine.cs` - Integrated camera follow and added hotkeys

### Key Features:
1. **Smooth Interpolation** - Camera doesn't snap to target; it moves at configurable follow speed (150 units/sec)
2. **Zoom Control** - Smooth zoom interpolation with target zoom levels (0.5x to 3x)
3. **Selectable Targets** - Can follow any entity that is selectable
4. **Velocity Prediction** - `GetLeadOffset()` method for predictive camera leading (useful for fast-moving targets)

## Usage

### Hotkeys:
- **F** - Follow the first selected entity (select via left-click or drag-box)
- **C** - Toggle camera follow on/off
- **+/=** - Zoom in (increase zoom target)
- **-/_** - Zoom out (decrease zoom target)

### In Code:
```csharp
// Set a target entity to follow
_cameraFollowSystem.SetTarget(myShip);

// Toggle follow on/off
_cameraFollowSystem.ToggleFollow();

// Set zoom target
_cameraFollowSystem.SetZoomTarget(1.5f);

// Check if following
if (_cameraFollowSystem.IsFollowing)
{
    // Camera is actively tracking
}

// Stop following
_cameraFollowSystem.SetTarget(null);
```

## Quick Test

1. Run the game: `dotnet run`
2. Press **F1** to spawn the Faction AI demo (spawns multiple ships)
3. Click on a ship to select it
4. Press **F** to follow it - camera smoothly tracks the ship
5. Use **+/-** to zoom in/out
6. Press **C** to toggle follow on/off
7. Press **WASD** to pan manually when not following

## Configuration

To adjust camera follow behavior, modify these values in `CameraFollowSystem.cs`:

```csharp
private float _followSpeed = 150f;     // Units per second for interpolation
private float _zoomSpeed = 2f;         // Zoom interpolation speed
```

Higher values = faster/snappier tracking.
Lower values = slower/smoother tracking.

## Architecture Notes

- Inherits from `ECS.System` to integrate with the entity manager
- Works alongside manual camera panning (WASD keys)
- Respects the existing culling and rendering systems
- Non-intrusive: doesn't affect other game logic

## Future Enhancements

1. **Screen Boundaries** - Keep camera within galaxy bounds
2. **Focus Group** - Follow multiple selected entities (centroid tracking)
3. **Predictive Leading** - Use `GetLeadOffset()` to lead fast-moving targets
4. **Cinematic Camera** - Optional lead/lag, swing, and ease-in/out curves
5. **Formation Following** - Track a fleet formation center

---

**Status:** Implemented and tested
**Phase:** Phase 3 (Gameplay)
