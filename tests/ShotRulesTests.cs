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

    private static void Test_SeatedShift_DownToTheSeatedCamera()
    {
        // Logged in game on a Lox with a bow: the eye 1.81 m above the saddle, the head 1.36 m. Seated, the camera hangs
        // 0.3 m above the head, 0.15 m below the eye: the shot moves down by that much.
        Near(new Vector3(0f, -0.15f, 0f), ShotRules.SeatedShift(head: new Vector3(0f, 1.36f, 0f), eye: new Vector3(0f, 1.81f, 0f)), "bow on a Lox");
        Near(new Vector3(0f, -0.33f, 0f), ShotRules.SeatedShift(head: new Vector3(0f, 1.18f, 0f), eye: new Vector3(0f, 1.81f, 0f)), "staff (head lower)");
    }

    private static void Test_SeatedShift_FollowsTheHeadsLean()
    {
        // The camera follows the head wherever the seated pose puts it, forward or to the side too.
        Near(new Vector3(0.1f, -0.15f, 0.2f), ShotRules.SeatedShift(head: new Vector3(0.1f, 1.36f, 0.2f), eye: new Vector3(0f, 1.81f, 0f)), "leaning");
    }

    private static void Test_SeatedShift_CameraAboveTheHeadAsInTheGame()
    {
        // GameCamera.GetCameraBaseOffset: attached or sitting, head + Vector3.up * 0.3f (a literal, not readable).
        Eq(0.3f, ShotRules.SeatedCameraAboveHead, "seated camera height above the head");
    }
}
