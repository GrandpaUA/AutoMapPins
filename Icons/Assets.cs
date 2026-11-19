using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AutoMapPins.Common;
using AutoMapPins.Data;
using BepInEx;
using TMPro;
using UnityEngine;
using Resources = AutoMapPins.Properties.Resources;

namespace AutoMapPins.Icons;

internal abstract class Assets
{
    private static readonly MethodInfo? LoadImageMethod =
        Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
            ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

    private static readonly Sprite DefaultIcon = LoadSpriteFromTexture(Resources.Dot);
    private static readonly HashSet<string> WarnedMissingIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> CustomIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> IconOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> CustomIconScales = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Minimap.PinType, (Sprite Icon, float Scale)> VanillaMapOverrides = new();
    // ZoneSystem location IDs are strings, not Minimap.PinType values (location pins use None).
    private static readonly Dictionary<string, (Sprite Icon, float Scale)> LocationMapOverrides = new(StringComparer.Ordinal);
    // Images decoded from an entry's own PNG, per group. An entry whose PNG cannot be
    // read on a later reload keeps this last working image and size, while every other
    // entry still applies its edit. Through 2.3.16 one unreadable PNG made each hot reload
    // reject the whole icon set, so `size`/new icons applied only after a game restart.
    private sealed class LoadedFile
    {
        internal LoadedFile(Sprite sprite, Texture2D texture, float scale)
        {
            Sprite = sprite;
            Texture = texture;
            Scale = scale;
        }

        internal readonly Sprite Sprite;
        internal readonly Texture2D Texture;
        internal readonly float Scale;
    }

