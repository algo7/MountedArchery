using System;
using HarmonyLib;

namespace MountedArchery
{
    /// <summary>
    /// Vanilla throws the rider off on any attack press (Player.SetControls: with a doodad controller, jump | attack |
    /// secondaryAttack | dodge clears the attack flags and stops the control). For a projectile attack on a saddle the
    /// press is hidden from that check and put back into the player's attack fields afterwards, so the attack update
    /// sees it and the rider stays seated. Jump and dodge are left alone. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class KeepSaddlePatches
    {
        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        [HarmonyPrefix]
        private static void Hide(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, out AttackFlags __state)
        {
            __state = default;
            try
            {
                if (!Riding.IsRiding(__instance)) return;
                var weapon = __instance.GetCurrentWeapon();
                var input = new AttackFlags { Attack = attack, AttackHold = attackHold, Secondary = secondaryAttack, SecondaryHold = secondaryAttackHold };
                __state = SaddleRules.Hide(true, input,
                    Riding.IsProjectile(Riding.AttackOf(weapon, secondary: false)),
                    Riding.IsProjectile(Riding.AttackOf(weapon, secondary: true)));
                if (!__state.Any) return;
                var rest = SaddleRules.Remaining(input, __state);
                attack = rest.Attack;
                attackHold = rest.AttackHold;
                secondaryAttack = rest.Secondary;
                secondaryAttackHold = rest.SecondaryHold;
            }
            catch (Exception e)
            {
                __state = default;
                LogOnce("Keeping the saddle on a shot failed (logged once; vanilla dismounts)", e);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        [HarmonyPostfix]
        private static void Restore(Player __instance, AttackFlags __state)
        {
            try
            {
                if (!__state.Any) return;
                var back = SaddleRules.Restore(Riding.IsRiding(__instance), __state);
                if (back.Attack) Riding.AttackFlag(__instance) = true;
                if (back.AttackHold) Riding.AttackHoldFlag(__instance) = true;
                if (back.Secondary) Riding.SecondaryFlag(__instance) = true;
                if (back.SecondaryHold) Riding.SecondaryHoldFlag(__instance) = true;
            }
            catch (Exception e)
            {
                LogOnce("Passing a shot on to the attack update failed (logged once)", e);
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
