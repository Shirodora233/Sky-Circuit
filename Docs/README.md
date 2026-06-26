# Sky Circuit Docs

This directory contains design history and prototype notes. For current setup, scene entry, build steps, and gameplay rules, start with the root `README.md`.

## Current References

- `../README.md`: current project overview, active scenes, modes, rules, build steps, and validation checklist.
- `aokana_flying_circus_rules.md`: external rules research used for design inspiration. This is reference material only, not a source of project naming, art, or copyrighted presentation.

## Historical Prototype Notes

These files describe earlier milestones. Keep them for context, but do not use them as current implementation instructions without checking the runtime code and root README.

- `v0_1_flight_prototype.md`: first flight-control prototype.
- `v0_2_match_prototype.md`: first buoy route match loop.
- `v0_3_dogfight_prototype.md`: early back-hit and repulsion prototype.
- `v0_4_profile_prototype.md`: first Speeder/Fighter/All-Rounder parameter pass.
- `v0_5_match_world_indicator.md`: early buoy and opponent indicator behavior.
- `v0_8_lan_multiplayer_plan.md`: LAN planning document. It predates the current main-menu ready flow and unified `CloudSeaRace` scene.
- `sky_circuit_development_plan.md`: long-term planning draft from the early prototype period. Some sections are still useful for product direction, but version naming, immediate next steps, and implementation status are outdated.

## Current Documentation Status

- Active player entry is `Assets/Scenes/V0_10_MainMenu.unity`.
- Active shared race scene is `Assets/SkyCircuit/Scenes/CloudSeaRace.unity`.
- Training and LAN combat should stay unified except for opponent control: AI in training, human network peer in LAN combat.
- `Assets/Scenes/V0_*` scenes are legacy/reference scenes unless a task explicitly targets a builder or prototype.
- `V0_9_CloudSeaRacePrototype` remains a builder source for `CloudSeaRace`, not the normal player entry.
- The LAN plan has already moved beyond preparation: runtime Netcode and Transport packages are present, and the current flow includes LAN connection, archetype selection, ready state, countdown, synchronized race state, and room exit handling.

## When Updating Docs

- Prefer updating the root `README.md` for current operating instructions.
- Add a short status note here when a historical document becomes misleading.
- Avoid renaming legacy files casually; their versioned names are useful for tracing implementation history.
- When documenting gameplay, mention whether the detail applies to training, LAN combat, or both.
