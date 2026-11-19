using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoMapPins.Common;
using AutoMapPins.Model;
using BepInEx.Bootstrap;
using UnityEngine;

namespace AutoMapPins.Discovery;

internal enum MapDiscoveryRadiusSource
{
    ConfigOnly,
    AutoDetectSkillMod,
    SpecificIntegration
}

internal interface IDiscoveryRadiusProvider
{
    string Name { get; }

    // Optional human-readable breakdown of the last TryGetRadius computation, logged for diagnostics.
    string? LastDebugInfo { get; }

    bool IsAvailable();
    bool TryGetRadius(Player player, out float radius);
}

internal sealed class GameExploreRadiusProvider : IDiscoveryRadiusProvider
{
    public string Name => "actual game explore radius (all mods included)";

    public string? LastDebugInfo => null;

    public bool IsAvailable() => DiscoveryRadiusService.HasCapturedGameRadius;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = DiscoveryRadiusService.CapturedGameRadius;
        return radius > 0.0f;
    }
}

internal sealed class ConfigDiscoveryRadiusProvider : IDiscoveryRadiusProvider
{
    public string Name => "AutoMapPins config fallback";

    public string? LastDebugInfo => null;

    public bool IsAvailable() => true;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = Math.Max(0.0f, AutoMapPinsPlugin.FallbackDiscoveryRadius.Value);
        return radius > 0.0f;
    }
}

internal sealed class ReflectionDiscoveryRadiusProvider : IDiscoveryRadiusProvider
{
    private bool _availabilityWarningLogged;
    private bool _runtimeWarningLogged;

    public string Name => "reflection skill-mod discovery radius provider";

    public string? LastDebugInfo => null;

    public bool IsAvailable()
    {
        string pluginGuid = AutoMapPinsPlugin.SkillRadiusPluginGuid.Value.Trim();
        string typeName = AutoMapPinsPlugin.SkillRadiusTypeName.Value.Trim();
        string memberName = AutoMapPinsPlugin.SkillRadiusMemberName.Value.Trim();

        if (String.IsNullOrWhiteSpace(pluginGuid) ||
            String.IsNullOrWhiteSpace(typeName) ||
            String.IsNullOrWhiteSpace(memberName))
            return false;

        if (!Chainloader.PluginInfos.ContainsKey(pluginGuid))
        {
            LogAvailabilityWarningOnce($"configured skill-radius plugin '{pluginGuid}' is not loaded");
            return false;
        }

        if (FindType(typeName) != null) return true;

        LogAvailabilityWarningOnce($"configured skill-radius type '{typeName}' was not found");
        return false;
    }

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;

        try
        {
            Type? type = FindType(AutoMapPinsPlugin.SkillRadiusTypeName.Value.Trim());
            if (type == null) return false;

            string memberName = AutoMapPinsPlugin.SkillRadiusMemberName.Value.Trim();
            object? instance = ResolvePluginInstance(type);
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            PropertyInfo? property = type.GetProperty(memberName, flags);
            if (property != null && TryConvertRadius(property.GetValue(property.GetGetMethod(true)?.IsStatic == true ? null : instance), out radius))
                return true;

            FieldInfo? field = type.GetField(memberName, flags);
            if (field != null && TryConvertRadius(field.GetValue(field.IsStatic ? null : instance), out radius))
                return true;

            foreach (MethodInfo method in type.GetMethods(flags).Where(method => method.Name == memberName))
            {
                ParameterInfo[] parameters = method.GetParameters();
                object?[]? args = parameters.Length switch
                {
                    0 => Array.Empty<object?>(),
                    1 when parameters[0].ParameterType.IsAssignableFrom(typeof(Player)) => new object?[] { player },
                    _ => null
                };

                if (args == null) continue;
                if (TryConvertRadius(method.Invoke(method.IsStatic ? null : instance, args), out radius))
                    return true;
            }

            LogRuntimeWarningOnce($"configured skill-radius member '{memberName}' did not return a numeric radius");
            return false;
        }
        catch (Exception e)
        {
            LogRuntimeWarningOnce($"failed reading skill-mod discovery radius: {e.GetBaseException().Message}");
            return false;
        }
    }

    private static Type? FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private static object? ResolvePluginInstance(Type type)
    {
        string pluginGuid = AutoMapPinsPlugin.SkillRadiusPluginGuid.Value.Trim();
        if (!Chainloader.PluginInfos.TryGetValue(pluginGuid, out var pluginInfo)) return null;
        object instance = pluginInfo.Instance;
        return type.IsInstanceOfType(instance) ? instance : null;
    }

    private static bool TryConvertRadius(object? value, out float radius)
    {
        radius = 0.0f;
        switch (value)
        {
            case float floatValue:
                radius = floatValue;
                return radius > 0.0f;
            case double doubleValue:
                radius = (float)doubleValue;
                return radius > 0.0f;
            case int intValue:
                radius = intValue;
                return radius > 0.0f;
            case long longValue:
                radius = longValue;
                return radius > 0.0f;
            default:
                return false;
        }
    }

    private void LogAvailabilityWarningOnce(string message)
    {
        if (_availabilityWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _availabilityWarningLogged = true;
    }

    private void LogRuntimeWarningOnce(string message)
    {
        if (_runtimeWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _runtimeWarningLogged = true;
    }
}

internal sealed class AdvizeCartographySkillRadiusProvider : IDiscoveryRadiusProvider
{
    private const string PluginGuid = "advize.CartographySkill";

    public string Name => "Advize CartographySkill";

    public string? LastDebugInfo => null;

    public bool IsAvailable() => Chainloader.PluginInfos.ContainsKey(PluginGuid) && Minimap.instance;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;
        if (!IsAvailable()) return false;

        radius = Minimap.instance.m_exploreRadius;
        return radius > 0.0f;
    }
}

