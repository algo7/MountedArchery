using System;
using HarmonyLib;
using UnityEngine;

namespace MountedArchery
{
    /// <summary>
    /// What the patches share: is this the local player sitting on a saddled animal, which of the weapon's attacks fire
    /// a projectile, and the game's private members, looked up once in Prepare. Plugin.Install calls Prepare before
    /// patching anything: if a game update removed a member, nothing is installed and the game stays vanilla.
    /// </summary>
    internal static class Riding
    {
        /// <summary>Player.m_attached: true while sitting on something (saddle, chair, ship's rudder...).</summary>
        internal static AccessTools.FieldRef<Player, bool> Attached;

        /// <summary>Character.m_attack / m_attackHold / m_secondaryAttack / m_secondaryAttackHold: what the attack update reads.</summary>
        internal static AccessTools.FieldRef<Character, bool> AttackFlag, AttackHoldFlag, SecondaryFlag, SecondaryHoldFlag;

        /// <summary>Humanoid.m_attackDrawTime: -1 idle, 0 about to start, above 0 drawing or holding a bow.</summary>
        internal static AccessTools.FieldRef<Humanoid, float> DrawTime;

        /// <summary>Character.m_body: the rigidbody whose rotation Attack.Start also sets.</summary>
        internal static AccessTools.FieldRef<Character, Rigidbody> Body;

        /// <summary>
        /// Character.GetNextOrCurrentAnimHash(): the tag of the full-body (base) layer's state, or of the one it is going
        /// to; the animator state CanMove looks at. Refreshed every physics step and every frame.
        /// </summary>
        internal static Func<Character, int> NextOrCurrentAnimHash;

        /// <summary>Character.s_animatorTagSitting: the tag of the ride (and chair) states.</summary>
        internal static int SittingTag;

        /// <summary>Humanoid.s_animatorTagAttack: the tag of the attack states.</summary>
        internal static int AttackTag;

        /// <summary>Attack.m_character: who is attacking.</summary>
        internal static AccessTools.FieldRef<Attack, Humanoid> Attacker;

        /// <summary>Player.UpdateEyeRotation(): points the eye (a child of the body) along the mouse look again.</summary>
        internal static Action<Player> AimEye;

        /// <summary>Player.m_attachAnimation: the animator bool of the seat's pose (attach_lox, attach_moose...).</summary>
        internal static AccessTools.FieldRef<Player, string> AttachAnimation;

        /// <summary>Character.m_zanim: sets animator parameters and syncs them to the other players.</summary>
        internal static AccessTools.FieldRef<Character, ZSyncAnimation> Anim;

        /// <summary>
        /// GameCamera.m_currentBaseOffset: where the camera hangs from, relative to the player, as on screen (the
        /// game eases it towards the head + 0.3 m when seated). Optional: null if a game update removed it.
        /// </summary>
        internal static AccessTools.FieldRef<GameCamera, Vector3> CameraBaseOffset;

        /// <summary>Player.m_hoveringCreature: the creature under the crosshair, whose health EnemyHud shows. Optional.</summary>
        internal static AccessTools.FieldRef<Player, Character> HoverCreature;

        /// <summary>Player.m_interactMask: the layers the game's crosshair ray (FindHoverObject) hits. Optional.</summary>
        internal static AccessTools.FieldRef<Player, int> InteractMask;

        /// <summary>Player.m_enableAutoPickup (static): the player's auto-pickup toggle. Optional.</summary>
        internal static AccessTools.FieldRef<bool> AutoPickupOn;

        /// <summary>Player.m_autoPickupMask: the layers auto-pickup looks for items on. Optional.</summary>
        internal static AccessTools.FieldRef<Player, int> AutoPickupMask;

        /// <summary>False once the bow-draw hook failed to install: bows then dismount as vanilla.</summary>
        internal static bool BowsEnabled = true;

