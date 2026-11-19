#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Icons;
using UnityEngine;

internal static class IconAssetTests
{
    private static int _failures;

    private static void Check(string name, bool condition)
    {
        if (condition) Console.WriteLine($"PASS {name}");
        else { Console.WriteLine($"FAIL {name}"); _failures++; }
    }

    private static byte[] FixturePng(string name) => File.ReadAllBytes(Path.Combine("Icons", "png", name));

    public static int Main()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AutoMapPinsIconAssetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        BepInEx.Paths.ConfigPath = tempRoot;

        string first = Path.Combine(tempRoot, "first.png");
        string second = Path.Combine(tempRoot, "second.png");
        File.WriteAllBytes(first, FixturePng("Dot.png"));
        File.WriteAllBytes(second, FixturePng("Fire.png"));

        Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["custom"] = new() { File = "first.png" }
            }
        });
        Sprite firstSprite = Assets.GetIcon("custom");
        Check("valid icon load returns custom sprite", firstSprite.Name.StartsWith("created:", StringComparison.Ordinal));
        File.WriteAllBytes(Path.Combine(tempRoot, "truncated.png"), FixturePng("Dot.png")[..20]);
        bool acceptedTruncated = Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["custom"] = new() { File = "truncated.png" }
            }
        });
        Check("truncated PNG keeps last-known-good icon", !acceptedTruncated &&
            ReferenceEquals(firstSprite, Assets.GetIcon("custom")) && !firstSprite.Destroyed);

        Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["custom"] = new() { File = "bad\0path.png", FallbackIcon = "dot" }
            }
        });
        Check("malformed path keeps last-known-good", ReferenceEquals(firstSprite, Assets.GetIcon("custom")));

        // Through 2.3.16 the broken neighbour rejected this valid edit too (tester 2026-11-12).
        int destroyedBeforePartialReload = UnityEngine.Object.DestroyedSprites;
        bool partialClean = Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["custom"] = new() { File = "second.png" },
                ["broken"] = new() { File = "bad\0path.png" }
            }
        });
        Check("valid entry applies although a neighbour's PNG path is broken",
            !partialClean && !ReferenceEquals(firstSprite, Assets.GetIcon("custom")) && firstSprite.Destroyed);
        Check("partial reload retires only the replaced sprite and keeps no stale one",
            UnityEngine.Object.DestroyedSprites == destroyedBeforePartialReload + 1 &&
            ReferenceEquals(Assets.GetIcon("broken"), Assets.GetIcon(Constants.NoConfig)));

        int destroyedBeforeReplacement = UnityEngine.Object.DestroyedSprites;
        Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["custom"] = new() { File = "second.png" }
            }
        });
        Sprite secondSprite = Assets.GetIcon("custom");
        Check("accepted reload replaces icon", !ReferenceEquals(firstSprite, secondSprite));
        Check("accepted reload disposes replaced sprite", UnityEngine.Object.DestroyedSprites == destroyedBeforeReplacement + 1);

        int destroyedTexturesBeforeLoadFailure = UnityEngine.Object.DestroyedTextures;
        MethodInfo loadTexture = typeof(Assets).GetMethod("LoadTextureFromRaw", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { loadTexture.Invoke(null, new object[] { Array.Empty<byte>() }); }
        catch (TargetInvocationException) { }
        Check("failed LoadImage destroys allocated texture", UnityEngine.Object.DestroyedTextures == destroyedTexturesBeforeLoadFailure + 1);

        int warningsBeforeNoConfig = AutoMapPins.AutoMapPinsPlugin.Log.Warnings;
        Sprite noConfig = Assets.GetIcon(Constants.NoConfig);
        Check("NoConfig returns default icon", noConfig != null);
        Check("NoConfig does not warn", AutoMapPins.AutoMapPinsPlugin.Log.Warnings == warningsBeforeNoConfig);

        Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig> { ["custom"] = new() { File = "first.png", Size = 150 } },
            VanillaOverrides = new Dictionary<string, Config.CustomIconConfig>
            {
                ["Icon0"] = new() { File = "second.png", Size = 200 },
                ["None"] = new() { File = "first.png", Size = 150 },
                ["legacyName"] = new() { File = "second.png" },
                ["StartTemple"] = new() { File = "first.png", Size = 125 },
                ["Vendor_BlackForest"] = new() { File = "second.png", Size = 150 },
                ["Hildir_camp"] = new() { File = "first.png" },
                ["BogWitch_Camp"] = new() { File = "second.png" },
                ["AncientUpgradeStation"] = new() { File = "first.png" }
            }
        });
        Check("custom icon size is map scale percent", Assets.GetIconScale("custom") == 1.5f);
        Check("vanilla map type override resolves sprite and scale",
            Assets.TryGetVanillaMapOverride(Minimap.PinType.Icon0, out Sprite replacement, out float scale) &&
            replacement != null && scale == 2f);
        Check("None and unknown key never replace vanilla map icons",
            !Assets.TryGetVanillaMapOverride(Minimap.PinType.None, out _, out _));
        Check("legacy vanillaOverrides name still resolves for AMP pins",
            Assets.GetIcon("legacyName").Name.StartsWith("created:", StringComparison.Ordinal));
        Check("five string location IDs resolve independent sprites and sizes",
            Assets.TryGetLocationMapOverride("StartTemple", out Sprite templeIcon, out float templeScale) &&
            Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite traderIcon, out float traderScale) &&
            Assets.TryGetLocationMapOverride("Hildir_camp", out _, out _) &&
            Assets.TryGetLocationMapOverride("BogWitch_Camp", out _, out _) &&
            Assets.TryGetLocationMapOverride("AncientUpgradeStation", out _, out _) &&
            !ReferenceEquals(templeIcon, traderIcon) && templeScale == 1.25f && traderScale == 1.5f);
        Check("unknown, wrong-case, enum and None keys never match a real location ID",
            !Assets.TryGetLocationMapOverride("UnknownLocation", out _, out _) &&
            !Assets.TryGetLocationMapOverride("starttemple", out _, out _) &&
            !Assets.TryGetLocationMapOverride("Icon0", out _, out _) &&
            !Assets.TryGetLocationMapOverride("None", out _, out _));
        var small = new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig> { ["custom"] = new() { File = "first.png", Size = 10 } },
            VanillaOverrides = new Dictionary<string, Config.CustomIconConfig> { ["Icon0"] = new() { File = "second.png", Size = 50 } }
        };
        Assets.LoadCustomIconConfig(small);
        Check("10 percent AMP size is actually smaller", Math.Abs(Assets.GetIconScale("custom") - 0.1f) < 0.0001f);
        Check("50 percent vanilla size is actually smaller",
            Assets.TryGetVanillaMapOverride(Minimap.PinType.Icon0, out _, out float half) && half == 0.5f);
        small.Icons!["custom"].Size = 1;
        Assets.LoadCustomIconConfig(small);
        Check("smallest positive 1 percent is allowed", Assets.GetIconScale("custom") == 0.01f);
        small.Icons["custom"].Size = 0;
        Assets.LoadCustomIconConfig(small);
        Check("zero size cannot silently hide icon", Assets.GetIconScale("custom") == 1f);
        small.Icons["custom"].Size = 301;
        Assets.LoadCustomIconConfig(small);
        Check("oversized percent remains bounded", Assets.GetIconScale("custom") == 1f);
        var locationConfig = new Config.IconConfigFile
        {
            VanillaOverrides = new Dictionary<string, Config.CustomIconConfig>
            {
                ["Vendor_BlackForest"] = new() { File = "first.png", Size = 175 }
            }
        };
        Assets.LoadCustomIconConfig(locationConfig);
        Check("initial location sprite and scale load", Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite beforeLocationEdit, out float locationScale) &&
            locationScale == 1.75f);
        File.WriteAllBytes(first, FixturePng("Portal.png"));
        Assets.LoadCustomIconConfig(locationConfig);
        Check("same-YAML PNG edit replaces location sprite and retires old",
            Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite afterLocationEdit, out float editedScale) &&
            !ReferenceEquals(beforeLocationEdit, afterLocationEdit) && beforeLocationEdit.Destroyed && editedScale == 1.75f);
        bool acceptedBrokenLocation = Assets.LoadCustomIconConfig(new Config.IconConfigFile
        {
            VanillaOverrides = new Dictionary<string, Config.CustomIconConfig>
            {
                ["Vendor_BlackForest"] = new() { File = "missing.png", Size = 25 }
            }
        });
        Sprite retainedLocation = null!;
        Check("missing location PNG retains last good sprite and scale", !acceptedBrokenLocation &&
            Assets.TryGetLocationMapOverride("Vendor_BlackForest", out retainedLocation, out float retainedScale) &&
            ReferenceEquals(retainedLocation, afterLocationEdit) && !retainedLocation.Destroyed && retainedScale == 1.75f);
        Assets.LoadCustomIconConfig(locationConfig);
        Check("valid location PNG recovery replaces retired sprite",
            Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite recoveredLocation, out _) &&
            !ReferenceEquals(recoveredLocation, retainedLocation) && retainedLocation.Destroyed);
        Assets.LoadCustomIconConfig(new Config.IconConfigFile());
        Check("removing location entry restores default lookup", !Assets.TryGetLocationMapOverride("Vendor_BlackForest", out _, out _));
        var fallbackOnly = new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["aliasChain"] = new() { FallbackIcon = "aliasCustom" },
                ["custom"] = new() { File = "first.png", Size = 10 },
                ["Ore_Gucksack"] = new() { FallbackIcon = "mine48", Size = 128 },
                ["Seed_Kale"] = new() { FallbackIcon = "seed", Size = 128 },
                ["Plant_Oak"] = new() { FallbackIcon = "tree48", Size = 128 },
                ["Camp_DvergrsAsh"] = new() { FallbackIcon = "temple48", Size = 128 },
                ["Misk_StoneRock"] = new() { FallbackIcon = "hand48", Size = 128 },
                ["aliasCustom"] = new() { FallbackIcon = "custom" }
            }
        };
        Assets.LoadCustomIconConfig(fallbackOnly);
        Sprite beforePngEdit = Assets.GetIcon("custom");
        File.WriteAllBytes(first, FixturePng("Fire.png"));
        Assets.LoadCustomIconConfig(fallbackOnly);
        Check("fallback-only tester entry permits unrelated PNG reload",
            !ReferenceEquals(beforePngEdit, Assets.GetIcon("custom")) && beforePngEdit.Destroyed);
        Check("fallback-only multi-hop alias follows new sprite, not destroyed old",
            ReferenceEquals(Assets.GetIcon("aliasCustom"), Assets.GetIcon("custom")) &&
            ReferenceEquals(Assets.GetIcon("aliasChain"), Assets.GetIcon("custom")) &&
            !Assets.GetIcon("aliasChain").Destroyed);
        Check("fallback-only built-in icon remains usable",
            !Assets.GetIcon("Ore_Gucksack").Destroyed && Assets.GetIconScale("Ore_Gucksack") == 1.28f);
        Sprite lastWorking = Assets.GetIcon("custom");
        var brokenWithFallbacks = new Config.IconConfigFile
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>(fallbackOnly.Icons!)
        };
        brokenWithFallbacks.Icons!["custom"] = new() { File = "bad\0path.png", FallbackIcon = "mine48" };
        bool brokenAccepted = Assets.LoadCustomIconConfig(brokenWithFallbacks);
        Check("broken configured PNG retains last good even with fallback-only entries",
            !brokenAccepted && ReferenceEquals(lastWorking, Assets.GetIcon("custom")) &&
            ReferenceEquals(lastWorking, Assets.GetIcon("aliasCustom")) && !lastWorking.Destroyed);
        Assets.LoadCustomIconConfig(new Config.IconConfigFile());
        Check("clearing override restores vanilla map lookup",
            !Assets.TryGetVanillaMapOverride(Minimap.PinType.Icon0, out _, out _) && Assets.GetIconScale("custom") == 1f);

        var throttle = new IconReloadThrottle();
        throttle.Schedule(0f);
        throttle.Schedule(0.2f);
        Check("icon reload waits until image writes finish", !throttle.TryConsume(0.34f) && !throttle.TryConsume(0.5f));
        Check("debounced icon reload fires exactly once", throttle.TryConsume(0.56f) && !throttle.TryConsume(0.57f));
        throttle.Schedule(1f);
        throttle.Clear();
        Check("closing plugin cancels pending image reload", !throttle.TryConsume(3f));
        throttle.ScheduleChange(10f);
        Check("changed file primes one reload", throttle.TryConsume(10.4f));
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            float now = 10f + 2f * attempt;
            Check($"transient PNG failure schedules retry {attempt}",
                throttle.RetryAfterFailure(now) && !throttle.TryConsume(now + 0.9f) && throttle.TryConsume(now + 1.1f));
        }
        Check("persistent PNG failure exhausts bounded retries", !throttle.RetryAfterFailure(18f));
        throttle.ScheduleChange(20f);
        Check("new file event rearms retries", throttle.TryConsume(20.4f) && throttle.RetryAfterFailure(20.5f));
        TestBrokenEntryDoesNotBlockHotChanges(tempRoot);
        TestIconStorageWatcher(tempRoot);
        TestAtomicYamlRenameGrace(tempRoot);
        TestArchivedTesterIconsWhenAvailable();

        Directory.Delete(tempRoot, true);
        return _failures == 0 ? 0 : 1;
    }

    // Tester report 2026-11-12 (2.3.16): `size` and new `iconName` targets applied only
    // after a game restart. At startup a missing PNG merely falls back, but every later
    // hot reload rejected the WHOLE icon set because of that one entry.
    private static void TestBrokenEntryDoesNotBlockHotChanges(string tempRoot)
    {
        Assets.LoadCustomIconConfig(new Config.IconConfigFile()); // fresh game start: nothing loaded yet
        File.WriteAllBytes(Path.Combine(tempRoot, "hot-good.png"), FixturePng("Dot.png"));
        File.WriteAllBytes(Path.Combine(tempRoot, "hot-kept.png"), FixturePng("Fire.png"));
        Config.IconConfigFile Make(int goodSize, string keptFile) => new()
        {
            Icons = new Dictionary<string, Config.CustomIconConfig>
            {
                ["hotGood"] = new() { File = "hot-good.png", Size = goodSize },
                ["hotKept"] = new() { File = keptFile, Size = 150 },
                ["hotMissing"] = new() { File = "AutoMapPins/no-such-icon.png", FallbackIcon = "mine48" },
                ["hotAlias"] = new() { FallbackIcon = "hotGood" }
            },
            VanillaOverrides = new Dictionary<string, Config.CustomIconConfig>
            {
                ["Icon0"] = new() { File = "hot-good.png", Size = goodSize }
            }
        };

        Sprite defaultIcon = Assets.GetIcon(Constants.NoConfig);
        Sprite builtInMine = Assets.GetIcon("mine48");
        Assets.LoadCustomIconConfig(Make(100, "hot-kept.png"));
        Sprite startupGood = Assets.GetIcon("hotGood");
        Check("startup tolerates one missing PNG and uses its fallback",
            !ReferenceEquals(startupGood, defaultIcon) && !startupGood.Destroyed && Assets.GetIconScale("hotGood") == 1f &&
            ReferenceEquals(Assets.GetIcon("hotMissing"), builtInMine));

        bool clean = Assets.LoadCustomIconConfig(Make(300, "hot-kept.png"));
        Check("hot size edit applies despite an unrelated missing PNG",
            Assets.GetIconScale("hotGood") == 3f &&
            Assets.TryGetVanillaMapOverride(Minimap.PinType.Icon0, out _, out float overrideScale) && overrideScale == 3f);
        Check("unrelated missing PNG still reports an incomplete reload for bounded retry", !clean);
        Check("hot reload replaces valid sprites, retires old ones and keeps fallbacks on the new set",
            !ReferenceEquals(startupGood, Assets.GetIcon("hotGood")) && startupGood.Destroyed &&
            ReferenceEquals(Assets.GetIcon("hotAlias"), Assets.GetIcon("hotGood")) &&
            ReferenceEquals(Assets.GetIcon("hotMissing"), builtInMine));

        var withNewIcon = Make(300, "hot-kept.png");
        withNewIcon.Icons!["hotNew"] = new() { File = "hot-good.png", Size = 50 };
        Assets.LoadCustomIconConfig(withNewIcon);
        Sprite hotNew = Assets.GetIcon("hotNew");
        Check("icon added while another PNG is missing is usable for a new iconName",
            !ReferenceEquals(hotNew, defaultIcon) && !hotNew.Destroyed &&
            !ReferenceEquals(hotNew, Assets.GetIcon("hotGood")) && Assets.GetIconScale("hotNew") == 0.5f);

        Sprite keptBefore = Assets.GetIcon("hotKept");
        Assets.LoadCustomIconConfig(Make(200, "AutoMapPins/renamed-away.png"));
        Check("entry whose own PNG disappears keeps its last working image and size",
            ReferenceEquals(keptBefore, Assets.GetIcon("hotKept")) && !keptBefore.Destroyed &&
            Assets.GetIconScale("hotKept") == 1.5f && Assets.GetIconScale("hotGood") == 2f);
        Assets.LoadCustomIconConfig(Make(200, "hot-kept.png"));
        Check("restored PNG replaces the kept image and retires it",
            !ReferenceEquals(keptBefore, Assets.GetIcon("hotKept")) && keptBefore.Destroyed);

        Sprite lastGood = Assets.GetIcon("hotGood");
        Sprite lastKept = Assets.GetIcon("hotKept");
        Assets.LoadCustomIconConfig(new Config.IconConfigFile());
        Check("clearing the config retires every hot-reload sprite",
            ReferenceEquals(Assets.GetIcon("hotGood"), defaultIcon) && Assets.GetIconScale("hotGood") == 1f &&
            lastGood.Destroyed && lastKept.Destroyed);
    }

    private static void TestArchivedTesterIconsWhenAvailable()
    {
        string? root = Environment.GetEnvironmentVariable("AMP_TESTER_ICON_CONFIG_ROOT");
        if (String.IsNullOrWhiteSpace(root)) return; // avoid private fixture dependency in CI
        string previousRoot = BepInEx.Paths.ConfigPath;
        BepInEx.Paths.ConfigPath = root!;
        var storage = new IconConfigStorage("FixItFelix.AutoMapPins");
        try
        {
            var actual = storage.ReadIconConfigFiles();
            Check("actual archived tester icons YAML loads all 73 entries", actual.Icons?.Count == 73);
            Assets.LoadCustomIconConfig(actual);
            Check("actual tester configured PNG loads", Assets.GetIcon("Ore_Copper").Name.StartsWith("created:", StringComparison.Ordinal) &&
                Assets.GetIconScale("Ore_Copper") == 1.28f);
            Check("actual tester five fileless aliases remain usable", Assets.GetIcon("Ore_Gucksack") != null &&
                Assets.GetIcon("Seed_Kale") != null && Assets.GetIcon("Plant_Oak") != null &&
                Assets.GetIcon("Camp_DvergrsAsh") != null && Assets.GetIcon("Misk_StoneRock") != null);
            Sprite old = Assets.GetIcon("Ore_Copper");
            bool applied = Assets.LoadCustomIconConfig(actual); // no YAML edit; fileless aliases must not reject set
            Check("actual tester fallback entries never block reload", applied && old.Destroyed &&
                !ReferenceEquals(old, Assets.GetIcon("Ore_Copper")));
        }
        finally
        {
            storage.Dispose();
            BepInEx.Paths.ConfigPath = previousRoot;
        }
    }

    private static void TestAtomicYamlRenameGrace(string tempRoot)
    {
        string configPath = Path.Combine(tempRoot, "rename-case");
        Directory.CreateDirectory(configPath);
        string previousRoot = BepInEx.Paths.ConfigPath;
        BepInEx.Paths.ConfigPath = configPath;
        string yaml = Path.Combine(configPath, "FixItFelix.AutoMapPins.icons.yaml");
        string moved = yaml + ".saving";
        File.WriteAllBytes(Path.Combine(configPath, "icon.png"), FixturePng("Dot.png"));
        File.WriteAllText(yaml, "vanillaOverrides:\n  Vendor_BlackForest:\n    file: icon.png\n    size: 125\n");
        DateTime now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var storage = new IconConfigStorage("FixItFelix.AutoMapPins", () => now);
        try
        {
            Assets.LoadCustomIconConfig(storage.ReadIconConfigFiles());
            Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite before, out _);
            File.Move(yaml, moved);
            now = now.AddSeconds(1);
            var duringRename = storage.ReadIconConfigFiles();
            Check("atomic YAML rename gap retains cached location icon pending retry",
                storage.HasPendingMissingFiles && duringRename.VanillaOverrides?.ContainsKey("Vendor_BlackForest") == true &&
                Assets.TryGetLocationMapOverride("Vendor_BlackForest", out Sprite active, out _) &&
                ReferenceEquals(before, active) && !before.Destroyed);
            File.Move(moved, yaml);
            now = now.AddSeconds(1);
            storage.ReadIconConfigFiles();
            Check("YAML returning before grace cancels pending removal", !storage.HasPendingMissingFiles);
            File.Move(yaml, moved);
            now = now.AddSeconds(1);
            storage.ReadIconConfigFiles();
            Check("deliberate YAML deletion first waits for grace", storage.HasPendingMissingFiles);
            now = now.AddSeconds(3);
            var afterDeletion = storage.ReadIconConfigFiles();
            Assets.LoadCustomIconConfig(afterDeletion);
            Check("persistent YAML deletion restores vanilla location sprite", !storage.HasPendingMissingFiles &&
                !Assets.TryGetLocationMapOverride("Vendor_BlackForest", out _, out _) && before.Destroyed);
        }
        finally
        {
            storage.Dispose();
            BepInEx.Paths.ConfigPath = previousRoot;
        }
    }

    private static void TestIconStorageWatcher(string tempRoot)
    {
        string iconsDir = Path.Combine(tempRoot, "AutoMapPins", "icons");
        Directory.CreateDirectory(iconsDir);
        string iconFile = Path.Combine(iconsDir, "source.png");
        File.WriteAllBytes(iconFile, FixturePng("Dot.png"));
        string yamlFile = Path.Combine(tempRoot, "FixItFelix.AutoMapPins.icons.yaml");
        const string unchangedYaml = "icons:\n  live:\n    file: AutoMapPins/icons/source.png\n    size: 10\n";
        File.WriteAllText(yamlFile, unchangedYaml);
        var storage = new IconConfigStorage("FixItFelix.AutoMapPins");
        try
        {
            var config = storage.ReadIconConfigFiles();
            Check("real YAML loader reads 10 percent icon", config.Icons!["live"].Size == 10);
            Assets.LoadCustomIconConfig(config);
            Sprite before = Assets.GetIcon("live");
            using var signal = new ManualResetEventSlim(false);
            AutoMapPins.AutoMapPinsPlugin.IconChanged = () => signal.Set();
            File.WriteAllBytes(iconFile, FixturePng("Portal.png"));
            Check("actual recursive PNG watcher detects content replacement", signal.Wait(TimeSpan.FromSeconds(5)));
            AutoMapPins.AutoMapPinsPlugin.IconChanged = null;
            Assets.LoadCustomIconConfig(storage.ReadIconConfigFiles());
            Check("PNG replacement reload changes same-name sprite and retires old",
                !ReferenceEquals(before, Assets.GetIcon("live")) && before.Destroyed);
            Check("PNG watcher does not require YAML edit", File.ReadAllText(yamlFile) == unchangedYaml);
        }
        finally
        {
            AutoMapPins.AutoMapPinsPlugin.IconChanged = null;
            storage.Dispose();
        }
    }
}

