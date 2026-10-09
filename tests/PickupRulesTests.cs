using MountedArchery;
using UnityEngine;

internal static partial class Tests
{
    private static void Test_PickupReach_TheGamesRangeAroundTheAnimalsBody()
    {
        // From the saddle, items are picked up around the mount as the game does around a player on foot, out to the
        // game's own pickup range (2 m) beyond the animal's body radius (read from the game files).
        Eq(3.5f, PickupRules.Reach(gameRange: 2f, bodyRadius: 1.5f), "lox");
        Eq(2f + 1.08f, PickupRules.Reach(2f, 1.08f), "asksvin");
        Eq(5f, PickupRules.Reach(gameRange: 3.5f, bodyRadius: 1.5f), "another mod's longer pickup range carries over");
        Eq(2f, PickupRules.Reach(2f, -1f), "a negative radius never shrinks the game's range");
    }

    private static void Test_PickupPull_SameAsTheGame()
    {
        // Vanilla Player.AutoPickup pulls an item at 15 m/s and picks it up within 0.3 m (pinned against its code).
        Eq(15f, PickupRules.PullSpeed, "pull speed");
        Eq(0.3f, PickupRules.PickupDistance, "pickup distance");
    }

    private static void Test_PickupStep_StandingStillIsTheGamesPull()
    {
        var step = PickupRules.Step(new Vector3(0f, 0f, 3f), Vector3.zero, 0.02f);
        Near(new Vector3(0f, 0f, 0.3f), step, "15 m/s towards the rider");
    }

    private static void Test_PickupStep_AGallopingRiderDoesNotOutrunIt()
    {
        // Rider galloping at 12 m/s along +z, the item 3 m behind and 2 m below: after the step the item is 0.3 m (15 m/s
        // for 0.02 s) closer to where the rider is now, as if the rider stood still.
        var item = Vector3.zero;
        var rider = new Vector3(0f, 2f, 3f);
        var velocity = new Vector3(0f, 0f, 12f);
        const float dt = 0.02f;
        var before = Vector3.Distance(item, rider);
        var moved = item + PickupRules.Step(rider - item, velocity, dt);
        var after = Vector3.Distance(moved, rider + velocity * dt);
        True(Mathf.Abs(before - after - 0.3f) < 1e-4f, $"closes in 0.3 m per step at a gallop: {before} -> {after}");
    }
}
