using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// The seat's pose. It is an animator bool named by the seat (Player.m_attachAnimation: attach_lox, attach_asksvin,
    /// attach_moose), and the player's animator puts its full-body layer into that pose from any state while the bool is
    /// on, over half a second. Most attack clips play on that same layer, so a rider's attack clip was pulled back into
    /// the pose as soon as it began: a shot later than about 0.6 s into its clip (spear and harpoon throws, bombs, the
    /// thunder staff, the staff of protection) never fired, though its stamina or eitr was spent. Once a rider's
    /// projectile attack has started and the full-body layer is in its clip (the slow ones enter with a blend of 0.1 s or
    /// more, several physics steps, before the pose can take it), the bool is off until the shot has fired; then it is
    /// back on and the rest of the clip blends into the seat, as a bow shot always did (see SaddleRules.PoseOff /
    /// PoseBack). An attack on the upper-body layer (the Frost staff's stream) never touches it: the legs stay seated.
    /// The bool is set through the game's ZSyncAnimation, so friends see the same. If the rider leaves the saddle
    /// meanwhile, the game has already turned it off and it stays off.
    /// Also, while riding and not in an attack, minor action or emote, the animator speed is put back to 1 every step,
    /// as vanilla does on foot (CharacterAnimEvent.CustomFixedUpdate, which skips it while the seat's pose stops CanMove):
    /// a throw clip that speeds the animator up (spear, bomb) would otherwise leave the ride pose sped up. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class RidePosePatches
    {
        /// <summary>The rider whose shot is in progress; null otherwise.</summary>
        private static Player s_shot;

        /// <summary>
        /// Whether that shot has fired, whether its attack keeps firing after the first shot (loops or bursts), and
        /// whether the seat's pose is off for it.
        /// </summary>
        private static bool s_fired, s_keepsFiring, s_poseOff;

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPostfix]
        private static void Started(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            try
            {
                var player = StartAttackPatches.Seated;
                if (!__result || player == null || __instance != player) return;
                var attack = Riding.AttackOf(player.GetCurrentWeapon(), secondaryAttack);
                // A pose still off from this rider's previous shot stays off and comes back with this one. Another Player
                // (respawned after dying mid-shot, when UpdateAttach no longer ran) starts clean.
                if (s_shot != player) s_poseOff = false;
                s_shot = player;
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
            if (s_shot != null && __instance == s_shot) s_fired = true;
        }

        [HarmonyPatch(typeof(Player), "UpdateAttach")]
        [HarmonyPostfix]
        private static void Step(Player __instance)
        {
            try
            {
                if (!Riding.IsRiding(__instance))
                {
                    if (__instance == s_shot) Forget();
                    return;
                }
                var inAttack = __instance.InAttack();
                var anim = Riding.Anim(__instance);
                if (!inAttack && !__instance.InMinorAction() && !__instance.InEmote() && anim != null) anim.SetSpeed(1f);
                if (__instance != s_shot) return;
                var back = SaddleRules.PoseBack(s_fired, s_keepsFiring, inAttack, Time.time - StartAttackPatches.ShotStartedAt);
                if (back)
                {
                    if (s_poseOff) SetPose(__instance, true);
                    Forget();
                }
                else if (!s_poseOff && SaddleRules.PoseOff(Riding.NextOrCurrentAnimHash(__instance) == Riding.AttackTag, back))
                {
                    s_poseOff = SetPose(__instance, false);
                }
            }
            catch (Exception e)
            {
                if (__instance == s_shot)
                {
                    // Never leave the rider standing in the saddle.
                    try
                    {
                        if (s_poseOff && Riding.IsRiding(__instance)) SetPose(__instance, true);
                    }
                    catch (Exception)
                    {
                        // the first error is logged below
                    }
                    Forget();
                }
                LogOnce("Putting the rider back in the seat's pose failed (logged once)", e);
            }
        }

        private static void Forget()
        {
            s_shot = null;
            s_poseOff = false;
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