internal sealed class SearicaExplorersVisionRadiusProvider : IDiscoveryRadiusProvider
{
    private const string PluginGuid = "Searica.Valheim.ExplorersVision";
    private bool _runtimeWarningLogged;

    public string Name => "Searica ExplorersVision";

    public string? LastDebugInfo => null;

    public bool IsAvailable() => Chainloader.PluginInfos.ContainsKey(PluginGuid) && Minimap.instance;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;
        if (!IsAvailable()) return false;

        try
        {
            Type? patchType = FindType("ExplorersVision.Patches.MapRadiusPatches");
            MethodInfo? method = patchType?.GetMethod("GetExploreRadius", BindingFlags.NonPublic | BindingFlags.Static);
            object? result = method?.Invoke(null, new object[] { player });
            if (result is not float floatValue) return false;

            radius = Math.Max(0.0f, floatValue);
            return radius > 0.0f;
        }
        catch (Exception e)
        {
            LogRuntimeWarningOnce($"failed reading Searica ExplorersVision discovery radius: {e.GetBaseException().Message}");
            return false;
        }
    }

    private static Type? FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private void LogRuntimeWarningOnce(string message)
    {
        if (_runtimeWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _runtimeWarningLogged = true;
    }
}

internal sealed class Blacks7arExplorerRadiusProvider : IDiscoveryRadiusProvider
{
    private const string PluginGuid = "blacks7ar.Explorer";
    private const string SkillName = "Explorer";
    private bool _runtimeWarningLogged;

    public string Name => "blacks7ar Explorer";

    public string? LastDebugInfo => null;

    public bool IsAvailable() => Chainloader.PluginInfos.ContainsKey(PluginGuid) && Minimap.instance;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;
        if (!IsAvailable()) return false;

        try
        {
            if (!TryGetExplorerIncreasePerLevel(out int increasePerLevel)) return false;
            if (!TryGetExplorerSkillFactor(player, out float skillFactor)) return false;

            radius = Minimap.instance.m_exploreRadius * (skillFactor * increasePerLevel + 1.0f);
            return radius > 0.0f;
        }
        catch (Exception e)
        {
            LogRuntimeWarningOnce($"failed reading blacks7ar Explorer discovery radius: {e.GetBaseException().Message}");
            return false;
        }
    }

    private static bool TryGetExplorerIncreasePerLevel(out int increasePerLevel)
    {
        increasePerLevel = 0;
        Type? pluginType = FindType("Explorer.Plugin");
        FieldInfo? field = pluginType?.GetField("_increasePerLevel", BindingFlags.Public | BindingFlags.Static);
        object? configEntry = field?.GetValue(null);
        PropertyInfo? valueProperty = configEntry?.GetType().GetProperty("Value");
        object? value = valueProperty?.GetValue(configEntry);

        if (value is int intValue)
        {
            increasePerLevel = Math.Max(0, intValue);
            return true;
        }

        return false;
    }

    private static bool TryGetExplorerSkillFactor(Player player, out float skillFactor)
    {
        skillFactor = 0.0f;
        Type? extensionsType = FindType("SkillManager.SkillExtensions");
        MethodInfo? method = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(candidate =>
            {
                if (candidate.Name != "GetSkillFactor") return false;
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType.IsAssignableFrom(typeof(Character)) &&
                       parameters[1].ParameterType == typeof(string);
            });

        object? result = method?.Invoke(null, new object[] { player, SkillName });
        if (result is float floatValue)
        {
            skillFactor = Math.Max(0.0f, floatValue);
            return true;
        }

        return false;
    }

    private static Type? FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private void LogRuntimeWarningOnce(string message)
    {
        if (_runtimeWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _runtimeWarningLogged = true;
    }
}

