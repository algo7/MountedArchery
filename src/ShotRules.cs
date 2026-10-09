using UnityEngine;

namespace MountedArchery
{
    /// <summary>A rider's shot (where it starts, how fast it leaves), as plain vector math (no live Unity), so it's unit tested.</summary>
    internal static class ShotRules
    {
        /// <summary>GameCamera.GetCameraBaseOffset: attached or sitting, the camera hangs this far above the head.</summary>
        public const float SeatedCameraAboveHead = 0.3f;

        /// <summary>
        /// The game lays a shot out from the body as if standing, against the eye, which stays at standing height. On
        /// foot the camera hangs off that eye; seated it hangs off the head instead, lower, so the crosshair's line is
        /// lower and a rider's shot started higher against it than on foot. Moving the shot by the gap from the eye to
        /// the camera's base puts it against the crosshair exactly as on foot.
        /// </summary>
        public static Vector3 ShotShift(Vector3 cameraBase, Vector3 eye) => cameraBase - eye;

        /// <summary>Where a seated camera's base settles: above the head (used when the camera's own value can't be read).</summary>
        public static Vector3 SeatedCameraBase(Vector3 head) => head + Vector3.up * SeatedCameraAboveHead;

        /// <summary>The slowest a rider's shot leaves (m/s): shooting behind faster than the shot flies never turns it around.</summary>
        public const float MinLaunchSpeed = 1f;

        /// <summary>
        /// A rider's shot as it leaves (LaunchPatches). The game launches every shot at its own speed, whatever the
        /// shooter's; from a galloping animal a forward throw then crawls ahead and the rider is on it a moment later. The
        /// rider's speed along the shot is added: ahead it flies faster, behind slower (never below MinLaunchSpeed, or its
        /// own speed if that is lower), to the side unchanged. The direction never changes, so the crosshair stays true.
        /// Something launched at speed 0 (the Staff of Protection's bubble) stays put.
        /// </summary>
        public static Vector3 LaunchVelocity(Vector3 velocity, Vector3 riderVelocity)
        {
            var speed = velocity.magnitude;
            if (speed < 0.01f) return velocity;
            var direction = velocity / speed;
            return direction * Mathf.Max(Mathf.Min(speed, MinLaunchSpeed), speed + Vector3.Dot(riderVelocity, direction));
        }
    }
}
