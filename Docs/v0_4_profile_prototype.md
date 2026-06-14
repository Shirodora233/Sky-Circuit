# V0.4 Profile Prototype

V0.4 adds the first playable shoe/profile layer for Speeder, Fighter, and All-Rounder.

## Scene

Use:

```text
Sky Circuit > Build V0.4 Profile Prototype Scene
```

Generated/default scene:

- `Assets/Scenes/V0_4_ProfilePrototype.unity`

## Runtime Switching

During Play, the player can switch profiles directly:

- `1`: Speeder
- `2`: Fighter
- `3`: All-Rounder

Switching changes flight parameters immediately and does not reset score, position, buoy target, or match timer.

## Editable Profile Assets

Profile assets live in:

```text
Assets/SkyCircuit/Configs/CompetitorProfiles
```

Each profile exposes:

- Flight Speed: speed caps, acceleration, vertical energy, turn drag.
- Flight Steering: yaw, pitch, bank, rotation response, external impulse decay.
- Dash Skill: charge size, starting charge, turn-charge scaling, drain rate, dash acceleration.
- AI Pilot: route speed, braking, vertical correction, dogfight engage distance.

The V0.4 scene starts the player as All-Rounder and the AI as Speeder.

## Dash Skill

Hold `Q` to dash while charge is available. Dash does not require holding `W`; it pushes toward the profile's absolute speed cap using the profile's dash acceleration.
When charge is fully depleted, dash enters a short profile-defined cooldown and requires releasing `Q` before it can trigger again.

Dash charge is restored only when not dashing, not cooling down, and not holding the dash key after depletion. The recharge rate is based on current speed, actual body turn rate, and the profile's type multiplier:

```text
chargeRate = baseTurnChargeRate
  * typeChargeMultiplier
  * speedFactor
  * turnFactor^2
```

`speedFactor` reaches full value near `chargeReferenceSpeed`, and `turnFactor` reaches full value near `chargeReferenceTurnRate`.
This rewards real high-speed maneuvering and avoids low-speed direction spam.

## Notes

- V0.4 keeps V0.3 Dogfight scoring and buoy scoring unchanged.
- Profile assets are ordinary ScriptableObjects, so tuning values should happen there instead of editing scene component internals.
- The V0.4 scene builder creates missing default profile assets without overwriting existing tuned assets.