internal sealed class SmoothbrainExplorationRadiusProvider : IDiscoveryRadiusProvider
{
    private const string PluginGuid = "org.bepinex.plugins.exploration";
    private const string SkillName = "Exploration";
    private bool _runtimeWarningLogged;

    public string Name => "Smoothbrain Exploration";

    public string? LastDebugInfo { get; private set; }

    public bool IsAvailable() => Chainloader.PluginInfos.ContainsKey(PluginGuid) && Minimap.instance;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;
        LastDebugInfo = null;
        if (!IsAvailable()) return false;

        try
        {
            Type? pluginType = FindType("Exploration.Exploration");
            if (!TryGetConfigValue(pluginType, "explorationRadiusIncrease", out int radiusIncrease)) return false;
            if (!TryGetSkillFactor(pluginType, player, SkillName, out float skillFactor)) return false;

            float multiplier = 1.0f + skillFactor * radiusIncrease / 100.0f;
            bool attachedToShip = IsAttachedToShip(player);
            float sailingMultiplier = 1.0f;
            string sailingDebug = "not attached to a ship";
            if (attachedToShip && Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.sailing"))
            {
                Type? sailingType = FindType("Sailing.Sailing");
                if (TryGetConfigValue(sailingType, "explorationRadiusIncrease", out float sailingRadiusIncrease) &&
                    TryGetSkillFactor(sailingType, player, "Sailing", out float sailingSkillFactor))
                {
                    sailingMultiplier = 1.0f + sailingSkillFactor * sailingRadiusIncrease;
                    multiplier *= sailingMultiplier;
                    sailingDebug = string.Format(CultureInfo.InvariantCulture,
                        "sailing skill {0:F2} * factor {1} -> x{2:F2}",
                        sailingSkillFactor, sailingRadiusIncrease, sailingMultiplier);
                }
                else
                {
                    sailingDebug = "sailing mod present but its config/skill could not be read";
                }
            }

            LastDebugInfo = string.Format(CultureInfo.InvariantCulture,
                "base {0} * exploration (skill {1:F2} * increase {2}%) x{3:F2} * sailing ({4}) x{5:F2}",
                Minimap.instance.m_exploreRadius, skillFactor, radiusIncrease,
                1.0f + skillFactor * radiusIncrease / 100.0f, sailingDebug, multiplier);

            radius = Minimap.instance.m_exploreRadius * multiplier;
            return radius > 0.0f;
        }
        catch (Exception e)
        {
            LogRuntimeWarningOnce($"failed reading Smoothbrain Exploration discovery radius: {e.GetBaseException().Message}");
            return false;
        }
    }

    private static bool IsAttachedToShip(Player player) => DiscoveryRadiusService.IsAttachedToShip(player);

    private static bool TryGetConfigValue<T>(Type? pluginType, string fieldName, out T value)
    {
        value = default!;
        FieldInfo? field = pluginType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        object? configEntry = field?.GetValue(null);
        PropertyInfo? valueProperty = configEntry?.GetType().GetProperty("Value");
        object? rawValue = valueProperty?.GetValue(configEntry);
        if (rawValue is not T typedValue) return false;

        value = typedValue;
        return true;
    }

