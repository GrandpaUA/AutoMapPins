using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Icons;
using AutoMapPins.Model;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace AutoMapPins.Patches;

[HarmonyPatch(typeof(Minimap))]
internal abstract class MinimapPatch : HasLogger
{
    private static readonly Dictionary<Minimap.PinData, IconDisplayState> IconDisplayStates = new();
    private static readonly Dictionary<Minimap.PinData, string> LocationPinNames = new();
    private static Minimap? _locationPinOwner;
    private static float _nextLocationPinScan;
    private static float _nextIconDisplayCleanup;

    private sealed class IconDisplayState
    {
        internal Sprite? OriginalIcon;
        internal Sprite? AppliedIcon;
        internal object? AppliedElement;
        internal RectTransform? ScaledElement;
        internal Vector3 OriginalScale;
        internal Vector3 AppliedScale;
        internal bool HasVanillaOverride;
        internal bool SuppressedVanillaOverride;
    }

    private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
        AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
    private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequiredRef =
        AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");
    // Optional on newer game builds: never fail MinimapPatch initialization if the
    // game's private location field changes; the normal map pins must still work.
    private static readonly FieldInfo? LocationPinsField = AccessTools.Field(typeof(Minimap), "m_locationPins");
    private static readonly MethodInfo? GetLocationIconsMethod = AccessTools.Method(
        typeof(ZoneSystem), "GetLocationIcons", new[] { typeof(Dictionary<Vector3, string>) });
    private static bool _warnedUnavailableLocations;

    [UsedImplicitly]
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    internal static void StartPickerPostfix(Minimap __instance) => PickerIconOverrides.Refresh(__instance);

    [UsedImplicitly]
    [HarmonyPatch("LoadMapData")]
    [HarmonyPostfix]
    internal static void LoadMapDataPostfix(ref Minimap __instance)
    {
        var pins = PinsRef(__instance);
        var pinsToRemove = new List<Minimap.PinData>();
        var convertedPins = new List<MapPin>();

        Log.LogInfo($"loading map data with '{pins.Count}' pins");

        foreach (var pin in pins)
        {
            if (!TryGetSavedAmpPinInternalName(pin, out string internalName)) continue;

            if (TryGetSavedPinConfig(pin, internalName, out Config.Pin config))
            {
                pinsToRemove.Add(pin);
                var newMapPin = new MapPin(pin, internalName, config);
                convertedPins.Add(newMapPin);
            }
            else if (SavedAmpPinHasLoadedNonPermanentOrInactiveConfig(internalName))
            {
                pinsToRemove.Add(pin);
            }
        }

        if (pinsToRemove.Count > 0)
        {
            Log.LogInfo(
                $"removing {pinsToRemove.Count} pins due to config updates and/or replacement by loading from player save file");

            foreach (var pin in pinsToRemove)
            {
                __instance.RemovePin(pin);
            }
        }

        if (convertedPins.Count > 0)
        {
            Log.LogInfo($"adding {convertedPins.Count} AMP pins to replace vanilla stored pins");

            foreach (var pin in convertedPins)
            {
                pins.Add(pin);
            }
        }

        Log.LogInfo($"Loaded map with {pins.Count} existing pins");
    }

    private static bool TryGetSavedAmpPinInternalName(Minimap.PinData pin, out string internalName)
    {
        internalName = "";
        return pin.m_type == Minimap.PinType.None &&
               pin.m_ownerID == 0 &&
               MapPin.TryGetSavedInternalName(pin, out internalName);
    }

    private static bool TryGetSavedPinConfig(
        Minimap.PinData pin,
        out string internalName,
        out Config.Pin config)
    {
        config = null!;
        if (!TryGetSavedAmpPinInternalName(pin, out internalName)) return false;
        return TryGetSavedPinConfig(pin, internalName, out config);
    }

