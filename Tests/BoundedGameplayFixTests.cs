#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoMapPins;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Icons;
using AutoMapPins.Model;
using AutoMapPins.Patches;
using UnityEngine;

internal static class BoundedGameplayFixTests
{
    private static int _failures;

    private static void Check(string name, bool condition)
    {
        if (condition) Console.WriteLine($"PASS {name}");
        else { Console.WriteLine($"FAIL {name}"); _failures++; }
    }

    public static int Main()
    {
        TestYamlSemanticValidation();
        TestArchivedTesterConfigWhenAvailable();
        TestYamlReadConfigFilesLastKnownGoodRoundtrip();
        TestCategorySingleFileMigration();
        TestExplorationSkillRequirement();
        TestRegistryDefaultsAndRescan();
        TestMapPinIconDefault();
        TestPinComponentBoundedRescan();
        TestTemporaryPinVisibilityModeChanges();
        TestExplorePostfixUsesPositionalArguments();
        TestLoadedObjectScanSummary();
        TestGeneratedZoneScannerDoesNotCreateWorld();
        TestGeneratedPinRequestPolicy();
        TestGeneratedPinRequestLimiter();
        TestGeneratedPinWireProtocol();
        TestGeneratedPinResponseTracker();
        TestGeneratedPinNetworkTransport();
        TestGeneratedPinServiceLocalLifecycle();
        TestMinimapSavedMetadataLifecycle();
        TestVanillaMapIconOverridesAndSizes();
        TestPickerIconOverrides();
        TestLocationIdOverridesAndRollback();
        TestPermanentPinsSurviveTemporaryAbsentConfig();
        TestOptimizedPinUpdatesAndGrouping();
        TestVisibleComponentRescanRestoresMissingPinAndDeduplicates();
        TestSharedMapExcludesAndRestoresAllAmpPins();
        TestClearPinsRemovesOnlyAmpPins();
        TestGroupedToPermanentTransition();
        TestPrintMapPinsAllowsDuplicateUserPins();
        Console.WriteLine($"SUMMARY {(_failures == 0 ? "PASS" : "FAIL")} failures={_failures}");
        return _failures == 0 ? 0 : 1;
    }

