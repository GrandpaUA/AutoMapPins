using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AutoMapPins.Common;
using BepInEx;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoMapPins.Data;

internal sealed class IconConfigStorage : HasLogger
{
    private const int ReadRetryCount = 3;
    private const int ReadRetryDelayMilliseconds = 50;
    private static readonly TimeSpan MissingFileGrace = TimeSpan.FromSeconds(2);

    private readonly string _modGuid;
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, string> _lastKnownGoodFileContents = new();
    private readonly Dictionary<string, DateTime> _missingSince = new();
    private readonly List<FileSystemWatcher> _configFileWatchers = new();
    internal bool HasPendingMissingFiles { get; private set; }

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal IconConfigStorage(string modGuid, Func<DateTime>? utcNow = null)
    {
        _modGuid = modGuid;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        SetupConfigFileWatchers();
    }

    internal Config.IconConfigFile ReadIconConfigFiles()
    {
        var iconFiles = FindIconConfigFiles().Where(File.Exists).ToList();
        var merged = new Config.IconConfigFile();
        HasPendingMissingFiles = false;

        // Atomic editor saves briefly remove/rename the YAML. Do not destroy the
        // live icon set for that gap; retry, but honor a deliberate deletion after
        // a bounded grace period so removing an override restores vanilla icons.
        foreach (string cachedFile in _lastKnownGoodFileContents.Keys.Except(iconFiles).ToList())
        {
            if (!_missingSince.TryGetValue(cachedFile, out DateTime since))
                _missingSince[cachedFile] = since = _utcNow();
            if (_utcNow() - since < MissingFileGrace)
            {
                HasPendingMissingFiles = true;
                AddCachedFileContentIfAvailable(cachedFile, merged);
            }
            else
            {
                _lastKnownGoodFileContents.Remove(cachedFile);
                _missingSince.Remove(cachedFile);
            }
        }
        foreach (string presentFile in iconFiles) _missingSince.Remove(presentFile);

        foreach (string fileName in iconFiles.Where(File.Exists).Distinct().OrderBy(fileName => fileName, StringComparer.Ordinal))
        {
            if (!TryReadConfigFile(fileName, out string fileContent))
            {
                AddCachedFileContentIfAvailable(fileName, merged);
                continue;
            }

            if (String.IsNullOrWhiteSpace(fileContent))
            {
                if (!AddCachedFileContentIfAvailable(fileName, merged))
                    Log.LogInfo($"icon config file '{fileName}' is empty and will be ignored");
                continue;
            }

            if (TryDeserializeFile(fileName, fileContent, out Config.IconConfigFile iconConfig))
            {
                _lastKnownGoodFileContents[fileName] = fileContent;
                MergeInto(merged, iconConfig);
            }
            else
            {
                AddCachedFileContentIfAvailable(fileName, merged);
            }
        }

        return merged;
    }

    private bool AddCachedFileContentIfAvailable(string fileName, Config.IconConfigFile merged)
    {
        if (!_lastKnownGoodFileContents.TryGetValue(fileName, out string cachedContent)) return false;
        if (!TryDeserializeFile(fileName, cachedContent, out Config.IconConfigFile cachedConfig)) return false;

        Log.LogWarning($"keeping last-known-good icon YAML content for '{fileName}'");
        MergeInto(merged, cachedConfig);
        return true;
    }

    private static void MergeInto(Config.IconConfigFile target, Config.IconConfigFile source)
    {
        if (source.Icons != null)
        {
            target.Icons ??= new Dictionary<string, Config.CustomIconConfig>();
            foreach (var icon in source.Icons)
                target.Icons[icon.Key] = icon.Value;
        }

        if (source.VanillaOverrides != null)
        {
            target.VanillaOverrides ??= new Dictionary<string, Config.CustomIconConfig>();
            foreach (var iconOverride in source.VanillaOverrides)
                target.VanillaOverrides[iconOverride.Key] = iconOverride.Value;
        }
    }

    private static bool TryDeserializeFile(
        string fileName,
        string fileContent,
        out Config.IconConfigFile iconConfig)
    {
        iconConfig = new Config.IconConfigFile();

        try
        {
            iconConfig = Deserializer.Deserialize<Config.IconConfigFile>(fileContent) ?? new Config.IconConfigFile();
            return true;
        }
        catch (Exception e)
        {
            Log.LogWarning(
                $"Unable to parse icon config file '{fileName}' due to '{e.Message}' " +
                $"because of '{e.GetBaseException().Message}', \n{e.StackTrace}");
            return false;
        }
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
            $"Unable to read icon config file '{fileName}' due to '{lastException?.Message}' " +
            $"because of '{lastException?.GetBaseException().Message}'");
        return false;
    }

    private List<string> FindIconConfigFiles()
    {
        return Directory.GetFiles(Paths.ConfigPath, $"{_modGuid}.icons*.yaml", SearchOption.AllDirectories)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToList();
    }

    private void SetupConfigFileWatchers()
    {
        // PNG content may change without a YAML write. Watch nested config icons too;
        // the plugin debounces the resulting create/write/rename bursts on its main thread.
        foreach (string filter in new[] { $"{_modGuid}.icons*.yaml", "*.png" })
        {
            var watcher = new FileSystemWatcher(Paths.ConfigPath, filter)
            {
                IncludeSubdirectories = true,
                SynchronizingObject = ThreadingHelper.SynchronizingObject,
                EnableRaisingEvents = true
            };
            watcher.Changed += AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Created += AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Deleted += AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Renamed += AutoMapPinsPlugin.ReadIconFileContent;
            _configFileWatchers.Add(watcher);
        }
    }

    internal void Dispose()
    {
        foreach (var watcher in _configFileWatchers)
        {
            watcher.Changed -= AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Created -= AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Deleted -= AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Renamed -= AutoMapPinsPlugin.ReadIconFileContent;
            watcher.Dispose();
        }

        _configFileWatchers.Clear();
    }
}
