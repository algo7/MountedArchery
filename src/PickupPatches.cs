using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// Picking items up from the saddle (a thrown spear, loot). Player.AutoPickup looks for items within the game's
    /// pickup range (2 m) of a point 1 m above the player's feet; seated on an animal that point is too high to reach the
    /// ground. Right after it, for a rider, the same pass runs again around the mount: 1 m above the animal's feet, out to
    /// the game's range beyond the animal's body (its collider's radius, not Character.GetRadius, which multiplies by the
    /// scale vector's length, 1.73 at scale 1; PickupRules.Reach). Everything else is the game's own rules: the
    /// auto-pickup toggle, items the player dropped, unique items, a free slot and the carry weight, items in tar, and
    /// ownership. Items fly to the rider (where the game pulls them on foot, 1 m above the player's feet), moving with the
    /// rider too, so a gallop doesn't outrun them (PickupRules.Step), and are picked up there with Humanoid.Pickup. An item picked up by either pass loses its network object at once, so the other
    /// pass skips it. Never throws.
    /// </summary>
    [HarmonyPatch]
    internal static class PickupPatches
    {
        private static readonly Collider[] s_colliders = new Collider[100];

        private static bool s_errorLogged;

        [HarmonyPatch(typeof(Player), "AutoPickup")]
        [HarmonyPostfix]
        private static void AroundTheMount(Player __instance, float dt)
        {
            try
            {
                if (!Riding.IsRiding(__instance) || __instance.IsTeleporting() || !Riding.AutoPickupOn()) return;
                var sadle = __instance.GetDoodadController() as Sadle;
                var mount = sadle != null ? sadle.GetCharacter() : null;
                if (mount == null) return;
                var collider = mount.GetCollider();
                var centre = mount.transform.position + Vector3.up;
                var reach = PickupRules.Reach(__instance.m_autoPickupRange, collider != null ? collider.radius : 0f);
                // Items fly to the rider, where the game pulls them on foot; on a mount so tall that the rider is out of
                // reach, they fly to the centre instead, so none stalls at the edge.
                var hand = __instance.transform.position + Vector3.up;
                var target = Vector3.Distance(hand, centre) <= reach ? hand : centre;
                var velocity = __instance.GetVelocity();
                var inventory = __instance.GetInventory();
                var count = Physics.OverlapSphereNonAlloc(centre, reach, s_colliders, Riding.AutoPickupMask(__instance));
                for (var i = 0; i < count; i++)
                {
                    var body = s_colliders[i].attachedRigidbody;
                    if (!body) continue;
                    var item = body.GetComponent<ItemDrop>();
                    FloatingTerrainDummy dummy = null;
                    if (item == null && (dummy = body.GetComponent<FloatingTerrainDummy>()) && dummy.m_parent != null)
                        item = dummy.m_parent.GetComponent<ItemDrop>();
                    if (item == null || !item.m_autoPickup || item.IsPiece() || __instance.HaveUniqueKey(item.m_itemData.m_shared.m_name)) continue;
                    var view = item.GetComponent<ZNetView>();
                    if (view == null || !view.IsValid()) continue;
                    if (!item.CanPickup())
                    {
                        item.RequestOwn();
                        continue;
                    }
                    if (item.InTar()) continue;
                    item.Load();
                    if (!inventory.CanAddItem(item.m_itemData) || item.m_itemData.GetWeight() + inventory.GetTotalWeight() > __instance.GetMaxCarryWeight()) continue;
                    if (Vector3.Distance(item.transform.position, centre) > reach) continue;
                    var toTarget = target - item.transform.position;
                    if (toTarget.magnitude < PickupRules.PickupDistance)
                    {
                        __instance.Pickup(item.gameObject);
                        continue;
                    }
                    var step = PickupRules.Step(toTarget, velocity, dt);
                    item.transform.position += step;
                    if (dummy) dummy.transform.position += step;
                }
            }
            catch (Exception e)
            {
                if (s_errorLogged) return;
                s_errorLogged = true;
                Plugin.Log.LogError($"Picking items up from the saddle failed (logged once): {e}");
            }
        }
    }
}