    private static void TestYamlSemanticValidation()
    {
        using var scope = new TempConfigScope();
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            MethodInfo method = typeof(YamlFileStorage).GetMethod("TryDeserializeFile", BindingFlags.Instance | BindingFlags.NonPublic)!;

            object?[] nullCategoryArgs = { "null-category.yaml", "badCategory:\n", null! };
            bool nullCategoryAccepted = (bool)method.Invoke(storage, nullCategoryArgs)!;
            Check("YAML null category rejected", !nullCategoryAccepted);

            object?[] nullPinArgs = { "null-pin.yaml", "cat:\n  isActive: true\n  individualConfiguredObjects:\n    GoodObject:\n", null! };
            bool nullPinAccepted = (bool)method.Invoke(storage, nullPinArgs)!;
            Check("YAML null individual pin rejected", !nullPinAccepted);

            object?[] emptyNameArgs = { "empty-name.yaml", "cat:\n  isActive: true\n  categoryConfiguredObjects:\n    - ''\n", null! };
            bool emptyNameAccepted = (bool)method.Invoke(storage, emptyNameArgs)!;
            Check("YAML empty object name rejected", !emptyNameAccepted);

            object?[] invalidSkillArgs = { "invalid-skill.yaml", "cat:\n  isActive: true\n  skillRequire: 101\n", null! };
            bool invalidSkillAccepted = (bool)method.Invoke(storage, invalidSkillArgs)!;
            Check("YAML skill requirement outside 0..100 rejected", !invalidSkillAccepted);

            // Real tester YAML uses `iconname` in 88 categories; do not silently
            // discard its icon while accepting the rest of the category.
            var testerCase = storage.DeserializeAndMergeFileData(new Dictionary<string, string>
            {
                ["tester-icons.yaml"] = "ore:\n  isActive: true\n  iconname: Ore_Copper\n  individualConfiguredObjects:\n    rock4_copper:\n      IconNAME: Ore_Tin\n"
            });
            Check("tester lower-case category iconname resolves", testerCase["ore"].IconName == "Ore_Copper");
            Check("mixed-case individual icon name resolves", testerCase["ore"].IndividualConfiguredObjects!["rock4_copper"].IconName == "Ore_Tin");
        }
        finally { storage.Dispose(); }
    }

    private static void TestArchivedTesterConfigWhenAvailable()
    {
        string? path = Environment.GetEnvironmentVariable("AMP_TESTER_CATEGORY_YAML");
        if (String.IsNullOrWhiteSpace(path)) return; // private archive never required by normal CI
        using var scope = new TempConfigScope();
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            var parsed = storage.DeserializeAndMergeFileData(new Dictionary<string, string>
            {
                ["archived-tester-category.yaml"] = File.ReadAllText(path!)
            });
            Check("actual tester config parses and normalizes lower-case icon names",
                parsed.Count >= 80 && parsed["Ore_Iron"].IconName == "Ore_Iron" &&
                parsed["Location_BurialChambers"].IconName == "Location_BurialChambers");
            Check("actual tester active categories preserve skill gate", parsed["Ore_Copper"].SkillRequire == 20);
        }
        finally { storage.Dispose(); }
    }

    private static void TestYamlReadConfigFilesLastKnownGoodRoundtrip()
    {
        using var scope = new TempConfigScope();
        var file = Path.Combine(scope.Path, "FixItFelix.AutoMapPins.categories.packaged.yaml");
        const string valid = "packaged:\n  isActive: true\n  isPermanent: true\n  categoryConfiguredObjects:\n    - PackagedObject\n";
        File.WriteAllText(file, valid);
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            var first = storage.ReadConfigFiles();
            var merged = storage.DeserializeAndMergeFileData(first);
            Check("YAML packaged file accepted", merged.ContainsKey("packaged") && merged["packaged"].CategoryConfiguredObjects!.Contains("PackagedObject"));

            File.WriteAllText(file, "broken:\n");
            var afterInvalid = storage.ReadConfigFiles();
            Check("YAML semantically invalid file keeps LKG", afterInvalid.TryGetValue(file, out var invalidLkg) && invalidLkg == valid);

            File.WriteAllText(file, "   \r\n");
            var afterEmpty = storage.ReadConfigFiles();
            Check("YAML empty file keeps LKG", afterEmpty.TryGetValue(file, out var emptyLkg) && emptyLkg == valid);
        }
        finally { storage.Dispose(); }
    }

    private static void TestCategorySingleFileMigration()
    {
        using var scope = new TempConfigScope();
        var current = Path.Combine(scope.Path, "FixItFelix.AutoMapPins.categories.yaml");
        var legacy = Path.Combine(scope.Path, "FixItFelix.AutoMapPins.categories.vanilla.yaml");
        var extra = Path.Combine(scope.Path, "FixItFelix.AutoMapPins.categories.mods.yaml");
        var nearMatch = Path.Combine(scope.Path, "FixItFelix.AutoMapPins.categoriesNotAConfig.yaml");
        const string currentYaml = "ore:\n  isActive: false\n";
        const string legacyYaml = "ore:\n  isActive: true\n";
        File.WriteAllText(current, currentYaml);
        File.WriteAllText(extra, "modded:\n  isActive: true\n");
        File.WriteAllText(nearMatch, "unrelated:\n  isActive: true\n");
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            var fresh = storage.ReadConfigFiles();
            Check("single-file category name loaded", fresh.ContainsKey(current));
            Check("named category files still loaded", fresh.ContainsKey(extra));
            Check("category near-match ignored", !fresh.ContainsKey(nearMatch));

            File.WriteAllText(legacy, legacyYaml);
            var upgrading = storage.ReadConfigFiles();
            Check("legacy edited file wins beside new packaged default",
                upgrading.ContainsKey(legacy) && !upgrading.ContainsKey(current) &&
                storage.DeserializeAndMergeFileData(upgrading)["ore"].IsActive == true);

            File.Delete(legacy);
            var migrated = storage.ReadConfigFiles();
            Check("new file becomes active after legacy rename or removal",
                migrated.ContainsKey(current) && !migrated.ContainsKey(legacy) &&
                storage.DeserializeAndMergeFileData(migrated)["ore"].IsActive == false);
        }
        finally { storage.Dispose(); }
    }

    private static void TestExplorationSkillRequirement()
    {
        using (var scope = new TempConfigScope())
        {
            var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
            try
            {
                var merged = storage.DeserializeAndMergeFileData(new Dictionary<string, string>
                {
                    ["base"] = "gated:\n  isActive: true\n  skillRequire: 35\n  categoryConfiguredObjects: [TempObject]\n  individualConfiguredObjects:\n    PermanentObject:\n      isPermanent: true\n",
                    ["override"] = "gated:\n  individualConfiguredObjects:\n    PermanentObject:\n      skillRequire: 40\n"
                });
                Check("skill requirement survives YAML category merge", merged["gated"].SkillRequire == 35);
                Check("individual skill override survives YAML merge", merged["gated"].IndividualConfiguredObjects!["PermanentObject"].SkillRequire == 40);

                ResetMinimap();
                DiscoveryRadiusService.ForceVisible = true;
                BepInEx.Bootstrap.Chainloader.PluginInfos["org.bepinex.plugins.exploration"] = new object();
                Player.m_localPlayer = new Player { ExplorationLevel = 30 };
                Registry.InitializeRegistry(merged);
                Check("category skill threshold inherited by prefab", Registry.ConfiguredPins["TempObject"].SkillRequire == 35);
                Check("individual skill threshold overrides category", Registry.ConfiguredPins["PermanentObject"].SkillRequire == 40);
                var temporary = CreatePinComponent("TempObject");
                var permanent = CreatePinComponent("PermanentObject", 10);
                temporary.Awake();
                permanent.Awake();
                Check("locked objects do not make pins", CountAmpPins("TempObject") == 0 && CountAmpPins("PermanentObject") == 0);

                Player.m_localPlayer.ExplorationLevel = 35;
                UnityEngine.Time.time = 200f;
                MinimapPatch.UpdateExploreSkillPostfix();
                Check("crossing category level unlocks visible temporary pin", CountAmpPins("TempObject") == 1 && CountAmpPins("PermanentObject") == 0);
                Player.m_localPlayer.ExplorationLevel = 40;
                UnityEngine.Time.time = 201f;
                MinimapPatch.UpdateExploreSkillPostfix();
                Check("crossing individual level unlocks permanent pin", CountAmpPins("PermanentObject") == 1);
                Player.m_localPlayer.ExplorationLevel = 30;
                UnityEngine.Time.time = 202f;
                MinimapPatch.UpdateExploreSkillPostfix();
                Check("lost skill removes temporary pin but preserves earned permanent pin", CountAmpPins("TempObject") == 0 && CountAmpPins("PermanentObject") == 1);

                BepInEx.Bootstrap.Chainloader.PluginInfos.Clear();
                Check("missing Exploration ignores positive skill requirement", ExplorationSkillGate.IsUnlocked(Registry.ConfiguredPins["TempObject"]));
                UnityEngine.Time.time = 203f;
                MinimapPatch.UpdateExploreSkillPostfix();
                Check("removing Exploration restores temporary pins without changing YAML", CountAmpPins("TempObject") == 1);
                Minimap.instance!.m_pins.Clear();
                Registry.InitializeRegistry(merged);
                Check("startup without Exploration creates gated pins", CountAmpPins("TempObject") == 1);
                Check("ungated config works without Exploration", ExplorationSkillGate.IsUnlocked(new Config.Pin()));
                temporary.OnDestroy();
                permanent.OnDestroy();
                DiscoveryRadiusService.ForceVisible = false;
                Player.m_localPlayer = null;
            }
            finally { storage.Dispose(); }
        }
    }

    private static void TestRegistryDefaultsAndRescan()
    {
        ResetMinimap();
        DiscoveryRadiusService.ForceVisible = true;
        var visible = CreatePinComponent("Tree");
        visible.Awake();
        Minimap.instance!.m_pins.Clear();
        var categories = new Dictionary<string, Config.Category>
        {
            ["defaultIconCategory"] = new()
            {
                IsActive = true,
                IsPermanent = false,
                CategoryConfiguredObjects = new List<string> { "Tree" }
            }
        };

        Registry.InitializeRegistry(categories);
        Check("Registry category icon defaults to Constants.NoConfig", Registry.ConfiguredPins["Tree"].IconName == Constants.NoConfig);
        Check("Registry reload rescans visible components once", CountAmpPins("Tree") == 1);
        visible.OnDestroy();
        DiscoveryRadiusService.ForceVisible = false;
    }

    private static PinComponent CreatePinComponent(string name, float x = 0)
    {
        var component = (PinComponent)Activator.CreateInstance(typeof(PinComponent), BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<object>(), null)!;
        component.gameObject = new GameObject(name);
        component.gameObject.transform.position = new Vector3(x, 0, 0);
        return component;
    }

    private static void TestMapPinIconDefault()
    {
        ResetMinimap();
        Assets.LastIconName = "";
        var pin = new MapPin(new PositionedObject("BerryBush", new Vector3(1, 2, 3)), new Config.Pin
        {
            Name = "Berry",
            IsActive = true,
            IsPermanent = true,
            IconName = null
        });

        Check("MapPin ApplyConfigUpdate stores safe default icon", pin.Config.IconName == Constants.NoConfig);
        Check("MapPin ApplyConfigUpdate requests default icon", Assets.LastIconName == Constants.NoConfig);
    }

    private static void TestPinComponentBoundedRescan()
    {
        ResetMinimap();
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["visible"] = PermanentCategory("VisibleObject") });
        var hidden = CreatePinComponent("HiddenObject");
        hidden.Awake();
        PinComponent.RescanRegisteredVisiblePins();
        Check("Hidden components are not rescanned", Minimap.instance!.m_pins.Count == 0);

        DiscoveryRadiusService.ForceVisible = true;
        var visible = CreatePinComponent("VisibleObject");
        visible.Awake();
        Minimap.instance!.m_pins.Clear();
        PinComponent.RescanRegisteredVisiblePins();
        Check("Visible registered components are rescanned on demand", CountAmpPins("VisibleObject") == 1);

        Minimap.instance!.m_pins.Clear();
        visible.OnDestroy();
        PinComponent.RescanRegisteredVisiblePins();
        Check("Destroyed components are not re-added", Minimap.instance!.m_pins.Count == 0);
        DiscoveryRadiusService.ForceVisible = false;
    }

    private static void TestMinimapSavedMetadataLifecycle()
    {
        ResetMinimap();
        var saved = SavedAmpPin("AncientSeed", 1);
        Minimap.instance!.m_pins.Add(saved);
        MinimapPatch.UpdatePins();
        Check("Saved AMP metadata before registry is preserved", Minimap.instance.m_pins.Contains(saved) && Minimap.instance.m_pins.Count == 1);

        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["perm"] = PermanentCategory("AncientSeed") });
        Check("Saved AMP metadata converts after config loads", CountAmpPins("AncientSeed") == 1 && !Minimap.instance.m_pins.Contains(saved));

        ResetMinimap();
        var unknown = SavedAmpPin("UnknownSaved", 2);
        Minimap.instance!.m_pins.Add(unknown);
        MinimapPatch.UpdatePins();
        Check("Unknown saved AMP metadata remains unmodified", ReferenceEquals(Minimap.instance.m_pins.Single(), unknown));

        ResetMinimap();
        var userPin = UserPin("User", 3);
        Minimap.instance!.m_pins.Add(userPin);
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["perm"] = PermanentCategory("User") });
        Check("Normal user pins are never converted or removed", ReferenceEquals(Minimap.instance.m_pins.Single(), userPin));

        ResetMinimap();
        var disabled = SavedAmpPin("DisabledSaved", 4);
        Minimap.instance!.m_pins.Add(disabled);
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["disabled"] = DisabledIndividualCategory("DisabledSaved") });
        Check("Explicit disabled saved AMP metadata is removed", Minimap.instance.m_pins.Count == 0);

        ResetMinimap();
        var nonPermanent = SavedAmpPin("TempSaved", 5);
        Minimap.instance!.m_pins.Add(nonPermanent);
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["temp"] = NonPermanentCategory("TempSaved") });
        Check("Nonpermanent saved AMP metadata is removed", Minimap.instance.m_pins.Count == 0);
    }

    private static void TestVanillaMapIconOverridesAndSizes()
    {
        ResetMinimap();
        var map = Minimap.instance!;
        var original = new Sprite("vanilla icon");
        var replacement = new Sprite("configured icon");
        var vanilla = new Minimap.PinData
        {
            m_type = Minimap.PinType.Icon0, m_icon = original,
            m_iconElement = new Minimap.IconElement { sprite = original },
            m_uiElement = new UnityEngine.RectTransform()
        };
        var plain = new Minimap.PinData { m_type = Minimap.PinType.None, m_icon = original,
            m_iconElement = new Minimap.IconElement { sprite = original } };
        var amp = new MapPin(new PositionedObject("AmpObject", new Vector3(1, 0, 0)), ActivePin("AMP", false))
        {
            m_uiElement = new UnityEngine.RectTransform()
        };
        amp.Config.IconName = "custom";
        amp.m_iconElement = new Minimap.IconElement { sprite = amp.m_icon };
        var staleAmpSprite = amp.m_icon;
        amp.ApplyConfigUpdate(amp.Config); // icon YAML reload keeps the same UI marker
        Check("AMP icon reload changes pin sprite without recreating UI", !ReferenceEquals(staleAmpSprite, amp.m_icon) &&
            ReferenceEquals(amp.m_iconElement.sprite, staleAmpSprite));
        Assets.IconScales["custom"] = 1.5f;
        Assets.MapOverrides[Minimap.PinType.Icon0] = (replacement, 2f);
        map.m_pins.Add(vanilla);
        map.m_pins.Add(plain);
        map.m_pins.Add(amp);
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("vanilla map pin only uses configured sprite", ReferenceEquals(vanilla.m_icon, replacement) &&
            ReferenceEquals(vanilla.m_iconElement!.sprite, replacement) && ReferenceEquals(plain.m_icon, original));
        Check("size percent scales vanilla and AMP icon UI", vanilla.m_uiElement!.localScale.x == 2f &&
            amp.m_uiElement!.localScale.x == 1.5f);
        Check("AMP pin never uses vanilla type override", !ReferenceEquals(amp.m_icon, replacement));
        Check("visible AMP UI sprite updates after icon reload", ReferenceEquals(amp.m_iconElement!.sprite, amp.m_icon));

        Assets.MapOverrides.Clear();
        Assets.IconScales.Clear();
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("removing vanilla override restores sprite and UI scale", ReferenceEquals(vanilla.m_icon, original) &&
            ReferenceEquals(vanilla.m_iconElement!.sprite, original) && vanilla.m_uiElement!.localScale.x == 1f);
        Check("removing AMP size restores original UI scale", amp.m_uiElement!.localScale.x == 1f);
        map.m_pins.Clear();
        MinimapPatch.UpdatePinsPostfix(ref map);
        TestScaledIconLabelOffset();
    }

    private static void TestScaledIconLabelOffset()
    {
        ResetMinimap();
        var map = Minimap.instance!;
        var amp = new MapPin(new PositionedObject("LabelObject", new Vector3(1, 0, 0)), ActivePin("Label", false))
        {
            m_uiElement = new UnityEngine.RectTransform { anchoredPosition = new UnityEngine.Vector2(100f, 200f) }
        };
        amp.Config.IconName = "labelled";
        amp.m_iconElement = new Minimap.IconElement { sprite = amp.m_icon };
        var label = new UnityEngine.RectTransform { anchoredPosition = new UnityEngine.Vector2(100f, 200f) };
        amp.m_NamePinData!.PinNameRectTransform = label;
        map.m_pins.Add(amp);

        Assets.IconScales["labelled"] = 3f;
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("300% icon moves its label below the enlarged icon", label.anchoredPosition.x == 100f && label.anchoredPosition.y == 200f - 32f);
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("repeated map passes do not accumulate label offset", label.anchoredPosition.y == 200f - 32f);

        Assets.IconScales["labelled"] = 0.5f;
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("50% icon pulls its label closer", label.anchoredPosition.y == 200f + 8f);

        Assets.IconScales.Clear();
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("removing size returns label to vanilla icon position", label.anchoredPosition.y == 200f && amp.m_uiElement!.localScale.x == 1f);

        var unscaled = new MapPin(new PositionedObject("PlainLabel", new Vector3(2, 0, 0)), ActivePin("Plain", false))
        {
            m_uiElement = new UnityEngine.RectTransform { anchoredPosition = new UnityEngine.Vector2(5f, 5f) }
        };
        var plainLabel = new UnityEngine.RectTransform { anchoredPosition = new UnityEngine.Vector2(7f, 9f) };
        unscaled.m_NamePinData!.PinNameRectTransform = plainLabel;
        map.m_pins.Add(unscaled);
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("100% pins leave vanilla label placement untouched", plainLabel.anchoredPosition.x == 7f && plainLabel.anchoredPosition.y == 9f);
        map.m_pins.Clear();
        MinimapPatch.UpdatePinsPostfix(ref map);
    }

    private static void TestPickerIconOverrides()
    {
        PickerIconOverrides.Reset();
        Assets.MapOverrides.Clear();
        var map = new Minimap();
        var original = new Sprite("vanilla Icon0");
        var replacement = new Sprite("file.png v1");
        var button = new UnityEngine.UI.Image { sprite = original, color = new Color(0.5f, 0.5f, 0.5f, 1f) };
        var selectionSprite = new Sprite("selected overlay");
        var overlay = new UnityEngine.UI.Image { enabled = true, sprite = selectionSprite };
        var parent = new UnityEngine.Transform { Component = button };
        overlay.transform.parent = parent;
        map.m_selectedIcon0 = overlay;
        map.m_icons.Add(new Minimap.SpriteData { m_name = Minimap.PinType.Icon0, m_icon = original });
        Assets.MapOverrides[Minimap.PinType.Icon0] = (replacement, 3f);
        Assets.MapOverrides[Minimap.PinType.None] = (new Sprite("location"), 3f);
        MinimapPatch.StartPickerPostfix(map);
        Check("picker changes only known vanilla button not overlay or None",
            ReferenceEquals(button.sprite, replacement) && ReferenceEquals(overlay.sprite, selectionSprite) &&
            overlay.enabled && button.color.r == 0.5f &&
            !ReferenceEquals(map.m_icons[0].m_icon, replacement));
        Check("picker retains static parent transform", ReferenceEquals(overlay.transform.parent, parent));

        // A failed PNG load keeps the last good asset dictionary unchanged.
        PickerIconOverrides.Refresh(map);
        Check("failed reload retains picker last good sprite", ReferenceEquals(button.sprite, replacement));
        var next = new Sprite("file.png v2 same path");
        Assets.MapOverrides[Minimap.PinType.Icon0] = (next, 0.1f);
        PickerIconOverrides.Refresh(map);
        Check("same path PNG byte reload refreshes picker independent of size", ReferenceEquals(button.sprite, next));
        Assets.MapOverrides.Clear();
        PickerIconOverrides.Refresh(map);
        Check("removing override restores original picker image", ReferenceEquals(button.sprite, original));

        Assets.MapOverrides[Minimap.PinType.Icon0] = (replacement, 2f);
        PickerIconOverrides.Refresh(map);
        var otherMod = new Sprite("other mod sprite");
        button.sprite = otherMod;
        PickerIconOverrides.Refresh(map);
        Assets.MapOverrides.Clear();
        PickerIconOverrides.Refresh(map);
        Check("picker preserves other mod image during rollback", ReferenceEquals(button.sprite, otherMod));
        PickerIconOverrides.Reset();

        var newMap = new Minimap();
        var freshButton = new UnityEngine.UI.Image { sprite = original };
        var freshOverlay = new UnityEngine.UI.Image();
        freshOverlay.transform.parent = new UnityEngine.Transform { Component = freshButton };
        newMap.m_selectedIcon0 = freshOverlay;
        newMap.m_icons.Add(new Minimap.SpriteData { m_name = Minimap.PinType.Icon0, m_icon = original });
        Assets.MapOverrides[Minimap.PinType.Icon0] = (replacement, 1f);
        PickerIconOverrides.Refresh(newMap);
        Check("recreated minimap accepts icon", ReferenceEquals(freshButton.sprite, replacement));
        PickerIconOverrides.Reset();
        Check("plugin unload restores picker", ReferenceEquals(freshButton.sprite, original));
        freshOverlay.transform.parent = null;
        PickerIconOverrides.Refresh(newMap);
        Check("missing picker hierarchy never writes UI", ReferenceEquals(freshButton.sprite, original));
        PickerIconOverrides.Reset();
        Assets.MapOverrides.Clear();
    }

    private static void TestLocationIdOverridesAndRollback()
    {
        ResetMinimap();
        Time.time = 0;
        ZoneSystem.instance = new ZoneSystem();
        var map = Minimap.instance!;
        var originalA = new Sprite("vanilla trader");
        var originalB = new Sprite("vanilla temple");
        var replacementA = new Sprite("new trader png");
        var replacementB = new Sprite("new temple png");
        var traderPos = new Vector3(15, 0, 10);
        var templePos = new Vector3(80, 0, 30);
        var trader = new Minimap.PinData { m_type = Minimap.PinType.None, m_icon = originalA,
            m_iconElement = new Minimap.IconElement { sprite = originalA }, m_uiElement = new RectTransform() };
        var temple = new Minimap.PinData { m_type = Minimap.PinType.None, m_icon = originalB,
            m_iconElement = new Minimap.IconElement { sprite = originalB }, m_uiElement = new RectTransform() };
        var unrelated = new Minimap.PinData { m_type = Minimap.PinType.None, m_icon = originalA,
            m_iconElement = new Minimap.IconElement { sprite = originalA } };
        map.m_locationPins[traderPos] = trader;
        map.m_locationPins[templePos] = temple;
        ZoneSystem.instance.Locations[traderPos] = "Vendor_BlackForest";
        ZoneSystem.instance.Locations[templePos] = "StartTemple";
        map.m_pins.AddRange(new[] { trader, temple, unrelated });
        Assets.LocationOverrides["Vendor_BlackForest"] = (replacementA, 1.5f);
        Assets.LocationOverrides["StartTemple"] = (replacementB, 2f);
        Assets.LocationOverrides["UnknownLocation"] = (new Sprite("unknown"), 3f);
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("distinct location IDs independently override None pins",
            ReferenceEquals(trader.m_icon, replacementA) && ReferenceEquals(temple.m_icon, replacementB) &&
            ReferenceEquals(unrelated.m_icon, originalA) && ReferenceEquals(trader.m_iconElement!.sprite, replacementA));
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("location size does not compound over repeated updates",
            trader.m_uiElement!.localScale.x == 1.5f && temple.m_uiElement!.localScale.x == 2f);
        var replacementPng = new Sprite("edited trader png");
        Assets.LocationOverrides["Vendor_BlackForest"] = (replacementPng, 0.5f);
        MinimapPatch.RefreshIconVisuals();
        Check("PNG reload refreshes existing location pin and UI immediately",
            ReferenceEquals(trader.m_icon, replacementPng) && ReferenceEquals(trader.m_iconElement!.sprite, replacementPng) &&
            trader.m_uiElement.localScale.x == 0.5f && ReferenceEquals(temple.m_icon, replacementB));
        var oldMarker = trader.m_uiElement;
        trader.m_uiElement = new RectTransform(); // map mode changes destroy/recreate UI marker
        trader.m_iconElement = new Minimap.IconElement { sprite = originalA };
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("map mode transition restores old UI scale and scales new marker once",
            oldMarker.localScale.x == 1f && trader.m_uiElement.localScale.x == 0.5f &&
            ReferenceEquals(trader.m_iconElement.sprite, replacementPng));
        trader.m_uiElement.localScale = new Vector3(3, 3, 3); // another UI mod changed it
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("external UI scale rebases override instead of being discarded",
            trader.m_uiElement.localScale.x == 1.5f);
        map.m_locationPins.Remove(templePos);
        ZoneSystem.instance.Locations.Remove(templePos);
        Time.time = 6f;
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("removed location registration does not leave an overridden orphan marker",
            ReferenceEquals(temple.m_icon, originalB) && ReferenceEquals(temple.m_iconElement!.sprite, originalB));
        var otherModIcon = new Sprite("other mod trader icon");
        trader.m_icon = otherModIcon;
        trader.m_uiElement.localScale = new Vector3(4, 4, 4);
        // Another mod can change sprite and scale just before YAML removal, without an intervening update.
        Assets.LocationOverrides.Clear();
        MinimapPatch.RefreshIconVisuals();
        Check("removing location overrides restores original or later external sprite and UI scale",
            ReferenceEquals(trader.m_icon, otherModIcon) && ReferenceEquals(trader.m_iconElement!.sprite, otherModIcon) &&
            ReferenceEquals(temple.m_icon, originalB) && ReferenceEquals(temple.m_iconElement!.sprite, originalB) &&
            trader.m_uiElement.localScale.x == 4f && temple.m_uiElement!.localScale.x == 1f);
        Assets.LocationOverrides["Vendor_BlackForest"] = (replacementPng, 2f);
        MinimapPatch.UpdatePinsPostfix(ref map);
        var otherModInterleaved = new Sprite("other mod owns marker between updates");
        trader.m_icon = otherModInterleaved;
        trader.m_iconElement!.sprite = otherModInterleaved;
        MinimapPatch.UpdatePinsPostfix(ref map);
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("other mod location marker retains ownership across updates",
            ReferenceEquals(trader.m_icon, otherModInterleaved) &&
            ReferenceEquals(trader.m_iconElement.sprite, otherModInterleaved));
        Assets.LocationOverrides.Clear();
        MinimapPatch.UpdatePinsPostfix(ref map);
        Check("removing competing override does not reset external sprite",
            ReferenceEquals(trader.m_icon, otherModInterleaved) &&
            ReferenceEquals(trader.m_iconElement.sprite, otherModInterleaved));
        map.m_locationPins.Clear();
        ZoneSystem.instance.Locations.Clear();
        ZoneSystem.instance = null!;
        Time.time = 0;
    }

    private static void TestPermanentPinsSurviveTemporaryAbsentConfig()
    {
        ResetMinimap();
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["perm"] = PermanentCategory("FreshPermanent") });
        var obj = new GameObject("FreshPermanent") { transform = { position = new Vector3(9, 0, 0) } };
        MinimapPatch.UpsertPin(obj);
        Check("Fresh permanent AMP pin is created", CountAmpPins("FreshPermanent") == 1);
        var preserved = Minimap.instance!.m_pins.OfType<MapPin>().Single();
        var oldIcon = preserved.m_icon;
        preserved.m_iconElement = new Minimap.IconElement { sprite = oldIcon };

        Registry.InitializeRegistry(new Dictionary<string, Config.Category>());
        Check("Fresh permanent AMP pin survives temporary absent config", CountAmpPins("FreshPermanent") == 1);
        Check("preserved permanent pin refreshes icon when config is temporarily absent",
            !ReferenceEquals(oldIcon, preserved.m_icon) && Minimap.instance.m_pins.Contains(preserved));
        MinimapPatch.RefreshIconVisuals();
        Check("preserved pin existing UI receives refreshed icon", ReferenceEquals(preserved.m_iconElement!.sprite, preserved.m_icon));

        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["disabled"] = DisabledIndividualCategory("FreshPermanent") });
        Check("Fresh permanent AMP pin is removed by explicit disabled config", CountAmpPins("FreshPermanent") == 0);

        ResetMinimap();
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["perm"] = PermanentCategory("ToNonPermanent") });
        MinimapPatch.UpsertPin(new GameObject("ToNonPermanent"));
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["temp"] = NonPermanentCategory("ToNonPermanent") });
        Check("Permanent AMP pin is removed by nonpermanent config", CountAmpPins("ToNonPermanent") == 0);
    }

    private static void TestOptimizedPinUpdatesAndGrouping()
    {
        ResetMinimap();
        var config = ActivePin("Cluster", permanent: false);
        config.Groupable = true;
        Registry.ConfiguredPins["Cluster"] = config;
        Minimap.instance!.m_pins.Add(UserPin("Unrelated user pin", 99));
        var first = new GameObject("Cluster") { transform = { position = new Vector3(0, 0, 0) } };
        var second = new GameObject("Cluster") { transform = { position = new Vector3(10, 0, 0) } };
        var distant = new GameObject("Cluster") { transform = { position = new Vector3(100, 0, 0) } };
        MinimapPatch.UpsertPin(first);
        MinimapPatch.UpsertPin(second);
        MinimapPatch.UpsertPin(second);
        MinimapPatch.UpsertPin(distant);
        Check("Upsert groups nearby objects, deduplicates and keeps far object separate",
            CountAmpPins("Cluster") == 2 &&
            Minimap.instance.m_pins.OfType<MapPin>().Any(pin => pin.m_pos.x == 5));
        MinimapPatch.UnpinObject(second);
        Check("Unpin preserves remaining grouped object and unrelated pin",
            CountAmpPins("Cluster") == 2 && Minimap.instance.m_pins.OfType<MapPin>().Any(pin => pin.m_pos.x == 0) &&
            Minimap.instance.m_pins.Any(pin => pin.m_ownerID == 123));
        MinimapPatch.UnpinObject(first);
        Check("Unpin removes empty grouped pin", CountAmpPins("Cluster") == 1);

        var remaining = Minimap.instance.m_pins.OfType<MapPin>().Single();
        remaining.m_iconElement = new Minimap.IconElement();
        remaining.RefreshPinLabelText();
        remaining.UpdatePinColor();
        int textWrites = remaining.m_NamePinData!.PinNameText!.Writes;
        int colorWrites = remaining.m_iconElement.Writes;
        remaining.RefreshPinLabelText();
        remaining.UpdatePinColor();
        Check("Unchanged pin label and color do not trigger UI writes",
            remaining.m_NamePinData.PinNameText.Writes == textWrites && remaining.m_iconElement.Writes == colorWrites);
        remaining.m_name = "Updated label";
        remaining.RefreshPinLabelText();
        Check("Changed pin label still refreshes", remaining.m_NamePinData.PinNameText.Writes == textWrites + 1);
    }

    private static void TestVisibleComponentRescanRestoresMissingPinAndDeduplicates()
    {
        ResetMinimap();
        DiscoveryRadiusService.ForceVisible = true;
        Registry.InitializeRegistry(new Dictionary<string, Config.Category> { ["perm"] = PermanentCategory("VisiblePermanent") });
        var visible = CreatePinComponent("VisiblePermanent", 11);
        visible.Awake();
        Minimap.instance!.m_pins.Clear();
        PinComponent.RescanRegisteredVisiblePins();
        Check("Visible component rescan restores missing pin", CountAmpPins("VisiblePermanent") == 1);
        PinComponent.RescanRegisteredVisiblePins();
        Check("Visible component rescan deduplicates existing pin", CountAmpPins("VisiblePermanent") == 1);
        visible.OnDestroy();
        Minimap.instance.m_pins.Clear();
        PinComponent.RescanRegisteredVisiblePins();
        Check("Destroyed visible component is not reintroduced", CountAmpPins("VisiblePermanent") == 0);
        DiscoveryRadiusService.ForceVisible = false;
    }

    private static void TestSharedMapExcludesAndRestoresAllAmpPins()
    {
        ResetMinimap();
        var mapPin = new MapPin(new PositionedObject("SharedMapPin", new Vector3(1, 0, 0)), ActivePin("SharedMapPin", true));
        var saved = SavedAmpPin("SharedSaved", 2);
        var user = UserPin("SharedUser", 3);
        Minimap.instance!.m_pins.Add(mapPin);
        Minimap.instance.m_pins.Add(saved);
        Minimap.instance.m_pins.Add(user);

        MinimapPatch.GetSharedMapDataPrefix(ref Minimap.instance!, out var state);
        Check("Shared map prefix excludes MapPin and saved AMP metadata", Minimap.instance.m_pins.SequenceEqual(new[] { user }));
        MinimapPatch.GetSharedMapDataPostfix(ref Minimap.instance!, state);
        MinimapPatch.GetSharedMapDataFinalizer(ref Minimap.instance!, null, state);
        Check("Shared map postfix and finalizer restore exactly once", Minimap.instance.m_pins.Count == 3 && Minimap.instance.m_pins.Contains(mapPin) && Minimap.instance.m_pins.Contains(saved) && Minimap.instance.m_pins.Contains(user));

        MinimapPatch.GetSharedMapDataPrefix(ref Minimap.instance!, out state);
        var ex = new InvalidOperationException("boom");
        var returned = MinimapPatch.GetSharedMapDataFinalizer(ref Minimap.instance!, ex, state);
        Check("Shared map finalizer restores after exception", ReferenceEquals(returned, ex) && Minimap.instance.m_pins.Contains(mapPin) && Minimap.instance.m_pins.Contains(saved));

        foreach (bool throws in new[] { false, true })
        {
            MinimapPatch.GetSharedMapDataPrefix(ref Minimap.instance!, out var outer);
            MinimapPatch.GetSharedMapDataPrefix(ref Minimap.instance!, out var inner);
            if (!throws) MinimapPatch.GetSharedMapDataPostfix(ref Minimap.instance!, inner);
            MinimapPatch.GetSharedMapDataFinalizer(ref Minimap.instance!, throws ? ex : null, inner);
            Check("Nested sharing does not expose outer AMP pins", Minimap.instance.m_pins.SequenceEqual(new[] { user }));
            if (!throws) MinimapPatch.GetSharedMapDataPostfix(ref Minimap.instance!, outer);
            MinimapPatch.GetSharedMapDataFinalizer(ref Minimap.instance!, throws ? ex : null, outer);
            Check("Nested sharing restores permanent and saved pins after " + (throws ? "exception" : "success"),
                Minimap.instance.m_pins.Count == 3 && Minimap.instance.m_pins.Contains(mapPin) && Minimap.instance.m_pins.Contains(saved));
        }
        Check("Uninitialized sharing state preserves original exception",
            ReferenceEquals(MinimapPatch.GetSharedMapDataFinalizer(ref Minimap.instance!, ex, null), ex));
    }

    private static void TestClearPinsRemovesOnlyAmpPins()
    {
        ResetMinimap();
        var temporary = new MapPin(new PositionedObject("ClearTemp", new Vector3(1, 0, 0)), ActivePin("ClearTemp", false));
        var permanent = new MapPin(new PositionedObject("ClearPerm", new Vector3(2, 0, 0)), ActivePin("ClearPerm", true));
        var saved = SavedAmpPin("ClearSaved", 3);
        var user = UserPin("ClearUser", 4);
        var death = new Minimap.PinData { m_type = Minimap.PinType.Death, m_pos = new Vector3(5, 0, 0), m_save = true };
        var location = new Minimap.PinData { m_type = Minimap.PinType.None, m_pos = new Vector3(6, 0, 0) };
        var ownerlessNamedPin = UserPin("FixItFelix.AutoMapPins_lookalike", 7);
        Minimap.instance!.m_pins.AddRange(new[] { temporary, permanent, saved, user, death, location, ownerlessNamedPin });

        int removed = MinimapPatch.ClearAmpPins();
        Check("clear_pins removes AMP temporary, permanent and saved-metadata pins only",
            removed == 3 && Minimap.instance.m_pins.Count == 4 &&
            Minimap.instance.m_pins.Contains(user) && Minimap.instance.m_pins.Contains(death) &&
            Minimap.instance.m_pins.Contains(location) && Minimap.instance.m_pins.Contains(ownerlessNamedPin));
        Check("clear_pins is idempotent", MinimapPatch.ClearAmpPins() == 0 && Minimap.instance.m_pins.Count == 4);
        Minimap.instance = null;
        Check("clear_pins without a map is a no-op", MinimapPatch.ClearAmpPins() == 0);
        ResetMinimap();
    }

    private static void TestGroupedToPermanentTransition()
    {
        ResetMinimap();
        var temporary = ActivePin("TransitionTree", false);
        temporary.Groupable = true;
        temporary.GroupingDistance = 30;
        var group = new MapPin(new PositionedObject("TransitionTree", new Vector3(0, 0, 0)), temporary);
        group.AddObjectToPin(new PositionedObject("TransitionTree", new Vector3(10, 0, 0)));
        group.m_checked = true;
        Minimap.instance!.m_pins.Add(group);
        var permanent = ActivePin("TransitionTree", true);
        permanent.Groupable = true;
        Registry.ConfiguredPins["TransitionTree"] = permanent;
        MinimapPatch.UpdatePins();
        var pins = Minimap.instance.m_pins.OfType<MapPin>().ToArray();
        Check("Existing temporary group splits into actual permanent object positions",
            pins.Length == 2 && pins.All(p => p.m_save && p.m_checked && p.InstanceCount == 1) &&
            pins.Select(p => p.m_pos.x).OrderBy(x => x).SequenceEqual(new[] { 0f, 10f }));
        MinimapPatch.UpdatePins();
        Check("Repeated permanent reload does not duplicate split pins", Minimap.instance.m_pins.Count == 2);
        // Simulate the real save's per-pin fields, then invoke the production loader.
        var saved = pins.Select(p => new Minimap.PinData { m_pos = p.m_pos, m_type = p.m_type,
            m_author = p.m_author, m_ownerID = p.m_ownerID, m_save = p.m_save, m_checked = p.m_checked }).ToList();
        Minimap.instance.m_pins = saved;
        MinimapPatch.LoadMapDataPostfix(ref Minimap.instance!);
        Check("Split permanent positions and identity survive save reload",
            Minimap.instance.m_pins.OfType<MapPin>().Count() == 2 &&
            Minimap.instance.m_pins.All(p => p.m_checked && p.m_save) &&
            Minimap.instance.m_pins.Select(p => p.m_pos.x).OrderBy(x => x).SequenceEqual(new[] { 0f, 10f }));
        Registry.ConfiguredPins["TransitionTree"] = temporary;
        MinimapPatch.UpdatePins();
        Check("Rollback to temporary removes saved split pins", Minimap.instance.m_pins.Count == 0);
    }

    private static void TestPrintMapPinsAllowsDuplicateUserPins()
    {
        ResetMinimap();
        Minimap.instance!.m_pins.Add(UserPin("Duplicate", 7));
        Minimap.instance.m_pins.Add(UserPin("Duplicate", 7));
        string printed = MinimapPatch.PrintMapPins();
        Check("PrintMapPins supports duplicate name and position user pins", printed.Split("vanilla pin:").Length - 1 == 2);
    }

    private static void TestGeneratedPinRequestPolicy()
    {
        var peer = new Vector3(500, 10, 800);
        var claimed = new Vector3(505, 100, 810);
        Check("Remote pin request validates peer offset but preserves requested scan center",
            GeneratedPinRequestPolicy.TryValidate(1, claimed, peer, 1050f, out var center) &&
            center.x == claimed.x && center.z == claimed.z);
        foreach (float offset in new[] { -128f, -10f, 0f, 10f, 128f })
        {
            var origin = new Vector3(0, 0, 0);
            bool admitted = GeneratedPinRequestPolicy.TryValidate(7, origin, new Vector3(offset, 0, 0), 100f, out var scanCenter);
            var tracker = new GeneratedPinResponseTracker();
            tracker.Begin(7, origin, 100f);
            var edges = new List<GeneratedPinWireProtocol.Entry>
            {
                new(new ZDOID(), "EastEdge", new Vector3(scanCenter.x + 100f, 0, scanCenter.z)),
                new(new ZDOID(), "WestEdge", new Vector3(scanCenter.x - 100f, 0, scanCenter.z))
            };
            Check("Server/client circles agree at both boundaries with peer lag " + offset,
                admitted && tracker.TryAccept(7, 0, true, false, edges, out bool complete) && complete);
        }
        Check("Peer offset beyond allowed boundary still rejected",
            !GeneratedPinRequestPolicy.TryValidate(1, new Vector3(0, 0, 0), new Vector3(128.01f, 0, 0), 100f, out _));
        Check("Remote pin request rejects spoofed center",
            !GeneratedPinRequestPolicy.TryValidate(1, new Vector3(2000, 0, 800), peer, 100f, out _));
        Check("Remote pin request rejects oversized radius",
            !GeneratedPinRequestPolicy.TryValidate(1, peer, peer, 1101f, out _));
        Check("Remote pin request rejects NaN and invalid id",
            !GeneratedPinRequestPolicy.TryValidate(1, peer, peer, float.NaN, out _) &&
            !GeneratedPinRequestPolicy.TryValidate(0, peer, peer, 100f, out _));
    }

    private static void TestGeneratedPinRequestLimiter()
    {
        var limiter = new GeneratedPinRequestLimiter();
        Check("Request limiter admits first peer and second concurrent peer",
            limiter.TryBegin(10, 100f) && limiter.TryBegin(20, 100f));
        Check("Request limiter rejects duplicate peer, excess concurrency and invalid clock",
            !limiter.TryBegin(10, 100f) && !limiter.TryBegin(30, 100f) &&
            !limiter.TryBegin(30, float.NaN));
        limiter.End(10);
        Check("Request limiter enforces minimum interval after completion",
            !limiter.TryBegin(10, 101f));
        Check("Request limiter permits later request when slot freed",
            limiter.TryBegin(30, 100f) && !limiter.TryBegin(10, 120f));
        limiter.End(20);
        limiter.End(30);
        Check("Request limiter permits elapsed request", limiter.TryBegin(10, 120f));
        limiter.Forget(10);
        Check("Request limiter clears disconnected peer state", limiter.TryBegin(10, 120f));
        limiter.Reset();
        Check("Request limiter resets on world shutdown", limiter.TryBegin(10, 0f));
        limiter.End(10);
        limiter.Prune(new HashSet<long> { 20 });
        Check("Request limiter prunes disconnected peers without retained cooldown", limiter.TryBegin(10, 0f));
    }

    private static void TestGeneratedPinWireProtocol()
    {
        var items = new List<GeneratedPinWireProtocol.Entry>
        {
            new(new ZDOID(), "FarTree", new Vector3(640, 0, 0))
        };
        ZPackage packet = GeneratedPinWireProtocol.Encode(41, 2, final: true, truncated: false, items);
        bool valid = GeneratedPinWireProtocol.TryDecode(packet, out int requestId, out int sequence,
            out bool final, out bool truncated, out var decoded);
        Check("Remote pin packet round-trips request, sequence and ZDO payload",
            valid && requestId == 41 && sequence == 2 && final && !truncated &&
            decoded.Count == 1 && decoded[0].Prefab == "FarTree" && decoded[0].Position.x == 640);
        var malformed = new ZPackage();
        malformed.Write(GeneratedPinWireProtocol.ProtocolVersion);
        malformed.Write(41); malformed.Write(0); malformed.Write(false); malformed.Write(false);
        malformed.Write(GeneratedPinWireProtocol.MaximumBatchSize + 1);
        Check("Remote pin packet rejects oversized candidate count before allocating",
            !GeneratedPinWireProtocol.TryDecode(malformed, out _, out _, out _, out _, out _));
        var tooLarge = new ZPackage();
        tooLarge.Write(new string('X', GeneratedPinWireProtocol.MaximumPacketBytes + 1));
        Check("Remote pin packet rejects oversized bytes",
            !GeneratedPinWireProtocol.TryDecode(tooLarge, out _, out _, out _, out _, out _));
        var invalid = new List<GeneratedPinWireProtocol.Entry>
        {
            new(new ZDOID(), "FarTree", new Vector3(float.NaN, 0, 0))
        };
        bool rejected = false;
        try { GeneratedPinWireProtocol.Encode(1, 0, true, false, invalid); }
        catch (ArgumentException) { rejected = true; }
        Check("Remote pin packet refuses non-finite coordinates", rejected);
    }

    private static void TestGeneratedPinResponseTracker()
    {
        var tracker = new GeneratedPinResponseTracker();
        var center = new Vector3(0, 0, 0);
        var nearby = new List<GeneratedPinWireProtocol.Entry>
            { new(new ZDOID(), "FarTree", new Vector3(640, 0, 0)) };
        tracker.Begin(4, center, 700f);
        Check("Response tracker rejects stale request and out-of-order sequence",
            !tracker.TryAccept(3, 0, false, false, nearby, out _) &&
            !tracker.TryAccept(4, 1, false, false, nearby, out _));
        Check("Response tracker accepts valid non-final batch",
            tracker.TryAccept(4, 0, false, false, nearby, out bool complete) && !complete);
        var far = new List<GeneratedPinWireProtocol.Entry>
            { new(new ZDOID(), "FarTree", new Vector3(1000, 0, 0)) };
        Check("Response tracker rejects out-of-range server candidate",
            !tracker.TryAccept(4, 1, true, false, far, out _));
        Check("Response tracker accepts final complete snapshot once",
            tracker.TryAccept(4, 1, true, false, nearby, out complete) && complete &&
            !tracker.TryAccept(4, 2, true, false, nearby, out _));
        tracker.Begin(5, center, 700f);
        Check("Truncated final response is not a complete snapshot",
            tracker.TryAccept(5, 0, true, true, nearby, out complete) && !complete);
        tracker.Reset();
        Check("Tracker reset rejects response from old world/session",
            !tracker.TryAccept(5, 1, true, false, nearby, out _));
    }

    private static void TestGeneratedPinNetworkTransport()
    {
        ResetMinimap();
        Registry.ConfiguredPins["FarTree"] = ActivePin("FarTree", permanent: false);
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = true;
        DiscoveryRadiusService.ForceVisible = true;
        DiscoveryRadiusService.CurrentRadius = 700f;
        Player.m_localPlayer = new Player();
        UnityEngine.Time.time = 1f;
        var serverPeer = new ZNetPeer { m_uid = 88 };
        ZNet.instance = new ZNet(isServer: false) { ServerPeer = serverPeer };
        var owner = new UnityEngine.MonoBehaviour();
        var pins = new GeneratedPinService(owner);
        var transport = new GeneratedPinNetworkService(owner);
        pins.Tick();
        transport.Tick(pins);
        Check("Remote client sends one capability hello before scan request",
            serverPeer.m_rpc.Outgoing.Count == 1 &&
            serverPeer.m_rpc.Outgoing[0].Method == "AMP.GZP.Hello.1");
        serverPeer.m_rpc.DeliverAs(new ZRpc(), "AMP.GZP.HelloAck.1", GeneratedPinWireProtocol.ProtocolVersion);
        transport.Tick(pins);
        Check("Client ignores capability reply from non-server connection",
            serverPeer.m_rpc.Outgoing.Count == 1);
        serverPeer.m_rpc.Deliver("AMP.GZP.HelloAck.1", GeneratedPinWireProtocol.ProtocolVersion);
        transport.Tick(pins);
        var request = serverPeer.m_rpc.Outgoing.Last();
        var payload = (ZPackage)request.Values.Single();
        payload.SetPos(0);
        int requestVersion = payload.ReadInt();
        int id = payload.ReadInt();
        Check("Remote client requests bounded radius only after server acknowledgement",
            request.Method == "AMP.GZP.Request.1" && requestVersion == GeneratedPinWireProtocol.ProtocolVersion &&
            payload.ReadVector3().x == 0 && payload.ReadSingle() == 700f);
        var items = new List<GeneratedPinWireProtocol.Entry>
            { new(new ZDOID(), "FarTree", new Vector3(640, 0, 0)) };
        var response = GeneratedPinWireProtocol.Encode(id, 0, final: true, truncated: false, items);
        serverPeer.m_rpc.DeliverAs(new ZRpc(), "AMP.GZP.Response.1", response);
        Check("Remote client ignores response from non-server connection", CountAmpPins("FarTree") == 0);
        serverPeer.m_rpc.Deliver("AMP.GZP.Response.1", response);
        Check("Remote client displays server-approved distant pin", CountAmpPins("FarTree") == 1);
        UnityEngine.Time.time = 30f;
        pins.Tick();
        transport.Tick(pins);
        var second = (ZPackage)serverPeer.m_rpc.Outgoing.Last().Values.Single();
        second.SetPos(0); second.ReadInt(); int secondId = second.ReadInt();
        var incomplete = GeneratedPinWireProtocol.Encode(secondId, 0, final: true, truncated: true,
            new List<GeneratedPinWireProtocol.Entry>());
        serverPeer.m_rpc.Deliver("AMP.GZP.Response.1", incomplete);
        Check("Truncated server snapshot preserves unseen distant pin", CountAmpPins("FarTree") == 1);
        UnityEngine.Time.time = 60f;
        pins.Tick();
        transport.Tick(pins);
        var third = (ZPackage)serverPeer.m_rpc.Outgoing.Last().Values.Single();
        third.SetPos(0); third.ReadInt(); int thirdId = third.ReadInt();
        var empty = GeneratedPinWireProtocol.Encode(thirdId, 0, final: true, truncated: false,
            new List<GeneratedPinWireProtocol.Entry>());
        serverPeer.m_rpc.Deliver("AMP.GZP.Response.1", empty);
        Check("Complete server snapshot removes vanished distant pin", CountAmpPins("FarTree") == 0);
        serverPeer.m_rpc = new ZRpc();
        transport.Tick(pins);
        Check("Replaced server connection re-registers and re-handshakes client",
            serverPeer.m_rpc.Outgoing.Count == 1 &&
            serverPeer.m_rpc.Outgoing[0].Method == "AMP.GZP.Hello.1");
        UnityEngine.Time.time = 75f; transport.Tick(pins);
        UnityEngine.Time.time = 90f; transport.Tick(pins);
        UnityEngine.Time.time = 110f; transport.Tick(pins);
        Check("Server without capability cannot trigger request flood",
            serverPeer.m_rpc.Outgoing.Count == 3 &&
            serverPeer.m_rpc.Outgoing.All(message => message.Method == "AMP.GZP.Hello.1"));
        transport.Reset(); pins.Reset();

        UnityEngine.Time.time = 1f;
        ZNet.instance = new ZNet(isServer: true);
        ZNet.instance.Peers[15] = new ZNetPeer { m_uid = 15, Position = new Vector3(0, 0, 0) };
        ZNetScene.instance = new ZNetScene();
        ZNetScene.instance.Prefabs[101] = new GameObject("FarTree");
        ZDOMan.instance = new ZDOMan();
        ZDOMan.instance.Sectors[new Vector2s(10, 0)] = new List<ZDO>
            { new(101, new Vector3(640, 0, 0)) };
        var serverOwner = new UnityEngine.MonoBehaviour();
        var serverTransport = new GeneratedPinNetworkService(serverOwner);
        serverTransport.Tick(null);
        var spoof = new ZPackage();
        spoof.Write(GeneratedPinWireProtocol.ProtocolVersion); spoof.Write(1);
        spoof.Write(new Vector3(4000, 0, 0)); spoof.Write(700f);
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", spoof);
        Check("Server RPC rejects spoofed client position without starting scan", serverOwner.LastStarted == null);
        var valid = new ZPackage();
        valid.Write(GeneratedPinWireProtocol.ProtocolVersion); valid.Write(2);
        valid.Write(new Vector3(0, 0, 0)); valid.Write(700f);
        ZNet.instance.Peers[15].m_rpc.DeliverAs(new ZRpc(), "AMP.GZP.Request.1", valid);
        Check("Server refuses request from unbound connection", serverOwner.LastStarted == null);
        ZNet.instance.Peers[16] = new ZNetPeer { m_uid = 16, Position = new Vector3(900, 0, 0) };
        serverTransport.Tick(null);
        ZNet.instance.Peers[16].m_rpc.Deliver("AMP.GZP.Request.1", valid);
        Check("Different connected peer cannot impersonate nearby peer position",
            serverOwner.LastStarted == null && ZNet.instance.Peers[16].m_rpc.Outgoing.Count == 0);
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", valid);
        if (serverOwner.LastStarted != null)
            while (serverOwner.LastStarted.MoveNext()) { }
        var reply = ZNet.instance.Peers[15].m_rpc.Outgoing.LastOrDefault();
        Check("Server RPC scans existing sector and sends finite final packet",
            reply.Method == "AMP.GZP.Response.1" &&
            GeneratedPinWireProtocol.TryDecode((ZPackage)reply.Values.Single(), out int replyId,
                out _, out bool final, out bool truncated, out var decoded) &&
            replyId == 2 && final && !truncated && decoded.Count == 1 && decoded[0].Prefab == "FarTree");
        // Exercise the real RPC -> scanner -> wire -> client tracker contract with
        // a lagging peer position, not only the policy and tracker in isolation.
        ZNet.instance.Peers[15].Position = new Vector3(100, 0, 0);
        ZDOMan.instance.Sectors[new Vector2s(-11, 0)] = new List<ZDO>
            { new(101, new Vector3(-690, 0, 0)) };
        ZDOMan.instance.Sectors[new Vector2s(12, 0)] = new List<ZDO>
            { new(101, new Vector3(750, 0, 0)) };
        UnityEngine.Time.time = 25f;
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", valid);
        while (serverOwner.LastStarted!.MoveNext()) { }
        var laggedReply = (ZPackage)ZNet.instance.Peers[15].m_rpc.Outgoing.Last().Values.Single();
        var edgeTracker = new GeneratedPinResponseTracker();
        edgeTracker.Begin(2, new Vector3(0, 0, 0), 700f);
        Check("Lagged-peer RPC scans complete client circle without out-of-range batch",
            GeneratedPinWireProtocol.TryDecode(laggedReply, out int edgeId, out int edgeSequence,
                out bool edgeFinal, out bool edgeTruncated, out var edgeEntries) &&
            edgeEntries.Any(entry => entry.Position.x == -690) &&
            edgeEntries.All(entry => entry.Position.x != 750) &&
            edgeTracker.TryAccept(edgeId, edgeSequence, edgeFinal, edgeTruncated, edgeEntries, out bool edgeComplete) && edgeComplete);
        ZDOMan.instance.Sectors.Remove(new Vector2s(-11, 0));
        ZDOMan.instance.Sectors.Remove(new Vector2s(12, 0));
        ZNet.instance.Peers[15].Position = new Vector3(0, 0, 0);
        ZDOMan.instance.Sectors[new Vector2s(10, 0)] = Enumerable.Range(0, 45)
            .Select(i => new ZDO(101, new Vector3(640 + i % 5, 0, i / 5))).ToList();
        UnityEngine.Time.time = 50f;
        var many = new ZPackage();
        many.Write(GeneratedPinWireProtocol.ProtocolVersion); many.Write(3);
        many.Write(new Vector3(0, 0, 0)); many.Write(700f);
        int previousPackets = ZNet.instance.Peers[15].m_rpc.Outgoing.Count;
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", many);
        while (serverOwner.LastStarted!.MoveNext()) { }
        var packets = ZNet.instance.Peers[15].m_rpc.Outgoing.Skip(previousPackets).ToList();
        bool split = packets.Count == 2 &&
            GeneratedPinWireProtocol.TryDecode((ZPackage)packets[0].Values.Single(), out int firstId,
                out int firstSequence, out bool firstFinal, out _, out var firstEntries) &&
            GeneratedPinWireProtocol.TryDecode((ZPackage)packets[1].Values.Single(), out int lastId,
                out int lastSequence, out bool lastFinal, out _, out var lastEntries) &&
            firstId == 3 && lastId == 3 && firstSequence == 0 && lastSequence == 1 &&
            !firstFinal && lastFinal && firstEntries.Count == 32 && lastEntries.Count == 13;
        Check("Server RPC chunks dense sector into bounded ordered packets", split);
        UnityEngine.Time.time = 70f;
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", many);
        ZRpc oldConnection = ZNet.instance.Peers[15].m_rpc;
        int beforeReplacement = oldConnection.Outgoing.Count;
        ZNet.instance.Peers[15] = new ZNetPeer { m_uid = 15, Position = new Vector3(0, 0, 0) };
        while (serverOwner.LastStarted!.MoveNext()) { }
        Check("Old scan never sends data to new connection reusing peer identity",
            oldConnection.Outgoing.Count == beforeReplacement &&
            ZNet.instance.Peers[15].m_rpc.Outgoing.Count == 0);
        UnityEngine.Time.time = 71f;
        serverTransport.Tick(null); // register handler on the replacement socket
        IEnumerator? cancelled = serverOwner.LastStarted;
        ZNet.instance.Peers[15].m_rpc.Deliver("AMP.GZP.Request.1", many);
        Check("New peer can request after stale scan released limiter",
            !ReferenceEquals(cancelled, serverOwner.LastStarted));
        bool stopSafe = true;
        try { serverTransport.Reset(); }
        catch (InvalidOperationException) { stopSafe = false; }
        Check("World shutdown safely cancels active server scan", stopSafe);
        bool worldSwitchSafe = true;
        ZNet.instance = new ZNet(isServer: true);
        try { serverTransport.Tick(null); }
        catch (ArgumentException) { worldSwitchSafe = false; }
        Check("Direct peer handler lifecycle tolerates world switch", worldSwitchSafe);
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = false;
        DiscoveryRadiusService.ForceVisible = false;
        Player.m_localPlayer = null;
        ZNet.instance = null!;
        ZNetScene.instance = null!;
        ZDOMan.instance = null!;
        Registry.ConfiguredPins.Clear();
    }

    private static void TestGeneratedPinServiceLocalLifecycle()
    {
        ResetMinimap();
        Registry.ConfiguredPins["FarTree"] = ActivePin("FarTree", permanent: false);
        ZNetScene.instance = new ZNetScene();
        ZNetScene.instance.Prefabs[101] = new GameObject("FarTree");
        ZDOMan.instance = new ZDOMan();
        ZDOMan.instance.Sectors[new Vector2s(10, 0)] = new List<ZDO>
            { new(101, new Vector3(640, 0, 0)) };
        Player.m_localPlayer = new Player();
        ZNet.instance = new ZNet(isServer: true);
        UnityEngine.Time.time = 1f;
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = true;
        DiscoveryRadiusService.ForceVisible = true;
        var owner = new UnityEngine.MonoBehaviour();
        var service = new GeneratedPinService(owner);
        AutoMapPinsPlugin.CurrentGeneratedService = service;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        Check("Local generated-sector scan creates a remote pin", CountAmpPins("FarTree") == 1);
        MinimapPatch.UnpinObject(new GameObject("FarTree") { transform = { position = new Vector3(640, 0, 0) } });
        Check("Local object unloading does not remove a tracked remote pin", CountAmpPins("FarTree") == 1);
        Player.m_localPlayer.transform.position = new Vector3(2000, 0, 0);
        service.Tick();
        Check("Leaving effective radius removes transient distant pin", CountAmpPins("FarTree") == 0);
        Player.m_localPlayer.transform.position = new Vector3(0, 0, 0);
        UnityEngine.Time.time = 20f;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        Check("Returning to generated sector restores distant pin", CountAmpPins("FarTree") == 1);
        ZDOMan.instance.Sectors[new Vector2s(10, 0)].Clear();
        UnityEngine.Time.time = 40f;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        Check("Destroying distant ZDO removes stale transient pin", CountAmpPins("FarTree") == 0);
        ZDOMan.instance.Sectors[new Vector2s(10, 0)].Add(new ZDO(101, new Vector3(640, 0, 0)));
        UnityEngine.Time.time = 60f;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        MapPin distantPin = Minimap.instance!.m_pins.OfType<MapPin>().Single();
        Player.m_localPlayer.transform.position = new Vector3(-60, 0, 0);
        service.Tick();
        Player.m_localPlayer.transform.position = new Vector3(-61, 0, 0);
        service.Tick();
        Check("Small movement defers a full temporary-pin cull", service.Owns(distantPin));
        UnityEngine.Time.time = 60.5f;
        service.Tick();
        Check("Scheduled cull removes an out-of-range pin and ownership", !service.Owns(distantPin) && CountAmpPins("FarTree") == 0);
        Player.m_localPlayer.transform.position = new Vector3(0, 0, 0);
        UnityEngine.Time.time = 75f;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        Check("Local scan restores pin after deferred cull", CountAmpPins("FarTree") == 1);
        DiscoveryRadiusService.CurrentRadius = 600f;
        UnityEngine.Time.time = 75.1f;
        service.Tick();
        Check("Shrinking discovery radius culls immediately", CountAmpPins("FarTree") == 0);
        DiscoveryRadiusService.CurrentRadius = 700f;
        UnityEngine.Time.time = 90f;
        service.Tick();
        while (owner.LastStarted!.MoveNext()) { }
        Check("Restoring discovery radius recovers distant pin", CountAmpPins("FarTree") == 1);
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = false;
        service.Tick();
        Check("Disabling generated-sector scan removes transient remote pin", CountAmpPins("FarTree") == 0);
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = true;
        DiscoveryRadiusService.CurrentRadius = 1f;
        UnityEngine.Time.time = 95f;
        service.Tick();
        IEnumerator firstTinyScan = owner.LastStarted!;
        while (firstTinyScan.MoveNext()) { }
        UnityEngine.Time.time = 115f;
        service.Tick();
        Check("Tiny synchronous scan does not block later refreshes",
            !ReferenceEquals(firstTinyScan, owner.LastStarted));
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = false;
        DiscoveryRadiusService.CurrentRadius = 700f;
        service.Reset();
        AutoMapPinsPlugin.CurrentGeneratedService = null;
        Player.m_localPlayer = null;
        ZNet.instance = null!;
        ZNetScene.instance = null!;
        ZDOMan.instance = null!;
        DiscoveryRadiusService.ForceVisible = false;
    }

    private static void TestLoadedObjectScanSummary()
    {
        Check("Loaded scan parses default/all and exact numeric radius",
            LoadedObjectScanOptions.TryParse(new[] { "amp", "scan" }, out float defaultRadius, out bool defaultAll) &&
            defaultRadius == 30f && !defaultAll &&
            LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "all" }, out float allRadius, out bool firstAll) &&
            allRadius == 30f && firstAll &&
            LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "2.5", "all" }, out float parsedRadius, out bool secondAll) &&
            parsedRadius == 2.5f && secondAll);
        Check("Loaded scan rejects invalid radius, flags and extra arguments",
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "0" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "101" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "NaN" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "Infinity" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "all", "all" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "20", "typo" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParse(new[] { "amp", "scan", "20", "all", "extra" }, out _, out _));
        Check("Legacy scan_nearby parses default, 'all' alone and invariant decimal radius",
            LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby" }, out float legacyDefault, out bool legacyDefaultAll) &&
            legacyDefault == 30f && !legacyDefaultAll &&
            LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "all" }, out float legacyAllRadius, out bool legacyAll) &&
            legacyAllRadius == 30f && legacyAll &&
            LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "300.5", "ALL" }, out float legacyRadius, out bool legacyTrailingAll) &&
            legacyRadius == 300.5f && legacyTrailingAll &&
            LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "1000" }, out _, out _));
        Check("Legacy scan_nearby rejects non-finite, negative, oversized, culture comma and malformed arguments",
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "Infinity" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "NaN" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "-5" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "1001" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "30,5" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "all", "30" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "30", "all", "all" }, out _, out _) &&
            !LoadedObjectScanOptions.TryParseLegacy(new[] { "amp", "scan_nearby", "30", "typo" }, out _, out _));
        var player = new GameObject("Player");
        var playerChild = new GameObject("Equipped_Item");
        playerChild.transform.parent = player.transform;
        var ui = new GameObject("Canvas") { transform = new RectTransform() };
        var far = new GameObject("Far") { transform = { position = new Vector3(500, 0, 0) } };
        var root = new GameObject("VegvisirRoot") { transform = { position = new Vector3(5, 0, 0) } };
        var child = new GameObject("Vegvisir_Boss") { transform = { position = new Vector3(5, 0, 0) } };
        child.transform.parent = root.transform;
        var networkedChild = new GameObject("Networked_Child")
        { Component = new ZNetView(), transform = { position = new Vector3(6, 0, 0) } };
        networkedChild.transform.parent = root.transform;
        var objects = new[] { far, player, playerChild, ui, root, child, networkedChild };
        LoadedObjectScanSummary rootsOnly = LoadedObjectSceneScanner.Scan(objects, new Vector3(),
            player.transform, 30f, false, id => id, id => id == "Vegvisir_Boss");
        LoadedObjectScanSummary includingChildren = LoadedObjectSceneScanner.Scan(objects, new Vector3(),
            player.transform, 30f, true, id => id, id => id == "Vegvisir_Boss");
        Check("Loaded scan filters player UI far objects and nested GameObjects unless all",
            rootsOnly.TotalObjects == 2 && rootsOnly.ExaminedObjects == 2 &&
            includingChildren.TotalObjects == 3 && includingChildren.ExaminedObjects == 3 &&
            includingChildren.Render(30f, true).Contains("Vegvisir_Boss: count=1") &&
            !includingChildren.Render(30f, true).Contains("Equipped_Item"));
        var dense = Enumerable.Range(0, LoadedObjectSceneScanner.MaximumNearbyCandidates + 2)
            .Select(i => new GameObject("Nearby") { transform = { position = new Vector3(2, 0, 0) } });
        LoadedObjectScanSummary cappedNearby = LoadedObjectSceneScanner.Scan(
            new[] { far, playerChild }.Concat(dense), new Vector3(), player.transform,
            30f, false, id => id, id => false);
        Check("Loaded scan cap applies only to nearby candidates and flags omissions",
            cappedNearby.Capped && cappedNearby.TotalObjects == LoadedObjectSceneScanner.MaximumNearbyCandidates &&
            cappedNearby.Render(30f, false).Contains("INCOMPLETE"));

        var summary = new LoadedObjectScanSummary();
        summary.Add("Vegvisir_Boss", 20f, false);
        summary.Add("Vegvisir_Boss", 2f, true);
        summary.Add("Other", 3f, false);
        summary.Add("Invalid", float.NaN, false);
        summary.Add("Invalid", -1f, false);
        summary.ExaminedObjects = 20001;
        summary.Capped = true;
        string rendered = summary.Render(30f, true);
        Check("Loaded scan aggregates exact IDs and reports closest/configured",
            rendered.Contains("Vegvisir_Boss: count=2, nearest=2.0m, configured") &&
            !rendered.Contains("Invalid") && rendered.Contains("[INCOMPLETE: scan cap reached]"));
        Check("Loaded scan sorts by nearest and bounds output",
            rendered.IndexOf("Vegvisir_Boss:", StringComparison.Ordinal) <
            rendered.IndexOf("Other:", StringComparison.Ordinal) &&
            summary.Render(30f, false, 1).Contains("1 farther IDs omitted"));
        Check("Loaded scan does not pretend to see remote zones",
            rendered.Contains("loaded scene objects ONLY"));
    }

    private static void TestGeneratedZoneScannerDoesNotCreateWorld()
    {
        var center = new Vector3(0, 0, 0);
        Check("Remote scan rejects invalid radius", !GeneratedZoneScanner.EnumerateSectors(center, float.NaN).Any());
        Check("Remote scan includes existing distant sector without generating zones",
            GeneratedZoneScanner.EnumerateSectors(center, 700).Contains(new Vector2s(10, 0)));
        Check("Remote scan excludes sector beyond radius",
            !GeneratedZoneScanner.EnumerateSectors(center, 700).Contains(new Vector2s(20, 0)));
        Check("Remote scan bounds excessively large radius",
            GeneratedZoneScanner.EnumerateSectors(center, 50000).Count() < 1300);
        Check("Remote scan rejects impossible world coordinates without integer overflow",
            !GeneratedZoneScanner.EnumerateSectors(new Vector3(float.MaxValue, 0, 0), 700).Any() &&
            !GeneratedPinRequestPolicy.TryValidate(1, new Vector3(float.MaxValue, 0, 0),
                new Vector3(float.MaxValue, 0, 0), 700, out _));

        Registry.ConfiguredPins.Clear();
        Registry.ConfiguredPins["FarTree"] = ActivePin("FarTree", permanent: false);
        ZNetScene.instance = new ZNetScene();
        ZNetScene.instance.Prefabs[101] = new GameObject("FarTree");
        ZNetScene.instance.Prefabs[102] = new GameObject("OtherTree");
        Registry.ConfiguredPins["PickedBerry"] = ActivePin("PickedBerry", permanent: false);
        ZNetScene.instance.Prefabs[103] = new GameObject("PickedBerry") { Component = new Pickable() };
        ZDOMan.instance = new ZDOMan();
        ZDOMan.instance.Sectors[new Vector2s(10, 0)] = new List<ZDO>
        {
            new(101, new Vector3(640, 0, 0)),
            new(102, new Vector3(641, 0, 0)),
            new(103, new Vector3(642, 0, 0)) { Picked = true },
            new(101, new Vector3(1200, 0, 0))
        };
        var found = new List<GeneratedZoneScanner.Candidate>();
        IEnumerator scan = GeneratedZoneScanner.Scan(center, 700, candidate => found.Add(candidate));
        while (scan.MoveNext()) { }
        Check("Remote scan excludes unconfigured, picked and out-of-radius ZDO",
            found.Count == 1 && found[0].Name == "FarTree" && found[0].Position.x == 640);
        Check("Remote scan uses only read-only existing-sector queries",
            ZDOMan.instance.Calls > 0 && ZDOMan.instance.GenerationCalls == 0);
        ZDOMan.instance.Sectors[new Vector2s(0, 0)] = Enumerable.Range(0, GeneratedZoneScanner.MaximumCandidates + 1)
            .Select(i => new ZDO(101, new Vector3(i % 5, 0, 0))).ToList();
        GeneratedZoneScanner.ScanStats? capped = null;
        int emitted = 0;
        IEnumerator cappedScan = GeneratedZoneScanner.Scan(center, 700, _ => emitted++, stats => capped = stats);
        while (cappedScan.MoveNext()) { }
        Check("Capped remote scan signals incomplete snapshot and bounds candidate count",
            capped.HasValue && capped.Value.Truncated &&
            capped.Value.Candidates == GeneratedZoneScanner.MaximumCandidates &&
            emitted == GeneratedZoneScanner.MaximumCandidates);
        ZDOMan.instance.Sectors[new Vector2s(0, 0)] = Enumerable.Range(0,
            GeneratedZoneScanner.MaximumExaminedObjects + 1)
            .Select(_ => new ZDO(102, center)).ToList();
        GeneratedZoneScanner.ScanStats? dense = null;
        int frameBreaks = 0;
        IEnumerator denseScan = GeneratedZoneScanner.Scan(center, 700, _ => { }, stats => dense = stats);
        while (denseScan.MoveNext()) frameBreaks++;
        Check("Dense unconfigured sector yields frames and stops at examination cap",
            dense.HasValue && dense.Value.Truncated && dense.Value.Candidates == 0 &&
            dense.Value.Examined == GeneratedZoneScanner.MaximumExaminedObjects &&
            frameBreaks >= GeneratedZoneScanner.MaximumExaminedObjects / GeneratedZoneScanner.ObjectsPerFrame);
        Registry.ConfiguredPins.Clear();
        ZNetScene.instance = null!;
        ZDOMan.instance = null!;
    }

    private static void TestExplorePostfixUsesPositionalArguments()
    {
        MethodInfo postfix = typeof(MinimapPatch).GetMethod("ExplorePostfix", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] names = postfix.GetParameters().Select(parameter => parameter.Name!).ToArray();
        Check("Explore patch binds positional arguments despite Valheim parameter rename",
            names.SequenceEqual(new[] { "__0", "__1" }));

        var position = new Vector3(12, 0, 34);
        MinimapPatch.ExplorePostfix(position, 1050f);
        Check("Explore patch forwards final radius to capture service",
            DiscoveryRadiusService.LastCapturedRadius == 1050f &&
            DiscoveryRadiusService.LastCapturedCenter.x == position.x);
    }

    private static void TestTemporaryPinVisibilityModeChanges()
    {
        ResetMinimap();
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = false;
        Registry.InitializeRegistry(new Dictionary<string, Config.Category>
        {
            ["temporary"] = NonPermanentCategory("VisibleTemporary"),
            ["permanent"] = PermanentCategory("VisiblePermanent")
        });

        DiscoveryRadiusService.ForceVisible = true;
        var temporary = CreatePinComponent("VisibleTemporary");
        var permanent = CreatePinComponent("VisiblePermanent", 3);
        temporary.Awake();
        permanent.Awake();
        Check("Temporary pin initially visible", CountAmpPins("VisibleTemporary") == 1);

        // A visible non-dynamic pin stopped checking after Awake; a config toggle must
        // resume checks and hide it even if the map was already explored.
        DiscoveryRadiusService.ForceVisible = false;
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = true;
        PinComponent.RefreshVisiblePinsAfterConfigChange();
        Check("Live radius-only toggle hides previously visible temporary pin", CountAmpPins("VisibleTemporary") == 0);
        Check("Live radius-only toggle preserves permanent pin", CountAmpPins("VisiblePermanent") == 1);

        // Advance the production coroutine explicitly: the test MonoBehaviour stub
        // does not run Unity's coroutine scheduler.
        var coroutine = (IEnumerator)typeof(PinComponent).GetMethod("VisibleCheck", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(temporary, null)!;
        coroutine.MoveNext(); // stagger
        DiscoveryRadiusService.ForceVisible = true;
        coroutine.MoveNext();
        Check("Returning within radius restores temporary pin", CountAmpPins("VisibleTemporary") == 1);
        object sharedWait = coroutine.Current;

        DiscoveryRadiusService.ForceVisible = false;
        coroutine.MoveNext(); // interval
        Check("Visibility polling reuses its interval wait", ReferenceEquals(sharedWait, coroutine.Current));
        coroutine.MoveNext();
        Check("Leaving radius hides temporary pin again", CountAmpPins("VisibleTemporary") == 0);

        temporary.OnDestroy();
        permanent.OnDestroy();
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = false;
    }

    private static void ResetMinimap()
    {
        Minimap.instance = new Minimap();
        Registry.ConfiguredPins.Clear();
    }

    private static int CountAmpPins(string internalName) => Minimap.instance!.m_pins.OfType<MapPin>().Count(pin => pin.InternalName == internalName);

    private static Config.Category PermanentCategory(string name) => new() { IsActive = true, IsPermanent = true, CategoryConfiguredObjects = new List<string> { name } };
    private static Config.Category NonPermanentCategory(string name) => new() { IsActive = true, IsPermanent = false, CategoryConfiguredObjects = new List<string> { name } };
    private static Config.Category DisabledIndividualCategory(string name) => new() { IsActive = true, IndividualConfiguredObjects = new Dictionary<string, Config.Pin> { [name] = new() { IsActive = false, IsPermanent = true } } };
    private static Config.Pin ActivePin(string name, bool permanent) => new() { Name = name, IsActive = true, IsPermanent = permanent, IconName = Constants.NoConfig, IconColorRGBA = Config.PinColor.White };

    private static Minimap.PinData SavedAmpPin(string internalName, float x) => new()
    {
        m_type = Minimap.PinType.None,
        m_ownerID = 0,
        m_name = internalName,
        m_pos = new Vector3(x, 0, 0),
        m_save = true,
        m_author = new Splatform.PlatformUserID("FixItFelix.AutoMapPins_" + internalName),
        m_NamePinData = new Minimap.PinNameData(null!) { PinNameText = new Minimap.Text { text = internalName } }
    };

    private static Minimap.PinData UserPin(string name, float x) => new()
    {
        m_type = Minimap.PinType.None,
        m_ownerID = 123,
        m_name = name,
        m_pos = new Vector3(x, 0, 0),
        m_save = true,
        m_author = Splatform.PlatformUserID.None,
        m_NamePinData = new Minimap.PinNameData(null!) { PinNameText = new Minimap.Text { text = name } }
    };

    private sealed class TempConfigScope : IDisposable
    {
        private readonly string _oldPath = BepInEx.Paths.ConfigPath;
        public string Path { get; } = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "test-bounded-gameplay-config", Guid.NewGuid().ToString("N"));
        public TempConfigScope() { Directory.CreateDirectory(Path); BepInEx.Paths.ConfigPath = Path; }
        public void Dispose() { BepInEx.Paths.ConfigPath = _oldPath; }
    }
}

