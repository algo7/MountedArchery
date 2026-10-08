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
        /// Nothing is patched unless every game member the hooks read was found. The saddle hook and the attack-start
        /// hook (Task 5) are both needed: with only one, a shot would neither fire nor dismount; if either fails,
        /// everything is removed and the game stays vanilla.
        /// </summary>
        private static void Install(Harmony harmony)
        {
            if (!Riding.Prepare()) return;
            if (!TryPatch(harmony, typeof(KeepSaddlePatches), "saddle hook") ||
                !TryPatch(harmony, typeof(StartAttackPatches), "attack start hook"))
            {
                harmony.UnpatchSelf();
                Log.LogError("Mounted shooting is off: attacks dismount you as in the unmodded game");
                return;
            }
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
