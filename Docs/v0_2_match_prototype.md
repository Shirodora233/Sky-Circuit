# V0.2 Match Prototype

## Goal

V0.2 turns the flight prototype into the first playable match loop: countdown, scoring, AI opponent, timer, and result.

## Scene

Use the Unity menu:

```text
Sky Circuit > Build V0.2 Match Prototype Scene
```

This generates:

- `Assets/Scenes/V0_2_MatchPrototype.unity`
- A player competitor and one AI competitor
- Four buoy route markers
- A shared scoring route
- A 3 second countdown
- A 180 second match timer
- Score and result HUD

## Rules

- Only the current target buoy scores.
- Player input and competitor flight are locked during countdown, then reset and released when the match starts.
- Competitor flight is locked again when the match finishes, so ships do not drift out of bounds after results.
- Touching the current target buoy gives `+1`.
- Player and AI track their target index independently.
- Only the player's target buoy is highlighted for now.
- The AI follows the same buoy route and can score.
- Falling below the water or flying too far resets position without resetting score.
- Dogfight, body-touch scoring, collisions, shortcuts, and role types are intentionally out of scope.
