using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Humanoid.StartAttack refuses while CanMove() is false, and Character.CanMove is false while the animator is in a
    /// state tagged "sitting", which riding is. For the moment a seated rider's projectile attack is being started, the
    /// sitting tag is ignored (staggering still refuses; so do Player.CanMove's own checks, which run before the base
    /// method this patches). Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class StartAttackPatches
    {
        /// <summary>The rider whose projectile attack StartAttack is starting right now; null otherwise.</summary>
        internal static Player Seated;

        /// <summary>When the local rider's last shot started (game time); the turn to the aim covers the gap before its clip.</summary>
        internal static float ShotStartedAt = float.NegativeInfinity;

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPrefix]
        private static void Begin(Humanoid __instance, bool secondaryAttack)
        {
            try
            {
                var player = __instance as Player;
                if (!Riding.IsRiding(player)) return;
                if (Riding.IsProjectile(Riding.AttackOf(player.GetCurrentWeapon(), secondaryAttack))) Seated = player;
            }
            catch (Exception e)
            {
                Seated = null;
                LogOnce("Starting a shot from the saddle failed (logged once)", e);
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPostfix]
        private static void Started(Humanoid __instance, bool __result)
        {
            if (__result && Seated != null && __instance == Seated) ShotStartedAt = Time.time;
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyFinalizer]
        private static void End() => Seated = null;

        [HarmonyPatch(typeof(Character), nameof(Character.CanMove))]
        [HarmonyPostfix]
        private static void CanMove(Character __instance, ref bool __result)
        {
            try
            {
                if (__result || Seated == null || __instance != Seated) return;
                __result = !__instance.IsStaggering() && Riding.NextOrCurrentAnimHash(__instance) == Riding.SittingTag;
            }
            catch (Exception e)
            {
                LogOnce("Allowing a shot from the saddle failed (logged once)", e);
            }
        }

        private static void LogOnce(string what, Exception e)
        {
            if (s_errorLogged) return;
            s_errorLogged = true;
            Plugin.Log.LogError($"{what}: {e}");
        }
    }
}
