using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Patches;
using HarmonyLib;
using UnityEngine;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace AutoMapPins.Model;

internal class PinComponent : MonoBehaviour
{
    // Invisible objects are re-checked on a relaxed interval instead of every FixedUpdate:
    // each check used to do a reflection call plus a skill-mod radius lookup, which tanked FPS
    // when many unpinned objects were loaded at once.
    private const float VisibilityCheckIntervalSeconds = 0.5f;
    private static readonly WaitForSeconds VisibilityCheckWait = new(VisibilityCheckIntervalSeconds);

    private static readonly MethodInfo? IsExploredMethod =
        AccessTools.DeclaredMethod(typeof(Minimap), "IsExplored", new[] { typeof(Vector3) });

    private static readonly Func<Minimap, Vector3, bool>? IsExploredFunc = CreateIsExploredFunc();

    private static Func<Minimap, Vector3, bool>? CreateIsExploredFunc()
    {
        if (IsExploredMethod == null) return null;
        try
        {
            return (Func<Minimap, Vector3, bool>)Delegate.CreateDelegate(
                typeof(Func<Minimap, Vector3, bool>), IsExploredMethod);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly HashSet<PinComponent> VisibleComponents = new();

    internal static bool IsExploredReflectionAvailable => IsExploredMethod != null;

    private PinComponent()
    {
    }

    private Vector3 Position;
    private PositionedObject? PinnedIdentity;
    private bool IsVisible;
    private Coroutine? Routine;

    internal void Awake()
    {
        Position = gameObject.transform.position;
        IsVisible = EvaluateVisibility();
        if (IsVisible) ShowPin();
        if (!IsVisible || DynamicVisibility) Routine = StartCoroutine(VisibleCheck());
    }

    internal void OnDestroy()
    {
        VisibleComponents.Remove(this);
        if (Routine != null) StopCoroutine(Routine);
        if (PinnedIdentity.HasValue) MinimapPatch.UnpinObject(PinnedIdentity.Value);
        PinnedIdentity = null;
    }

    // Temporary pins can be configured to live only while their object stays inside the
    // discovery radius. They need continuous re-evaluation instead of the default latch.
    private bool DynamicVisibility =>
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value && IsTemporaryPin();

    private bool IsTemporaryPin()
    {
        string internalName = Constants.ParseInternalName(gameObject.name);
        return !Registry.ConfiguredPins.TryGetValue(internalName, out Config.Pin config) ||
               config.IsPermanent != true;
    }

    private bool EvaluateVisibility()
    {
        if (DynamicVisibility) return DiscoveryRadiusService.IsWithinDiscoveryRadius(Position);
        return Minimap.instance && CallIsExplored(Minimap.instance, Position) ||
               DiscoveryRadiusService.IsWithinDiscoveryRadius(Position);
    }

    private IEnumerator VisibleCheck()
    {
        // stagger the first check so many components do not poll in the same frame
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.0f, VisibilityCheckIntervalSeconds));
        while (true)
        {
            bool visible = EvaluateVisibility();
            if (visible != IsVisible)
            {
                IsVisible = visible;
                if (IsVisible) ShowPin();
                else HidePin();
            }

            if (IsVisible && !DynamicVisibility)
            {
                Routine = null;
                yield break;
            }
            yield return VisibilityCheckWait;
        }
    }

    // Config.Reload and YAML hot reload can enable dynamic visibility after a visible
    // pin's coroutine has already finished. Recheck it immediately and restart polling.
    internal static void RefreshVisiblePinsAfterConfigChange()
    {
        foreach (PinComponent component in VisibleComponents.ToList())
        {
            if (!component || !component.gameObject)
            {
                VisibleComponents.Remove(component!);
                continue;
            }

            if (!component.EvaluateVisibility())
            {
                component.IsVisible = false;
                component.HidePin();
            }

            if (component.Routine == null && (!component.IsVisible || component.DynamicVisibility))
                component.Routine = component.StartCoroutine(component.VisibleCheck());
        }
    }

    private void ShowPin()
    {
        VisibleComponents.Add(this);
        var identity = PositionedObject.FromGameObject(gameObject);
        // Remove by the identity used when inserting, not the object's later transform.
        // Permanent discoveries deliberately survive UnpinObject.
        if (PinnedIdentity.HasValue && !PinnedIdentity.Value.Equals(identity))
            MinimapPatch.UnpinObject(PinnedIdentity.Value);
        MinimapPatch.UpsertPin(gameObject);
        PinnedIdentity = identity;
    }

    private void HidePin()
    {
        VisibleComponents.Remove(this);
        if (PinnedIdentity.HasValue) MinimapPatch.UnpinObject(PinnedIdentity.Value);
        PinnedIdentity = null;
    }

    internal static void RescanRegisteredVisiblePins()
    {
        foreach (PinComponent component in VisibleComponents.ToList())
        {
            if (!component || !component.gameObject)
            {
                VisibleComponents.Remove(component!);
                continue;
            }

            if (!component.IsVisible) continue;

            component.ShowPin();
        }
    }

    private static bool CallIsExplored(Minimap minimap, Vector3 position)
    {
        if (IsExploredFunc != null) return IsExploredFunc(minimap, position);
        if (IsExploredMethod == null) return false;

        object? result = IsExploredMethod.Invoke(minimap, new object[] { position });
        return result is bool explored && explored;
    }
}