using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Icons;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace AutoMapPins.Model;

public class MapPin : Minimap.PinData
{
    // Vanilla PlatformUserID only round-trips strings in "Platform_UserID" format;
    // anything else parses to None and the saved pin silently loses its AMP identity.
    private const string SavedPinAuthorPrefix = "FixItFelix.AutoMapPins_";

    private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequiredRef =
        AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");

    private static readonly MethodInfo? CreateMapNamePinMethod =
        AccessTools.DeclaredMethod(typeof(Minimap), "CreateMapNamePin");

    private static readonly MethodInfo? DestroyMapMarkerMethod =
        AccessTools.DeclaredMethod(typeof(Minimap.PinNameData), "DestroyMapMarker");

    internal readonly string InternalName;
    internal readonly bool LoadedFromSave;
    internal Config.Pin Config = null!;
    private readonly List<Vector3> InstancePositions = new();

    internal MapPin(PositionedObject positionedObject, Config.Pin config)
    {
        InternalName = positionedObject.ObjectName;
        LoadedFromSave = false;
        InstancePositions.Add(positionedObject.ObjectPosition);
        m_type = Minimap.PinType.None;
        m_checked = false;
        m_ownerID = 0;
        ApplyConfigUpdate(config);
    }

    internal MapPin(Minimap.PinData fromVanilla, string internalName, Config.Pin config)
    {
        InternalName = internalName;
        LoadedFromSave = true;
        InstancePositions.Add(fromVanilla.m_pos);
        m_type = fromVanilla.m_type;
        m_checked = fromVanilla.m_checked;
        m_ownerID = fromVanilla.m_ownerID;
        ApplyConfigUpdate(config);
    }

    // Snapshot before a config transition: a saved pin stores only one position.
    internal int InstanceCount => InstancePositions.Count;
    internal Vector3[] GetInstancePositions() => InstancePositions.ToArray();

    internal PositionedObject GetPinnedPosition()
    {
        return new PositionedObject(InternalName, m_pos);
    }

    internal static bool TryGetSavedInternalName(Minimap.PinData pin, out string internalName)
    {
        internalName = "";
        string author = pin.m_author.ToString();

        if (string.IsNullOrEmpty(author) || !author.StartsWith(SavedPinAuthorPrefix)) return false;

        internalName = author.Substring(SavedPinAuthorPrefix.Length);
        return !string.IsNullOrWhiteSpace(internalName);
    }

    internal void ApplyConfigUpdate(Config.Pin config)
    {
        Config = config;
        Config.IconName = string.IsNullOrWhiteSpace(Config.IconName) ? Constants.NoConfig : Config.IconName;
        m_name = Config.Name;
        m_save = Config.IsPermanent == true;
        ApplySavedIdentity();

        m_icon = Assets.GetIcon(Config.IconName ?? Constants.NoConfig);

        if (m_NamePinData != null)
            DestroyMapMarkerMethod?.Invoke(m_NamePinData, null);

        m_NamePinData = new Minimap.PinNameData(this);
        UpdatePin();
    }

    internal void RefreshIcon()
    {
        // A previously discovered permanent pin may survive a temporarily absent
        // category. Its old custom Sprite must still be replaced on icon reload,
        // without changing saved identity, label or pin configuration.
        m_icon = Assets.GetIcon(Config.IconName ?? Constants.NoConfig);
        if (Minimap.instance)
            PinUpdateRequiredRef(Minimap.instance) = true;
    }

    private void ApplySavedIdentity()
    {
        m_author = Config.IsPermanent == true
            ? new PlatformUserID(SavedPinAuthorPrefix + InternalName)
            : PlatformUserID.None;
    }

    private void UpdatePin()
    {
        UpdatePosition();
        UpdatePinText();
        UpdatePinColor();

        // make sure that (independently of calls inside called methods) we do update the map pins
        if (Minimap.instance)
            PinUpdateRequiredRef(Minimap.instance) = true;
    }

