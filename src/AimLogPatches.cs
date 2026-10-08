#if AIMLOG
using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Test builds only (`make package AIMLOG=true`): one log line per projectile the local player fires, on foot and in
    /// the saddle, measuring the shot against the crosshair at the moment the game computes it (after AimAtFirePatches).
    /// angle: degrees between the shot's direction and the camera's forward. offRight / offUp: where the shot's start
    /// lies off the crosshair's line, in the camera's right / up, in metres; the shot flies parallel to that line, so it
    /// stays that far off at any distance (plus the drop). Comparing riding lines with on-foot lines shows what the
    /// saddle adds. speed: the shooter's speed (the animal's, in the saddle). eyeUp / headUp: the eye and the head above
    /// the body's pivot (the camera hangs off the eye on foot, off the head + 0.3 m when seated).
    /// </summary>
    [HarmonyPatch]
    internal static class AimLogPatches
    {
        [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
        [HarmonyPostfix]
        private static void Log(Attack __instance, ref Vector3 spawnPoint, ref Vector3 aimDir)
        {
            try
            {
                var player = Riding.Attacker(__instance) as Player;
                if (player == null || player != Player.m_localPlayer) return;
                var camera = GameCamera.instance;
                if (camera == null) return;
                var c = camera.transform;
                var forward = c.forward;
                var fromCamera = spawnPoint - c.position;
                var off = fromCamera - forward * Vector3.Dot(fromCamera, forward);
                var pivot = player.transform.position;
                Plugin.Log.LogInfo(string.Format(CultureInfo.InvariantCulture,
                    "[aim] riding={0} attack={1} speed={2:0.0} angle={3:0.00} offRight={4:0.00} offUp={5:0.00} eyeUp={6:0.00} headUp={7:0.00}",
                    Riding.IsRiding(player), __instance.m_attackAnimation, player.GetVelocity().magnitude,
                    Vector3.Angle(aimDir, forward), Vector3.Dot(off, c.right), Vector3.Dot(off, c.up),
                    player.m_eye.position.y - pivot.y, player.GetHeadPoint().y - pivot.y));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[aim] log failed: {e.Message}");
            }
        }
    }
}
#endif
