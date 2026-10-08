using System;
using HarmonyLib;

namespace MountedArchery
{
    /// <summary>
    /// The fire moment. Attack.GetProjectileSpawnPoint lays the start point out along the body's facing and takes the
    /// direction from the eye's current forward (Humanoid.GetAimDir is Character.GetLookDir, m_eye.forward). The eye is a
    /// child of the body, so every saddle snap and every turn swings it, and the game only re-aims it later in the frame.
    /// Right before the game computes a rider's shot, the rider faces the mouse look and the eye is re-aimed, so the
    /// game's own formula gives the shot it gives on foot. Runs for the trigger effects and for each burst; doing it
    /// twice is harmless. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class AimAtFirePatches
    {
        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
        [HarmonyPrefix]
        private static void Align(Attack __instance)
        {
            try
            {
                var player = Riding.Attacker(__instance) as Player;
                if (!Riding.IsRiding(player)) return;
                var yaw = player.GetLookYaw();
                player.transform.rotation = yaw;
                var body = Riding.Body(player);
                if (body != null) body.rotation = yaw;
                Riding.AimEye(player);
            }
            catch (Exception e)
            {
                if (s_errorLogged) return;
                s_errorLogged = true;
                Plugin.Log.LogError($"Aiming a shot from the saddle failed (logged once): {e}");
            }
        }
    }
}
