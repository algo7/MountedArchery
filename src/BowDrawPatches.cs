using System;
using HarmonyLib;

namespace MountedArchery
{
    /// <summary>
    /// The bow: Player.UpdateAttackBowDraw gives up at its top while IsAttached() is true, for draw, hold and release
    /// alike. While that routine runs for a rider with a bow, IsAttached answers false. The rest of the game still
    /// sees the rider as attached. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class BowDrawPatches
    {
        /// <summary>The rider whose bow draw is being updated right now; null otherwise.</summary>
        internal static Player Drawing;

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
        [HarmonyPrefix]
        private static void Begin(Player __instance, ItemDrop.ItemData weapon)
        {
            try
            {
                if (Riding.IsRiding(__instance) && Riding.IsBow(Riding.AttackOf(weapon, secondary: false))) Drawing = __instance;
            }
            catch (Exception e)
            {
                Drawing = null;
                LogOnce("Drawing a bow from the saddle failed (logged once)", e);
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
        [HarmonyFinalizer]
        private static void End() => Drawing = null;

        [HarmonyPatch(typeof(Player), nameof(Player.IsAttached))]
        [HarmonyPostfix]
        private static void IsAttached(Player __instance, ref bool __result)
        {
            if (Drawing != null && __instance == Drawing) __result = false;
        }

        private static void LogOnce(string what, Exception e)
        {
            if (s_errorLogged) return;
            s_errorLogged = true;
            Plugin.Log.LogError($"{what}: {e}");
        }
    }
}
