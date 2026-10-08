using MountedArchery;

internal static partial class Tests
{
    private static AttackFlags Flags(bool attack = false, bool attackHold = false, bool secondary = false, bool secondaryHold = false)
        => new AttackFlags { Attack = attack, AttackHold = attackHold, Secondary = secondary, SecondaryHold = secondaryHold };

    private static string Show(AttackFlags f) => $"{f.Attack},{f.AttackHold},{f.Secondary},{f.SecondaryHold}";

    private static void Test_IsProjectile_OnlyTheProjectileKind()
    {
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: true, hasAnimation: true), "crossbow / staff / spear throw");
        False(SaddleRules.IsProjectile(Attack.AttackType.Horizontal, false, true, hasAnimation: true), "sword swing");
        False(SaddleRules.IsProjectile(Attack.AttackType.Vertical, false, true, hasAnimation: true), "axe chop");
        False(SaddleRules.IsProjectile(Attack.AttackType.Area, false, true, hasAnimation: true), "ground slam");
        False(SaddleRules.IsProjectile(Attack.AttackType.None, false, true, hasAnimation: true), "no attack");
    }

    private static void Test_IsProjectile_BowCountsOnlyWhenBowsEnabled()
    {
        // A bow is a Projectile attack with a draw. If the bow-draw hook failed to install, a bow press must dismount as
        // vanilla instead of becoming a dead click.
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: true, bowsEnabled: true, hasAnimation: true), "bow with the draw hook");
        False(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: true, bowsEnabled: false, hasAnimation: true), "bow without the draw hook");
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: false, hasAnimation: true), "crossbow never needs the draw hook");
    }

    private static void Test_IsProjectile_EmptySlotNeverCounts()
    {
        // The game's own "has a secondary attack" test is the animation name, not the type: a weapon's unused secondary
        // slot can carry any type. Without an animation the attack can't start, so hiding its press would be a dead click.
        False(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: true, hasAnimation: false), "empty slot typed Projectile");
        True(SaddleRules.IsProjectile(Attack.AttackType.Projectile, bowDraw: false, bowsEnabled: true, hasAnimation: true), "real projectile attack");
    }

    private static void Test_Hide_NothingWhenNotRiding()
    {
        var hidden = SaddleRules.Hide(riding: false, Flags(attack: true, attackHold: true, secondary: true, secondaryHold: true), true, true);
        False(hidden.Any, "on foot nothing is hidden");
    }

    private static void Test_Hide_MeleeStaysVanilla()
    {
        var hidden = SaddleRules.Hide(true, Flags(attack: true, attackHold: true), primaryKeepsSaddle: false, secondaryKeepsSaddle: false);
        False(hidden.Any, "a sword swing is not hidden: vanilla dismounts");
    }

    private static void Test_Hide_ProjectilePrimaryHidesPrimaryOnly()
    {
        var input = Flags(attack: true, attackHold: true, secondary: true, secondaryHold: false);
        var hidden = SaddleRules.Hide(true, input, primaryKeepsSaddle: true, secondaryKeepsSaddle: false);
        Eq("True,True,False,False", Show(hidden), "crossbow: primary hidden, secondary left for vanilla");
    }

    private static void Test_Hide_SecondaryStaysVanillaWhenItDismounts()
    {
        // A secondary attack that dismounts (melee): the secondary press must reach vanilla's check.
        var hidden = SaddleRules.Hide(true, Flags(secondary: true, secondaryHold: true), primaryKeepsSaddle: true, secondaryKeepsSaddle: false);
        False(hidden.Any, "secondary press of a dismounting attack is not hidden");
    }

    private static void Test_KeepsSaddle_ProjectileAttacks()
    {
        True(SaddleRules.KeepsSaddle(isProjectile: true, hasAnimation: true, secondary: false), "crossbow / bow / staff");
        True(SaddleRules.KeepsSaddle(isProjectile: true, hasAnimation: true, secondary: true), "spear throw");
    }

    private static void Test_KeepsSaddle_MissingSecondaryDoesNothing()
    {
        // Bow / crossbow / staff have no secondary attack: on foot the press does nothing, so on a mount it must not
        // throw the rider off either.
        True(SaddleRules.KeepsSaddle(isProjectile: false, hasAnimation: false, secondary: true), "no secondary attack");
    }

    private static void Test_KeepsSaddle_MeleeStillDismounts()
    {
        False(SaddleRules.KeepsSaddle(isProjectile: false, hasAnimation: true, secondary: false), "sword swing");
        False(SaddleRules.KeepsSaddle(isProjectile: false, hasAnimation: true, secondary: true), "spear poke as a secondary, axe secondary");
    }

    private static void Test_KeepsSaddle_MissingPrimaryStaysVanilla()
    {
        False(SaddleRules.KeepsSaddle(isProjectile: false, hasAnimation: false, secondary: false), "only the secondary press is kept");
    }

    private static void Test_Hide_SpearThrowOnlySecondary()
    {
        // Spear: primary is a poke (melee), secondary is the throw.
        var input = Flags(attack: true, attackHold: false, secondary: true, secondaryHold: true);
        var hidden = SaddleRules.Hide(true, input, primaryKeepsSaddle: false, secondaryKeepsSaddle: true);
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

    private const float LongAgo = float.PositiveInfinity;

    private static void Test_Shooting_DuringAnAttackClip()
    {
        True(SaddleRules.Shooting(inAttack: true, sinceShotStart: LongAgo, drawTime: -1f, bowWeapon: false), "crossbow fire clip");
        True(SaddleRules.Shooting(inAttack: true, sinceShotStart: LongAgo, drawTime: -1f, bowWeapon: true), "bow fire clip");
    }

    private static void Test_Shooting_WhileDrawingABow()
    {
        // m_attackDrawTime: -1 = not drawing, 0 = draw about to start, > 0 = drawing / holding.
        True(SaddleRules.Shooting(false, LongAgo, 0.3f, bowWeapon: true), "bow drawn");
        False(SaddleRules.Shooting(false, LongAgo, 0f, bowWeapon: true), "draw not started yet");
        False(SaddleRules.Shooting(false, LongAgo, -1f, bowWeapon: true), "bow idle");
        False(SaddleRules.Shooting(false, LongAgo, 0.3f, bowWeapon: false), "stale draw time with a crossbow in hand");
    }

    private static void Test_Shooting_JustAfterTheShotStarts()
    {
        // The release (or click) starts the attack, but the fire clip only begins on a later animator update: in between
        // the draw timer is already 0 and the game isn't "in attack" yet. Without this the rider flicks back to the
        // animal's heading for a step or two.
        True(SaddleRules.Shooting(inAttack: false, sinceShotStart: 0.05f, drawTime: 0f, bowWeapon: true), "bow just released");
        True(SaddleRules.Shooting(false, 0.05f, -1f, bowWeapon: false), "crossbow just clicked");
    }

    private static void Test_Shooting_StartGraceRunsOut()
    {
        // An attack whose clip never begins mustn't keep the rider turned: the grace is short.
        False(SaddleRules.Shooting(false, 1f, -1f, false), "a second later with no clip");
        True(SaddleRules.ShotStartGrace > 0f && SaddleRules.ShotStartGrace <= 0.5f, "grace is a fraction of a second");
    }

    private static void Test_Shooting_IdleRider()
    {
        False(SaddleRules.Shooting(false, LongAgo, -1f, false), "just riding");
    }
}
