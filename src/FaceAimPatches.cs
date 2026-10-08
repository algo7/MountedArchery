using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// The look of it: every physics step Player.UpdateAttach snaps the rider to the saddle's rotation, undoing the turn
    /// Attack.Start makes towards the aim. While a shot is in progress or a bow is drawn, the rider is turned back to the
    /// look direction right after the snap, so the rider is seen facing the target. That includes the short gap after
    /// the release or click, before the fire clip begins, which would otherwise show as a flick back to the animal. The shot itself is lined up at the
    /// fire moment (AimAtFirePatches), whatever this did. Position, gravity and velocity stay vanilla; the next step
    /// after the shot puts the rider back to the saddle's rotation. Never throws.
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
                var sinceShotStart = Time.time - StartAttackPatches.ShotStartedAt;
                if (!SaddleRules.Shooting(__instance.InAttack(), sinceShotStart, Riding.DrawTime(__instance), bow)) return;
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