    private static bool TryGetSkillFactor(Type? pluginType, Player player, string skillName, out float skillFactor)
    {
        skillFactor = 0.0f;
        Type? extensionsType = pluginType?.Assembly.GetType("SkillManager.SkillExtensions", throwOnError: false);
        MethodInfo? method = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(candidate =>
            {
                if (candidate.Name != "GetSkillFactor") return false;
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType.IsAssignableFrom(typeof(Character)) &&
                       parameters[1].ParameterType == typeof(string);
            });

        object? result = method?.Invoke(null, new object[] { player, skillName });
        if (result is not float floatValue) return false;

        skillFactor = Math.Max(0.0f, floatValue);
        return true;
    }

    private static Type? FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private void LogRuntimeWarningOnce(string message)
    {
        if (_runtimeWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _runtimeWarningLogged = true;
    }
}

internal sealed class SmoothbrainSailingRadiusProvider : IDiscoveryRadiusProvider
{
    private const string PluginGuid = "org.bepinex.plugins.sailing";
    private const string SkillName = "Sailing";
    private bool _runtimeWarningLogged;

    public string Name => "Smoothbrain Sailing";

    public string? LastDebugInfo { get; private set; }

    public bool IsAvailable() => Chainloader.PluginInfos.ContainsKey(PluginGuid) && Minimap.instance;

    public bool TryGetRadius(Player player, out float radius)
    {
        radius = 0.0f;
        LastDebugInfo = null;
        if (!IsAvailable()) return false;

        try
        {
            Type? pluginType = FindType("Sailing.Sailing");
            float multiplier = 1.0f;
            bool attachedToShip = IsAttachedToShip(player);
            if (attachedToShip)
            {
                if (!TryGetConfigValue(pluginType, "explorationRadiusIncrease", out float radiusIncrease)) return false;
                if (!TryGetSkillFactor(pluginType, player, SkillName, out float skillFactor)) return false;
                multiplier += skillFactor * radiusIncrease;
                LastDebugInfo = string.Format(CultureInfo.InvariantCulture,
                    "base {0} * sailing (skill {1:F2} * factor {2}) x{3:F2}",
                    Minimap.instance.m_exploreRadius, skillFactor, radiusIncrease, multiplier);
            }
            else
            {
                LastDebugInfo = string.Format(CultureInfo.InvariantCulture,
                    "base {0}, not attached to a ship", Minimap.instance.m_exploreRadius);
            }

            radius = Minimap.instance.m_exploreRadius * multiplier;
            return radius > 0.0f;
        }
        catch (Exception e)
        {
            LogRuntimeWarningOnce($"failed reading Smoothbrain Sailing discovery radius: {e.GetBaseException().Message}");
            return false;
        }
    }

    private static bool IsAttachedToShip(Player player) => DiscoveryRadiusService.IsAttachedToShip(player);

    private static bool TryGetConfigValue<T>(Type? pluginType, string fieldName, out T value)
    {
        value = default!;
        FieldInfo? field = pluginType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        object? configEntry = field?.GetValue(null);
        PropertyInfo? valueProperty = configEntry?.GetType().GetProperty("Value");
        object? rawValue = valueProperty?.GetValue(configEntry);
        if (rawValue is not T typedValue) return false;

        value = typedValue;
        return true;
    }

    private static bool TryGetSkillFactor(Type? pluginType, Player player, string skillName, out float skillFactor)
    {
        skillFactor = 0.0f;
        Type? extensionsType = pluginType?.Assembly.GetType("SkillManager.SkillExtensions", throwOnError: false);
        MethodInfo? method = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(candidate =>
            {
                if (candidate.Name != "GetSkillFactor") return false;
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType.IsAssignableFrom(typeof(Character)) &&
                       parameters[1].ParameterType == typeof(string);
            });

        object? result = method?.Invoke(null, new object[] { player, skillName });
        if (result is not float floatValue) return false;

        skillFactor = Math.Max(0.0f, floatValue);
        return true;
    }

    private static Type? FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private void LogRuntimeWarningOnce(string message)
    {
        if (_runtimeWarningLogged) return;
        AutoMapPinsPlugin.Log.LogWarning(message);
        _runtimeWarningLogged = true;
    }
}

internal static class DiscoveryRadiusService
{
    // Character exposes public virtual IsAttached()/IsAttachedToShip() that Player overrides;
    // the underlying fields differ across game versions (private on Player in some, public on
    // Character in others), so call the stable public API instead of reflecting on fields.
    internal static bool IsAttachedToShip(Player player) => player.IsAttached() && player.IsAttachedToShip();

