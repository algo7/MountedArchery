using UnityEngine;

namespace MountedArchery
{
    /// <summary>Picking items up from the saddle (PickupPatches), as plain vector math (no live Unity), so it's unit tested.</summary>
    internal static class PickupRules
    {
        /// <summary>Player.AutoPickup's pull speed (m/s) and pickup distance (m).</summary>
        public const float PullSpeed = 15f, PickupDistance = 0.3f;

        /// <summary>
        /// How far around a mount the rider picks items up: the game's own pickup range, out from the animal's body (its
        /// radius) instead of out from a player standing on the ground. A bigger animal sweeps a wider path; a mod that
        /// changes the game's range carries over.
        /// </summary>
        public static float Reach(float gameRange, float bodyRadius) => gameRange + Mathf.Max(0f, bodyRadius);

        /// <summary>
        /// How far an item moves this step towards the rider. The game pulls items at 15 m/s towards a player on foot,
        /// who is slow next to that; a galloping rider would outrun the pull and leave items hanging behind. The item also
        /// moves with the rider, so it closes in at 15 m/s relative to the rider, as on foot, at any speed.
        /// </summary>
        public static Vector3 Step(Vector3 toTarget, Vector3 riderVelocity, float dt)
            => toTarget.normalized * (PullSpeed * dt) + riderVelocity * dt;
    }
}
