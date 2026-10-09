using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// The rider's speed in the shot. The game launches every projectile at its own speed (Attack.FireProjectileBurst ->
    /// IProjectile.Setup(owner, aim * speed, ...)), whatever the shooter's; from a galloping animal a forward throw then
    /// crawls ahead and the rider is on it a moment later. While a riding local player's attack launches, the rider's
    /// speed along each shot is added to it (ShotRules.LaunchVelocity): ahead faster, behind slower, to the side as is;
    /// the direction never changes. Only Projectile (what flies; the Staff of Protection's bubble is an Aoe), and only
    /// during the attack's own launch: Projectile.Setup is also called for what a projectile spawns on impact. Damage is
    /// set at release from the weapon, skill and draw, never from speed. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class LaunchPatches
    {
        /// <summary>The rider whose attack is launching its projectiles right now; null otherwise.</summary>
        private static Player s_rider;

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
        [HarmonyPrefix]
        private static void Begin(Attack __instance)
        {
            try
            {
                var player = Riding.Attacker(__instance) as Player;
                s_rider = Riding.IsRiding(player) ? player : null;
            }
            catch (Exception e)
            {
                s_rider = null;
                LogOnce(e);
            }
        }

        [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
        [HarmonyFinalizer]
        private static void End() => s_rider = null;

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        [HarmonyPrefix]
        private static void Launch(Character owner, ref Vector3 velocity)
        {
            try
            {
                if (s_rider == null || owner != s_rider) return;
                velocity = ShotRules.LaunchVelocity(velocity, s_rider.GetVelocity());
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        private static void LogOnce(Exception e)
        {
            if (s_errorLogged) return;
            s_errorLogged = true;
            Plugin.Log.LogError($"Adding the rider's speed to a shot failed (logged once): {e}");
        }
    }
}