    private static bool TryGetSavedPinConfig(
        Minimap.PinData pin,
        string internalName,
        out Config.Pin config)
    {
        config = null!;

        if (!Registry.ConfiguredPins.TryGetValue(internalName, out config))
        {
            if (Registry.HasConfiguredObject(internalName))
            {
                Log.LogWarning(
                    $"removing saved AMP pin '{pin.m_name}' with internal name '{internalName}' because its loaded config is not active and permanent");
            }
            else
            {
                Log.LogWarning(
                    $"preserving saved AMP pin '{pin.m_name}' with internal name '{internalName}' because its config is not loaded yet");
            }

            config = null!;
            return false;
        }

        if (config.IsActive == true && config.IsPermanent == true) return true;

        Log.LogWarning(
            $"removing saved AMP pin '{pin.m_name}' with internal name '{internalName}' because its loaded config is not active and permanent");
        config = null!;
        return false;
    }

    private static bool SavedAmpPinHasLoadedNonPermanentOrInactiveConfig(string internalName)
    {
        if (Registry.ConfiguredPins.TryGetValue(internalName, out Config.Pin config))
            return config.IsActive != true || config.IsPermanent != true;

        return Registry.HasConfiguredObject(internalName);
    }

    [UsedImplicitly]
    [HarmonyPatch("UpdateExplore")]
    [HarmonyPostfix]
    internal static void UpdateExploreSkillPostfix()
    {
        ExplorationSkillGate.Tick();
    }

    // Captures the explore radius the game actually uses. Every radius-altering mod
    // (Smoothbrain Exploration/Sailing, Advize CartographySkill, ...) funnels through this
    // method, so the postfix sees the final value after all their prefixes ran.
    // Index-based argument names: the parameter is named 'p' in some game builds and
    // 'center' in others, and Harmony binds original arguments by name.
    [UsedImplicitly]
    [HarmonyPatch("Explore", typeof(Vector3), typeof(float))]
    [HarmonyPostfix]
    internal static void ExplorePostfix(Vector3 __0, float __1)
    {
        DiscoveryRadiusService.CaptureGameExploreRadius(__0, __1);
    }

    [UsedImplicitly]
    [HarmonyPatch("UpdatePins")]
    [HarmonyPostfix]
    internal static void UpdatePinsPostfix(ref Minimap __instance)
    {
        RefreshLocationPinNames(__instance);
        var pins = PinsRef(__instance);
        foreach (var pin in pins)
        {
            if (pin is MapPin mapPin)
            {
                if (mapPin.m_iconElement)
                {
                    // Valheim only assigns Image.sprite when creating the UI marker.
                    // Icon YAML reload replaces m_icon on an existing MapPin as well.
                    if (!ReferenceEquals(mapPin.m_iconElement.sprite, mapPin.m_icon))
                        mapPin.m_iconElement.sprite = mapPin.m_icon;
                    mapPin.UpdatePinColor();
                }
                mapPin.RefreshPinLabelText();
                ApplyIconScale(pin, Assets.GetIconScale(mapPin.Config.IconName));
            }
            else if (LocationPinNames.TryGetValue(pin, out string locationId) &&
                     Assets.TryGetLocationMapOverride(locationId, out Sprite locationIcon, out float locationScale))
            {
                ApplyVanillaIconOverride(pin, locationIcon, locationScale);
            }
            else if (Assets.TryGetVanillaMapOverride(pin.m_type, out Sprite overrideIcon, out float scale))
            {
                ApplyVanillaIconOverride(pin, overrideIcon, scale);
            }
            else
            {
                RestoreIconDisplay(pin);
            }
        }

        // Removed pins no longer receive Minimap.UpdatePins; forget their state so
        // long sessions cannot retain references to every pin ever seen.
        if (IconDisplayStates.Count > 0 && Time.time >= _nextIconDisplayCleanup)
        {
            _nextIconDisplayCleanup = Time.time + 15f;
            var currentPins = new HashSet<Minimap.PinData>(pins);
            foreach (var pin in IconDisplayStates.Keys.Where(pin => !currentPins.Contains(pin)).ToList())
                IconDisplayStates.Remove(pin);
        }
    }

