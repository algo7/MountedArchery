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
is independent of the saddle: the aim is the camera direction, the projectile is spawned by the attack animation's
event, and the shooter's own velocity is never added. What the saddle does change is the body's facing: every physics
step the rider is snapped back to the saddle's rotation, so the projectile's spawn point (laid out along the body's
forward) would sit beside the crosshair line when aiming to the side.

Four Harmony patches, all on the local player and only while attached to a `Sadle`:

- `Player.SetControls` prefix / postfix: a press whose attack is of the game's `Projectile` type is hidden from the
  dismount check, then put back into the player's attack fields so the attack update sees it. Melee, jump and dodge
  are untouched and dismount as always.
- `Humanoid.StartAttack` prefix / finalizer + `Character.CanMove` postfix: the attack would be refused because the
  ride animation is tagged "sitting"; for the moment a rider's projectile attack starts, that tag is ignored
  (staggering still refuses).
- `Player.UpdateAttackBowDraw` prefix / finalizer + `Player.IsAttached` postfix: the bow-draw routine gives up while
  attached; during its run for a rider with a bow, `IsAttached` answers false.
- `Player.UpdateAttach` postfix: while a shot is in progress or a bow is drawn, the rider is turned to the look
  direction right after vanilla's snap, so the projectile leaves from where it would on foot.

The vanilla standing fire animation plays in the saddle and the rider drops back into the ride pose afterwards, the
same way the crossbow reload already does. Nothing is written to the network; other clients receive the vanilla attach
state, animation trigger and transform.

## Building

Needs the .NET SDK (8+) and the game's DLLs (a local Valheim install, or the dedicated server's).

```sh
make build      # bin/Release/MountedArchery.dll
make test       # unit tests: the saddle rules, and the game members the patches use
make package    # Algo7-MountedArchery-<version>.zip for r2modman's "Import local mod"
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
| `src/Riding.cs` | Shared helpers: is the local player on a saddle, which attacks fire a projectile |
| `src/KeepSaddlePatches.cs`, `src/StartAttackPatches.cs`, `src/BowDrawPatches.cs`, `src/FaceAimPatches.cs` | The four patches |
| `tests/` | Unit tests (`make test`) |
| `package/` | Thunderstore page and icon |

## License

MIT
