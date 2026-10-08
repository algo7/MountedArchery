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
        /// lower and a rider's shot started higher against it than on foot. Moving the shot by the gap from the eye to
        /// the camera's base puts it against the crosshair exactly as on foot.
        /// </summary>
        public static Vector3 ShotShift(Vector3 cameraBase, Vector3 eye) => cameraBase - eye;

        /// <summary>Where a seated camera's base settles: above the head (used when the camera's own value can't be read).</summary>
        public static Vector3 SeatedCameraBase(Vector3 head) => head + Vector3.up * SeatedCameraAboveHead;
    }
}