    private const float RadiusCacheDurationSeconds = 0.5f;

    // The captured game radius counts as fresh while it is this recent. Minimap calls
    // Explore() every frame while in a world, so anything older means we left the world
    // (or the capture stopped) and providers should take over.
    private const float CapturedRadiusFreshnessSeconds = 3.0f;

    private static float _capturedGameRadius;
    private static float _capturedGameRadiusTimestamp = -1.0f;

    internal static bool HasCapturedGameRadius =>
        _capturedGameRadiusTimestamp >= 0.0f &&
        Time.time - _capturedGameRadiusTimestamp < CapturedRadiusFreshnessSeconds;

    internal static float CapturedGameRadius => HasCapturedGameRadius ? _capturedGameRadius : 0.0f;

    // Called from the Minimap.Explore(Vector3, float) postfix. Ignores calls that are not the
    // local player's own exploration (e.g. shared map data applied at another position).
    internal static void CaptureGameExploreRadius(Vector3 center, float radius)
    {
        Player player = Player.m_localPlayer;
        if (!player) return;
        if (!IsPositiveFinite(radius)) return;
        float distance = Utils.DistanceXZ(center, player.transform.position);
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance > 1.0f) return;

        _capturedGameRadius = radius;
        _capturedGameRadiusTimestamp = Time.time;
    }

    private static readonly IDiscoveryRadiusProvider GameRadiusProvider = new GameExploreRadiusProvider();
    private static readonly IDiscoveryRadiusProvider ConfigProvider = new ConfigDiscoveryRadiusProvider();
    private static readonly IDiscoveryRadiusProvider AdvizeCartographySkillProvider = new AdvizeCartographySkillRadiusProvider();
    private static readonly IDiscoveryRadiusProvider SearicaExplorersVisionProvider = new SearicaExplorersVisionRadiusProvider();
    private static readonly IDiscoveryRadiusProvider SmoothbrainExplorationProvider = new SmoothbrainExplorationRadiusProvider();
    private static readonly IDiscoveryRadiusProvider SmoothbrainSailingProvider = new SmoothbrainSailingRadiusProvider();
    private static readonly IDiscoveryRadiusProvider Blacks7arExplorerProvider = new Blacks7arExplorerRadiusProvider();
    private static readonly IDiscoveryRadiusProvider ReflectionProvider = new ReflectionDiscoveryRadiusProvider();
    private static string _lastProviderName = "";
    private static float _lastLoggedRadius = -1.0f;
    private static float _cachedRadius;
    private static bool _cachedRadiusValid;
    private static float _cacheTimestamp = -1.0f;

    internal static bool TryGetCurrentRadius(out float radius)
    {
        Player player = Player.m_localPlayer;
        if (player) return TryGetEffectiveRadius(player, out radius);
        radius = 0f;
        return false;
    }

    internal static bool IsWithinDiscoveryRadius(Vector3 position)
    {
        Player player = Player.m_localPlayer;
        if (!player) return false;
        if (!TryGetEffectiveRadius(player, out float radius)) return false;

        return Utils.DistanceXZ(player.transform.position, position) <= radius;
    }

    internal static void LogActiveProvider()
    {
        Player player = Player.m_localPlayer;
        IDiscoveryRadiusProvider provider = SelectProvider(player);
        AutoMapPinsPlugin.Log.LogInfo($"map discovery radius provider: {provider.Name}");
    }

    // Fresh (uncached) snapshot of everything that feeds the effective radius, for the
    // 'amp debug_radius' console command.
    internal static string GetDebugBreakdown()
    {
        Player player = Player.m_localPlayer;
        if (!player) return "no local player found, join a world first";

        IDiscoveryRadiusProvider provider = SelectProvider(player);
        bool providerOk = provider.TryGetRadius(player, out float providerRadius);
        bool effectiveOk = TryGetCurrentRadius(out float effectiveRadius);

        return string.Format(CultureInfo.InvariantCulture,
            "provider: {0} (success: {1})\n" +
            "raw provider radius: {2:F1}, AMP radius multiplier: {3:F2}, effective radius: {4:F1} (valid: {12}, cached up to 0.5s)\n" +
            "provider breakdown: {5}\n" +
            "captured game explore radius: {6:F1} (fresh: {7})\n" +
            "Minimap.m_exploreRadius: {8:F1}\n" +
            "player IsAttached: {9}, IsAttachedToShip: {10}\n" +
            "Minimap.IsExplored reflection available: {11}",
            provider.Name, providerOk, providerRadius,
            AutoMapPinsPlugin.DiscoveryRadiusMultiplier.Value, effectiveRadius,
            provider.LastDebugInfo ?? "n/a",
            _capturedGameRadius, HasCapturedGameRadius,
            Minimap.instance ? Minimap.instance.m_exploreRadius : -1.0f,
            player.IsAttached(), player.IsAttachedToShip(),
            PinComponent.IsExploredReflectionAvailable, effectiveOk);
    }

