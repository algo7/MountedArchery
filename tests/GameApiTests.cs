using System;
using System.Linq;
using System.Reflection;
using MountedArchery;
using UnityEngine;

/// <summary>
/// The game members the patches rely on, checked against the game DLL: a game update that renames or retypes one fails
/// here (CI builds against the dedicated server's DLLs) instead of silently in game.
/// </summary>
internal static partial class Tests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Field(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, Instance);
        True(field != null, $"{type.Name}.{name} exists");
        Eq(fieldType, field.FieldType, $"{type.Name}.{name} type");
    }

    private static void StaticField(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, Static);
        True(field != null, $"{type.Name}.{name} (static) exists");
        Eq(fieldType, field.FieldType, $"{type.Name}.{name} type");
    }

    private static MethodInfo Method(Type type, string name)
    {
        var methods = type.GetMethods(Instance).Where(m => m.Name == name && m.DeclaringType == type).ToArray();
        Eq(1, methods.Length, $"{type.Name}.{name}: one method (Harmony patches it by name)");
        return methods[0];
    }

    private static void Param(MethodInfo method, string name, Type type)
    {
        var p = method.GetParameters().FirstOrDefault(x => x.Name == name);
        True(p != null, $"{method.DeclaringType.Name}.{method.Name} has '{name}'");
        Eq(type, p.ParameterType, $"{method.DeclaringType.Name}.{method.Name} '{name}' type");
    }

    private static void Test_Game_Riding()
    {
        Field(typeof(Player), "m_attached", typeof(bool));
        Eq(typeof(IDoodadController), Method(typeof(Player), "GetDoodadController").ReturnType, "Player.GetDoodadController()");
        True(typeof(IDoodadController).IsAssignableFrom(typeof(Sadle)), "Sadle is a doodad controller");
        // The game's own "riding": a live (not destroyed) doodad controller that is a Sadle.
        var riding = Method(typeof(Player), "IsRiding");
        True(riding.IsPublic && riding.ReturnType == typeof(bool) && riding.GetParameters().Length == 0, "Player.IsRiding() is public bool");
        Eq(typeof(ItemDrop.ItemData), typeof(Humanoid).GetMethod("GetCurrentWeapon", Type.EmptyTypes)?.ReturnType, "Humanoid.GetCurrentWeapon()");
    }

    private static void Test_Game_TheWeaponsAttacks()
    {
        Field(typeof(ItemDrop.ItemData), "m_shared", typeof(ItemDrop.ItemData.SharedData));
        Field(typeof(ItemDrop.ItemData.SharedData), "m_attack", typeof(Attack));
        Field(typeof(ItemDrop.ItemData.SharedData), "m_secondaryAttack", typeof(Attack));
        Field(typeof(Attack), "m_attackType", typeof(Attack.AttackType));
        Field(typeof(Attack), "m_bowDraw", typeof(bool));
        Field(typeof(Attack), "m_attackAnimation", typeof(string));
        True(typeof(ItemDrop.ItemData).GetMethod("HaveSecondaryAttack", System.Type.EmptyTypes) != null, "ItemData.HaveSecondaryAttack() (the game's own slot test, by animation)");
        True(Enum.IsDefined(typeof(Attack.AttackType), "Projectile"), "Attack.AttackType.Projectile");
    }

    private static void Test_Game_TheControls()
    {
        var setControls = Method(typeof(Player), "SetControls");
        True(setControls.IsPublic, "Player.SetControls is public (nameof)");
        Param(setControls, "attack", typeof(bool));
        Param(setControls, "attackHold", typeof(bool));
        Param(setControls, "secondaryAttack", typeof(bool));
        Param(setControls, "secondaryAttackHold", typeof(bool));
        Param(setControls, "jump", typeof(bool));
        Field(typeof(Character), "m_attack", typeof(bool));
        Field(typeof(Character), "m_attackHold", typeof(bool));
        Field(typeof(Character), "m_secondaryAttack", typeof(bool));
        Field(typeof(Character), "m_secondaryAttackHold", typeof(bool));
    }

    private static void Test_Game_TheAttackStart()
    {
        var start = Method(typeof(Humanoid), "StartAttack");
        True(start.IsPublic, "Humanoid.StartAttack is public (nameof)");
        Param(start, "secondaryAttack", typeof(bool));
        var canMove = Method(typeof(Character), "CanMove");
        True(canMove.IsPublic && canMove.IsVirtual, "Character.CanMove is public virtual (Player's override calls it)");
        True(typeof(Player).GetMethod("CanMove", Type.EmptyTypes)?.DeclaringType == typeof(Player), "Player overrides CanMove");
        Eq(typeof(int), typeof(Character).GetMethod("GetNextOrCurrentAnimHash", Instance, null, Type.EmptyTypes, null)?.ReturnType, "Character.GetNextOrCurrentAnimHash()");
        StaticField(typeof(Character), "s_animatorTagSitting", typeof(int));
        True(typeof(Character).GetMethod("IsStaggering", Type.EmptyTypes)?.IsPublic == true, "Character.IsStaggering()");
    }

    private static void Test_Game_TheBowDraw()
    {
        var draw = Method(typeof(Player), "UpdateAttackBowDraw");
        Param(draw, "weapon", typeof(ItemDrop.ItemData));
        Param(draw, "dt", typeof(float));
        var attached = Method(typeof(Player), "IsAttached");
        True(attached.IsPublic, "Player.IsAttached is public (nameof)");
        Field(typeof(Humanoid), "m_attackDrawTime", typeof(float));
    }

    private static void Test_Game_TheShot()
    {
        // The fire moment: GetProjectileSpawnPoint computes start point and direction; the direction is the eye's current
        // forward (Humanoid.GetAimDir -> Character.GetLookDir -> m_eye.forward), re-aimed by Player.UpdateEyeRotation.
        var spawn = Method(typeof(Attack), "GetProjectileSpawnPoint");
        Param(spawn, "spawnPoint", typeof(Vector3).MakeByRefType());
        Param(spawn, "aimDir", typeof(Vector3).MakeByRefType());
        Field(typeof(Attack), "m_character", typeof(Humanoid));
        Eq(0, Method(typeof(Player), "UpdateEyeRotation").GetParameters().Length, "Player.UpdateEyeRotation() declared on Player");
        Field(typeof(Character), "m_eye", typeof(Transform));
        // Seated, the camera hangs off the head; the shot is moved to the camera's current base (SeatedCameraPatches).
        Eq(typeof(Vector3), typeof(Character).GetMethod("GetHeadPoint", Type.EmptyTypes)?.ReturnType, "Character.GetHeadPoint()");
        Field(typeof(GameCamera), "m_currentBaseOffset", typeof(Vector3));
        Field(typeof(Attack), "m_attackOriginJoint", typeof(string));
    }

    private static void Test_Game_TheSeatedCameraHeight()
    {
        // GameCamera.GetCameraBaseOffset: attached or sitting, GetHeadPoint() + Vector3.up * 0.3f. The 0.3 is a literal in
        // the method's code (ldc.r4 0.3 = 22 9A 99 99 3E); ShotRules.SeatedCameraAboveHead must match it.
        var il = Method(typeof(GameCamera), "GetCameraBaseOffset").GetMethodBody().GetILAsByteArray();
        var literal = new byte[] { 0x22 }.Concat(BitConverter.GetBytes(ShotRules.SeatedCameraAboveHead)).ToArray();
        True(Enumerable.Range(0, il.Length - literal.Length + 1).Any(i => il.Skip(i).Take(literal.Length).SequenceEqual(literal)),
            $"GameCamera.GetCameraBaseOffset loads {ShotRules.SeatedCameraAboveHead}f");
    }

    private static void Test_Game_TheSeat()
    {
        True(Method(typeof(Player), "UpdateAttach").GetParameters().Length == 0, "Player.UpdateAttach()");
        Field(typeof(Character), "m_body", typeof(Rigidbody));
        Eq(typeof(Quaternion), typeof(Character).GetMethod("GetLookYaw", Type.EmptyTypes)?.ReturnType, "Character.GetLookYaw()");
        True(typeof(Character).GetMethod("InAttack", Type.EmptyTypes)?.IsPublic == true, "Character.InAttack()");
    }

    private static void Test_Game_TheRidePose()
    {
        // The seat's pose is an animator bool named by the seat (attach_lox, attach_moose...), set through the player's
        // ZSyncAnimation so other players see it; RidePosePatches turns it off while a rider's attack clip plays.
        Field(typeof(Player), "m_attachAnimation", typeof(string));
        Field(typeof(Character), "m_zanim", typeof(ZSyncAnimation));
        True(typeof(ZSyncAnimation).GetMethod("SetBool", new[] { typeof(string), typeof(bool) })?.IsPublic == true, "ZSyncAnimation.SetBool(string, bool)");
        // The shot: the clip's fire event (Humanoid.OnAttackTrigger); a looping attack keeps its clip.
        True(Method(typeof(Humanoid), "OnAttackTrigger").IsPublic, "Humanoid.OnAttackTrigger is public (nameof)");
        Eq(typeof(Humanoid), typeof(Player).GetMethod("OnAttackTrigger", Type.EmptyTypes)?.DeclaringType, "Player doesn't override OnAttackTrigger (the postfix is on Humanoid's)");
        Field(typeof(Attack), "m_loopingAttack", typeof(bool));
        Field(typeof(Attack), "m_projectileBursts", typeof(int));
        // Which layer the attack plays on: the full-body (base) layer's state tag, as Humanoid.InAttack reads it.
        StaticField(typeof(Humanoid), "s_animatorTagAttack", typeof(int));
        // Vanilla's idle speed reset, as on foot: ZSyncAnimation.SetSpeed and the states it is skipped in.
        True(typeof(ZSyncAnimation).GetMethod("SetSpeed", new[] { typeof(float) })?.IsPublic == true, "ZSyncAnimation.SetSpeed(float)");
        True(typeof(Character).GetMethod("InMinorAction", Type.EmptyTypes)?.IsPublic == true, "Character.InMinorAction()");
        True(typeof(Character).GetMethod("InEmote", Type.EmptyTypes)?.IsPublic == true, "Character.InEmote()");
    }

    private static void Test_Game_TheCreatureLookedAt()
    {
        // EnemyHud shows a creature's health for a minute after Player.GetHoverCreature() returned it. Player.UpdateHover
        // clears it while riding; HoverPatches looks it up again the way Player.FindHoverObject does, skipping the mount.
        Eq(0, Method(typeof(Player), "UpdateHover").GetParameters().Length, "Player.UpdateHover()");
        Field(typeof(Player), "m_hoveringCreature", typeof(Character));
        Field(typeof(Player), "m_interactMask", typeof(int));
        Eq(typeof(Character), typeof(Player).GetMethod("GetHoverCreature", Type.EmptyTypes)?.ReturnType, "Player.GetHoverCreature()");
        True(typeof(ParticleMist).GetMethod("IsMistBlocked", new[] { typeof(Vector3), typeof(Vector3) })?.IsStatic == true, "ParticleMist.IsMistBlocked(Vector3, Vector3)");
        True(typeof(BaseAI).GetMethod("IsSleeping", Type.EmptyTypes)?.IsPublic == true, "BaseAI.IsSleeping()");
        Eq(typeof(Character), typeof(Sadle).GetMethod("GetCharacter", Type.EmptyTypes)?.ReturnType, "Sadle.GetCharacter()");
        Eq(typeof(IDoodadController), typeof(Player).GetMethod("GetDoodadController", Type.EmptyTypes)?.ReturnType, "Player.GetDoodadController()");
    }

    private static void Test_Game_OnlyTheHealthBarsReadTheCreatureLookedAt()
    {
        // HoverPatches sets a rider's hover creature, which vanilla leaves empty while riding. That only shows health
        // bars as long as nothing else reads it: any method whose code calls Player.GetHoverCreature (call / callvirt
        // + its metadata token) besides EnemyHud.UpdateHuds, or loads Player.m_hoveringCreature or its address (ldfld /
        // ldflda) besides that getter and Player.UpdateHover (which writes it through FindHoverObject's out), fails here.
        var getter = typeof(Player).GetMethod("GetHoverCreature", Type.EmptyTypes);
        var field = typeof(Player).GetField("m_hoveringCreature", Instance);
        Eq("EnemyHud.UpdateHuds", string.Join(", ", CodeUsing(new byte[] { 0x28, 0x6F }, getter.MetadataToken)), "who calls Player.GetHoverCreature()");
        Eq("Player.GetHoverCreature, Player.UpdateHover", string.Join(", ", CodeUsing(new byte[] { 0x7B, 0x7C }, field.MetadataToken)), "who reads Player.m_hoveringCreature");
    }

    /// <summary>"Type.Method" of every method in the game DLL whose code has one of the opcodes followed by the token.</summary>
    private static string[] CodeUsing(byte[] opcodes, int metadataToken)
    {
        var token = BitConverter.GetBytes(metadataToken);
        Type[] types;
        try { types = typeof(Player).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
        var readers = new System.Collections.Generic.List<string>();
        foreach (var type in types)
        foreach (var method in type.GetMethods(Instance | Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                     .Concat(type.GetConstructors(Instance | Static | BindingFlags.DeclaredOnly)))
        {
            byte[] il;
            try { il = method.GetMethodBody()?.GetILAsByteArray(); }
            catch { continue; }
            if (il == null) continue;
            for (var i = 0; i + 5 <= il.Length; i++)
            {
                if (opcodes.Contains(il[i]) && il[i + 1] == token[0] && il[i + 2] == token[1] && il[i + 3] == token[2] && il[i + 4] == token[3])
                {
                    readers.Add($"{type.Name}.{method.Name}");
                    break;
                }
            }
        }
        return readers.Distinct().OrderBy(r => r).ToArray();
    }
}