    // Valheim's location markers are PinType.None with no saved name. Match only pins
    // owned by its m_locationPins dictionary to ZoneSystem's position -> location ID;
    // this leaves unrelated None pins and AMP pins untouched. Refresh at the native
    // location update cadence, or immediately on a successful PNG/YAML reload.
    private static void RefreshLocationPinNames(Minimap minimap, bool force = false)
    {
        if (!force && ReferenceEquals(_locationPinOwner, minimap) && Time.time < _nextLocationPinScan) return;
        _locationPinOwner = minimap;
        _nextLocationPinScan = Time.time + 5f;
        LocationPinNames.Clear();
        if (!Assets.HasLocationMapOverrides || !ZoneSystem.instance) return;
        try
        {
            if (LocationPinsField?.GetValue(minimap) is not Dictionary<Vector3, Minimap.PinData> locationPins ||
                GetLocationIconsMethod == null)
            {
                if (!_warnedUnavailableLocations)
                {
                    Log.LogWarning("location icon overrides unavailable in this game build; ordinary map pins remain unchanged");
                    _warnedUnavailableLocations = true;
                }
                return;
            }
            var locations = new Dictionary<Vector3, string>();
            GetLocationIconsMethod.Invoke(ZoneSystem.instance, new object[] { locations });
            foreach (var entry in locationPins)
                if (entry.Value != null && entry.Value.m_type == Minimap.PinType.None &&
                    locations.TryGetValue(entry.Key, out string locationId))
                    LocationPinNames[entry.Value] = locationId;
        }
        catch (Exception e)
        {
            LocationPinNames.Clear();
            if (!_warnedUnavailableLocations)
            {
                Log.LogWarning($"location icon lookup failed: {e.GetBaseException().Message}; ordinary map pins remain unchanged");
                _warnedUnavailableLocations = true;
            }
        }
    }

    private static void ApplyVanillaIconOverride(Minimap.PinData pin, Sprite overrideIcon, float scale)
    {
        if (IconDisplayStates.TryGetValue(pin, out IconDisplayState state))
        {
            if (state.SuppressedVanillaOverride) return;
            if (state.HasVanillaOverride &&
                (!ReferenceEquals(pin.m_icon, state.AppliedIcon) ||
                 (pin.m_iconElement != null && !ReferenceEquals(pin.m_iconElement.sprite, state.AppliedIcon) &&
                  !( !ReferenceEquals(pin.m_iconElement, state.AppliedElement) &&
                     ReferenceEquals(pin.m_iconElement.sprite, state.OriginalIcon)))))
            {
                // A competing mod now owns the marker. Restore only our own data,
                // then leave it alone until the YAML entry is removed.
                RestoreIconDisplay(pin);
                IconDisplayStates[pin] = new IconDisplayState { SuppressedVanillaOverride = true };
                return;
            }
        }
        else
        {
            if (pin.m_iconElement != null && !ReferenceEquals(pin.m_iconElement.sprite, pin.m_icon)) return;
            IconDisplayStates[pin] = state = new IconDisplayState { OriginalIcon = pin.m_icon };
        }

        state.HasVanillaOverride = true;
        state.AppliedIcon = overrideIcon;
        pin.m_icon = overrideIcon;
        if (pin.m_iconElement != null)
        {
            if (!ReferenceEquals(pin.m_iconElement.sprite, overrideIcon))
                pin.m_iconElement.sprite = overrideIcon;
            state.AppliedElement = pin.m_iconElement;
        }
        ApplyIconScale(pin, scale);
    }

    internal static void RefreshIconVisuals()
    {
        if (!Minimap.instance) return;
        // Refresh existing Image components immediately after icon YAML reload, in
        // the same frame that the previous custom sprites are retired.
        Minimap minimap = Minimap.instance!;
        PickerIconOverrides.Refresh(minimap);
        RefreshLocationPinNames(minimap, force: true);
        UpdatePinsPostfix(ref minimap);
    }

