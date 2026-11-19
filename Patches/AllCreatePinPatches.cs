using AutoMapPins.Model;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace AutoMapPins.Patches;

internal static class CommonPatchLogic
{
    internal static void Patch(GameObject gameObject)
    {
        if (gameObject.transform.position.y < AutoMapPinsPlugin.MaxDetectionHeight.Value &&
            !gameObject.TryGetComponent(out PinComponent _))
            gameObject.AddComponent<PinComponent>();
    }
}

[HarmonyPatch(typeof(Destructible), "Start")]
internal class DestructiblePatch
{
    [UsedImplicitly]
    static void Postfix(ref Destructible __instance)
    {
        // in case an object has the Destructible and Pickable component, we skip patching the Destructible to
        // not patch the object twice, since the pickable patch will handle if object was already picked
        if (!__instance.TryGetComponent(out Pickable _)) CommonPatchLogic.Patch(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(TreeBase), "Awake")]
internal class TreeBasePatch
{
    [UsedImplicitly]
    static void Postfix(ref TreeBase __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(Beehive), "Awake")]
internal class BeehivePatch
{
    [UsedImplicitly]
    static void Postfix(ref Beehive __instance)
    {
        // Wild beehives should be discoverable, but player-built beehives are Piece objects and stay opt-in only.
        // Note: in Valheim 1.0.15 wild Ashlands beehives ('Beehive' prefab) carry SpawnOnDamaged instead of a
        // Beehive component, so they are covered by SpawnOnDamagedPatch; this hook only fires where a Beehive
        // component actually exists.
        if (!__instance.TryGetComponent(out Piece _)) CommonPatchLogic.Patch(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(SpawnOnDamaged), "Start")]
internal class SpawnOnDamagedPatch
{
    [UsedImplicitly]
    static void Postfix(ref SpawnOnDamaged __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(ResourceRoot), "Awake")]
internal class ResourceRootPatch
{
    [UsedImplicitly]
    static void Postfix(ref ResourceRoot __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(MineRock), "Start")]
internal class MineRockPatch
{
    [UsedImplicitly]
    static void Postfix(ref MineRock __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(MineRock5), "Awake")]
internal class MineRock5Patch
{
    [UsedImplicitly]
    static void Postfix(ref MineRock5 __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(Location), "Awake")]
internal class LocationPatch
{
    [UsedImplicitly]
    static void Postfix(ref Location __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(Leviathan), "Awake")]
internal class LeviathanPatch
{
    [UsedImplicitly]
    static void Postfix(ref Leviathan __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(TeleportWorld), "Awake")]
internal class TeleportWorldPatch
{
    [UsedImplicitly]
    static void Postfix(ref TeleportWorld __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(PickableItem), "Awake")]
internal class PickableItemPatch
{
    [UsedImplicitly]
    static void Postfix(ref PickableItem __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}

[HarmonyPatch(typeof(Container), "Awake")]
internal class ContainerPatch
{
    [UsedImplicitly]
    static void Postfix(ref Container __instance) => CommonPatchLogic.Patch(__instance.gameObject);
}
