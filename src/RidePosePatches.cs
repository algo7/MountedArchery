using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// The seat's pose. It is an animator bool named by the seat (Player.m_attachAnimation: attach_lox, attach_asksvin,
    /// attach_moose), and the player's animator goes into that pose from any state while the bool is on, over half a
    /// second. Attack clips play on the same full-body layer, so a rider's attack clip was pulled back into the pose as
    /// soon as it began: a shot later than about 0.6 s into its clip (spear and harpoon throws, bombs, the thunder staff)
    /// never fired, though its stamina or eitr was spent. From the moment a rider's projectile attack starts until its
    /// shot has fired, the bool is off; then it is back on and the rest of the clip blends into the seat, as a bow shot
    /// always did (a looping or multi-burst attack keeps it off until its clip is over; see SaddleRules.PoseBack). The
    /// bool is set through the game's ZSyncAnimation, so friends see the same. If the rider leaves the saddle meanwhile,
    /// the game has already turned it off and it stays off.
    /// Also, while riding and not in an attack, minor action or emote, the animator speed is put back to 1 every step,
    /// as vanilla does on foot (CharacterAnimEvent.CustomFixedUpdate, which skips it while the seat's pose stops CanMove):
    /// a throw clip that speeds the animator up (spear, bomb) would otherwise leave the ride pose sped up. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class RidePosePatches
    {
        /// <summary>The rider whose seat pose is off for a shot; null otherwise.</summary>
        private static Player s_off;

        /// <summary>Whether that shot has fired, and whether its attack keeps firing after the first shot (loops or bursts).</summary>
        private static bool s_fired, s_keepsFiring;

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPostfix]
        private static void Off(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            try
            {
                var player = StartAttackPatches.Seated;
                if (!__result || player == null || __instance != player) return;
                var attack = Riding.AttackOf(player.GetCurrentWeapon(), secondaryAttack);
                if (!SetPose(player, false)) return;
                s_off = player;
                s_fired = false;
                s_keepsFiring = attack != null && (attack.m_loopingAttack || attack.m_projectileBursts > 1);
            }
            catch (Exception e)
            {
                LogOnce("Keeping the attack clip for a shot from the saddle failed (logged once)", e);
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.OnAttackTrigger))]
        [HarmonyPostfix]
        private static void Fired(Humanoid __instance)
        {
            if (s_off != null && __instance == s_off) s_fired = true;
        }

        [HarmonyPatch(typeof(Player), "UpdateAttach")]
        [HarmonyPostfix]
        private static void Back(Player __instance)
        {
            try
            {
                if (!Riding.IsRiding(__instance))
                {
                    if (__instance == s_off) s_off = null;
                    return;
                }
                var inAttack = __instance.InAttack();
                var anim = Riding.Anim(__instance);
                if (!inAttack && !__instance.InMinorAction() && !__instance.InEmote() && anim != null) anim.SetSpeed(1f);
                if (__instance != s_off) return;
                if (!SaddleRules.PoseBack(s_fired, s_keepsFiring, inAttack, Time.time - StartAttackPatches.ShotStartedAt)) return;
                s_off = null;
                SetPose(__instance, true);
            }
            catch (Exception e)
            {
                if (__instance == s_off) s_off = null;
                LogOnce("Putting the rider back in the seat's pose failed (logged once)", e);
            }
        }

        /// <summary>Turns the current seat's pose on or off; false if the seat has none.</summary>
        private static bool SetPose(Player player, bool on)
        {
            var pose = Riding.AttachAnimation(player);
            var anim = Riding.Anim(player);
            if (string.IsNullOrEmpty(pose) || anim == null) return false;
            anim.SetBool(pose, on);
            return true;
        }

        private static void LogOnce(string what, Exception e)
        {
            if (s_errorLogged) return;
            s_errorLogged = true;
            Plugin.Log.LogError($"{what}: {e}");
        }
    }
}