namespace AutoMapPins.Common
{
    public abstract class HasLogger { protected static readonly TestLogger Log = new(); }
    public sealed class TestLogger
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) => Console.WriteLine("WARN " + message);
    }
}

namespace AutoMapPins.Icons
{
    internal static class Assets
    {
        public static string LastIconName = "";
        public static readonly Dictionary<Minimap.PinType, (UnityEngine.Sprite Icon, float Scale)> MapOverrides = new();
        public static readonly Dictionary<string, (UnityEngine.Sprite Icon, float Scale)> LocationOverrides = new(StringComparer.Ordinal);
        public static bool HasLocationMapOverrides => LocationOverrides.Count > 0;
        public static readonly Dictionary<string, float> IconScales = new();
        public static UnityEngine.Sprite GetIcon(string name) { LastIconName = name; return new UnityEngine.Sprite(name); }
        public static float GetIconScale(string? name) => name != null && IconScales.TryGetValue(name, out float scale) ? scale : 1f;
        public static bool TryGetVanillaMapOverride(Minimap.PinType type, out UnityEngine.Sprite icon, out float scale)
        {
            if (MapOverrides.TryGetValue(type, out var found)) { icon = found.Icon; scale = found.Scale; return true; }
            icon = null!; scale = 1f; return false;
        }
        public static bool TryGetLocationMapOverride(string locationId, out UnityEngine.Sprite icon, out float scale)
        {
            if (LocationOverrides.TryGetValue(locationId, out var found)) { icon = found.Icon; scale = found.Scale; return true; }
            icon = null!; scale = 1f; return false;
        }
    }
}

