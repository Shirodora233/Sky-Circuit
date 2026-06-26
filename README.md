# Sky Circuit

Sky Circuit is a Unity 6 aerial racing prototype. The current playable loop is a 1v1 cloud-sea race with buoy scoring, dogfight back hits, selectable pilot archetypes, AI training, LAN multiplayer, tutorial guidance, and shared race presentation.

This README describes the current project structure. Older `V0_*` documents and scenes are historical prototype references unless this file says otherwise.

## Current Entry Points

- Unity version: `6000.4.0f1`.
- Main menu scene: `Assets/Scenes/V0_10_MainMenu.unity`.
- Shared race scene: `Assets/SkyCircuit/Scenes/CloudSeaRace.unity`.
- Build output: `Builds/Sky Circuit.exe`.
- Build settings currently include the main menu and `CloudSeaRace`.

The main menu is still named `V0_10_MainMenu` because it came from the old versioned prototype series. It is the current player-facing entry. Training and LAN combat both launch the same `CloudSeaRace` scene so flight handling, score rules, indicators, contrails, pause/exit behavior, and type parameters stay aligned.

## Game Modes

### Training

Use the main menu training card to choose a pilot archetype and start an AI match. Training uses the same race scene as LAN combat, with the opponent controlled by `RouteAIPilotController`.

- Buoy route scoring is active.
- Dogfight unlock and back-hit scoring are active.
- The AI uses the selected archetype behavior profile.
- `Esc` opens the in-match room/control overlay so the player can exit back to the menu.

### LAN Combat

Use the main menu LAN/settings flow from `V0_10_MainMenu`:

1. One instance starts as Host.
2. The other connects as Client by LAN IP and port.
3. Click combat after the connection is established.
4. Each side chooses a pilot archetype.
5. Both players ready up.
6. After all players are ready, the menu starts a three-second countdown and loads `CloudSeaRace`.

LAN combat uses host authority through Unity Netcode for GameObjects and Unity Transport. Player input, replicated race state, contrails, scores, target buoy state, disconnect handling, and exit-room behavior are expected to stay synchronized between Host and Client.

### Tutorial

The tutorial explains flight control, buoy scoring, back-hit scoring, and the LAN entry flow. It reuses the real race presentation and player parameters rather than a separate simplified sandbox.

## Core Rules

- The match length defaults to 180 seconds.
- Each competitor follows the current target buoy in route order.
- Touching the current buoy scores `+1` and advances that competitor's target.
- Dogfight becomes available after the first buoy score.
- A valid back hit scores `+2` and triggers repulsion/feedback.
- Highest score wins when the timer ends.

## Pilot Archetypes

The three archetypes live in `Assets/SkyCircuit/Configs/CompetitorProfiles`:

- `SC_Speeder.asset`: route-first, stronger speed/boost identity, conservative dogfight behavior.
- `SC_AllRounder.asset`: balanced route and dogfight behavior.
- `SC_Fighter.asset`: stronger turning and more active dogfight pursuit, with route-return limits.

Runtime code validates profile values through `CompetitorProfile` and applies AI-specific steering, recovery, route prediction, and dogfight decision parameters through `RouteAIPilotSettings`.

## Important Runtime Areas

- `Assets/SkyCircuit/Runtime/Flight`: flight input and movement.
- `Assets/SkyCircuit/Runtime/Match`: match state, competitors, buoy route scoring, debug HUD.
- `Assets/SkyCircuit/Runtime/Combat`: back-hit detection, feedback, and dogfight scoring.
- `Assets/SkyCircuit/Runtime/Race`: race launch requests and scene bootstrap.
- `Assets/SkyCircuit/Runtime/AI`: AI route pursuit, recovery, racing line, and dogfight decisions.
- `Assets/SkyCircuit/Runtime/Menu`: main menu, LAN setup, type selection, and ready countdown.
- `Assets/SkyCircuit/Runtime/Networking`: LAN bootstrap and connection settings.
- `Assets/SkyCircuit/Runtime/Presentation`: HUD, indicators, contrails, score graphics, and visual feedback.
- `Assets/SkyCircuit/Runtime/Tutorial`: tutorial overlay and tutorial flow.

## Scenes And Builders

Current scenes:

- `Assets/Scenes/V0_10_MainMenu.unity`: current menu entry.
- `Assets/SkyCircuit/Scenes/CloudSeaRace.unity`: current shared training/LAN race scene.

Useful editor menu items:

- `Sky Circuit/Build Cloud Sea Race Scene`: rebuilds `CloudSeaRace` from the cloud-sea race base.
- `Sky Circuit/Build Windows Player`: builds the Windows player into `Builds`.
- `Sky Circuit/Legacy Scene Builders/...`: rebuilds older prototype scenes for reference or migration.

Legacy scenes under `Assets/Scenes/V0_*` should not be treated as the active gameplay source unless a task explicitly targets them. `V0_9_CloudSeaRacePrototype` remains the base scene used by the CloudSeaRace builder, while gameplay should normally be entered from the main menu into `CloudSeaRace`.

## Build And Validation

Fast C# compile check:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Unity player build:

```powershell
Unity.exe -batchmode -quit -projectPath "E:\Project\Unity\Sky Circuit" -executeMethod SkyCircuit.EditorTools.SkyCircuitBuildPlayer.BuildWindowsPlayer
```

In the editor, use `Sky Circuit/Build Windows Player`.

Recommended manual smoke checks:

- Open `Assets/Scenes/V0_10_MainMenu.unity`.
- Start training, choose each archetype, and confirm the player and AI load into `CloudSeaRace`.
- Score at least one buoy and confirm dogfight unlock behavior.
- Press `Esc`, choose exit, and confirm the cursor and scene return correctly.
- Run Host/Client LAN flow, ready both sides, and confirm the three-second countdown starts the race.

## Documentation Map

Read `Docs/README.md` first. The old `Docs/v0_*` files are milestone notes, not current operating instructions. They are useful for design history, but some details predate the unified `CloudSeaRace` scene, type-selection ready flow, current LAN handling, and AI improvements.
