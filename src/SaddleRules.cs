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
        /// Whether pressing this attack's button keeps the rider in the saddle: a projectile attack, or a secondary
        /// button on a weapon that has no secondary attack (on foot that press does nothing, so it mustn't throw the
        /// rider off either). Melee keeps vanilla's dismount.
        /// </summary>
        public static bool KeepsSaddle(bool isProjectile, bool hasAnimation, bool secondary)
            => isProjectile || (secondary && !hasAnimation);

        /// <summary>
        /// The flags to hide from vanilla's dismount check: the primary press/hold when the primary attack keeps the
        /// saddle, the secondary ones when the secondary does (see KeepsSaddle). On foot nothing is hidden. Jump and
        /// dodge are not flags here: they dismount as always.
        /// </summary>
        public static AttackFlags Hide(bool riding, AttackFlags input, bool primaryKeepsSaddle, bool secondaryKeepsSaddle)
        {
            var hidden = default(AttackFlags);
            if (!riding) return hidden;
            if (primaryKeepsSaddle)
            {
                hidden.Attack = input.Attack;
                hidden.AttackHold = input.AttackHold;
            }
            if (secondaryKeepsSaddle)
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

        /// <summary>Seconds after a shot starts during which the rider stays turned before its clip has begun.</summary>
        public const float ShotStartGrace = 0.3f;

        /// <summary>
        /// A rider's attack clip is playing (only projectile attacks can start while riding), or a shot started a moment
        /// ago and its clip hasn't begun yet (the release or click starts the attack, the animator only enters the clip on
        /// a later update).
        /// </summary>
        public static bool AttackPlaying(bool inAttack, float sinceShotStart) => inAttack || sinceShotStart < ShotStartGrace;

        /// <summary>
        /// When the seat's pose goes off for a rider's shot (RidePosePatches): once the full-body layer is in the attack
        /// clip, the only clips the pose takes back, and while the pose isn't due back yet (see PoseBack). An attack on the
        /// upper-body layer (the Frost staff's stream, the shovel) leaves the legs in the seat and the pose alone.
        /// </summary>
        public static bool PoseOff(bool fullBodyInAttack, bool poseBack) => fullBodyInAttack && !poseBack;

        /// <summary>
        /// When the seat's pose comes back after a rider's shot started (RidePosePatches): as soon as the shot has fired,
        /// so the rest of the clip blends into the seat; for an attack that keeps firing after its first shot (a looping
        /// attack, fired again and again while the button is held, or one of several bursts: the game stops firing once
        /// the attack is no longer playing) only once its clip is over; and in any case once the attack is no longer
        /// playing (staggered, or the clip never began).
        /// </summary>
        public static bool PoseBack(bool shotFired, bool keepsFiring, bool inAttack, float sinceShotStart)
            => (shotFired && !keepsFiring) || !AttackPlaying(inAttack, sinceShotStart);

        /// <summary>Player.AutoPickup's pull speed (m/s) and pickup distance (m), mirrored by PickupPatches.</summary>
        public const float PickupPullSpeed = 15f, PickupDistance = 0.3f;

        /// <summary>
        /// How far around a mount the rider picks items up (PickupPatches): the game's own pickup range, out from the
        /// animal's body (its radius) instead of out from a player standing on the ground. A bigger animal sweeps a
        /// wider path; a mod that changes the game's range carries over.
        /// </summary>
        public static float PickupReach(float gameRange, float bodyRadius) => gameRange + System.Math.Max(0f, bodyRadius);

        /// <summary>
        /// When the rider faces the aim instead of the saddle: an attack is playing (see AttackPlaying), or a bow is drawn
        /// (the game's draw timer is above 0 while drawing or holding; -1 idle, 0 about to start) and the weapon in hand
        /// is the bow.
        /// </summary>
        public static bool Shooting(bool inAttack, float sinceShotStart, float drawTime, bool bowWeapon)
            => AttackPlaying(inAttack, sinceShotStart) || (bowWeapon && drawTime > 0f);
    }
}
