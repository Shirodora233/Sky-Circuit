# V0.8 LAN Multiplayer Preparation

## Goal

V0.8 prepares the project for a two-player local network match. One player starts as Host, the other joins as Client by LAN address. The first playable target is not a full online service; it is a stable same-network 1v1 match that can reuse the current flight, route scoring, dogfight, and HUD prototypes.

## Current Project Fit

The existing gameplay code already has useful seams for networking:

- `PlayerFlightInput` reads local device input and converts it into `FlightInputState`.
- `SkyCircuitFlightController` is shared by human and AI control through `SetInput`.
- `MatchController` owns countdown, match time, score flow, and phase transitions.
- `BuoyRoute` owns route target and buoy scoring checks.
- `DogfightController` owns back-hit scoring and repulsion.
- `Competitor` owns score, target buoy index, profile, body, and spawn references.

The first LAN pass should keep these roles intact and add a network layer around them instead of replacing the flight model.

## Technology Choice

Use Unity Netcode for GameObjects plus Unity Transport when implementation begins.

Reasons:

- It is the official Unity multiplayer stack and fits a Unity 6 project.
- LAN Host/Client can run without accounts, lobbies, relay, or matchmaking.
- The current game has a small player count and simple session shape.
- Host authority is easier to reason about than peer-to-peer physics.

The project currently has `com.unity.multiplayer.center`, but not runtime Netcode or Transport packages. Add those only when the networking implementation starts, after unrelated work has landed.

## Authority Model

Use Host authority for V0.8.

Host owns:

- Match phase, countdown, remaining time, and final result.
- Player spawning and assigned competitor slots.
- Flight simulation for both competitors.
- Buoy scoring and target index advancement.
- Dogfight hit validation, score awards, cooldown, and repulsion.

Client owns:

- Local input capture.
- Camera target and local HUD presentation.
- Optional client-side smoothing for replicated movement.

Client sends input to Host. Host simulates and replicates state back.

## Minimum Network Data

Client to Host:

- Throttle
- Turn
- Vertical
- Look delta
- Boost held
- Optional profile selection request

Host to Clients:

- Competitor position and rotation
- Competitor velocity if smoothing needs it
- Score
- Buoy score count
- Back-hit score count
- Target buoy index
- Selected profile or archetype
- Match phase
- Countdown remaining
- Match time remaining
- Result text or result enum
- Dogfight cooldown and last hit event

## Scene and Flow

First implementation scene can be a duplicate or builder-generated variant of the current match prototype:

- `V0_8_LanMultiplayerPrototype`
- One `NetworkManager`
- One `UnityTransport`
- One LAN bootstrap/debug UI
- Two networked competitor prefabs or scene objects
- Existing route, dogfight, camera, and HUD objects adapted through network wrappers

Debug flow:

1. Host player clicks or presses Start Host.
2. Client player enters Host LAN IP and starts Client.
3. Host waits for exactly two connected players.
4. Host assigns slot 0 and slot 1.
5. Host starts countdown.
6. Both players fly the same match.
7. Host ends the match and replicates the result.

## Implementation Phases

### Phase 1: Preparation

- Add this plan.
- Add LAN connection settings asset type.
- Keep existing gameplay scripts unchanged.
- Do not add Netcode packages yet.

### Phase 2: Package and Bootstrap

- Add `com.unity.netcode.gameobjects`.
- Add `com.unity.transport`.
- Create a LAN bootstrap component with Host, Client, and Shutdown controls.
- Create a simple connection-state HUD for editor and development builds.

### Phase 3: Input and Ownership

- Add a network input bridge.
- Local owner reads `PlayerFlightInput` or equivalent input source.
- Non-owner input is ignored locally.
- Client sends input to Host at a fixed send rate.
- Host applies received input through `SkyCircuitFlightController.SetInput`.

### Phase 4: Match Authority

- Gate `MatchController`, `BuoyRoute`, and `DogfightController` authority so scoring only runs on Host.
- Replicate match phase, time, scores, and target indices.
- Emit replicated events for back-hit feedback and result display.

### Phase 5: Smoothing and Polish

- Add interpolation for remote competitor movement.
- Add timeout and disconnect handling.
- Add IP and port fields to the debug UI.
- Add clear errors for full session, failed connect, and host shutdown.

## Acceptance Criteria

- Two editor/build instances on the same LAN can connect by IP and port.
- Only two players can join a match.
- Both players can complete a full timed match.
- Buoy and back-hit scoring agree on both machines.
- Host and Client see the same winner.
- A disconnected Client stops affecting the match cleanly.
- Existing single-player and AI prototype scenes still compile and run.

## Risks

- Flight feel can degrade if only replicated positions are used without smoothing.
- Current match code is not authority-aware yet, so Phase 4 must be careful.
- Dogfight and repulsion are physics-sensitive; keep them Host-only first.
- Package installation may update lockfiles and generated project files.
- Later online services such as Relay or lobby should not be mixed into the LAN milestone.

## Deferred

- Internet matchmaking
- Unity Relay
- Lobby browser
- Spectator mode
- Reconnect into an active match
- Prediction and rollback
- More than two competitors
