using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    [BepInPlugin(Guid, Name, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "algo7.mountedarchery";
        public const string Name = "MountedArchery";
        public const string PluginVersion = PluginInfo.Version; // from the git tag, generated at build time (MinVer)

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            if (Application.isBatchMode)
            {
                Log.LogInfo($"{Name} loaded (v{PluginVersion}): dedicated server, nothing to do");
                return;
            }
            var harmony = new Harmony(Guid);
            Install(harmony);
            Log.LogInfo($"{Name} loaded (v{PluginVersion})");
        }

        /// <summary>
        /// Nothing is patched unless every game member the hooks read was found. The saddle, attack-start, fire-aim and
        /// seat-pose hooks are all needed: without the first two a shot would neither fire nor dismount, without the third
        /// shots to the side would fly off line, without the fourth slow throws would spend stamina or eitr and never fire;
        /// if any fails, everything is removed and the game stays vanilla. Without the
        /// bow-draw hook, bows go back to dismounting (never a dead click); without the turn hook, the rider doesn't
        /// visibly turn, but shots still fly true; without the seated camera hook, shots from the saddle start a little
        /// higher against the crosshair than on foot; without the health bar hook (or its two fields), enemies' health bars
        /// don't show while riding, as in the unmodded game.
        /// </summary>
        private static void Install(Harmony harmony)
        {
            if (!Riding.Prepare()) return;
            if (!TryPatch(harmony, typeof(KeepSaddlePatches), "saddle hook") ||
                !TryPatch(harmony, typeof(StartAttackPatches), "attack start hook") ||
                !TryPatch(harmony, typeof(AimAtFirePatches), "fire aim hook") ||
                !TryPatch(harmony, typeof(RidePosePatches), "seat pose hook"))
            {
                harmony.UnpatchSelf();
                Log.LogError("Mounted shooting is off: attacks dismount you as in the unmodded game");
                return;
            }
            // If only part of this class patches, the leftover prefix just sets BowDrawPatches.Drawing, which nothing else
            // reads; bows then dismount as vanilla.
            if (!TryPatch(harmony, typeof(BowDrawPatches), "bow draw hook"))
            {
                Riding.BowsEnabled = false;
                Log.LogWarning("Bows dismount you as in the unmodded game (crossbows, spears and staffs still fire from the saddle)");
            }
            if (!TryPatch(harmony, typeof(FaceAimPatches), "turn hook"))
                Log.LogWarning("The rider won't visibly turn to the aim (shots still fly true)");
            if (!TryPatch(harmony, typeof(SeatedCameraPatches), "seated camera hook"))
                Log.LogWarning("Shots from the saddle start a little higher against the crosshair than on foot");
            if (Riding.HoverCreature != null && !TryPatch(harmony, typeof(HoverPatches), "health bar hook"))
                Log.LogWarning("Enemies' health bars won't show while riding (as in the unmodded game)");
#if AIMLOG
            if (TryPatch(harmony, typeof(AimLogPatches), "aim log")) Log.LogWarning("Test build: logging every shot ([aim] lines)");
#endif
        }

        /// <summary>True when patched; false (logged) on failure.</summary>
        internal static bool TryPatch(Harmony harmony, Type patches, string what)
        {
            try
            {
                harmony.PatchAll(patches);
                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"Could not install the {what}: {e}");
                return false;
            }
        }
    }
}
