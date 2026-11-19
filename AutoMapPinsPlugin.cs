using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Icons;
using AutoMapPins.Model;
using AutoMapPins.Patches;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace AutoMapPins
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class AutoMapPinsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AutoMapPins";
        internal const string ModVersion = "2.3.17";
        private const string ModAuthor = "FixItFelix";
        private const string ModGuid = ModAuthor + "." + ModName;
        private const string ConfigFileName = ModGuid + ".cfg";

        private static AutoMapPinsPlugin _instance = null!;

        private readonly Harmony _harmony = new(ModGuid);
        private FileSystemWatcher? _configFileWatcher;
        private GeneratedPinService? _generatedPins;
        private GeneratedPinNetworkService? _generatedPinNetwork;

        public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModGuid);

        internal static YamlFileStorage FileIO = null!;
        internal static IconConfigStorage IconIO = null!;

        private static readonly ConfigSync ConfigSync = new(ModGuid)
        {
            DisplayName = ModName,
            CurrentVersion = ModVersion
        };

        private static ConfigEntry<bool> _configLocked = null!;
        private static CustomSyncedValue<Dictionary<string, string>> _categoryPinsConfigFilesContent = null!;
        private static Dictionary<string, string>? _lastLocalYamlContent;
        private static readonly IconReloadThrottle IconReload = new();
        internal static ConfigEntry<float> GroupingRadius = null!;
        internal static ConfigEntry<float> MaxDetectionHeight = null!;
        internal static ConfigEntry<bool> PrefabDiscoveryEnabled = null!;
        internal static ConfigEntry<bool> SilentDiscoveryEnabled = null!;
        internal static ConfigEntry<MapDiscoveryRadiusSource> DiscoveryRadiusSource = null!;
        internal static ConfigEntry<float> FallbackDiscoveryRadius = null!;
        internal static ConfigEntry<float> DiscoveryRadiusMultiplier = null!;
        internal static ConfigEntry<bool> TemporaryPinsWithinDiscoveryRadiusOnly = null!;
        internal static ConfigEntry<bool> DiscoverGeneratedZonePins = null!;

        internal static bool OwnsDistantPin(Model.MapPin pin) => _instance?._generatedPins?.Owns(pin) == true;
        internal static ConfigEntry<string> SkillRadiusPluginGuid = null!;
        internal static ConfigEntry<string> SkillRadiusTypeName = null!;
        internal static ConfigEntry<string> SkillRadiusMemberName = null!;

        private void Awake()
        {
            _instance = this;

            _configLocked = CreateConfig("1 - General", "Lock Configuration", true,
                "If 'true' and playing on a server, config can only be changed on server-side configuration, " +
                "clients cannot override");
            ConfigSync.AddLockingConfigEntry(_configLocked);

            var generalGroup = "1 - General";
            PrefabDiscoveryEnabled = CreateConfig(generalGroup, "Enable the prefab discovery", false,
                "This option will either enable (true) or disable (false, default) the discovery of new " +
                "prefabs that have not yet been configured. For smoother gameplay you can simply disable it, the mod " +
                "will then not print to console that there are new objects that have not been configured, yet. If you " +
                "want to create new configurations for not configured prefabs, you will need to enable this flag.");

            SilentDiscoveryEnabled = CreateConfig(generalGroup, "Prefab discovery silent mode", true,
                "This option will either enable (true, default) or disable (false) the log messages on " +
                "discovery of new prefabs that have not yet been configured. For smoother gameplay you can simply " +
                "enable it, the mod will then not print to console that there are new objects that have not been " +
                "configured, yet. For finding out if there are prefabs missing that were not configured, you will " +
                "need to disable this flag.");

            MaxDetectionHeight = CreateConfig(generalGroup, "Maximum height to map objects", 4000.0f,
                "This option will set the height over sea level until where game objects will get the auto " +
                "map pins logic applied. The usual default is '4000', since over 4000 meters over ground the dungeons " +
                "are placed in the game scene. BEWARE: if you set this to a lower height, your map has chances to get " +
                "pins added that point to a location inside a dungeon that is just above the height on ground," +
                " you should better not change this.");

            var groupingGroup = "2 - Grouping";
            GroupingRadius = CreateConfig(groupingGroup, "Fallback General Grouping Radius", 15.0f,
                "Grouping radius can be set per configured pin, but if it was set to 0 or no config for a " +
                "pin was provided, this value will be used instead. Radius that will be applied when trying to " +
                "group pins together that have grouping enabled. Default 15.0");

            var mapDiscoveryGroup = "3 - Map Discovery Radius";
            DiscoveryRadiusSource = CreateConfig(mapDiscoveryGroup, "Radius Source", MapDiscoveryRadiusSource.AutoDetectSkillMod,
                "ConfigOnly keeps the historical explored-map behavior unless Fallback Radius is above 0. " +
                "AutoDetectSkillMod can read Advize CartographySkill, Searica ExplorersVision, Smoothbrain Exploration, " +
                "Smoothbrain Sailing, blacks7ar Explorer, or a configured soft dependency. " +
                "SpecificIntegration only uses the configured soft dependency.");
            FallbackDiscoveryRadius = CreateConfig(mapDiscoveryGroup, "Fallback Radius", 0.0f,
                "Additional local-player XZ radius where pins may become visible before Minimap.IsExplored returns true. " +
                "Default 0 preserves legacy behavior.");
            DiscoveryRadiusMultiplier = CreateConfig(mapDiscoveryGroup, "Radius Multiplier", 1.0f,
                "Multiplier applied to the effective discovery radius from config or a skill integration.");
            TemporaryPinsWithinDiscoveryRadiusOnly = CreateConfig(mapDiscoveryGroup, "Temporary Pins Within Discovery Radius Only", false,
                "If 'true', temporary (non-permanent) pins are only shown while their object is inside the " +
                "current discovery radius: leave the radius and the pin disappears, come back and it reappears. " +
                "Explored map area no longer keeps temporary pins visible. Permanent pins are not affected.");
            TemporaryPinsWithinDiscoveryRadiusOnly.SettingChanged += OnTemporaryVisibilitySettingChanged;
            DiscoverGeneratedZonePins = CreateConfig(mapDiscoveryGroup, "Scan Previously Generated Zones (Local World Only)", false,
                "Experimental, OFF by default: scan existing server ZDOs up to 1100m for configured objects. " +
                "Never generates new world zones. May affect performance and cannot find non-networked objects. " +
                "Dedicated-server transport is not yet verified in a real multiplayer session: install the same version " +
                "on server and clients, enable on server, and test only in a backed-up world. The historical setting " +
                "name is retained for compatibility with 2.3.6.");
            SkillRadiusPluginGuid = CreateConfig(mapDiscoveryGroup, "Skill Radius Plugin GUID", "",
                "Optional BepInEx plugin GUID for a soft skill-mod integration. Leave empty to use fallback radius only.");
            SkillRadiusTypeName = CreateConfig(mapDiscoveryGroup, "Skill Radius Type Name", "",
                "Optional fully-qualified type name that exposes the skill-mod discovery radius.");
            SkillRadiusMemberName = CreateConfig(mapDiscoveryGroup, "Skill Radius Member Name", "",
                "Optional field, property, or method name returning a numeric radius. Methods may take no args or Player.");

            _categoryPinsConfigFilesContent = new(ConfigSync,
                "CategoryPinsConfigFilesContent", new Dictionary<string, string>());
            _categoryPinsConfigFilesContent.ValueChanged += ReloadRegistry;
            IconIO = new IconConfigStorage(ModGuid);
            ReadIconFileContent(null, null);
            FileIO = new YamlFileStorage(ModGuid);
            ReadYamlFileContent(null, null);
            SetupFileWatcher(ConfigFileName);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            _generatedPins = new GeneratedPinService(this);
            _generatedPinNetwork = new GeneratedPinNetworkService(this);
            LogUsageMode();
            DiscoveryRadiusService.LogActiveProvider();
        }

        internal static void ReadYamlFileContent(object? _, FileSystemEventArgs? change)
        {
            if (_categoryPinsConfigFilesContent is null || FileIO is null) return;

            Dictionary<string, string> files = FileIO.ReadConfigFiles();
            // FileSystemWatcher can report the same write more than once. Still allow
            // an explicit call without an event to force a reload when necessary.
            if (change != null && SameFileContents(_lastLocalYamlContent, files)) return;
            Log.LogInfo("loading pin configs from yaml files");
            _categoryPinsConfigFilesContent.Value = files;
            _lastLocalYamlContent = new Dictionary<string, string>(files);
        }

        private static bool SameFileContents(Dictionary<string, string>? previous, Dictionary<string, string> current)
        {
            if (previous == null || previous.Count != current.Count) return false;
            foreach (var entry in current)
                if (!previous.TryGetValue(entry.Key, out string oldContent) ||
                    !String.Equals(oldContent, entry.Value, StringComparison.Ordinal)) return false;
            return true;
        }

        internal static void ReadIconFileContent(object? _, FileSystemEventArgs? change)
        {
            if (IconIO is null) return;
            if (change != null)
            {
                // FileSystemWatcher fires while editors are still writing PNG/YAML.
                // Wait for a quiet 350 ms before reading/disposing any live sprites.
                IconReload.ScheduleChange(Time.realtimeSinceStartup);
                return;
            }

            Log.LogInfo("loading icon configs from yaml files");
            Config.IconConfigFile iconConfig = IconIO.ReadIconConfigFiles();
            if (IconIO.HasPendingMissingFiles)
            {
                // An editor may be replacing the YAML by atomic rename. Keep
                // live sprites until the file returns or deletion survives grace.
                IconReload.RetryAfterFailure(Time.realtimeSinceStartup);
                return;
            }
            // Every readable entry applies now (size, new icons, PNG edits). An entry whose
            // PNG is unreadable keeps its last working image; the editor may still hold
            // the file open after the watcher went quiet, so retry a bounded number of times.
            if (Assets.LoadCustomIconConfig(iconConfig))
                IconReload.Clear();
            else
                IconReload.RetryAfterFailure(Time.realtimeSinceStartup);
            MinimapPatch.UpdatePins();
            MinimapPatch.RefreshIconVisuals();
        }

        private static void ReloadRegistry()
        {
            Registry.InitializeRegistry(
                newConfiguredCategories: FileIO.DeserializeAndMergeFileData(_categoryPinsConfigFilesContent.Value));
        }

        private void SetupFileWatcher(string fileName)
        {
            _configFileWatcher = new FileSystemWatcher(Paths.ConfigPath, fileName)
            {
                IncludeSubdirectories = true,
                SynchronizingObject = ThreadingHelper.SynchronizingObject,
                EnableRaisingEvents = true
            };
            _configFileWatcher.Changed += ReloadConfig;
            _configFileWatcher.Created += ReloadConfig;
            _configFileWatcher.Renamed += ReloadConfig;
        }

        private void ReloadConfig(object? _, FileSystemEventArgs? __)
        {
            Log.LogWarning($"config file '{ConfigFileName}' change registered");
            Config.Reload();
            PinComponent.RefreshVisiblePinsAfterConfigChange();
            LogUsageMode();
            DiscoveryRadiusService.LogActiveProvider();
        }

        private static void OnTemporaryVisibilitySettingChanged(object sender, EventArgs args)
        {
            PinComponent.RefreshVisiblePinsAfterConfigChange();
        }

        private static void LogUsageMode()
        {
            if (PrefabDiscoveryEnabled.Value)
                Log.LogWarning("loaded mod with game object discovery enabled, deactivate to increase performance");
            if (!SilentDiscoveryEnabled.Value)
                Log.LogWarning(
                    "silent discovery mode was deactivated, this will print log each time an object without " +
                    "config will be approached");
        }

        private void Update()
        {
            if (IconReload.TryConsume(Time.realtimeSinceStartup))
                ReadIconFileContent(null, null);
            _generatedPins?.Tick();
            _generatedPinNetwork?.Tick(_generatedPins);
        }

        public void OnDestroy()
        {
            _generatedPinNetwork?.Reset();
            _generatedPins?.Reset();
            if (TemporaryPinsWithinDiscoveryRadiusOnly != null)
                TemporaryPinsWithinDiscoveryRadiusOnly.SettingChanged -= OnTemporaryVisibilitySettingChanged;

            if (_categoryPinsConfigFilesContent != null)
                _categoryPinsConfigFilesContent.ValueChanged -= ReloadRegistry;

            if (_configFileWatcher != null)
            {
                _configFileWatcher.Changed -= ReloadConfig;
                _configFileWatcher.Created -= ReloadConfig;
                _configFileWatcher.Renamed -= ReloadConfig;
                _configFileWatcher.Dispose();
                _configFileWatcher = null;
            }

            PickerIconOverrides.Reset();
            IconIO?.Dispose();
            FileIO?.Dispose();
            _lastLocalYamlContent = null;
            IconReload.Clear();
            _harmony.UnpatchSelf();
        }

        private static ConfigEntry<T> CreateConfig<T>(string group, string parameterName, T value,
            ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = _instance.Config.Bind(group, parameterName, value, description);
            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }

        private static ConfigEntry<T> CreateConfig<T>(string group, string parameterName, T value, string description,
            bool synchronizedSetting = true) => CreateConfig(group, parameterName, value,
            new ConfigDescription(description), synchronizedSetting);
    }
}
