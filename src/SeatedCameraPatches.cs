using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Where a rider's shot starts. Attack.GetProjectileSpawnPoint lays the start out from the body as if standing, but
    /// a seated rider's camera hangs off the head (GameCamera.GetCameraBaseOffset), lower than the standing eye, so the
    /// shot started higher against the crosshair than on foot. Right after the game computes a rider's shot, its start
    /// moves by the gap between the eye and the seated camera (ShotRules.SeatedShift): it then sits against the crosshair
    /// as it does on foot, after vanilla's dismount. The direction is untouched. Never throws.
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
                if (!Riding.IsRiding(player)) return;
                spawnPoint += ShotRules.SeatedShift(player.GetHeadPoint(), player.m_eye.position);
            }
            catch (Exception e)
            {
                if (s_errorLogged) return;
                s_errorLogged = true;
                Plugin.Log.LogError($"Starting a shot at the seated camera's height failed (logged once): {e}");
            }
        }
    }
}