    private void UpdatePosition()
    {
        if (InstancePositions.Count > 1)
        {
            float x = 0f, y = 0f, z = 0f;
            foreach (Vector3 position in InstancePositions)
            {
                x += position.x;
                y += position.y;
                z += position.z;
            }
            float count = InstancePositions.Count;
            m_pos = new Vector3(x / count, y / count, z / count);
        }
        else
        {
            m_pos = InstancePositions.First();
        }
    }

    private void UpdatePinText()
    {
        if (InstancePositions.Count > 1)
        {
            var countString = InstancePositions.Count + "";
            m_name = string.IsNullOrEmpty(Config.Name)
                ? countString
                : countString == "" ? Config.Name : countString + " " + Config.Name;
        }
        else
        {
            m_name = string.IsNullOrEmpty(Config.Name) ? " " : Config.Name;
        }

        // this is required to be able to re-create the text on textual changes due to grouping
        if (m_NamePinData != null)
            DestroyMapMarkerMethod?.Invoke(m_NamePinData, null);

        if (!Minimap.instance) return;

        RectTransform root = Minimap.instance.m_mode == Minimap.MapMode.Large
            ? Minimap.instance.m_pinNameRootLarge
            : Minimap.instance.m_pinNameRootSmall;

        CreateMapNamePinMethod?.Invoke(Minimap.instance, new object[] { this, root });

        RefreshPinLabelText();
    }

    internal void RefreshPinLabelText()
    {
        if (m_NamePinData?.PinNameText == null) return;
        string localized = Localization.instance.Localize(m_name);
        if (m_NamePinData.PinNameText.text != localized)
            m_NamePinData.PinNameText.text = localized;
    }

    internal bool RepresentsObject(PositionedObject positionedObject)
    {
        return InternalName == positionedObject.ObjectName &&
               InstancePositions.Contains(positionedObject.ObjectPosition);
    }

    internal void UpdatePinColor()
    {
        if (!m_iconElement) return;

        Color color = Config.IconColorRGBA?.FromConfig() ?? Color.white;
        if (!SameColor(m_iconElement.color, color))
            m_iconElement.color = color;

        if (m_NamePinData?.PinNameText != null && !SameColor(m_NamePinData.PinNameText.color, color))
            m_NamePinData.PinNameText.color = color;
    }

    private static bool SameColor(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    internal bool AcceptsObject(PositionedObject objectToGroup)
    {
        return Config.Groupable == true &&
               InternalName == objectToGroup.ObjectName &&
               WithinGroupDistance(objectToGroup.ObjectPosition);
    }

    private bool WithinGroupDistance(Vector3 newPosition)
    {
        float groupingRadius = Config is { GroupingDistance: > 0 }
            ? Config.GroupingDistance.Value
            : AutoMapPinsPlugin.GroupingRadius.Value;

        return Utils.DistanceXZ(m_pos, newPosition) < groupingRadius;
    }

    internal void AddObjectToPin(PositionedObject objectToAdd)
    {
        if (InstancePositions.Contains(objectToAdd.ObjectPosition)) return; // no duplicates
        InstancePositions.Add(objectToAdd.ObjectPosition);
        UpdatePin();
    }

    internal bool RemoveObjectFromPin(PositionedObject positionedObject, out bool pinIsEmpty)
    {
        pinIsEmpty = false;
        if (Config.IsPermanent == true) return false;
        if (InternalName != positionedObject.ObjectName) return false;
        if (!InstancePositions.Remove(positionedObject.ObjectPosition)) return false;
        pinIsEmpty = InstancePositions.Count == 0;
        if (!pinIsEmpty) UpdatePin();
        return true;
    }

    public override string ToString()
    {
        string result =
            $"custom AMP pin: '{m_name}' (game object name '{InternalName}') at position '{m_pos}' " +
            $"with icon '{Config.IconName}', permanent '{Config.IsPermanent}'";

        if (InstancePositions.Count <= 1) return result;

        result += "; grouped pin, containing positions:";
        foreach (var position in InstancePositions)
            result += "\n  " + position;

        return result;
    }
}
