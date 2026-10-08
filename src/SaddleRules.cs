namespace MountedArchery
{
    /// <summary>The four attack flags Player.SetControls receives: press and hold, primary and secondary.</summary>
    internal struct AttackFlags
    {
        public bool Attack;
        public bool AttackHold;
        public bool Secondary;
        public bool SecondaryHold;

        public bool Any => Attack || AttackHold || Secondary || SecondaryHold;
    }

    /// <summary>
    /// The mod's decisions as plain rules (no Unity), so they're unit tested. The patches gather the facts (riding, the
    /// weapon's attack types, the animator state) and apply what these return.
    /// </summary>
    internal static class SaddleRules
    {
        /// <summary>
        /// An attack that fires a projectile: the game's own Projectile kind, with an animation (the game's own test for
        /// "this weapon has that attack"; an unused secondary slot has none, whatever type it carries). A bow (a
        /// projectile attack with a draw) counts only while the bow-draw hook is installed. Anything else keeps its press
        /// vanilla (dismount), never a dead click.
        /// </summary>
        public static bool IsProjectile(Attack.AttackType type, bool bowDraw, bool bowsEnabled, bool hasAnimation)
        {
            if (!hasAnimation || type != Attack.AttackType.Projectile) return false;
            return !bowDraw || bowsEnabled;
        }

        /// <summary>
        /// The flags to hide from vanilla's dismount check: the primary press/hold when the primary attack is a
        /// projectile, the secondary ones when the secondary attack is. On foot nothing is hidden. Jump and dodge are
        /// not flags here: they dismount as always.
        /// </summary>
        public static AttackFlags Hide(bool riding, AttackFlags input, bool primaryIsProjectile, bool secondaryIsProjectile)
        {
            var hidden = default(AttackFlags);
            if (!riding) return hidden;
            if (primaryIsProjectile)
            {
                hidden.Attack = input.Attack;
                hidden.AttackHold = input.AttackHold;
            }
            if (secondaryIsProjectile)
            {
                hidden.Secondary = input.Secondary;
                hidden.SecondaryHold = input.SecondaryHold;
            }
            return hidden;
        }

        /// <summary>What vanilla's SetControls gets: the input minus the hidden flags.</summary>
        public static AttackFlags Remaining(AttackFlags input, AttackFlags hidden) => new AttackFlags
        {
            Attack = input.Attack && !hidden.Attack,
            AttackHold = input.AttackHold && !hidden.AttackHold,
            Secondary = input.Secondary && !hidden.Secondary,
            SecondaryHold = input.SecondaryHold && !hidden.SecondaryHold,
        };

        /// <summary>
        /// After vanilla's checks: the hidden flags go into the player's attack fields only while still in the saddle. A
        /// jump or dodge in the same tick dismounted the player; the press is then dropped, not applied standing.
        /// </summary>
        public static AttackFlags Restore(bool stillAttached, AttackFlags hidden) => stillAttached ? hidden : default;

        /// <summary>
        /// When the rider faces the aim instead of the saddle: an attack clip is playing (only projectile attacks can
        /// start while riding), or a bow is drawn (the game's draw timer is above 0 while drawing or holding; -1 idle,
        /// 0 about to start) and the weapon in hand is the bow.
        /// </summary>
        public static bool Shooting(bool inAttack, float drawTime, bool bowWeapon) => inAttack || (bowWeapon && drawTime > 0f);
    }
}
