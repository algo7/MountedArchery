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

Nine Harmony patches, all on the local player and only while attached to a `Sadle`:

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
- `Humanoid.StartAttack` / `Humanoid.OnAttackTrigger` / `Player.UpdateAttach` postfixes: the seat's pose is an
  animator bool (`Player.m_attachAnimation`: `attach_lox`, `attach_moose`...), and the player's animator puts its
  full-body layer into it from any state while it is on, over 0.5 s. Most attack clips are on that layer, so a slow
  throw (spear, harpoon, bomb, thunder staff, staff of protection: shot 0.7 s or more into the clip) was pulled back
  into the pose before its shot and never fired; and the bow's draw pose is only entered from standing
  (`Movement -> bow aim`), so a rider drew with no draw pose. The bool is off while a bow is drawn (the rider stands
  to draw, as on foot), and for a shot from the moment its clip is on the full-body layer (or from the draw it was
  loosed from) until the shot has fired (a looping or multi-burst attack: until its clip is over); then it is back on
  and the rest of the clip blends into the seat. Attacks on the upper-body layer (the Frost staff's stream) leave it
  alone: the legs stay seated. It is never turned back on once
  the rider has left the saddle (the game turned it off then). While riding and idle, the animator speed is put back
  to 1 as vanilla does on foot (its own reset needs `CanMove`, which the seat's pose fails), so a throw that speeds
  the clip up (spear, bomb) doesn't leave the ride pose sped up.
- `Player.UpdateHover` postfix: enemies' health bars (`EnemyHud`) show for a creature the crosshair was on in the last
  minute (`Player.GetHoverCreature`), but vanilla clears what you look at while riding. For a rider (not holding a
  build tool), the creature under the crosshair is looked up as `Player.FindHoverObject` does on foot, looking through
  the mount as through your own body. Only the creature is set; the object to interact with stays cleared, so Use
  still dismounts.
- `Player.AutoPickup` postfix: auto-pickup searches the game's pickup range (2 m) around a point 1 m above the
  player's feet, which a seated rider holds too high to reach the ground (a thrown spear stayed where it fell). For a
  rider the same pass runs again around the mount: 1 m above the animal's feet, out to the game's range beyond the
  animal's body radius (lox 3.5 m, asksvin and moose about 3.1 m). Toggle, dropped items, unique items, free slots,
  carry weight, tar and ownership work as the game's; items fly to the rider and are picked up as on foot.

The vanilla standing fire animation plays in the saddle and the rider drops back into the ride pose afterwards, the
same way the crossbow reload already does. Nothing new is written to the network; other clients receive the vanilla
attach state, seat pose and animation trigger through the game's own sync (the trigger by RPC at once, the pose by
ZDO a tick or two later, so around a slow throw a friend may briefly see the rider stand in the saddle). The turn to
the aim is local: while attached to a creature the game syncs the rider with an identity relative rotation, so other
players see the fire clip facing the animal's forward. The projectile is the owner's object and flies where the owner
aimed.

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
| `src/KeepSaddlePatches.cs`, `src/StartAttackPatches.cs`, `src/BowDrawPatches.cs`, `src/AimAtFirePatches.cs`, `src/SeatedCameraPatches.cs`, `src/FaceAimPatches.cs`, `src/RidePosePatches.cs`, `src/HoverPatches.cs`, `src/PickupPatches.cs` | The nine patches |
| `tests/` | Unit tests (`make test`) |
| `package/` | Thunderstore page and icon |

## License

MIT
