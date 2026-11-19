using System;
using System.Linq;
using System.Reflection;
using AutoMapPins.Data;
using AutoMapPins.Model;
using AutoMapPins.Patches;
using BepInEx.Bootstrap;
using UnityEngine;

namespace AutoMapPins.Discovery;

// This is the player's actual Exploration skill level, not the explored-map radius:
// Sailing and other mods can multiply that radius without changing the skill.
internal static class ExplorationSkillGate
{
    private const string ExplorationGuid = "org.bepinex.plugins.exploration";
    private const float PollIntervalSeconds = 0.5f;
    private static MethodInfo? _fromName;
    private static Skills.SkillType? _resolvedSkill;
    private static float _nextPoll;
    private static float _lastLevel = float.NaN;
    private static bool _warnedMissingApi;

    internal static bool IsUnlocked(Config.Pin config) =>
        config.SkillRequire is not > 0 ||
        !Chainloader.PluginInfos.ContainsKey(ExplorationGuid) ||
        TryGetLevel(out float level) && level >= config.SkillRequire.Value;

    internal static void Tick()
    {
        if (!Registry.HasSkillRequirements || Time.time < _nextPoll) return;
        _nextPoll = Time.time + PollIntervalSeconds;
        float level = TryGetLevel(out float value) ? value : -1f;
        if (level.Equals(_lastLevel)) return;
        _lastLevel = level;
        MinimapPatch.UpdatePins();
        PinComponent.RescanRegisteredVisiblePins();
    }

    private static bool TryGetLevel(out float level)
    {
        level = 0f;
        Player? player = Player.m_localPlayer;
        if (!Chainloader.PluginInfos.ContainsKey(ExplorationGuid) || !player) return false;
        try
        {
            _fromName ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("SkillManager.Skill", throwOnError: false))
                .FirstOrDefault(type => type != null)
                ?.GetMethod("fromName", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string) }, null);
            if (!_resolvedSkill.HasValue)
            {
                object? skillType = _fromName?.Invoke(null, new object[] { "Exploration" });
                if (skillType is Skills.SkillType resolved) _resolvedSkill = resolved;
            }
            if (!_resolvedSkill.HasValue)
            {
                if (!_warnedMissingApi)
                {
                    AutoMapPinsPlugin.Log.LogWarning("Exploration skill API unavailable; skillRequire pins stay locked");
                    _warnedMissingApi = true;
                }
                return false;
            }
            level = player.GetSkillLevel(_resolvedSkill.Value);
            return true;
        }
        catch (Exception e)
        {
            if (!_warnedMissingApi)
            {
                AutoMapPinsPlugin.Log.LogWarning($"Exploration skill lookup failed; skillRequire pins stay locked: {e.GetBaseException().Message}");
                _warnedMissingApi = true;
            }
            return false;
        }
    }
}
