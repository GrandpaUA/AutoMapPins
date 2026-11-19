using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AutoMapPins.Common;
using BepInEx;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoMapPins.Data;

internal class YamlFileStorage : HasLogger
{
    internal YamlFileStorage(string modGuid)
    {
        _modGuid = modGuid;
        SetupConfigFileWatcher();
        FindConfigFiles();
    }

    private const int ReadRetryCount = 3;
    private const int ReadRetryDelayMilliseconds = 50;

    private readonly string _modGuid;
    private readonly List<string> _yamlFiles = new();
    private readonly Dictionary<string, string> _lastKnownGoodFileContents = new();
    private FileSystemWatcher? _configFileWatcher;

    // YamlDotNet 15 matches fields case-sensitively. A tester's legacy YAML uses
    // `iconname` for almost every pin: the old parser silently ignored those keys.
    private static readonly Regex LegacyIconNameKey = new(
        @"^([ \t]+)iconname([ \t]*:)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal static readonly ISerializer Serializer = new SerializerBuilder()
        .DisableAliases()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    // Watch both the single-file name and optional named category files.
    private string GetModFilePattern(string fileInfix = "categories")
    {
        return $"{_modGuid}.{fileInfix}*.yaml";
    }

    internal string GetSingleFile(string fileInfix)
    {
        return Path.Combine(Paths.ConfigPath, $"{_modGuid}.{fileInfix}.yaml");
    }

    internal void WriteFile(string file, Dictionary<string, Config.Category> objects)
    {
        var yamlContent = Serializer.Serialize(objects);
        File.WriteAllText(file, yamlContent);
        Log.LogInfo($"wrote yaml content to file '{file}'");
    }

    private bool TryDeserializeFile(
        string fileName,
        string fileContent,
        out Dictionary<string, Config.Category> categories)
    {
        categories = new Dictionary<string, Config.Category>();

        try
        {
            string normalized = LegacyIconNameKey.Replace(fileContent, "$1iconName$2");
            if (!String.Equals(normalized, fileContent, StringComparison.Ordinal))
                Log.LogWarning($"category file '{fileName}' uses noncanonical iconname spelling; " +
                               "reading it as iconName (please fix the YAML when convenient)");
            categories = Deserializer.Deserialize<Dictionary<string, Config.Category>>(normalized) ??
                         new Dictionary<string, Config.Category>();
            if (!ValidateConfigFile(fileName, categories))
            {
                categories = new Dictionary<string, Config.Category>();
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            Log.LogWarning(
                $"Unable to parse config file '{fileName}' due to '{e.Message}' " +
                $"because of '{e.GetBaseException().Message}', \n{e.StackTrace}");
            return false;
        }
    }

    private static bool ValidateConfigFile(string fileName, Dictionary<string, Config.Category> categories)
    {
        foreach (var categoryEntry in categories)
        {
            if (String.IsNullOrWhiteSpace(categoryEntry.Key))
            {
                Log.LogWarning($"Rejecting config file '{fileName}' because it contains a category with a null or empty name");
                return false;
            }

            Config.Category? category = categoryEntry.Value;
            if (category?.SkillRequire is < 0 or > 100)
            {
                Log.LogWarning($"Rejecting config file '{fileName}' because category '{categoryEntry.Key}' has skillRequire outside 0..100");
                return false;
            }
            if (category == null)
            {
                Log.LogWarning($"Rejecting config file '{fileName}' because category '{categoryEntry.Key}' has no values");
                return false;
            }

            if (category.CategoryConfiguredObjects != null)
            {
                foreach (string configuredObject in category.CategoryConfiguredObjects)
                {
                    if (!String.IsNullOrWhiteSpace(configuredObject)) continue;

                    Log.LogWarning($"Rejecting config file '{fileName}' because category '{categoryEntry.Key}' contains a null or empty object name");
                    return false;
                }
            }

            if (category.IndividualConfiguredObjects == null) continue;

            foreach (var pinEntry in category.IndividualConfiguredObjects)
            {
                if (String.IsNullOrWhiteSpace(pinEntry.Key))
                {
                    Log.LogWarning($"Rejecting config file '{fileName}' because category '{categoryEntry.Key}' contains a null or empty individual object name");
                    return false;
                }

                if (pinEntry.Value == null)
                {
                    Log.LogWarning($"Rejecting config file '{fileName}' because individual object '{pinEntry.Key}' in category '{categoryEntry.Key}' has no values");
                    return false;
                }
                if (pinEntry.Value.SkillRequire is < 0 or > 100)
                {
                    Log.LogWarning($"Rejecting config file '{fileName}' because individual object '{pinEntry.Key}' in category '{categoryEntry.Key}' has skillRequire outside 0..100");
                    return false;
                }
            }
        }

        return true;
    }

    internal Dictionary<string, string> ReadConfigFiles()
    {
        FindConfigFiles();

        foreach (string cachedFile in _lastKnownGoodFileContents.Keys.Except(_yamlFiles).ToList())
        {
            _lastKnownGoodFileContents.Remove(cachedFile);
        }

        var result = new Dictionary<string, string>();
        foreach (string fileName in _yamlFiles.Where(File.Exists).Distinct().OrderBy(fileName => fileName, StringComparer.Ordinal))
        {
            if (!TryReadConfigFile(fileName, out string fileContent))
            {
                AddCachedFileContentIfAvailable(fileName, result);
                continue;
            }

            if (String.IsNullOrWhiteSpace(fileContent))
            {
                if (!AddCachedFileContentIfAvailable(fileName, result))
                    Log.LogInfo($"config file '{fileName}' is empty and will be ignored");
                continue;
            }

            if (TryDeserializeFile(fileName, fileContent, out _))
            {
                _lastKnownGoodFileContents[fileName] = fileContent;
                result[fileName] = fileContent;
            }
            else
            {
                AddCachedFileContentIfAvailable(fileName, result);
            }
        }

        return result;
    }

    private bool AddCachedFileContentIfAvailable(string fileName, Dictionary<string, string> result)
    {
        if (!_lastKnownGoodFileContents.TryGetValue(fileName, out string cachedContent)) return false;

        Log.LogWarning($"keeping last-known-good YAML content for '{fileName}'");
        result[fileName] = cachedContent;
        return true;
    }

    private bool TryReadConfigFile(string fileName, out string fileContent)
    {
        fileContent = "";
        Exception? lastException = null;

        for (int attempt = 1; attempt <= ReadRetryCount; attempt++)
        {
            try
            {
                fileContent = File.ReadAllText(fileName);
                return true;
            }
            catch (Exception e)
            {
                lastException = e;
                if (attempt < ReadRetryCount)
                    Thread.Sleep(ReadRetryDelayMilliseconds);
            }
        }

        Log.LogWarning(
            $"Unable to read config file '{fileName}' due to '{lastException?.Message}' " +
            $"because of '{lastException?.GetBaseException().Message}'");
        return false;
    }

    private void FindConfigFiles(string fileInfix = "categories")
    {
        _yamlFiles.Clear();
        string singleFileName = $"{_modGuid}.{fileInfix}.yaml";
        string legacyFileName = $"{_modGuid}.{fileInfix}.vanilla.yaml";
        var files = Directory.GetFiles(Paths.ConfigPath, singleFileName, SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Paths.ConfigPath, $"{_modGuid}.{fileInfix}.*.yaml",
                SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToList();

        foreach (string fileString in files)
        {
            // An upgrade can leave the user's edited .vanilla.yaml next to the new
            // packaged default. Keep the user's file authoritative; do not silently
            // replace it with the packaged defaults or load both copies.
            if (String.Equals(Path.GetFileName(fileString), singleFileName, StringComparison.OrdinalIgnoreCase) &&
                files.Any(other => String.Equals(Path.GetDirectoryName(other), Path.GetDirectoryName(fileString),
                                       StringComparison.OrdinalIgnoreCase) &&
                                   String.Equals(Path.GetFileName(other), legacyFileName,
                                       StringComparison.OrdinalIgnoreCase)))
            {
                Log.LogWarning($"ignoring '{fileString}' while the legacy '{legacyFileName}' exists beside it; " +
                               "back up and rename the legacy file to use the new name");
                continue;
            }

            Log.LogInfo($"found category and pin config file '{fileString}'");
            _yamlFiles.Add(fileString);
        }
    }

    private void SetupConfigFileWatcher()
    {
        _configFileWatcher = new FileSystemWatcher(Paths.ConfigPath, GetModFilePattern())
        {
            IncludeSubdirectories = true,
            SynchronizingObject = ThreadingHelper.SynchronizingObject,
            EnableRaisingEvents = true
        };
        _configFileWatcher.Changed += AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Created += AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Deleted += AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Renamed += AutoMapPinsPlugin.ReadYamlFileContent;
    }

    internal void Dispose()
    {
        if (_configFileWatcher == null) return;

        _configFileWatcher.Changed -= AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Created -= AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Deleted -= AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Renamed -= AutoMapPinsPlugin.ReadYamlFileContent;
        _configFileWatcher.Dispose();
        _configFileWatcher = null;
    }

    private static bool MergeBool(IEnumerable<bool?> values, bool defaultValue = false)
    {
        return values.LastOrDefault(value => value.HasValue) ?? defaultValue;
    }

    private static float MergeGroupingDistance(IEnumerable<float?> values)
    {
        return values.LastOrDefault(value => value.HasValue) ?? Constants.DefaultGroupingDistance;
    }

    private static bool? MergeNullableBool(IEnumerable<bool?> values)
    {
        return values.LastOrDefault(value => value.HasValue);
    }

    private static float? MergeNullableFloat(IEnumerable<float?> values)
    {
        return values.LastOrDefault(value => value.HasValue);
    }

    private static int? MergeNullableInt(IEnumerable<int?> values)
    {
        return values.LastOrDefault(value => value.HasValue);
    }

    private static T? MergeLastSpecified<T>(IEnumerable<T?> values) where T : class
    {
        return values.LastOrDefault(value => value != null);
    }

    private static Config.Pin MergePin(IEnumerable<Config.Pin> values)
    {
        List<Config.Pin> pinValues = values.ToList();
        return new Config.Pin
        {
            Name = MergeLastSpecified(pinValues.Select(pin => pin.Name)),
            IconName = MergeLastSpecified(pinValues.Select(pin => pin.IconName)),
            IconColorRGBA = MergeLastSpecified(pinValues.Select(pin => pin.IconColorRGBA)),
            IsPermanent = MergeNullableBool(pinValues.Select(pin => pin.IsPermanent)),
            IsActive = MergeNullableBool(pinValues.Select(pin => pin.IsActive)),
            Groupable = MergeNullableBool(pinValues.Select(pin => pin.Groupable)),
            GroupingDistance = MergeNullableFloat(pinValues.Select(pin => pin.GroupingDistance)),
            SkillRequire = MergeNullableInt(pinValues.Select(pin => pin.SkillRequire))
        };
    }

    internal Dictionary<string, Config.Category> DeserializeAndMergeFileData(
        Dictionary<string, string> configFileContents)
    {
        return configFileContents
            .Where(kv => !String.IsNullOrWhiteSpace(kv.Value))
            // Synced files must merge identically on peers with different OS cultures.
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => TryDeserializeFile(fileName: kv.Key, fileContent: kv.Value, out var categories)
                ? categories
                : new Dictionary<string, Config.Category>())
            .SelectMany(x => x)
            .GroupBy(kv => kv.Key)
            .ToDictionary(group => group.Key, group =>
            {
                var categoryValues = group.Select(category => category.Value).ToList();
                bool active = MergeBool(categoryValues.Select(category => category.IsActive));
                bool isPermanent = MergeBool(categoryValues.Select(category => category.IsPermanent));
                bool groupable = MergeBool(categoryValues.Select(category => category.Groupable));
                float groupingDistance = MergeGroupingDistance(categoryValues.Select(category => category.GroupingDistance));
                int? skillRequire = MergeNullableInt(categoryValues.Select(category => category.SkillRequire));
                string? iconName = MergeLastSpecified(categoryValues.Select(category => category.IconName));
                string? name = MergeLastSpecified(categoryValues.Select(category => category.Name));
                Config.PinColor? color = MergeLastSpecified(categoryValues.Select(category => category.IconColorRGBA));
                Dictionary<string, Config.Pin> individualPins = new();
                List<string> categoryPins = new();
                foreach (Config.Category categoryConfig in categoryValues)
                {
                    if (categoryConfig.IndividualConfiguredObjects != null)
                        individualPins = individualPins.Concat(categoryConfig.IndividualConfiguredObjects)
                            .GroupBy(pin => pin.Key)
                            .ToDictionary(pinGroup => pinGroup.Key,
                                pinGroup => MergePin(pinGroup.Select(pin => pin.Value))
                            );
                    if (categoryConfig.CategoryConfiguredObjects != null)
                        categoryPins = categoryPins.Concat(categoryConfig.CategoryConfiguredObjects).ToList();
                }

                return new Config.Category
                {
                    Name = name,
                    IsActive = active,
                    IsPermanent = isPermanent,
                    Groupable = groupable,
                    GroupingDistance = groupingDistance,
                    SkillRequire = skillRequire,
                    IconName = iconName,
                    IconColorRGBA = color,
                    IndividualConfiguredObjects = individualPins,
                    CategoryConfiguredObjects = categoryPins
                };
            });
    }
}
