using System;
using MountedArchery;
using UnityEngine;

internal static partial class Tests
{
    private static void Near(Vector3 expected, Vector3 actual, string what)
    {
        if (Math.Abs(expected.x - actual.x) > 1e-4f || Math.Abs(expected.y - actual.y) > 1e-4f || Math.Abs(expected.z - actual.z) > 1e-4f)
            throw new Exception($"{what}: expected <{expected}>, got <{actual}>");
    }

    private static void Test_ShotShift_FromTheEyeToTheCamera()
    {
        // Logged in game on a Lox with a bow: the eye 1.81 m above the saddle, the seated camera's base 1.66 m (the head
        // 1.36 m + 0.3 m). The shot moves down by the gap.
        Near(new Vector3(0f, -0.15f, 0f), ShotRules.ShotShift(cameraBase: new Vector3(0f, 1.66f, 0f), eye: new Vector3(0f, 1.81f, 0f)), "bow on a Lox");
        Near(Vector3.zero, ShotRules.ShotShift(cameraBase: new Vector3(0f, 1.81f, 0f), eye: new Vector3(0f, 1.81f, 0f)), "camera at the eye (as on foot): no shift");
    }

    private static void Test_ShotShift_FollowsTheCameraAnywhere()
    {
        // The camera's base can sit forward or to the side of the eye (the seated head leans; the camera is still
        // settling after mounting): the whole gap counts.
        Near(new Vector3(0.1f, -0.15f, 0.2f), ShotRules.ShotShift(cameraBase: new Vector3(0.1f, 1.66f, 0.2f), eye: new Vector3(0f, 1.81f, 0f)), "leaning");
    }

    private static void Test_SeatedCameraBase_AboveTheHead()
    {
        // Where the seated camera settles (GameCamera.GetCameraBaseOffset: head + 0.3 m up), used when the camera's own
        // value can't be read.
        Near(new Vector3(0.1f, 1.66f, 0.2f), ShotRules.SeatedCameraBase(head: new Vector3(0.1f, 1.36f, 0.2f)), "head + 0.3 m up");
    }
}