namespace AutoMapPins.Discovery
{
    internal static class DiscoveryRadiusService
    {
        public static bool ForceVisible;
        public static float CurrentRadius = 700f;
        public static float LastCapturedRadius;
        public static UnityEngine.Vector3 LastCapturedCenter;
        public static bool IsWithinDiscoveryRadius(UnityEngine.Vector3 position) => ForceVisible;
        public static bool TryGetCurrentRadius(out float radius) { radius = CurrentRadius; return ForceVisible; }
        public static void CaptureGameExploreRadius(UnityEngine.Vector3 center, float radius)
        {
            LastCapturedCenter = center;
            LastCapturedRadius = radius;
        }
    }
}

namespace BepInEx
{
    public static class Paths { public static string ConfigPath = Directory.GetCurrentDirectory(); }
    public static class ThreadingHelper { public static System.ComponentModel.ISynchronizeInvoke? SynchronizingObject => null; }
}

namespace BepInEx.Bootstrap
{
    public static class Chainloader
    {
        public static readonly Dictionary<string, object> PluginInfos = new();
    }
}

namespace SkillManager
{
    public static class Skill
    {
        public static Skills.SkillType fromName(string englishName) => Skills.SkillType.Exploration;
    }
}

public static class Skills { public enum SkillType { Exploration = 1 } }

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch(params object[] args) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizer : Attribute { }
    public static class AccessTools
    {
        public static MethodInfo? DeclaredMethod(Type type, string name, Type[]? parameters = null) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameters ?? Type.EmptyTypes, null);
        public static MethodInfo? Method(Type type, string name, Type[]? parameters = null) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters ?? Type.EmptyTypes, null);
        public static FieldInfo? Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        public delegate ref TField FieldRef<T, TField>(T instance);
        public static FieldRef<T, TField> FieldRefAccess<T, TField>(string name)
        {
            if (typeof(T) == typeof(Minimap) && typeof(TField) == typeof(List<Minimap.PinData>) && name == "m_pins")
                return (FieldRef<T, TField>)(object)(FieldRef<Minimap, List<Minimap.PinData>>)((Minimap instance) => ref instance.m_pins);
            if (typeof(T) == typeof(Minimap) && typeof(TField) == typeof(bool) && name == "m_pinUpdateRequired")
                return (FieldRef<T, TField>)(object)(FieldRef<Minimap, bool>)((Minimap instance) => ref instance.m_pinUpdateRequired);
            throw new NotSupportedException($"FieldRef {typeof(T).Name}.{name}");
        }
    }
}

