# MountedArchery

[![CI](https://github.com/algo7/MountedArchery/actions/workflows/ci.yml/badge.svg)](https://github.com/algo7/MountedArchery/actions/workflows/ci.yml)
[![Thunderstore](https://img.shields.io/badge/Thunderstore-MountedArchery-blue)](https://thunderstore.io/c/valheim/p/Algo7/MountedArchery/)

A client-side [BepInEx](https://github.com/BepInEx/BepInEx) mod for Valheim: fire bows, crossbows, thrown spears and
projectile staffs from the saddle without being dismounted. Players without the mod only ever see vanilla things.

What it does for players is in [package/README.md](package/README.md), which is also the mod's Thunderstore page.
Changes: [CHANGELOG.md](CHANGELOG.md).

Made with AI assistance.

## How it works

Vanilla throws the rider off in one place: `Player.SetControls`, which, while a doodad controller (the saddle) is
active, clears the attack flags and stops the control on any attack, jump or dodge press. Everything else about a shot
is independent of the saddle: the projectile is spawned by the attack animation's event, and the shooter's own
velocity is never added. What the saddle does change is the body's facing: every physics step the rider is snapped
back to the saddle's rotation. That matters twice at the fire moment. The spawn point is laid out along the body's
forward, and the direction is the eye's current forward (`GetAimDir` is `m_eye.forward`). The eye (`EyePos`) is a
child of the body, so every snap swings it too, and the game only re-aims it later in the frame
(`PlayerController.LateUpdate`). Left alone, every shot that isn't straight ahead would fly off line.

Six Harmony patches, all on the local player and only while attached to a `Sadle`:

- `Player.SetControls` prefix / postfix: a press whose attack is of the game's `Projectile` type is hidden from the
  dismount check, then put back into the player's attack fields so the attack update sees it. So is the secondary
  press of a weapon that has no secondary attack (on foot it does nothing, so it doesn't throw you off either). Melee,
  jump and dodge are untouched and dismount as always.
- `Humanoid.StartAttack` prefix / finalizer + `Character.CanMove` postfix: the attack would be refused because the
  ride animation is tagged "sitting"; for the moment a rider's projectile attack starts, that tag is ignored
  (staggering still refuses).
- `Player.UpdateAttackBowDraw` prefix / finalizer + `Player.IsAttached` postfix: the bow-draw routine gives up while
  attached; during its run for a rider with a bow, `IsAttached` answers false.
- `Attack.GetProjectileSpawnPoint` prefix: right before the game computes a rider's shot, the rider faces the mouse
  look and the eye is re-aimed (`Player.UpdateEyeRotation`), so the game's own formula gives the on-foot shot. This
  is the fire moment itself, so it doesn't depend on the order Unity runs physics, animation and input in.
- `Attack.GetProjectileSpawnPoint` postfix: the game lays the shot out against the eye, which stays at standing
  height, but a seated rider's camera hangs 0.3 m above the head (`GameCamera.GetCameraBaseOffset`), lower than the
  eye. The shot's start moves by the gap between the eye and the camera's base as on screen
  (`GameCamera.m_currentBaseOffset`, eased by the game), so it sits against the crosshair as it does on foot. The
  direction is untouched, so shots still fly parallel to the crosshair as vanilla's do.
- `Player.UpdateAttach` postfix: while a shot is in progress or a bow is drawn, the rider is turned to the look
  direction right after vanilla's snap, so the rider is seen facing the target (local only, see below).

The vanilla standing fire animation plays in the saddle and the rider drops back into the ride pose afterwards, the
same way the crossbow reload already does. Nothing is written to the network; other clients receive the vanilla attach
state and animation trigger. The turn to the aim is local: while attached to a creature the game syncs the rider with
an identity relative rotation, so other players see the fire clip facing the animal's forward. The projectile is the
owner's object and flies where the owner aimed.

## Building

Needs the .NET SDK (8+) and the game's DLLs (a local Valheim install, or the dedicated server's).

```sh
make build      # bin/Release/MountedArchery.dll
make test       # unit tests: the saddle and shot rules, and the game members the patches use
make package    # Algo7-MountedArchery-<version>.zip for r2modman's "Import local mod"
make package AIMLOG=true   # test build (...-aimlog.zip): logs every shot against the crosshair ([aim] lines, `make log`)
```

The version comes from the git tag (MinVer).

## CI / CD

CI builds and tests against the free dedicated server's DLLs (`.github/scripts/valheim-managed.sh`). A `vX.Y.Z` tag
with a matching `## X.Y.Z` section in `CHANGELOG.md` releases to GitHub and Thunderstore after manual approval.

## Layout

| Path | What |
|---|---|
| `src/Plugin.cs` | Entry point, patching, graceful degradation |
| `src/SaddleRules.cs` | The decisions (Unity-free, unit tested) |
| `src/ShotRules.cs` | Where a rider's shot starts (plain vector math, unit tested) |
| `src/Riding.cs` | Shared helpers: is the local player on a saddle, which attacks fire a projectile |
| `src/KeepSaddlePatches.cs`, `src/StartAttackPatches.cs`, `src/BowDrawPatches.cs`, `src/AimAtFirePatches.cs`, `src/SeatedCameraPatches.cs`, `src/FaceAimPatches.cs` | The six patches |
| `tests/` | Unit tests (`make test`) |
| `package/` | Thunderstore page and icon |

## License

MIT
