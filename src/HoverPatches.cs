using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Enemies' health bars. EnemyHud shows a creature's bar for a minute after the crosshair was on it
    /// (Player.GetHoverCreature), but Player.UpdateHover clears what the player looks at while a doodad controller is in
    /// hand, so a rider never saw an enemy's health. Right after that, for a rider, the creature under the crosshair is
    /// looked up as Player.FindHoverObject does on foot (the camera's ray, its layers and 50 m, the first thing hit;
    /// not a sleeping one, not behind mist; nothing while a build tool is out, as UpdateHover does on foot), except
    /// that the mount is looked through like the rider's own body: aiming
    /// down past its head shows the enemy behind. Only the creature is set (nothing but EnemyHud reads it); the object
    /// to interact with stays cleared, so Use still dismounts and no hover text shows. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class HoverPatches
    {
        private static readonly RaycastHit[] s_hits = new RaycastHit[64];

        private static readonly IComparer<RaycastHit> s_byDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Player), "UpdateHover")]
        [HarmonyPostfix]
        private static void LookAtCreature(Player __instance)
        {
            try
            {
                if (!Riding.IsRiding(__instance) || __instance.InPlaceMode()) return;
                var sadle = __instance.GetDoodadController() as Sadle;
                var mount = sadle != null ? sadle.GetCharacter() : null;
                Riding.HoverCreature(__instance) = CreatureUnderCrosshair(__instance, mount);
            }
            catch (Exception e)
            {
                if (s_errorLogged) return;
                s_errorLogged = true;
                Plugin.Log.LogError($"Showing enemies' health from the saddle failed (logged once): {e}");
            }
        }

        private static Character CreatureUnderCrosshair(Player player, Character mount)
        {
            var camera = GameCamera.instance;
            if (camera == null) return null;
            var view = camera.transform;
            var count = Physics.RaycastNonAlloc(view.position, view.forward, s_hits, 50f, Riding.InteractMask(player));
            Array.Sort(s_hits, 0, count, s_byDistance);
            for (var i = 0; i < count; i++)
            {
                var collider = s_hits[i].collider;
                var body = collider.attachedRigidbody;
                if (body && body.gameObject == player.gameObject) continue;
                var character = body ? body.GetComponent<Character>() : collider.GetComponent<Character>();
                if (character != null && character == mount) continue;
                if (character == null) return null;
                var ai = character.GetBaseAI();
                if ((ai && ai.IsSleeping()) || ParticleMist.IsMistBlocked(player.GetCenterPoint(), character.GetCenterPoint())) return null;
                return character;
            }
            return null;
        }
    }
}
