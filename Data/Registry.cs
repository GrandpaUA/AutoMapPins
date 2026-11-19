using System.Collections.Generic;
using System.Linq;
using AutoMapPins.Common;
using AutoMapPins.Icons;
using AutoMapPins.Model;
using AutoMapPins.Patches;

namespace AutoMapPins.Data;

public abstract class Registry : HasLogger
{
    internal static Dictionary<string, Config.Pin> ConfiguredPins = new();
    internal static bool HasSkillRequirements;
    private static HashSet<string> ConfiguredObjects = new();
    internal static readonly HashSet<string> MissingConfigs = new();

    internal static void InitializeRegistry(Dictionary<string, Config.Category> newConfiguredCategories)
    {
        Log.LogDebug("initializing registry");
        MissingConfigs.Clear();
        ConfiguredObjects.Clear();
        ConfiguredPins.Clear();
        ConfiguredPins = LoadActivePinConfigs(newConfiguredCategories);
        HasSkillRequirements = ConfiguredPins.Values.Any(pin => pin.SkillRequire is > 0);
        ConfiguredObjects = GetConfiguredObjects(newConfiguredCategories);
        MinimapPatch.UpdatePins();
        PinComponent.RefreshVisiblePinsAfterConfigChange();
        PinComponent.RescanRegisteredVisiblePins();
        Log.LogDebug("registry initialization successful");
    }


    private static Dictionary<string, Config.Pin> LoadActivePinConfigs(
        Dictionary<string, Config.Category> newConfiguredCategories)
    {
        var activePinConfigs = new Dictionary<string, Config.Pin>();
        foreach (KeyValuePair<string, Config.Category> category in newConfiguredCategories)
        {
            if (category.Value.IsActive != true) continue;
            if (category.Value.IndividualConfiguredObjects != null)
                foreach (KeyValuePair<string, Config.Pin> configuredObject in
                         category.Value.IndividualConfiguredObjects)
                {
                    Config.Pin objectFixedByCategory = configuredObject.Value;
                    bool objectIsActive = objectFixedByCategory.IsActive ?? category.Value.IsActive ?? false;
                    if (!objectIsActive) continue;

                    objectFixedByCategory.IsActive = objectIsActive;
                    objectFixedByCategory.IsPermanent ??= category.Value.IsPermanent;
                    objectFixedByCategory.Groupable ??= category.Value.Groupable;
                    objectFixedByCategory.GroupingDistance ??= category.Value.GroupingDistance ?? Constants.DefaultGroupingDistance;
                    objectFixedByCategory.SkillRequire ??= category.Value.SkillRequire;
                    objectFixedByCategory.Name ??= category.Value.Name;
                    objectFixedByCategory.IconName ??= category.Value.IconName ?? Constants.NoConfig;
                    objectFixedByCategory.IconColorRGBA = objectFixedByCategory.IconColorRGBA?.ClampColor() ??
                                                          category.Value.IconColorRGBA?.ClampColor() ??
                                                          Config.PinColor.White;
                    if (!activePinConfigs.ContainsKey(configuredObject.Key))
                        activePinConfigs.Add(configuredObject.Key, objectFixedByCategory);
                    else
                        Log.LogWarning(
                            $"tried to add an individually configured object '{configuredObject.Key}' " +
                            $"from category '{category.Key}' that already exists - skipping object");
                }

            if (category.Value.CategoryConfiguredObjects != null)
                foreach (string configuredObject in category.Value.CategoryConfiguredObjects)
                {
                    // An explicit individual disable overrides this category's bulk list.
                    // Do not let the fallback membership silently reactivate the object.
                    if (category.Value.IndividualConfiguredObjects != null &&
                        category.Value.IndividualConfiguredObjects.TryGetValue(configuredObject, out Config.Pin individual) &&
                        individual.IsActive == false) continue;
                    if (!activePinConfigs.ContainsKey(configuredObject))
                        activePinConfigs.Add(configuredObject, new Config.Pin
                        {
                            Name = category.Value.Name,
                            IsActive = category.Value.IsActive,
                            IsPermanent = category.Value.IsPermanent,
                            Groupable = category.Value.Groupable,
                            GroupingDistance = category.Value.GroupingDistance ?? Constants.DefaultGroupingDistance,
                            SkillRequire = category.Value.SkillRequire,
                            IconName = category.Value.IconName ?? Constants.NoConfig,
                            IconColorRGBA = category.Value.IconColorRGBA?.ClampColor() ?? Config.PinColor.White
                        });
                    else
                        Log.LogWarning(
                            $"tried to add a by category '{category.Key}' configured object '{configuredObject}' " +
                            $"that does already exist in individual configured objects list - skipping object");
                }
        }

        Log.LogInfo($"loaded '{activePinConfigs.Count}' configs for active pins");
        return activePinConfigs;
    }

    private static HashSet<string> GetConfiguredObjects(Dictionary<string, Config.Category> newConfiguredCategories)
    {
        List<string> categoryConfiguredTotal = newConfiguredCategories
            .Where(cat => cat.Value.CategoryConfiguredObjects != null)
            .SelectMany(cat => cat.Value.CategoryConfiguredObjects).ToList();

        List<string> individualConfiguredTotal = new List<string>();
        foreach (var category in newConfiguredCategories)
        {
            if (category.Value.IndividualConfiguredObjects == null) continue;
            foreach (var configuredObject in category.Value.IndividualConfiguredObjects)
                individualConfiguredTotal.Add(configuredObject.Key ?? Constants.NoConfig);
        }

        var configuredTotal = categoryConfiguredTotal.Concat(individualConfiguredTotal).ToList();
        var duplicates = configuredTotal.GroupBy(x => x)
            .ToDictionary(group => group.Key, group => group.Count())
            .Where(kv => kv.Value > 1)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        if (duplicates.Count > 0)
        {
            Log.LogWarning($"found '{duplicates.Count}' duplicates in config:");
            foreach (var duplicate in duplicates)
            {
                Log.LogWarning($"object name '{duplicate.Key}' found '{duplicate.Value}' times");
            }
        }

        var result = configuredTotal.Distinct().ToHashSet();
        Log.LogInfo($"loaded '{result.Count}' configured objects in total (also inactive ones)");
        return result;
    }

    internal static bool HasConfiguredObject(string internalName) => ConfiguredObjects.Contains(internalName);

    internal static void AddMissingConfig(string internalName)
    {
        if (!AutoMapPinsPlugin.PrefabDiscoveryEnabled.Value) return;
        if (ConfiguredObjects.Contains(internalName)) return;
        if (MissingConfigs.Add(internalName))
        {
            if (AutoMapPinsPlugin.SilentDiscoveryEnabled.Value) return;
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                $"discovered object '{internalName}' that was not configured", icon: Assets.GetIcon("amp"));
            Log.LogWarning($"discovered new game object named '{internalName}' that has no configuration");
        }
    }
}
