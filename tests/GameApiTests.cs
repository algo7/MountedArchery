using System;
using System.Linq;
using System.Reflection;
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
        // Seated, the camera hangs off the head (GameCamera.GetCameraBaseOffset), which the shot is moved to match.
        Eq(typeof(Vector3), typeof(Character).GetMethod("GetHeadPoint", Type.EmptyTypes)?.ReturnType, "Character.GetHeadPoint()");
        True(Method(typeof(GameCamera), "GetCameraBaseOffset") != null, "GameCamera.GetCameraBaseOffset (the seated camera)");
    }

    private static void Test_Game_TheSeat()
    {
        True(Method(typeof(Player), "UpdateAttach").GetParameters().Length == 0, "Player.UpdateAttach()");
        Field(typeof(Character), "m_body", typeof(Rigidbody));
        Eq(typeof(Quaternion), typeof(Character).GetMethod("GetLookYaw", Type.EmptyTypes)?.ReturnType, "Character.GetLookYaw()");
        True(typeof(Character).GetMethod("InAttack", Type.EmptyTypes)?.IsPublic == true, "Character.InAttack()");
    }
}
