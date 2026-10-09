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

    private static void Test_LaunchVelocity_StandingStillIsVanilla()
    {
        var shot = new Vector3(0f, 0f, 60f);
        Near(shot, ShotRules.LaunchVelocity(shot, Vector3.zero), "no speed added");
    }

    private static void Test_LaunchVelocity_AddsTheRidersSpeedAlongTheAim()
    {
        // Moose galloping at 12.6 m/s along +z. Only the speed along the shot changes; its direction never does.
        var gallop = new Vector3(0f, 0f, 12.6f);
        Near(new Vector3(0f, 0f, 72.6f), ShotRules.LaunchVelocity(new Vector3(0f, 0f, 60f), gallop), "bow shot ahead: faster");
        Near(new Vector3(0f, 0f, -47.4f), ShotRules.LaunchVelocity(new Vector3(0f, 0f, -60f), gallop), "bow shot behind: slower");
        Near(new Vector3(60f, 0f, 0f), ShotRules.LaunchVelocity(new Vector3(60f, 0f, 0f), gallop), "to the side: unchanged, no drift");
        var up = new Vector3(0f, 30f, 30f);
        var launched = ShotRules.LaunchVelocity(up, gallop);
        Near(up.normalized, launched.normalized, "aimed up and ahead: same direction");
        True(Math.Abs(launched.magnitude - (up.magnitude + 12.6f * up.normalized.z)) < 1e-3f, $"aimed up and ahead: gains the gallop along the aim ({launched.magnitude})");
    }

    private static void Test_LaunchVelocity_NothingThatDoesntFly()
    {
        // The Staff of Protection launches its bubble at speed 0: it must stay where it is put.
        Near(Vector3.zero, ShotRules.LaunchVelocity(Vector3.zero, new Vector3(0f, 0f, 12.6f)), "speed 0 stays 0");
    }

    private static void Test_LaunchVelocity_NeverTurnsAShotAround()
    {
        // A barely drawn bow (2 m/s) shot behind at a gallop would go backwards: it keeps its direction at a crawl.
        var weak = new Vector3(0f, 0f, -2f);
        var launched = ShotRules.LaunchVelocity(weak, new Vector3(0f, 0f, 12.6f));
        Near(weak.normalized, launched.normalized, "same direction");
        True(launched.magnitude > 0f && launched.magnitude <= ShotRules.MinLaunchSpeed + 1e-4f, $"slowest launch {launched.magnitude}");
    }
}