    private static bool TryGetEffectiveRadius(Player player, out float radius)
    {
        // Radius providers do reflection into skill mods; pin visibility checks call this
        // per invisible object, so the result is cached briefly instead of recomputed every call.
        if (_cacheTimestamp >= 0.0f && Time.time - _cacheTimestamp < RadiusCacheDurationSeconds)
        {
            radius = _cachedRadius;
            return _cachedRadiusValid;
        }

        IDiscoveryRadiusProvider provider = SelectProvider(player);
        if (!provider.TryGetRadius(player, out radius) || !IsPositiveFinite(radius))
        {
            provider = ConfigProvider;
            if (!provider.TryGetRadius(player, out radius) || !IsPositiveFinite(radius))
            {
                radius = 0.0f;
                _cachedRadius = 0.0f;
                _cachedRadiusValid = false;
                _cacheTimestamp = Time.time;
                return false;
            }
        }

        float multiplier = AutoMapPinsPlugin.DiscoveryRadiusMultiplier.Value;
        radius = IsPositiveFinite(multiplier) ? radius * multiplier : 0.0f;
        if (!IsPositiveFinite(radius))
        {
            radius = 0.0f;
            _cachedRadius = 0.0f;
            _cachedRadiusValid = false;
            _cacheTimestamp = Time.time;
            return false;
        }

        // Log provider switches and meaningful radius changes: skill factor changes are
        // continuous, so small drifts are ignored to avoid log spam.
        bool radiusChanged = _lastLoggedRadius < 0.0f ||
                             Math.Abs(radius - _lastLoggedRadius) > Math.Max(10.0f, _lastLoggedRadius * 0.1f);
        if (_lastProviderName != provider.Name || radiusChanged)
        {
            string debugInfo = provider.LastDebugInfo != null ? $" ({provider.LastDebugInfo})" : "";
            AutoMapPinsPlugin.Log.LogInfo(
                $"using map discovery radius provider '{provider.Name}' with radius '{radius}'{debugInfo}");
            _lastProviderName = provider.Name;
            _lastLoggedRadius = radius;
        }

        _cachedRadius = radius;
        _cachedRadiusValid = radius > 0.0f;
        _cacheTimestamp = Time.time;
        return _cachedRadiusValid;
    }

    private static bool IsPositiveFinite(float value) =>
        value > 0.0f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static IDiscoveryRadiusProvider SelectProvider(Player? player)
    {
        switch (AutoMapPinsPlugin.DiscoveryRadiusSource.Value)
        {
            case MapDiscoveryRadiusSource.SpecificIntegration:
                return ReflectionProvider.IsAvailable() ? ReflectionProvider : ConfigProvider;
            case MapDiscoveryRadiusSource.AutoDetectSkillMod:
                if (GameRadiusProvider.IsAvailable()) return GameRadiusProvider;
                if (AdvizeCartographySkillProvider.IsAvailable()) return AdvizeCartographySkillProvider;
                if (SearicaExplorersVisionProvider.IsAvailable()) return SearicaExplorersVisionProvider;
                if (SmoothbrainExplorationProvider.IsAvailable()) return SmoothbrainExplorationProvider;
                if (SmoothbrainSailingProvider.IsAvailable()) return SmoothbrainSailingProvider;
                if (Blacks7arExplorerProvider.IsAvailable()) return Blacks7arExplorerProvider;
                return ReflectionProvider.IsAvailable() ? ReflectionProvider : ConfigProvider;
            case MapDiscoveryRadiusSource.ConfigOnly:
            default:
                return ConfigProvider;
        }
    }
}