namespace JetBrains.Annotations { [AttributeUsage(AttributeTargets.All)] public sealed class UsedImplicitlyAttribute : Attribute { } }

public static class Localization { public static LocalizationInstance instance = new(); }
public sealed class LocalizationInstance { public string Localize(string? value) => value ?? ""; }
public static class Utils { public static float DistanceXZ(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z); }
public sealed class MessageHud { public enum MessageType { TopLeft } }
public sealed class Player : UnityEngine.Object { public static Player? m_localPlayer; public UnityEngine.Transform transform = new(); public float ExplorationLevel; public float GetSkillLevel(Skills.SkillType type) => ExplorationLevel; public void Message(MessageHud.MessageType type, string text, UnityEngine.Sprite icon) { } }
public sealed class ZoneSystem : UnityEngine.Object
{
    public static ZoneSystem instance = null!;
    public readonly Dictionary<UnityEngine.Vector3, string> Locations = new();
    public void GetLocationIcons(Dictionary<UnityEngine.Vector3, string> icons)
    {
        foreach (var location in Locations) icons[location.Key] = location.Value;
    }
}
public sealed class ZNet : UnityEngine.Object
{
    public static ZNet instance = null!;
    private readonly bool _server;
    public readonly Dictionary<long, ZNetPeer> Peers = new();
    public ZNetPeer? ServerPeer;
    public ZNet(bool isServer) { _server = isServer; }
    public bool IsServer() => _server;
    public ZNetPeer? GetPeer(long id) => Peers.TryGetValue(id, out var peer) ? peer : null;
    public ZNetPeer? GetServerPeer() => ServerPeer;
    public List<ZNetPeer> GetPeers() => Peers.Values.ToList();
}
public sealed class ZNetPeer
{
    public long m_uid;
    public ZRpc m_rpc = new();
    public bool Ready = true;
    public UnityEngine.Vector3 Position;
    public bool IsReady() => Ready;
    public UnityEngine.Vector3 GetRefPos() => Position;
}
public sealed class ZRpc
{
    private readonly Dictionary<string, Delegate> _handlers = new();
    public readonly List<(string Method, object[] Values)> Outgoing = new();
    public bool Connected = true;
    public bool IsConnected() => Connected;
    public void Register<T>(string name, Action<ZRpc, T> handler) => _handlers[name] = handler;
    public void Invoke(string method, params object[] values) => Outgoing.Add((method, values));
    public void Deliver<T>(string method, T value) => ((Action<ZRpc, T>)_handlers[method])(this, value);
    public void DeliverAs<T>(ZRpc connection, string method, T value) =>
        ((Action<ZRpc, T>)_handlers[method])(connection, value);
}
public readonly struct Vector2s : IEquatable<Vector2s>
{
    public readonly short x, y;
    public Vector2s(int x, int y) { this.x = (short)x; this.y = (short)y; }
    public bool Equals(Vector2s other) => x == other.x && y == other.y;
    public override bool Equals(object? other) => other is Vector2s vector && Equals(vector);
    public override int GetHashCode() => HashCode.Combine(x, y);
}
public readonly struct ZDOID { }
public sealed class ZPackage
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;
    private readonly BinaryReader _reader;
    public ZPackage() { _writer = new BinaryWriter(_stream); _reader = new BinaryReader(_stream); }
    public void Write(int value) => _writer.Write(value);
    public void Write(float value) => _writer.Write(value);
    public void Write(bool value) => _writer.Write(value);
    public void Write(string value) => _writer.Write(value);
    public void Write(ZDOID value) => _writer.Write(0L);
    public void Write(UnityEngine.Vector3 value) { _writer.Write(value.x); _writer.Write(value.y); _writer.Write(value.z); }
    public int ReadInt() => _reader.ReadInt32();
    public float ReadSingle() => _reader.ReadSingle();
    public bool ReadBool() => _reader.ReadBoolean();
    public string ReadString() => _reader.ReadString();
    public ZDOID ReadZDOID() { _reader.ReadInt64(); return new ZDOID(); }
    public UnityEngine.Vector3 ReadVector3() => new(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
    public int Size() => checked((int)_stream.Length);
    public int GetPos() => checked((int)_stream.Position);
    public void SetPos(int pos) => _stream.Position = pos;
}
public sealed class SimulationDistance { public SimulationDistance(int near, int far) { } }
public sealed class Pickable : UnityEngine.Object { }
public static class ZDOVars { public static readonly int s_picked = 1; }
public sealed class ZDO
{
    private readonly int _prefab;
    private readonly UnityEngine.Vector3 _position;
    public ZDOID m_uid;
    public bool Picked;
    public ZDO(int prefab, UnityEngine.Vector3 position) { _prefab = prefab; _position = position; }
    public bool IsValid() => true;
    public int GetPrefab() => _prefab;
    public UnityEngine.Vector3 GetPosition() => _position;
    public bool GetBool(int key) => Picked;
}
public sealed class ZDOMan
{
    public static ZDOMan instance = null!;
    public readonly Dictionary<Vector2s, List<ZDO>> Sectors = new();
    public int Calls, GenerationCalls;
    public void FindSectorObjects(Vector2s sector, SimulationDistance distance, List<ZDO> result)
    {
        Calls++;
        if (Sectors.TryGetValue(sector, out var zdos)) result.AddRange(zdos);
    }
}
public sealed class ZNetView : UnityEngine.Object { }
public sealed class ZNetScene : UnityEngine.Object
{
    public static ZNetScene instance = null!;
    public readonly Dictionary<int, UnityEngine.GameObject> Prefabs = new();
    public UnityEngine.GameObject GetPrefab(int hash) => Prefabs.TryGetValue(hash, out var prefab) ? prefab : null!;
}

