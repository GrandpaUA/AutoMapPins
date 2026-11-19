using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapPins.Common;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using HarmonyLib;
using JetBrains.Annotations;
using System.Reflection;
using UnityEngine;

namespace AutoMapPins.Patches;

[HarmonyPatch(typeof(Console), nameof(Console.Awake))]
internal class ConsolePatches : HasLogger
{
    private const string WriteMissingConfigsOption = "write_missing_configs_file";
    private const string PrintEffectiveConfig = "print_effective_config";
    private const string DebugLogPins = "debug_log_pins";
    private const string ClearPins = "clear_pins";
    private const string ScanNearby = "scan_nearby";
    private const string Scan = "scan";
    private const string DebugRadius = "debug_radius";

    private const string ClearAmpMessage =
        "removed {0} AutoMapPins pins from map; your own, death, boss and location pins were kept";

    private const string PrintEffectiveConfigRequiredNameMessage =
        "for printing the effective config of an object, please provide the object name as argument to the command";

    [UsedImplicitly]
    private static void Postfix(Console __instance)
    {
        _ = new Terminal.ConsoleCommand("amp", description: "auto map pins commands",
            action: (Terminal.ConsoleEvent)(consoleEventArgs =>
            {
                if (consoleEventArgs.Length > 1)
                {
                    AutoMapPinsPlugin.Log.LogInfo(
                        $"called amp with args: '{string.Join(", ", consoleEventArgs.Args.ToList())}'");
                    switch (consoleEventArgs.Args[1])
                    {
                        case DebugLogPins:
                            if (Minimap.instance)
                                Log.LogInfo("vanilla map registered pins:\n" + MinimapPatch.PrintMapPins());
                            break;
                        case ClearPins:
                            if (Minimap.instance)
                            {
                                int removed = MinimapPatch.ClearAmpPins();
                                string message = string.Format(ClearAmpMessage, removed);
                                __instance.Print(message);
                                Log.LogWarning(message);
                            }

                            break;
                        case PrintEffectiveConfig:
                            if (consoleEventArgs.Args.Length < 3)
                            {
                                __instance.Print(PrintEffectiveConfigRequiredNameMessage);
                                break;
                            }

                            var objectName = consoleEventArgs.Args[2];
                            if (!String.IsNullOrEmpty(objectName) &&
                                Registry.ConfiguredPins.TryGetValue(objectName, out Config.Pin config))
                            {
                                __instance.Print($"{objectName}:");
                                __instance.Print("  " + config.ToString().Replace("\n", "\n  "));
                            }
                            else __instance.Print($"no config found for object name '{objectName}'");


                            break;
                        case WriteMissingConfigsOption:
                            WriteMissingConfigs();
                            break;
                        case ScanNearby:
                        {
                            if (!LoadedObjectScanOptions.TryParseLegacy(consoleEventArgs.Args, out float radius,
                                    out bool listAll))
                            {
                                __instance.Print(
                                    $"usage: amp scan_nearby [radius 1..{LoadedObjectScanOptions.MaximumLegacyRadius:F0}] [all]");
                                break;
                            }
                            ScanNearbyObjects(__instance, radius, listAll);
                            break;
                        }
                        case Scan:
                        {
                            if (!LoadedObjectScanOptions.TryParse(consoleEventArgs.Args, out float scanRadius,
                                    out bool includeChildren))
                            {
                                __instance.Print("usage: amp scan [radius 1..100] [all]");
                                break;
                            }
                            ScanLoadedObjectIds(__instance, scanRadius, includeChildren);
                            break;
                        }
                        case DebugRadius:
                        {
                            string breakdown = DiscoveryRadiusService.GetDebugBreakdown();
                            Log.LogInfo("discovery radius debug:\n" + breakdown);
                            __instance.Print(breakdown);
                            break;
                        }
                        default:
                            PrintUsage(__instance);
                            break;
                    }
                }
                else PrintUsage(__instance);
            }),
            optionsFetcher: (Terminal.ConsoleOptionsFetcher)OptionFetcher
        );
    }

    private static void PrintUsage(Console __instance)
    {
        __instance.Print(
            "Auto Map Pins (amp) console commands - use 'amp' followed by one of the following options");
        __instance.Print($" {DebugLogPins} --> print all active pins to log");
        __instance.Print(
            $" {ClearPins} --> remove all pins created by AutoMapPins (including its saved permanent " +
            $"pins); your own, death, boss and location pins stay. Pins of nearby objects reappear when rediscovered");
        __instance.Print(
            $" {PrintEffectiveConfig} <object name> --> replace '<object name>' with the " +
            $"object to print the effective config for, this can be used to debug config " +
            $"issues and config inheritance");
        __instance.Print(
            $" {WriteMissingConfigsOption} --> will write all objects without config to a yaml file");
        __instance.Print(
            $" {ScanNearby} [radius] [all] --> list all pinnable objects near the player (default 30m) " +
            $"with their real object names, component types and config status; use this to find " +
            $"the correct object names for yaml configs; add 'all' to also list every GameObject " +
            $"nearby with all of its components, including objects without a known pinnable component");
        __instance.Print(
            $" {Scan} [radius] [all] --> summarize loaded object IDs, counts and nearest distance " +
            $"within 1..100m (default 30m). 'all' includes child objects; only loaded objects are visible");
        __instance.Print(
            $" {DebugRadius} --> print the current map discovery radius breakdown " +
            $"(provider, base radius, skill factors, ship attachment) to console and log");
    }