public sealed class Minimap
{
    public enum PinType { Icon0, None }
}

namespace AutoMapPins.Common
{
    internal static class Constants { internal const string NoConfig = "n_a"; }
    internal abstract class HasLogger { protected static readonly AutoMapPins.TestLogger Log = AutoMapPins.AutoMapPinsPlugin.Log; }
}

namespace AutoMapPins.Data
{
    public abstract class Config
    {
        public sealed class IconConfigFile
        {
            public Dictionary<string, CustomIconConfig>? Icons = null!;
            public Dictionary<string, CustomIconConfig>? VanillaOverrides = null!;
        }

        public sealed class CustomIconConfig
        {
            public string? File = null!;
            public string? FallbackIcon = null!;
            public int? Size;
        }
    }
}

namespace AutoMapPins.Properties
{
    internal static class Resources
    {
        private static readonly byte[] Bytes = File.ReadAllBytes(Path.Combine("Icons", "png", "Dot.png"));
        public static byte[] AMP => Bytes; public static byte[] Axe => Bytes; public static byte[] Axe48 => Bytes;
        public static byte[] Berry => Bytes; public static byte[] Berry48 => Bytes; public static byte[] Bones => Bytes;
        public static byte[] Bones48 => Bytes; public static byte[] Dot => Bytes; public static byte[] Dot48 => Bytes;
        public static byte[] Dungeon => Bytes; public static byte[] Dungeon48 => Bytes; public static byte[] Fire => Bytes;
        public static byte[] Fire48 => Bytes; public static byte[] Flower => Bytes; public static byte[] Flower48 => Bytes;
        public static byte[] Hand => Bytes; public static byte[] Hand48 => Bytes; public static byte[] Hay => Bytes;
        public static byte[] Hay48 => Bytes; public static byte[] Herb => Bytes; public static byte[] Herb48 => Bytes;
        public static byte[] Island => Bytes; public static byte[] Island48 => Bytes; public static byte[] Mine => Bytes;
        public static byte[] Mine48 => Bytes; public static byte[] Monument => Bytes; public static byte[] Monument48 => Bytes;
        public static byte[] Mushroom => Bytes; public static byte[] Mushroom48 => Bytes; public static byte[] Portal => Bytes;
        public static byte[] Portal48 => Bytes; public static byte[] Rune => Bytes; public static byte[] Rune48 => Bytes;
        public static byte[] Seed => Bytes; public static byte[] Seed48 => Bytes; public static byte[] Spawner => Bytes;
        public static byte[] Spawner48 => Bytes; public static byte[] Temple => Bytes; public static byte[] Temple48 => Bytes;
        public static byte[] Treasure => Bytes; public static byte[] Treasure48 => Bytes; public static byte[] Tree => Bytes;
        public static byte[] Tree48 => Bytes; public static byte[] Village => Bytes; public static byte[] Village48 => Bytes;
        public static byte[] Whale => Bytes; public static byte[] Whale48 => Bytes;
    }
}

