using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Where a rider's shot starts. Attack.GetProjectileSpawnPoint lays the start out from the body as if standing,
    /// against the eye, but a seated rider's camera hangs off the head (GameCamera.GetCameraBaseOffset), lower, so the
    /// shot started higher against the crosshair than on foot. Right after the game computes a rider's shot, its start
    /// moves by the gap between the eye and the camera's base as on screen (GameCamera.m_currentBaseOffset, which
    /// eases after mounting and barely follows the head's bob in a fire clip): it then sits against the crosshair as it
    /// does on foot. Without that field, the gap to where the seated camera settles (head + 0.3 m) is used. The
    /// direction is untouched. Weapons that fire from a bone (m_attackOriginJoint; no vanilla player weapon does) are
    /// left alone: their start already moves with the seated pose. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class SeatedCameraPatches
    {
        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
        [HarmonyPostfix]
        private static void Shift(Attack __instance, ref Vector3 spawnPoint)
        {
            try
            {
                var player = Riding.Attacker(__instance) as Player;
                if (!Riding.IsRiding(player) || !string.IsNullOrEmpty(__instance.m_attackOriginJoint)) return;
                spawnPoint += ShotRules.ShotShift(CameraBase(player), player.m_eye.position);
            }
            catch (Exception e)
            {
                if (s_errorLogged) return;
                s_errorLogged = true;
                Plugin.Log.LogError($"Starting a shot at the seated camera's height failed (logged once): {e}");
            }
        }

        /// <summary>The point the camera hangs from, in the world: as on screen, else where it settles when seated.</summary>
        internal static Vector3 CameraBase(Player player)
        {
            var camera = GameCamera.instance;
            if (Riding.CameraBaseOffset != null && camera != null) return player.transform.position + Riding.CameraBaseOffset(camera);
            return ShotRules.SeatedCameraBase(player.GetHeadPoint());
        }
    }
}
