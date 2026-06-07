# V0.1 Flight Prototype

## Goal

V0.1 validates the most important early risk: whether free 3D flight over a simple buoy route feels readable and controllable.

## Scene

Use the Unity menu:

```text
Sky Circuit > Build V0.1 Flight Prototype Scene
```

This generates:

- `Assets/Scenes/V0_1_FlightPrototype.unity`
- A water-plane test arena
- A capsule player with arcade flight controls
- Four buoy markers and a route line
- A target-buoy practice loop
- Contrail feedback
- A debug HUD
- Cinemachine camera setup when the package is available, otherwise a custom fallback camera

## Controls

- `W/S`: accelerate / decelerate
- `A/D`: yaw turn and bank
- Mouse: look / steer
- `Space`: ascend
- `Left Ctrl`, `Right Ctrl`, or `C`: descend
- `Shift`: boost while accelerating
- `Esc`: unlock cursor
- Left click Game view: lock cursor again

## Acceptance Checks

- The player can fly a full loop around the four buoys.
- Speed changes are visible through the HUD and contrail.
- The camera stays behind the player and keeps the route readable.
- Falling below the water or flying too far resets the player to spawn.
- The prototype works with placeholder geometry and does not require final art.