    private static void ScanLoadedObjectIds(Console console, float radius, bool all)
    {
        if (!Player.m_localPlayer)
        {
            console.Print("no local player found, join a world first");
            return;
        }

        Vector3 center = Player.m_localPlayer.transform.position;
        var summary = LoadedObjectSceneScanner.Scan(
            UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            center, Player.m_localPlayer.transform, radius, all,
            Constants.ParseInternalName, Registry.ConfiguredPins.ContainsKey);
        string result = $"player position: {center}\n" + summary.Render(radius, all);
        Log.LogInfo(result);
        console.Print(result);
    }

    private static void ScanNearbyObjects(Console console, float radius, bool listAll)
    {
        if (!Player.m_localPlayer)
        {
            console.Print("no local player found, join a world first");
            return;
        }

        Vector3 center = Player.m_localPlayer.transform.position;
        var entries = new List<string>();
        int scannedComponents = 0;

        // includeInactive: objects on inactive parents are still worth listing for debugging
        foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            scannedComponents++;
            if (component is not (Destructible or TreeBase or Beehive or MineRock or MineRock5 or
                                  Location or Leviathan or TeleportWorld or PickableItem or Container or Pickable
                                  or SpawnOnDamaged or ResourceRoot))
                continue;

            float distance = Vector3.Distance(center, component.transform.position);
            if (distance > radius) continue;

            string internalName = Constants.ParseInternalName(component.name);
            string status = Registry.ConfiguredPins.ContainsKey(internalName) ? "configured" : "NOT configured";
            entries.Add($"{distance,6:F1}m  '{component.name}' (internal '{internalName}') " +
                        $"[{component.GetType().Name}] {status}");
        }

        entries.Sort(StringComparer.Ordinal);

        string result = $"player position: {center}\n" +
                        $"scanned {scannedComponents} MonoBehaviour components (including inactive)\n" +
                        $"pinnable objects within {radius}m of the player:\n" + String.Join("\n", entries);

        if (listAll)
        {
            // The player's own hierarchy (body parts, attached gear, weather FX following the
            // player) floods the nearest-by-distance list, so it is excluded. World objects
            // (ZNetView) are listed in full; non-networked objects are limited to root
            // GameObjects so child meshes/bones do not crowd out the useful entries.
            Transform playerTransform = Player.m_localPlayer.transform;
            var worldEntries = new List<string>();
            var otherEntries = new List<string>();
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject.transform.IsChildOf(playerTransform)) continue;

                float distance = Vector3.Distance(center, gameObject.transform.position);
                if (distance > radius) continue;

                bool networked = gameObject.GetComponent<ZNetView>();
                if (!networked && gameObject.transform.parent != null) continue;

                string components = String.Join(", ", gameObject.GetComponents<Component>()
                    .Select(c => c ? c.GetType().Name : "null"));
                string entry = $"{distance,6:F1}m  '{gameObject.name}' active={gameObject.activeInHierarchy} [{components}]";
                (networked ? worldEntries : otherEntries).Add(entry);
            }

            worldEntries.Sort(StringComparer.Ordinal);
            otherEntries.Sort(StringComparer.Ordinal);
            const int maxOtherEntries = 30;
            result += $"\nnetworked objects (ZNetView) within {radius}m ({worldEntries.Count}):\n" +
                      String.Join("\n", worldEntries) +
                      $"\nother root objects within {radius}m (showing {Math.Min(maxOtherEntries, otherEntries.Count)} of {otherEntries.Count}):\n" +
                      String.Join("\n", otherEntries.Take(maxOtherEntries));
        }

        Log.LogInfo(result);
        console.Print(result);
    }

    private static void WriteMissingConfigs()
    {
        Dictionary<string, Config.Category> missingConfigs = Registry.MissingConfigs
            .Select(config => new KeyValuePair<string, string>("category", config))
            .GroupBy(kv => kv.Key)
            .ToDictionary(group => group.Key, group =>
                new Config.Category
                {
                    IsActive = false,
                    IndividualConfiguredObjects = new Dictionary<string, Config.Pin>
                    {
                        {
                            "example", new Config.Pin
                            {
                                Name = "example name",
                                IconName = "mine",
                                IconColorRGBA = Config.PinColor.White
                            }
                        }
                    },
                    CategoryConfiguredObjects = group
                        .Select(kv => kv.Value)
                        .OrderBy(x => x)
                        .ToList()
                }
            );
        if (missingConfigs.Count > 0)
            AutoMapPinsPlugin.FileIO.WriteFile(AutoMapPinsPlugin.FileIO.GetSingleFile(WriteMissingConfigsOption),
                missingConfigs);
        else
            Log.LogWarning("could not print any configs, since no config was recorded during game play");
    }

    private static List<string> OptionFetcher() => new()
        { PrintEffectiveConfig, WriteMissingConfigsOption, DebugLogPins, ClearPins, ScanNearby, Scan, DebugRadius };
}