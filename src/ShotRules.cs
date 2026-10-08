using UnityEngine;

namespace MountedArchery
{
    /// <summary>Where a rider's shot starts, as plain vector math (no live Unity), so it's unit tested.</summary>
    internal static class ShotRules
    {
        /// <summary>GameCamera.GetCameraBaseOffset: attached or sitting, the camera hangs this far above the head.</summary>
        public const float SeatedCameraAboveHead = 0.3f;

        /// <summary>
        /// The game lays a shot out from the body as if standing, against the eye, which stays at standing height. On
        /// foot the camera hangs off that eye; seated it hangs off the head instead, lower, so the crosshair's line is
        /// lower and a rider's shot started higher against it than on foot. Moving the shot from the eye to the seated
        /// camera puts it against the crosshair exactly as on foot.
        /// </summary>
        public static Vector3 SeatedShift(Vector3 head, Vector3 eye) => head + Vector3.up * SeatedCameraAboveHead - eye;
    }
}
