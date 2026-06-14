# V0.5 Match World Indicator

First V0.5 readability module: show only the player's current target buoy.

## Behavior

- When the target buoy is on screen, show `BUOY n` and distance above it.
- When the target is close to the touch radius, show `TOUCH` and switch to the touch color.
- When the target is off screen or behind the camera, show a stable edge arrow with `Bn` and distance.
- The indicator does not show AI targets, opponent position, shortcuts, or dogfight prompts.

## Player Threat Indicator

Runtime component: `MatchPlayerIndicator`

- Shows only the opponent, never the player.
- Works during `Running`, after Dogfight is unlocked by default.
- Hides when the opponent is on screen or outside `threatRange`.
- Uses one coarse halo band at a time: `Left`, `Right`, or `Bottom`.
- The band communicates broad threat direction only, not an exact opponent position.
- The halo grows stronger as the opponent gets closer.
- The halo switches from orange to red when the opponent is close, behind the player, and roughly facing the player.

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

Player threat fields:

- `requireDogfightUnlocked`: hides the threat halo until Dogfight is available.
- `threatRange`: maximum distance for showing an off-screen opponent threat.
- `fullIntensityDistance`: distance where the halo reaches full base intensity.
- `criticalThreatRange`: maximum distance for switching to the red danger color.
- `rearCenterRatio`: how wide the rear-center zone is before side halos take over.
- `edgeHoldTime`: minimum lock time before changing from one edge to another.
- `edgeThickness`: inward width of the halo band.
- `sideBandHeight / bottomBandWidth`: broad band coverage for side and bottom halos.
