#if AIMLOG
using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Test builds only (`make package AIMLOG=true`): one log line per projectile the local player fires, on foot and in
    /// the saddle, measuring the shot against the crosshair at the moment the game computes it (after AimAtFirePatches
    /// and SeatedCameraPatches). pitch: degrees the camera looks up (negative: down); the shot starts 1-2 m ahead of
    /// the body, so how far up or down you look moves it against the crosshair, on foot too.
    /// angle: degrees between the shot's direction and the camera's forward. offRight / offUp: where the shot's start
    /// lies off the crosshair's line, in the camera's right / up, in metres; the shot flies parallel to that line, so it
    /// stays that far off at any distance (plus the drop). Comparing riding lines with on-foot lines shows what the
    /// saddle adds. speed: the shooter's speed (the animal's, in the saddle). eyeUp / headUp: the eye and the head above
    /// the body's pivot (the camera hangs off the eye on foot, off the head + 0.3 m when seated); camBaseUp: where the
    /// camera actually hangs from this frame, above the pivot (it eases towards those).
    /// </summary>
    [HarmonyPatch]
    internal static class AimLogPatches
    {
        [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
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
                    "[aim] riding={0} attack={1} speed={2:0.0} pitch={3:0} angle={4:0.00} offRight={5:0.00} offUp={6:0.00} eyeUp={7:0.00} headUp={8:0.00} camBaseUp={9:0.00}",
                    Riding.IsRiding(player), __instance.m_attackAnimation, player.GetVelocity().magnitude,
                    Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg,
                    Vector3.Angle(aimDir, forward), Vector3.Dot(off, c.right), Vector3.Dot(off, c.up),
                    player.m_eye.position.y - pivot.y, player.GetHeadPoint().y - pivot.y,
                    Riding.CameraBaseOffset != null ? Riding.CameraBaseOffset(camera).y : float.NaN));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[aim] log failed: {e.Message}");
            }
        }

        /// <summary>
        /// One line per projectile the local player launches (after LaunchPatches): launched = its speed as it leaves,
        /// rider = the shooter's speed along the shot (what LaunchPatches added while riding; negative = shot behind).
        /// </summary>
        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Launch(Character owner, Vector3 velocity)
        {
            try
            {
                var player = owner as Player;
                if (player == null || player != Player.m_localPlayer) return;
                var along = velocity.sqrMagnitude > 0f ? Vector3.Dot(player.GetVelocity(), velocity.normalized) : 0f;
                Plugin.Log.LogInfo(string.Format(CultureInfo.InvariantCulture, "[launch] riding={0} launched={1:0.0} rider={2:0.0}",
                    Riding.IsRiding(player), velocity.magnitude, along));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[launch] log failed: {e.Message}");
            }
        }
    }
}
#endif