namespace AutoMapPins
{
    public static class AutoMapPinsPlugin
    {
        public static readonly TestLogger Log = new();
        public static Action? IconChanged;
        public static void ReadIconFileContent(object? sender, FileSystemEventArgs eventArgs) => IconChanged?.Invoke();
    }
    public sealed class TestLogger
    {
        public int Warnings;
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { Warnings++; Console.WriteLine("WARN " + message); }
    }
}

namespace BepInEx
{
    public static class Paths { public static string ConfigPath = Directory.GetCurrentDirectory(); }
    public static class ThreadingHelper { public static System.ComponentModel.ISynchronizeInvoke? SynchronizingObject => null; }
}

namespace TMPro { public sealed class TMP_FontAsset { } }

namespace UnityEngine
{
    public class Object
    {
        public static int DestroyedSprites;
        public static int DestroyedTextures;
        public bool Destroyed;
        public static void Destroy(Object obj)
        {
            obj.Destroyed = true;
            if (obj is Sprite) DestroyedSprites++;
            if (obj is Texture2D) DestroyedTextures++;
        }
    }

    public sealed class Texture2D : Object
    {
        public int width = 2;
        public int height = 2;
        public Texture2D(int width, int height) { this.width = width; this.height = height; }
    }

    public sealed class Sprite : Object
    {
        public readonly string Name;
        private Sprite(string name) { Name = name; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => new($"created:{texture.GetHashCode()}:{pixelsPerUnit}");
    }

    public readonly struct Rect { public Rect(float x, float y, float width, float height) { } }
    public readonly struct Vector2 { public Vector2(float x, float y) { } }

    public static class ImageConversion
    {
        // Offline stub, not Unity's decoder: require PNG signature and IEND.
        public static bool LoadImage(Texture2D texture, byte[] bytes)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            byte[] end = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
            if (bytes.Length < 33 + end.Length) return false;
            for (int i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) return false;
            for (int i = 0; i < end.Length; i++) if (bytes[bytes.Length - end.Length + i] != end[i]) return false;
            return true;
        }
    }
}
