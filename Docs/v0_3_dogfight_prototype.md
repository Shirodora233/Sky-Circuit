# V0.3 Dogfight Prototype

## Goal

V0.3 adds the first playable Dogfight loop on top of V0.2: back-hit scoring, anti-gravity repulsion, and a simple AI chase behavior.

## Scene

Use the Unity menu:

```text
Sky Circuit > Build V0.3 Dogfight Prototype Scene
```

This generates:

- `Assets/Scenes/V0_3_DogfightPrototype.unity`
- The V0.2 1v1 buoy match setup
- A Dogfight Controller
- Back Hit Feedback markers on both competitors
- Dogfight HUD readouts
- AI dogfight pursuit toward the player's back when nearby

## Rules

- Buoy scoring remains `+1`.
- Back-hit scoring is `+2`.
- Dogfight unlocks only after either competitor scores at least one buoy.
- A valid back hit requires close range, attacker behind the target, and attacker facing the target.
- Back hits apply a short shared cooldown to avoid repeated scoring in one contact.
- Back hits repel both competitors with a short external velocity impulse.
- Repulsion does not change current target buoy, reset score, or lock player input.
- Match countdown, finish freeze, out-of-bounds reset, and V0.2 scoring flow remain unchanged.

## Out Of Scope

- Shortcut rules
- Competitor role types
- Formal UI Toolkit HUD
- Final Point Field VFX
- Audio
- Advanced dogfight AI
