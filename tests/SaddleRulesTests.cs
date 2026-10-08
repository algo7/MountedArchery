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

    private static void Test_AttackPlaying_UntilTheClipIsOver()
    {
        True(SaddleRules.AttackPlaying(inAttack: true, sinceShotStart: LongAgo), "thunder staff clip still playing");
        True(SaddleRules.AttackPlaying(inAttack: false, sinceShotStart: 0.05f), "just clicked, the clip hasn't begun");
        False(SaddleRules.AttackPlaying(inAttack: false, sinceShotStart: LongAgo), "clip over");
        False(SaddleRules.AttackPlaying(inAttack: false, sinceShotStart: 1f), "a second later with no clip");
    }

    private static void Test_PoseBack_AtTheShot()
    {
        // The seat's pose stays off from the shot's start so a slow throw reaches its shot, and comes back as soon as the
        // shot has fired: the rest of the clip blends into the seat, as a bow shot always did.
        False(SaddleRules.PoseBack(shotFired: false, keepsFiring: false, inAttack: true, sinceShotStart: 0.6f), "thunder staff winding up");
        False(SaddleRules.PoseBack(false, false, inAttack: false, sinceShotStart: 0.05f), "just clicked, the clip hasn't begun");
        True(SaddleRules.PoseBack(shotFired: true, keepsFiring: false, inAttack: true, sinceShotStart: 0.8f), "thunder staff fired");
        True(SaddleRules.PoseBack(shotFired: true, false, inAttack: false, sinceShotStart: 0.05f), "crossbow fired at once");
    }

    private static void Test_SeatPoseOff_ForAShotOnlyWhenTheFullBodyLayerPlaysIt()
    {
        // The seat's pose only takes clips on the full-body layer. The Frost staff's stream (and the shovel) play on the
        // upper-body layer while the legs stay in the seat: turning the pose off there would stand the rider up.
        False(SaddleRules.SeatPoseOff(bowDrawn: false, shotInProgress: true, poseOffNow: false, fullBodyInAttack: false, poseBack: false), "Frost staff stream: legs stay seated");
        True(SaddleRules.SeatPoseOff(false, shotInProgress: true, poseOffNow: false, fullBodyInAttack: true, poseBack: false), "thunder staff clip entering");
        True(SaddleRules.SeatPoseOff(false, shotInProgress: true, poseOffNow: true, fullBodyInAttack: true, poseBack: false), "thunder staff winding up");
        False(SaddleRules.SeatPoseOff(false, shotInProgress: true, poseOffNow: true, fullBodyInAttack: true, poseBack: true), "fired: back in the seat");
        False(SaddleRules.SeatPoseOff(false, shotInProgress: false, poseOffNow: false, fullBodyInAttack: false, poseBack: false), "just riding");
    }

    private static void Test_SeatPoseOff_WhileABowIsDrawn()
    {
        // The game's draw pose only starts from standing: while a bow is drawn the rider stands to draw, and stays up from
        // the release until the arrow has left, then sits back.
        True(SaddleRules.SeatPoseOff(bowDrawn: true, shotInProgress: false, poseOffNow: false, fullBodyInAttack: false, poseBack: false), "drawing");
        True(SaddleRules.SeatPoseOff(bowDrawn: false, shotInProgress: true, poseOffNow: true, fullBodyInAttack: false, poseBack: false), "released, fire clip not begun yet");
        False(SaddleRules.SeatPoseOff(bowDrawn: false, shotInProgress: true, poseOffNow: true, fullBodyInAttack: true, poseBack: true), "arrow away");
        True(SaddleRules.SeatPoseOff(bowDrawn: true, shotInProgress: true, poseOffNow: true, fullBodyInAttack: true, poseBack: true), "drawing the next one at once");
        False(SaddleRules.SeatPoseOff(bowDrawn: false, shotInProgress: false, poseOffNow: true, fullBodyInAttack: false, poseBack: false), "draw let go without a shot");
    }

    private static void Test_PoseBack_AnAttackThatKeepsFiringKeepsItsClip()
    {
        // A looping attack on the full-body layer (none in vanilla; a modded one may) fires again and again while the
        // button is held; the seat's pose taking its clip would end it after the first shot.
        False(SaddleRules.PoseBack(shotFired: true, keepsFiring: true, inAttack: true, sinceShotStart: 2f), "stream still flowing");
        True(SaddleRules.PoseBack(shotFired: true, keepsFiring: true, inAttack: false, sinceShotStart: 2f), "button let go, clip over");
        // Same for several bursts from one shot (no vanilla player weapon; a modded one may): the game stops firing the
        // remaining bursts once the attack's clip is gone.
        False(SaddleRules.PoseBack(shotFired: true, keepsFiring: true, inAttack: true, sinceShotStart: 0.6f), "first of three bursts out");
    }

    private static void Test_PoseBack_WhenTheClipIsOverOrNeverBegan()
    {
        // Staggered before the shot, or the clip never began: the pose must not stay off.
        True(SaddleRules.PoseBack(shotFired: false, keepsFiring: false, inAttack: false, sinceShotStart: LongAgo), "clip over without a shot");
        True(SaddleRules.PoseBack(false, false, inAttack: false, sinceShotStart: 1f), "a second later with no clip");
    }

    private static void Test_Shooting_IsAPlayingAttackOrADrawnBow()
    {
        // A drawn bow turns the rider to the aim, but isn't an attack clip: the rider draws seated.
        True(SaddleRules.Shooting(false, LongAgo, 0.3f, bowWeapon: true) && !SaddleRules.AttackPlaying(false, LongAgo), "drawing: turned, still seated");
    }

    private static void Test_PickupReach_TheGamesRangeAroundTheAnimalsBody()
    {
        // From the saddle, items are picked up around the mount as the game does around a player on foot, out to the
        // game's own pickup range (2 m) beyond the animal's body radius (read from the game files).
        Eq(3.5f, SaddleRules.PickupReach(gameRange: 2f, bodyRadius: 1.5f), "lox");
        Eq(3.08f, SaddleRules.PickupReach(2f, 1.08f), "asksvin");
        Eq(5f, SaddleRules.PickupReach(gameRange: 3.5f, bodyRadius: 1.5f), "another mod's longer pickup range carries over");
        Eq(2f, SaddleRules.PickupReach(2f, -1f), "a negative radius never shrinks the game's range");
    }

    private static void Test_PickupPull_SameAsTheGame()
    {
        // Vanilla Player.AutoPickup pulls an item at 15 m/s and picks it up within 0.3 m (pinned against its code).
        Eq(15f, SaddleRules.PickupPullSpeed, "pull speed");
        Eq(0.3f, SaddleRules.PickupDistance, "pickup distance");
    }
}