    private static readonly Dictionary<string, LoadedFile> LoadedCustomFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, LoadedFile> LoadedOverrideFiles = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Sprite> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        { "amp", LoadSpriteFromTexture(Resources.AMP) },
        { "axe", LoadSpriteFromTexture(Resources.Axe) },
        { "axe48", LoadSpriteFromTexture(Resources.Axe48) },
        { "berry", LoadSpriteFromTexture(Resources.Berry) },
        { "berry48", LoadSpriteFromTexture(Resources.Berry48) },
        { "bones", LoadSpriteFromTexture(Resources.Bones) },
        { "bones48", LoadSpriteFromTexture(Resources.Bones48) },
        { "dot", LoadSpriteFromTexture(Resources.Dot) },
        { "dot48", LoadSpriteFromTexture(Resources.Dot48) },
        { "dungeon", LoadSpriteFromTexture(Resources.Dungeon) },
        { "dungeon48", LoadSpriteFromTexture(Resources.Dungeon48) },
        { "fire", LoadSpriteFromTexture(Resources.Fire) },
        { "fire48", LoadSpriteFromTexture(Resources.Fire48) },
        { "flower", LoadSpriteFromTexture(Resources.Flower) },
        { "flower48", LoadSpriteFromTexture(Resources.Flower48) },
        { "hand", LoadSpriteFromTexture(Resources.Hand) },
        { "hand48", LoadSpriteFromTexture(Resources.Hand48) },
        { "hay", LoadSpriteFromTexture(Resources.Hay) },
        { "hay48", LoadSpriteFromTexture(Resources.Hay48) },
        { "herb", LoadSpriteFromTexture(Resources.Herb) },
        { "herb48", LoadSpriteFromTexture(Resources.Herb48) },
        { "island", LoadSpriteFromTexture(Resources.Island) },
        { "island48", LoadSpriteFromTexture(Resources.Island48) },
        { "mine", LoadSpriteFromTexture(Resources.Mine) },
        { "mine48", LoadSpriteFromTexture(Resources.Mine48) },
        { "monument", LoadSpriteFromTexture(Resources.Monument) },
        { "monument48", LoadSpriteFromTexture(Resources.Monument48) },
        { "mushroom", LoadSpriteFromTexture(Resources.Mushroom) },
        { "mushroom48", LoadSpriteFromTexture(Resources.Mushroom48) },
        { "portal", LoadSpriteFromTexture(Resources.Portal) },
        { "portal48", LoadSpriteFromTexture(Resources.Portal48) },
        { "rune", LoadSpriteFromTexture(Resources.Rune) },
        { "rune48", LoadSpriteFromTexture(Resources.Rune48) },
        { "seed", LoadSpriteFromTexture(Resources.Seed) },
        { "seed48", LoadSpriteFromTexture(Resources.Seed48) },
        { "spawner", LoadSpriteFromTexture(Resources.Spawner) },
        { "spawner48", LoadSpriteFromTexture(Resources.Spawner48) },
        { "temple", LoadSpriteFromTexture(Resources.Temple) },
        { "temple48", LoadSpriteFromTexture(Resources.Temple48) },
        { "treasure", LoadSpriteFromTexture(Resources.Treasure) },
        { "treasure48", LoadSpriteFromTexture(Resources.Treasure48) },
        { "tree", LoadSpriteFromTexture(Resources.Tree) },
        { "tree48", LoadSpriteFromTexture(Resources.Tree48) },
        { "village", LoadSpriteFromTexture(Resources.Village) },
        { "village48", LoadSpriteFromTexture(Resources.Village48) },
        { "whale", LoadSpriteFromTexture(Resources.Whale) },
        { "whale48", LoadSpriteFromTexture(Resources.Whale48) },
    };

    internal static Sprite GetIcon(string iconName)
    {
        if (String.IsNullOrWhiteSpace(iconName) || String.Equals(iconName, Constants.NoConfig, StringComparison.OrdinalIgnoreCase))
            return DefaultIcon;

        if (IconOverrides.TryGetValue(iconName, out Sprite iconOverride)) return iconOverride;
        if (CustomIcons.TryGetValue(iconName, out Sprite customIcon)) return customIcon;
        if (Icons.TryGetValue(iconName, out Sprite result)) return result;

        if (WarnedMissingIcons.Add(iconName))
            AutoMapPinsPlugin.Log.LogWarning($"icon '{iconName}' was not found; using default icon");
        return DefaultIcon;
    }

    internal static float GetIconScale(string? iconName) =>
        iconName != null && CustomIconScales.TryGetValue(iconName, out float scale) ? scale : 1f;

    internal static bool TryGetVanillaMapOverride(Minimap.PinType type, out Sprite icon, out float scale)
    {
        if (VanillaMapOverrides.TryGetValue(type, out var configured))
        {
            icon = configured.Icon;
            scale = configured.Scale;
            return true;
        }
        icon = null!;
        scale = 1f;
        return false;
    }

    internal static bool HasLocationMapOverrides => LocationMapOverrides.Count > 0;

    internal static bool TryGetLocationMapOverride(string locationId, out Sprite icon, out float scale)
    {
        if (LocationMapOverrides.TryGetValue(locationId, out var configured))
        {
            icon = configured.Icon;
            scale = configured.Scale;
            return true;
        }
        icon = null!;
        scale = 1f;
        return false;
    }

    private static float GetConfiguredScale(Config.CustomIconConfig? config)
    {
        if (config?.Size == null) return 1f;
        if (config.Size is >= 1 and <= 300) return config.Size.Value / 100f;
        AutoMapPinsPlugin.Log.LogWarning($"icon size '{config.Size}' is outside 1..300%; using 100%");
        return 1f;
    }

    // Applies the new icon config and returns whether every configured PNG was read.
    // A false result never rejects the other entries: it only asks the caller for a
    // bounded retry, e.g. while an image editor still holds the PNG open.
    internal static bool LoadCustomIconConfig(Config.IconConfigFile iconConfig)
    {
        var newCustomIcons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        var newIconOverrides = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        var newCustomFiles = new Dictionary<string, LoadedFile>(StringComparer.OrdinalIgnoreCase);
        var newOverrideFiles = new Dictionary<string, LoadedFile>(StringComparer.OrdinalIgnoreCase);

        bool customFilesRead = LoadIconGroup(iconConfig.Icons, newCustomIcons, newCustomFiles, LoadedCustomFiles, "custom icon", out int loadedCustomIcons);
        bool overrideFilesRead = LoadIconGroup(iconConfig.VanillaOverrides, newIconOverrides, newOverrideFiles, LoadedOverrideFiles, "vanilla icon override", out int loadedOverrides);

        // Resolve aliases only against the NEW icon set, including multi-hop
        // fallback chains. Never keep an old sprite about to be destroyed.
        for (int pass = 0; pass < newCustomIcons.Count + newIconOverrides.Count; pass++)
        {
            bool changed = RepairFallbackIcons(iconConfig.Icons, newCustomIcons, newCustomFiles, newIconOverrides, newCustomIcons);
            changed |= RepairFallbackIcons(iconConfig.VanillaOverrides, newIconOverrides, newOverrideFiles, newIconOverrides, newCustomIcons);
            if (!changed) break;
        }
        WarnUnresolvedFallbacks(iconConfig.Icons, newCustomIcons, newCustomFiles);
        WarnUnresolvedFallbacks(iconConfig.VanillaOverrides, newIconOverrides, newOverrideFiles);

        Dictionary<Sprite, Texture2D> oldOwned = OwnedImages();

        CustomIcons.Clear();
        IconOverrides.Clear();
        CustomIconScales.Clear();
        VanillaMapOverrides.Clear();
        LocationMapOverrides.Clear();
        LoadedCustomFiles.Clear();
        LoadedOverrideFiles.Clear();
        foreach (var file in newCustomFiles) LoadedCustomFiles[file.Key] = file.Value;
        foreach (var file in newOverrideFiles) LoadedOverrideFiles[file.Key] = file.Value;

        foreach (var icon in newCustomIcons)
        {
            CustomIcons[icon.Key] = icon.Value;
            CustomIconScales[icon.Key] = GetEntryScale(icon.Key, iconConfig.Icons, newCustomFiles);
        }
        foreach (var iconOverride in newIconOverrides)
        {
            IconOverrides[iconOverride.Key] = iconOverride.Value;
            float scale = GetEntryScale(iconOverride.Key, iconConfig.VanillaOverrides, newOverrideFiles);
            CustomIconScales[iconOverride.Key] = scale;
            if (Enum.TryParse(iconOverride.Key, true, out Minimap.PinType type) &&
                Enum.IsDefined(typeof(Minimap.PinType), type))
            {
                // None is shared by location pins, AMP pins and other unsaved pins.
                // Never apply an override to all of them merely because its key is None.
                if (type != Minimap.PinType.None && iconOverride.Value != DefaultIcon)
                    VanillaMapOverrides[type] = (iconOverride.Value, scale);
                continue;
            }
            // Non-enum keys may name ZoneSystem locations (or legacy AMP icon names).
            // The minimap will match these only against actual vanilla location pins.
            if (iconOverride.Value != DefaultIcon)
                LocationMapOverrides[iconOverride.Key] = (iconOverride.Value, scale);
        }
        Dictionary<Sprite, Texture2D> newOwned = OwnedImages();
        foreach (var owned in oldOwned)
        {
            if (newOwned.ContainsKey(owned.Key)) continue;
            DestroyUnityObject(owned.Key);
            DestroyUnityObject(owned.Value);
        }

        AutoMapPinsPlugin.Log.LogInfo(
            $"loaded '{loadedCustomIcons}' custom icons and '{loadedOverrides}' vanilla icon overrides");
        return customFilesRead && overrideFilesRead;
    }

    private static Dictionary<Sprite, Texture2D> OwnedImages()
    {
        var owned = new Dictionary<Sprite, Texture2D>();
        foreach (LoadedFile file in LoadedCustomFiles.Values) owned[file.Sprite] = file.Texture;
        foreach (LoadedFile file in LoadedOverrideFiles.Values) owned[file.Sprite] = file.Texture;
        return owned;
    }

    // An image kept from an earlier load keeps the size it was shown with; everything
    // else (fresh PNGs, fallback-only aliases) uses the size now in the YAML.
    private static float GetEntryScale(string key, Dictionary<string, Config.CustomIconConfig>? configs,
        Dictionary<string, LoadedFile> files)
    {
        if (files.TryGetValue(key, out LoadedFile file)) return file.Scale;
        return configs != null && configs.TryGetValue(key, out Config.CustomIconConfig config) ? GetConfiguredScale(config) : 1f;
    }

    private static void WarnUnresolvedFallbacks(Dictionary<string, Config.CustomIconConfig>? configs,
        Dictionary<string, Sprite> loaded, Dictionary<string, LoadedFile> files)
    {
        if (configs == null) return;
        foreach (var entry in configs)
        {
            string? fallbackIcon = entry.Value?.FallbackIcon;
            if (String.IsNullOrWhiteSpace(fallbackIcon) || files.ContainsKey(entry.Key)) continue;
            if (!loaded.TryGetValue(entry.Key, out Sprite sprite) || sprite == DefaultIcon)
                AutoMapPinsPlugin.Log.LogWarning(
                    $"fallback icon '{fallbackIcon}' of icon '{entry.Key}' was not found; using default icon");
        }
    }

    // Fallback icons are resolved after both groups are loaded: a fallback may reference
    // another icon from the same config file, which does not exist in the previously
    // loaded set yet when the referencing icon itself fails to load.
    private static bool RepairFallbackIcons(
        Dictionary<string, Config.CustomIconConfig>? iconConfigs,
        Dictionary<string, Sprite> targetGroup,
        Dictionary<string, LoadedFile> targetFiles,
        Dictionary<string, Sprite> overridesLookup,
        Dictionary<string, Sprite> customLookup)
    {
        if (iconConfigs == null) return false;
        bool changed = false;

        foreach (var iconConfig in iconConfigs)
        {
            Config.CustomIconConfig? configured = iconConfig.Value;
            if (configured == null || String.IsNullOrWhiteSpace(configured.FallbackIcon)) continue;
            if (!targetGroup.TryGetValue(iconConfig.Key, out Sprite sprite)) continue;
            // The entry's own image (new or kept) wins. Everything else aliases
            // the NEW set, never an old sprite this reload is about to dispose.
            if (targetFiles.ContainsKey(iconConfig.Key)) continue;

            string fallbackName = configured.FallbackIcon!;
            if (overridesLookup.TryGetValue(fallbackName, out Sprite resolved) ||
                customLookup.TryGetValue(fallbackName, out resolved) ||
                Icons.TryGetValue(fallbackName, out resolved))
            {
                if (resolved != DefaultIcon && !ReferenceEquals(resolved, sprite))
                {
                    targetGroup[iconConfig.Key] = resolved;
                    changed = true;
                }
            }
        }
        return changed;
    }

    // Returns false when at least one configured PNG could not be read this time.
    private static bool LoadIconGroup(
        Dictionary<string, Config.CustomIconConfig>? iconConfigs,
        Dictionary<string, Sprite> target,
        Dictionary<string, LoadedFile> loadedFiles,
        Dictionary<string, LoadedFile> previousFiles,
        string groupName,
        out int loaded)
    {
        loaded = 0;
        if (iconConfigs == null || iconConfigs.Count == 0) return true;

        bool allFilesRead = true;
        foreach (var iconConfig in iconConfigs)
        {
            if (String.IsNullOrWhiteSpace(iconConfig.Key))
            {
                AutoMapPinsPlugin.Log.LogWarning($"skipping {groupName} with empty name");
                continue;
            }

            Config.CustomIconConfig? configured = iconConfig.Value;
            target[iconConfig.Key] = DefaultIcon; // a fallbackIcon is resolved against the new set later
            loaded++;
            if (configured == null || String.IsNullOrWhiteSpace(configured.File))
            {
                // A fallback-only entry is intentional (the tester has five). Neither
                // file nor fallback is a config choice too: same result as at startup.
                if (String.IsNullOrWhiteSpace(configured?.FallbackIcon))
                    AutoMapPinsPlugin.Log.LogWarning(
                        $"{groupName} '{iconConfig.Key}' defines neither a PNG file nor a fallback icon; using default icon");
                continue;
            }

            if (TryLoadConfiguredSprite(iconConfig.Key, configured.File!, out Sprite sprite, out Texture2D texture))
            {
                target[iconConfig.Key] = sprite;
                loadedFiles[iconConfig.Key] = new LoadedFile(sprite, texture, GetConfiguredScale(configured));
                continue;
            }

            allFilesRead = false;
            if (previousFiles.TryGetValue(iconConfig.Key, out LoadedFile previous))
            {
                AutoMapPinsPlugin.Log.LogWarning(
                    $"keeping last working image and size of {groupName} '{iconConfig.Key}' until its PNG can be read");
                target[iconConfig.Key] = previous.Sprite;
                loadedFiles[iconConfig.Key] = previous;
            }
        }

        return allFilesRead;
    }

    private static bool TryLoadConfiguredSprite(
        string iconName,
        string configuredFile,
        out Sprite sprite,
        out Texture2D texture)
    {
        sprite = DefaultIcon;
        texture = null!;
        string iconPath = configuredFile;
        try
        {
            if (!TryResolveIconPath(configuredFile, out iconPath))
            {
                AutoMapPinsPlugin.Log.LogWarning($"icon '{iconName}' file path '{configuredFile}' is not allowed; using fallback");
                return false;
            }

            if (!String.Equals(Path.GetExtension(iconPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                AutoMapPinsPlugin.Log.LogWarning($"icon '{iconName}' file '{iconPath}' is not a PNG file; using fallback");
                return false;
            }

            if (!File.Exists(iconPath))
            {
                AutoMapPinsPlugin.Log.LogWarning($"icon '{iconName}' file '{iconPath}' does not exist; using fallback");
                return false;
            }

            Texture2D loadedTexture = LoadTextureFromRaw(File.ReadAllBytes(iconPath));
            try
            {
                sprite = LoadSpriteFromTexture(loadedTexture);
                texture = loadedTexture;
                return true;
            }
            catch
            {
                DestroyUnityObject(loadedTexture);
                throw;
            }
        }
        catch (Exception e)
        {
            AutoMapPinsPlugin.Log.LogWarning(
                $"failed loading icon '{iconName}' from '{iconPath}': {e.GetBaseException().Message}; using fallback");
            sprite = DefaultIcon;
            return false;
        }
    }

    private static bool TryResolveIconPath(string configuredPath, out string iconPath)
    {
        string configRoot = Path.GetFullPath(Paths.ConfigPath);
        string candidatePath = Path.GetFullPath(Path.Combine(configRoot, configuredPath));
        string configRootWithSeparator = configRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                         Path.DirectorySeparatorChar;

        bool insideConfigRoot = candidatePath.StartsWith(configRootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
                                String.Equals(candidatePath, configRoot, StringComparison.OrdinalIgnoreCase);
        iconPath = "";
        if (!insideConfigRoot) return false;

        // The configured root is the user's trust anchor (it may itself be a
        // managed-profile link). Reject links BELOW it, including the PNG file,
        // so lexical containment cannot traverse a junction into another tree.
        // This is defense in depth, not a race-free sandbox against local writers.
        string rootComparison = configRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? current = candidatePath;
        while (!String.Equals(current?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   rootComparison, StringComparison.OrdinalIgnoreCase))
        {
            if (String.IsNullOrEmpty(current)) return false;
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return false;
            current = Path.GetDirectoryName(current);
        }
        iconPath = candidatePath;
        return true;
    }

    private static void DestroyUnityObject(UnityEngine.Object? unityObject)
    {
        if (unityObject == null) return;
        UnityEngine.Object.Destroy(unityObject);
    }

    private static Texture2D LoadTextureFromRaw(byte[] bytes)
    {
        Texture2D tex = new Texture2D(2, 2);

        try
        {
            if (LoadImageMethod == null)
                throw new InvalidOperationException("Could not find UnityEngine.ImageConversion.LoadImage at runtime.");

            object? result = LoadImageMethod.Invoke(null, new object[] { tex, bytes });
            if (result is bool loaded && !loaded)
                throw new InvalidOperationException("UnityEngine.ImageConversion.LoadImage returned false.");

            return tex;
        }
        catch
        {
            DestroyUnityObject(tex);
            throw;
        }
    }

    private static Sprite LoadSpriteFromTexture(Texture2D spriteTexture, float pixelsPerUnit = 100f)
    {
        AutoMapPinsPlugin.Log.LogDebug($"Making Sprite from Texture {spriteTexture}");
        return Sprite.Create(
            spriteTexture,
            new Rect(0.0f, 0.0f, spriteTexture.width, spriteTexture.height),
            new Vector2(0.0f, 0.0f),
            pixelsPerUnit);
    }

    private static Sprite LoadSpriteFromTexture(byte[] bytes)
    {
        return LoadSpriteFromTexture(LoadTextureFromRaw(bytes));
    }
}