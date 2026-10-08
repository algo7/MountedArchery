using System;
using HarmonyLib;

namespace MountedArchery
{
    /// <summary>
    /// Every physics step Player.UpdateAttach snaps the rider to the saddle's rotation, undoing the turn Attack.Start
    /// makes towards the aim. While a shot is in progress or a bow is drawn, the rider is turned back to the look
    /// direction right after the snap, so the projectile leaves from where it would on foot. Position, gravity and
    /// velocity stay vanilla; the next step after the shot puts the rider back to the saddle's rotation. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class FaceAimPatches
    {
        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Player), "UpdateAttach")]
        [HarmonyPostfix]
        private static void FaceAim(Player __instance)
        {
            try
            {
                if (!Riding.IsRiding(__instance)) return;
                var bow = Riding.IsBow(Riding.AttackOf(__instance.GetCurrentWeapon(), secondary: false));
                if (!SaddleRules.Shooting(__instance.InAttack(), Riding.DrawTime(__instance), bow)) return;
                var yaw = __instance.GetLookYaw();
                __instance.transform.rotation = yaw;
                var body = Riding.Body(__instance);
                if (body != null) body.rotation = yaw;
            }
            catch (Exception e)
            {
                LogOnce("Turning the rider to the aim failed (logged once)", e);
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