        /// <summary>
        /// Looks the private members up once. False (one log line) if a game update removed a required one; a missing
        /// camera offset only logs a warning (SeatedCameraPatches then uses where the camera settles), and so do missing
        /// health bar or pickup fields (HoverPatches / PickupPatches are then not installed).
        /// </summary>
        internal static bool Prepare()
        {
            if (Attached != null) return true;
            try
            {
                Attached = AccessTools.FieldRefAccess<Player, bool>("m_attached");
                AttackFlag = AccessTools.FieldRefAccess<Character, bool>("m_attack");
                AttackHoldFlag = AccessTools.FieldRefAccess<Character, bool>("m_attackHold");
                SecondaryFlag = AccessTools.FieldRefAccess<Character, bool>("m_secondaryAttack");
                SecondaryHoldFlag = AccessTools.FieldRefAccess<Character, bool>("m_secondaryAttackHold");
                DrawTime = AccessTools.FieldRefAccess<Humanoid, float>("m_attackDrawTime");
                Body = AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");
                var hash = AccessTools.Method(typeof(Character), "GetNextOrCurrentAnimHash");
                if (hash == null) throw new MissingMethodException("Character.GetNextOrCurrentAnimHash");
                NextOrCurrentAnimHash = AccessTools.MethodDelegate<Func<Character, int>>(hash);
                SittingTag = AccessTools.StaticFieldRefAccess<int>(typeof(Character), "s_animatorTagSitting");
                AttackTag = AccessTools.StaticFieldRefAccess<int>(typeof(Humanoid), "s_animatorTagAttack");
                Attacker = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
                var eye = AccessTools.DeclaredMethod(typeof(Player), "UpdateEyeRotation");
                if (eye == null) throw new MissingMethodException("Player.UpdateEyeRotation");
                AimEye = AccessTools.MethodDelegate<Action<Player>>(eye, virtualCall: false);
                AttachAnimation = AccessTools.FieldRefAccess<Player, string>("m_attachAnimation");
                Anim = AccessTools.FieldRefAccess<Character, ZSyncAnimation>("m_zanim");
            }
            catch (Exception e)
            {
                Attached = null;
                Plugin.Log.LogError($"Mounted shooting is off: a game member the hooks need wasn't found (a game update?): {e.Message}");
                return false;
            }
            try
            {
                CameraBaseOffset = AccessTools.FieldRefAccess<GameCamera, Vector3>("m_currentBaseOffset");
            }
            catch (Exception e)
            {
                CameraBaseOffset = null;
                Plugin.Log.LogWarning($"Shots from the saddle line up with where the camera settles, not where it is (a game update?): {e.Message}");
            }
            try
            {
                HoverCreature = AccessTools.FieldRefAccess<Player, Character>("m_hoveringCreature");
                InteractMask = AccessTools.FieldRefAccess<Player, int>("m_interactMask");
            }
            catch (Exception e)
            {
                HoverCreature = null;
                InteractMask = null;
                Plugin.Log.LogWarning($"Enemies' health bars won't show while riding (a game update?): {e.Message}");
            }
            try
            {
                var toggle = AccessTools.Field(typeof(Player), "m_enableAutoPickup");
                if (toggle == null || !toggle.IsStatic) throw new MissingFieldException("Player.m_enableAutoPickup");
                AutoPickupOn = AccessTools.StaticFieldRefAccess<bool>(toggle);
                AutoPickupMask = AccessTools.FieldRefAccess<Player, int>("m_autoPickupMask");
            }
            catch (Exception e)
            {
                AutoPickupOn = null;
                AutoPickupMask = null;
                Plugin.Log.LogWarning($"Items won't be picked up from the saddle (a game update?): {e.Message}");
            }
            return true;
        }

        /// <summary>
        /// The local player, attached, and steering a saddle (never a ship's rudder or a cart). Player.IsRiding is the
        /// game's own test: a live saddle, so not one whose animal was just destroyed.
        /// </summary>
        internal static bool IsRiding(Player player)
        {
            if (player == null || player != Player.m_localPlayer || Attached == null || !Attached(player)) return false;
            return player.IsRiding();
        }

        /// <summary>
        /// The weapon's primary or secondary attack slot, or null without a weapon. A slot is never null on a weapon; an
        /// attack the weapon doesn't have is a slot with no animation (see IsProjectile).
        /// </summary>
        internal static Attack AttackOf(ItemDrop.ItemData weapon, bool secondary)
        {
            var shared = weapon?.m_shared;
            if (shared == null) return null;
            return secondary ? shared.m_secondaryAttack : shared.m_attack;
        }

        /// <summary>An attack that fires a projectile and may be used from the saddle (see SaddleRules.IsProjectile).</summary>
        internal static bool IsProjectile(Attack attack)
            => attack != null && SaddleRules.IsProjectile(attack.m_attackType, attack.m_bowDraw, BowsEnabled, !string.IsNullOrEmpty(attack.m_attackAnimation));

        /// <summary>Pressing this slot's button keeps the rider in the saddle (see SaddleRules.KeepsSaddle).</summary>
        internal static bool KeepsSaddle(Attack attack, bool secondary)
            => attack != null && SaddleRules.KeepsSaddle(IsProjectile(attack), !string.IsNullOrEmpty(attack.m_attackAnimation), secondary);

        /// <summary>A bow: a projectile attack with a draw.</summary>
        internal static bool IsBow(Attack attack) => attack != null && attack.m_attackType == Attack.AttackType.Projectile && attack.m_bowDraw;
    }
}