namespace Splatform
{
    // Mirrors vanilla: only "Platform_UserID" strings survive; anything else becomes None.
    public readonly struct PlatformUserID
    {
        private readonly string _value;
        public PlatformUserID(string value)
        {
            int separator = value.IndexOf('_');
            _value = separator > 0 && separator < value.Length - 1 ? value : "";
        }
        public static PlatformUserID None => new("");
        public override string ToString() => _value;
    }
}

namespace UnityEngine
{
    public class Object { public static bool operator true(Object? o) => o != null && !o.Destroyed; public static bool operator false(Object? o) => o == null || o.Destroyed; public static bool operator !(Object? o) => o == null || o.Destroyed; public static implicit operator bool(Object? o) => o != null && !o.Destroyed; public bool Destroyed; }
    public class MonoBehaviour : Object
    {
        public static bool operator !(MonoBehaviour? o) => o == null || o.Destroyed;
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public IEnumerator? LastStarted;
        public Coroutine StartCoroutine(IEnumerator routine) { LastStarted = routine; routine.MoveNext(); return new(routine); }
        public void StopCoroutine(Coroutine routine) { (routine.Routine as IDisposable)?.Dispose(); }
    }
    public sealed class Coroutine { public readonly IEnumerator Routine; public Coroutine(IEnumerator routine) { Routine = routine; } }
    public sealed class WaitForFixedUpdate { }
    public sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    public static class Time { public static float time; }
    public static class Random { public static float Range(float min, float max) => min; }
    public class GameObject : Object { public static bool operator !(GameObject? o) => o == null || o.Destroyed; public string name; public Transform transform = new(); public GameObject(string name) { this.name = name; } public object? Component; public T? GetComponent<T>() where T : class => Component as T; }
    public class Transform : Object
    {
        public Vector3 position;
        public Transform? parent;
        public object? Component;
        public T? GetComponent<T>() where T : class => Component as T;
        public bool IsChildOf(Transform ancestor)
        {
            for (Transform? current = parent; current != null; current = current.parent)
                if (ReferenceEquals(current, ancestor)) return true;
            return false;
        }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator *(Vector3 vector, float scale) => new(vector.x * scale, vector.y * scale, vector.z * scale);
        public static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt(
            (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z));
        public override string ToString() => $"({x},{y},{z})";
    }
    public struct Color { public float r, g, b, a; public static Color white => new() { r = 1, g = 1, b = 1, a = 1 }; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public sealed class Sprite { public readonly string Name; public Sprite(string name) { Name = name; } }
    public sealed class RectTransform : Transform { public Vector3 localScale = new(1, 1, 1); public Vector2 anchoredPosition; public Rect rect = new() { height = 32f }; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Rect { public float height; }
}

namespace UnityEngine.UI
{
    public sealed class Image : UnityEngine.Object
    {
        public UnityEngine.Transform transform = new();
        public UnityEngine.Sprite? sprite;
        public UnityEngine.Color color;
        public bool enabled;
    }
}

public sealed class Minimap : UnityEngine.Object
{
    public static bool operator !(Minimap? o) => o == null || o.Destroyed;
    public static bool operator true(Minimap? o) => o != null && !o.Destroyed;
    public static bool operator false(Minimap? o) => o == null || o.Destroyed;
    public static implicit operator bool(Minimap? o) => o != null && !o.Destroyed;
    public static Minimap? instance;
    public enum PinType { None, User, Icon0, Icon1, Icon2, Icon3, Icon4, Death, Boss }
    public struct SpriteData { public PinType m_name; public UnityEngine.Sprite m_icon; }
    public List<SpriteData> m_icons = new();
    public UnityEngine.UI.Image? m_selectedIcon0, m_selectedIcon1, m_selectedIcon2, m_selectedIcon3,
        m_selectedIcon4, m_selectedIconDeath, m_selectedIconBoss;
    public enum MapMode { Large, Small }
    public MapMode m_mode;
    public bool m_pinUpdateRequired;
    public List<PinData> m_pins = new();
    public Dictionary<UnityEngine.Vector3, PinData> m_locationPins = new();
    public UnityEngine.RectTransform m_pinNameRootLarge = new();
    public UnityEngine.RectTransform m_pinNameRootSmall = new();
    public void RemovePin(PinData pin) => m_pins.Remove(pin);
    public void CreateMapNamePin(PinData pin, UnityEngine.RectTransform root) { pin.m_NamePinData = new PinNameData(pin); }
    public bool IsExplored(UnityEngine.Vector3 position) => false;
    public class PinNameData { public PinNameData(PinData pin) { PinNameText = new Text(); } public Text? PinNameText; public UnityEngine.RectTransform? PinNameRectTransform; public void DestroyMapMarker() { } }
    public sealed class Text
    {
        private string _text = "";
        private UnityEngine.Color _color;
        public int Writes;
        public string text { get => _text; set { _text = value; Writes++; } }
        public UnityEngine.Color color { get => _color; set { _color = value; Writes++; } }
    }
    public class PinData
    {
        public PinType m_type;
        public bool m_checked;
        public long m_ownerID;
        public string? m_name;
        public bool m_save;
        public UnityEngine.Sprite? m_icon;
        public UnityEngine.Vector3 m_pos;
        public Splatform.PlatformUserID m_author;
        public PinNameData? m_NamePinData;
        public IconElement? m_iconElement;
        public UnityEngine.RectTransform? m_uiElement;
    }
    public sealed class IconElement
    {
        public static bool operator true(IconElement? o) => o != null;
        public static bool operator false(IconElement? o) => o == null;
        public static bool operator !(IconElement? o) => o == null;
        private UnityEngine.Color _color;
        public int Writes;
        public UnityEngine.Color color { get => _color; set { _color = value; Writes++; } }
        public UnityEngine.Sprite? sprite;
    }
}

namespace AutoMapPins
{
    public static class AutoMapPinsPlugin
    {
        public static readonly Setting<bool> PrefabDiscoveryEnabled = new(true);
        public static readonly Setting<bool> SilentDiscoveryEnabled = new(true);
        public static readonly Setting<float> GroupingRadius = new(30f);
        public static readonly Setting<bool> TemporaryPinsWithinDiscoveryRadiusOnly = new(false);
        public static readonly Setting<bool> DiscoverGeneratedZonePins = new(false);
        internal static AutoMapPins.Discovery.GeneratedPinService? CurrentGeneratedService;
        public static bool OwnsDistantPin(AutoMapPins.Model.MapPin pin) => CurrentGeneratedService?.Owns(pin) == true;
        public static readonly AutoMapPins.Common.TestLogger Log = new();
        public static int ReadYamlEvents;
        public static void ReadYamlFileContent(object sender, FileSystemEventArgs e) { ReadYamlEvents++; }
    }
}

public sealed class Setting<T>
{
    public Setting(T value) { Value = value; }
    public T Value { get; set; }
}
