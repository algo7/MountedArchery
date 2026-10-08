using MountedArchery;

internal static partial class Tests
{
    private static AttackFlags Flags(bool attack = false, bool attackHold = false, bool secondary = false, bool secondaryHold = false)
        => new AttackFlags { Attack = attack, AttackHold = attackHold, Secondary = secondary, SecondaryHold = secondaryHold };

    private static string Show(AttackFlags f) => $"{f.Attack},{f.AttackHold},{f.Secondary},{f.SecondaryHold}";

    private static void Test_IsProjectile_OnlyTheProjectileKind()
    {
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: true), "crossbow / staff / spear throw");
        False(SaddleRules.IsProjectile(Attack.AttackType.Horizontal, false, true), "sword swing");
        False(SaddleRules.IsProjectile(Attack.AttackType.Vertical, false, true), "axe chop");
        False(SaddleRules.IsProjectile(Attack.AttackType.Area, false, true), "ground slam");
        False(SaddleRules.IsProjectile(Attack.AttackType.None, false, true), "no attack");
    }

    private static void Test_IsProjectile_BowCountsOnlyWhenBowsEnabled()
    {
        // A bow is a Projectile attack with a draw. If the bow-draw hook failed to install, a bow press must dismount as
        // vanilla instead of becoming a dead click.
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: true, bowsEnabled: true), "bow with the draw hook");
        False(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: true, bowsEnabled: false), "bow without the draw hook");
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: false), "crossbow never needs the draw hook");
    }

    private static void Test_Hide_NothingWhenNotRiding()
    {
        var hidden = SaddleRules.Hide(riding: false, Flags(attack: true, attackHold: true, secondary: true, secondaryHold: true), true, true);
        False(hidden.Any, "on foot nothing is hidden");
    }

    private static void Test_Hide_MeleeStaysVanilla()
    {
        var hidden = SaddleRules.Hide(true, Flags(attack: true, attackHold: true), primaryIsProjectile: false, secondaryIsProjectile: false);
        False(hidden.Any, "a sword swing is not hidden: vanilla dismounts");
    }

    private static void Test_Hide_ProjectilePrimaryHidesPrimaryOnly()
    {
        var input = Flags(attack: true, attackHold: true, secondary: true, secondaryHold: false);
        var hidden = SaddleRules.Hide(true, input, primaryIsProjectile: true, secondaryIsProjectile: false);
        Eq("True,True,False,False", Show(hidden), "crossbow: primary hidden, secondary left for vanilla");
    }

    private static void Test_Hide_SecondaryStaysVanillaWithoutProjectileSecondary()
    {
        // Bow / crossbow / staff: no secondary attack. The secondary press must still dismount as vanilla.
        var hidden = SaddleRules.Hide(true, Flags(secondary: true, secondaryHold: true), primaryIsProjectile: true, secondaryIsProjectile: false);
        False(hidden.Any, "secondary press with no projectile secondary is not hidden");
    }

    private static void Test_Hide_SpearThrowOnlySecondary()
    {
        // Spear: primary is a poke (melee), secondary is the throw.
        var input = Flags(attack: true, attackHold: false, secondary: true, secondaryHold: true);
        var hidden = SaddleRules.Hide(true, input, primaryIsProjectile: false, secondaryIsProjectile: true);
        Eq("False,False,True,True", Show(hidden), "spear: only the throw is hidden");
    }

    private static void Test_Hide_OnlyPressedFlagsAreHidden()
    {
        var hidden = SaddleRules.Hide(true, Flags(attackHold: true), true, true);
        Eq("False,True,False,False", Show(hidden), "a held primary alone");
    }

    private static void Test_Remaining_IsInputMinusHidden()
    {
        var input = Flags(attack: true, attackHold: true, secondary: true, secondaryHold: true);
        var hidden = Flags(attack: true, attackHold: true);
        Eq("False,False,True,True", Show(SaddleRules.Remaining(input, hidden)), "vanilla sees the rest");
        Eq(Show(input), Show(SaddleRules.Remaining(input, default)), "nothing hidden: unchanged");
    }

    private static void Test_Restore_HiddenFlagsComeBackWhileAttached()
    {
        var hidden = Flags(attack: true, attackHold: true);
        Eq(Show(hidden), Show(SaddleRules.Restore(stillAttached: true, hidden)), "still in the saddle: the press reaches the attack update");
    }

    private static void Test_Restore_NothingOnceDismounted()
    {
        // Jump (or dodge) in the same tick: vanilla dismounted for the jump. The hidden press must not land on a standing player.
        var hidden = Flags(attack: true, attackHold: true);
        False(SaddleRules.Restore(stillAttached: false, hidden).Any, "dismounted this tick: nothing restored");
    }

    private static void Test_Shooting_DuringAnAttackClip()
    {
        True(SaddleRules.Shooting(inAttack: true, drawTime: -1f, bowWeapon: false), "crossbow fire clip");
        True(SaddleRules.Shooting(inAttack: true, drawTime: -1f, bowWeapon: true), "bow fire clip");
    }

    private static void Test_Shooting_WhileDrawingABow()
    {
        // m_attackDrawTime: -1 = not drawing, 0 = draw about to start, > 0 = drawing / holding.
        True(SaddleRules.Shooting(false, 0.3f, bowWeapon: true), "bow drawn");
        False(SaddleRules.Shooting(false, 0f, bowWeapon: true), "draw not started yet");
        False(SaddleRules.Shooting(false, -1f, bowWeapon: true), "bow idle");
        False(SaddleRules.Shooting(false, 0.3f, bowWeapon: false), "stale draw time with a crossbow in hand");
    }

    private static void Test_Shooting_IdleRider()
    {
        False(SaddleRules.Shooting(false, -1f, false), "just riding");
    }
}
