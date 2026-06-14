# V0.5 Match World Indicator

First V0.5 readability module: show only the player's current target buoy.

## Behavior

- When the target buoy is on screen, show `BUOY n` and distance above it.
- When the target is close to the touch radius, show `TOUCH` and switch to the touch color.
- When the target is off screen or behind the camera, show a stable edge arrow with `Bn` and distance.
- The indicator does not show AI targets, opponent position, shortcuts, or dogfight prompts.

## Implementation

- Runtime component: `MatchWorldIndicator`
- Default placement: attached to `Match HUD`
- References:
  - `Camera`
  - `MatchController`
  - `BuoyRoute`

The V0.4 builder now attaches and configures this component when rebuilding the current prototype scene.

## Tuning

Key fields:

- `screenMargin`: edge-arrow safe margin.
- `viewportInsetX / viewportInsetY`: how close to screen edge still counts as off-screen.
- `worldLabelOffset`: vertical offset above the buoy.
- `behindDeadZone`: stabilizes the indicator when the target is almost directly behind the camera.
- `behindEdgeAngle`: angle from directly behind that maps to the bottom left/right edge.
- `behindEasePower`: higher values make the behind-camera indicator stay near the bottom center longer.
- `touchPromptMultiplier`: distance threshold for switching from meters to `TOUCH`.