    private static bool SameScale(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

    private static void RestoreIconScale(IconDisplayState state)
    {
        if (state.ScaledElement == null) return;
        // A UI mod may have changed the marker since our last update. Leave its
        // new value alone instead of restoring our stale original snapshot.
        if (SameScale(state.ScaledElement.localScale, state.AppliedScale))
            state.ScaledElement.localScale = state.OriginalScale;
        state.ScaledElement = null;
    }

    private static void ApplyIconScale(Minimap.PinData pin, float scale)
    {
        if (!IconDisplayStates.TryGetValue(pin, out IconDisplayState state))
        {
            if (scale == 1f || pin.m_uiElement == null) return;
            IconDisplayStates[pin] = state = new IconDisplayState();
        }
        if (state.ScaledElement != null && state.ScaledElement != pin.m_uiElement)
            RestoreIconScale(state);
        if (scale == 1f || pin.m_uiElement == null)
        {
            if (state.ScaledElement != null) OffsetLabelForScale(pin, 1f);
            RestoreIconScale(state);
            if (!state.HasVanillaOverride) IconDisplayStates.Remove(pin);
            return;
        }
        if (state.ScaledElement == null)
        {
            state.ScaledElement = pin.m_uiElement;
            state.OriginalScale = pin.m_uiElement.localScale;
        }
        else if (!SameScale(state.ScaledElement.localScale, state.AppliedScale))
            state.OriginalScale = state.ScaledElement.localScale;
        state.AppliedScale = state.OriginalScale * scale;
        pin.m_uiElement.localScale = state.AppliedScale;
        OffsetLabelForScale(pin, scale);
    }

    // Vanilla UpdatePins puts the label at the icon's anchored position every pass; the
    // name prefab's own offset only clears a 100% icon. Shift it by half the height change
    // so an enlarged icon does not cover its label (and a shrunken one is not far from it).
    // Derived from the icon position, so repeated passes never accumulate.
    private static void OffsetLabelForScale(Minimap.PinData pin, float scale)
    {
        RectTransform? label = pin.m_NamePinData?.PinNameRectTransform;
        if (label == null || pin.m_uiElement == null) return;
        float shift = pin.m_uiElement.rect.height * (scale - 1f) * 0.5f;
        if (float.IsNaN(shift) || float.IsInfinity(shift)) return;
        Vector2 iconPosition = pin.m_uiElement.anchoredPosition;
        label.anchoredPosition = new Vector2(iconPosition.x, iconPosition.y - shift);
    }

    private static void RestoreIconDisplay(Minimap.PinData pin)
    {
        if (!IconDisplayStates.TryGetValue(pin, out IconDisplayState state)) return;
        // Another mod may replace the pin between our last pass and YAML removal.
        // Restore its latest sprite rather than a stale snapshot in that case.
        Sprite? restoreIcon = ReferenceEquals(pin.m_icon, state.AppliedIcon) ? state.OriginalIcon : pin.m_icon;
        if (state.HasVanillaOverride && ReferenceEquals(pin.m_icon, state.AppliedIcon))
            pin.m_icon = restoreIcon;
        if (state.HasVanillaOverride && pin.m_iconElement != null &&
            ReferenceEquals(pin.m_iconElement.sprite, state.AppliedIcon))
            pin.m_iconElement.sprite = restoreIcon;
        RestoreIconScale(state);
        IconDisplayStates.Remove(pin);
    }

    [UsedImplicitly]
    [HarmonyPatch("GetSharedMapData")]
    [HarmonyPrefix]
    internal static void GetSharedMapDataPrefix(ref Minimap __instance, out List<Minimap.PinData> __state)
    {
        var pins = PinsRef(__instance);
        // Harmony keeps this state per invocation. A nested sharing call must not
        // clear the outer call's removed pins (including permanent saved metadata).
        __state = pins.Where(IsAmpPinData).ToList();

        foreach (var ampPin in __state)
        {
            pins.Remove(ampPin);
        }
    }

    [UsedImplicitly]
    [HarmonyPatch("GetSharedMapData")]
    [HarmonyPostfix]
    internal static void GetSharedMapDataPostfix(ref Minimap __instance, List<Minimap.PinData>? __state)
    {
        RestoreMapTablePins(__instance, __state);
    }

    [UsedImplicitly]
    [HarmonyPatch("GetSharedMapData")]
    [HarmonyFinalizer]
    internal static Exception? GetSharedMapDataFinalizer(ref Minimap __instance, Exception? __exception,
        List<Minimap.PinData>? __state)
    {
        RestoreMapTablePins(__instance, __state);
        return __exception;
    }

    private static void RestoreMapTablePins(Minimap minimap, List<Minimap.PinData>? state)
    {
        // A different prefix may throw before ours initializes state.
        if (state == null || state.Count == 0) return;

        var pins = PinsRef(minimap);
        foreach (var ampPin in state)
        {
            if (!pins.Contains(ampPin))
                pins.Add(ampPin);
        }

        state.Clear(); // postfix + finalizer must restore exactly once
    }

    private static bool IsAmpPinData(Minimap.PinData pin) =>
        pin is MapPin || TryGetSavedAmpPinInternalName(pin, out _);

    // 'amp clear_pins' cleanup: remove only pins created or saved by this mod.
    // Vanilla Minimap.ClearPins would also wipe the player's own, death, boss and
    // location markers, which this diagnostic command must never touch.
    internal static int ClearAmpPins()
    {
        if (!Minimap.instance) return 0;
        var ampPins = PinsRef(Minimap.instance).Where(IsAmpPinData).ToList();
        foreach (var pin in ampPins)
            Minimap.instance.RemovePin(pin);
        return ampPins.Count;
    }

    private static void ConvertSavedAmpPins()
    {
        if (!Minimap.instance) return;

        var pins = PinsRef(Minimap.instance);
        var convertedPins = new List<(Minimap.PinData SavedPin, MapPin AmpPin)>();
        var staleSavedPins = new List<Minimap.PinData>();

        foreach (var pin in pins.ToList())
        {
            if (pin is MapPin) continue;
            if (!TryGetSavedAmpPinInternalName(pin, out string internalName)) continue;

            if (TryGetSavedPinConfig(pin, internalName, out Config.Pin config))
                convertedPins.Add((pin, new MapPin(pin, internalName, config)));
            else if (SavedAmpPinHasLoadedNonPermanentOrInactiveConfig(internalName))
                staleSavedPins.Add(pin);
        }

        foreach (var savedPin in staleSavedPins)
        {
            Minimap.instance.RemovePin(savedPin);
        }

        foreach (var convertedPin in convertedPins)
        {
            Minimap.instance.RemovePin(convertedPin.SavedPin);
            pins.Add(convertedPin.AmpPin);
        }
    }

    internal static void UpsertPin(GameObject objectToPin)
    {
        if (!Minimap.instance) return;

        var pins = PinsRef(Minimap.instance);
        if (pins == null) return;

        string internalName = Constants.ParseInternalName(objectToPin.name);

        if (!Registry.ConfiguredPins.TryGetValue(internalName, out Config.Pin config) || config.IsActive != true)
        {
            Registry.AddMissingConfig(internalName);
            return;
        }
        if (!ExplorationSkillGate.IsUnlocked(config)) return;

        // LoadMapData and Registry.UpdatePins convert saved metadata once per load/reload;
        // doing so here scanned the entire map again for every spawned object.
        var positionedObject = new PositionedObject(internalName, objectToPin.transform.position);
        MapPin? groupingPin = null;
        bool canGroup = config.IsPermanent != true && config.Groupable == true;
        foreach (Minimap.PinData candidate in pins)
        {
            if (candidate is not MapPin pin || pin.InternalName != internalName) continue;
            if (pin.RepresentsObject(positionedObject)) return;
            if (canGroup && groupingPin == null && pin.AcceptsObject(positionedObject))
                groupingPin = pin;
        }

        // Permanent pins must remain separate: the save stores only one position per pin.
        if (groupingPin != null)
            groupingPin.AddObjectToPin(positionedObject);
        else
            pins.Add(new MapPin(positionedObject, config));
    }

    internal static MapPin? UpsertDistantPin(PositionedObject position)
    {
        if (!Minimap.instance || !Registry.ConfiguredPins.TryGetValue(position.ObjectName, out Config.Pin config) ||
            config.IsActive != true || !ExplorationSkillGate.IsUnlocked(config)) return null;

        var pins = PinsRef(Minimap.instance);
        if (pins.OfType<MapPin>().Any(pin => pin.RepresentsObject(position))) return null;
        var created = new MapPin(position, config);
        pins.Add(created);
        return created;
    }

    internal static bool ContainsDistantPin(MapPin pin) =>
        Minimap.instance && PinsRef(Minimap.instance).Contains(pin);

    internal static void RemoveDistantPin(MapPin pin)
    {
        if (Minimap.instance && pin.Config.IsPermanent != true && PinsRef(Minimap.instance).Contains(pin))
            Minimap.instance.RemovePin(pin);
    }

    internal static void UnpinObject(GameObject objectToRemove) =>
        UnpinObject(PositionedObject.FromGameObject(objectToRemove));

    internal static void UnpinObject(PositionedObject positionToRemove)
    {
        if (!Minimap.instance) return;

        var pins = PinsRef(Minimap.instance);
        for (int index = 0; index < pins.Count; index++)
        {
            if (pins[index] is not MapPin pin || pin.InternalName != positionToRemove.ObjectName ||
                AutoMapPinsPlugin.OwnsDistantPin(pin)) continue;
            if (!pin.RemoveObjectFromPin(positionToRemove, out bool pinIsEmpty)) continue;
            if (pinIsEmpty) Minimap.instance.RemovePin(pin);
            return;
        }
    }

    internal static void UpdatePins()
    {
        if (!Minimap.instance) return;

        Log.LogInfo("updating pins");

        ConvertSavedAmpPins();

        var pinsToRemove = new List<MapPin>();
        var pinsToAdd = new List<MapPin>();

        foreach (var pin in PinsRef(Minimap.instance).OfType<MapPin>())
        {
            if (Registry.ConfiguredPins.TryGetValue(pin.InternalName, out Config.Pin config))
            {
                // A discovered permanent pin has already been earned. Keep it in the
                // player save if the skill later falls (for example after death).
                if (config.IsPermanent != true && !ExplorationSkillGate.IsUnlocked(config))
                    pinsToRemove.Add(pin);
                else if (pin.Config.IsPermanent == true && config.IsPermanent != true)
                    pinsToRemove.Add(pin);
                else if (config.IsPermanent == true && pin.InstanceCount > 1)
                {
                    // Existing temporary groups also need splitting on hot reload;
                    // merely disabling grouping for new pins would save their centroid.
                    pinsToRemove.Add(pin);
                    foreach (Vector3 position in pin.GetInstancePositions())
                    {
                        var separate = new MapPin(new PositionedObject(pin.InternalName, position), config);
                        separate.m_checked = pin.m_checked;
                        pinsToAdd.Add(separate);
                    }
                }
                else
                    pin.ApplyConfigUpdate(config);
            }
            else if (pin.Config.IsPermanent == true && !Registry.HasConfiguredObject(pin.InternalName))
            {
                pin.RefreshIcon();
            }
            else
            {
                pinsToRemove.Add(pin);
            }
        }

        foreach (var pin in pinsToRemove)
        {
            Minimap.instance.RemovePin((Minimap.PinData)pin);
        }
        PinsRef(Minimap.instance).AddRange(pinsToAdd);
        // Config/icon reload can affect only vanilla pins. Force the next map UI pass
        // even when no AMP MapPin exists to call ApplyConfigUpdate.
        PinUpdateRequiredRef(Minimap.instance) = true;
    }

    internal static string PrintMapPins()
    {
        string result = "";

        if (!Minimap.instance) return result;

        var pins = PinsRef(Minimap.instance)
            .Select((pin, index) => new
            {
                Position = new PositionedObject(pin.m_NamePinData?.PinNameText?.text ?? "no name", pin.m_pos),
                Pin = pin,
                Index = index
            });

        foreach (var pin in pins.OrderBy(entry => entry.Position).ThenBy(entry => entry.Index))
        {
            if (pin.Pin is MapPin mapPin)
            {
                result += mapPin + "\n";
            }
            else
            {
                result += $"vanilla pin: {pin.Position} of type '{pin.Pin.m_type}'\n";
            }
        }

        return result;
    }
}
