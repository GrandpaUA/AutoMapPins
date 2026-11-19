#nullable enable
using System;
using System.Collections.Generic;
using AutoMapPins;
using AutoMapPins.Discovery;
using UnityEngine;

internal static class DiscoveryRadiusTests
{
    private static int failures;
    private static void Check(string name, bool ok)
    { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
    public static int Main()
    {
        Player.m_localPlayer = new Player();
        Time.time = 10;
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value = 20;
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), 100);
        Check("actual capture preferred over fallback", DiscoveryRadiusService.TryGetCurrentRadius(out float r) && r == 100);
        Time.time = 11;
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(100,0,0), 900);
        Check("remote exploration ignored", DiscoveryRadiusService.TryGetCurrentRadius(out r) && r == 100);
        Time.time = 14;
        Check("expired capture falls back", DiscoveryRadiusService.TryGetCurrentRadius(out r) && r == 20);
        Time.time = 20;
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), 100);
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), float.PositiveInfinity);
        Check("infinite capture does not replace good radius", DiscoveryRadiusService.TryGetCurrentRadius(out r) && r == 100);
        Time.time = 30;
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), 100);
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(float.NaN,0,0), 900);
        Check("nonfinite capture center rejected", DiscoveryRadiusService.TryGetCurrentRadius(out r) && r == 100);
        Time.time = 40;
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), 100);
        DiscoveryRadiusService.CaptureGameExploreRadius(new Vector3(0,0,0), float.NaN);
        Check("NaN capture does not poison fallback", DiscoveryRadiusService.TryGetCurrentRadius(out r) && r == 100);
        Time.time = 50;
        AutoMapPinsPlugin.DiscoveryRadiusSource.Value = MapDiscoveryRadiusSource.ConfigOnly;
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value = float.PositiveInfinity;
        Check("infinite config does not reveal all loaded pins", !DiscoveryRadiusService.TryGetCurrentRadius(out _));
        Time.time = 60;
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value = float.MaxValue;
        AutoMapPinsPlugin.DiscoveryRadiusMultiplier.Value = 2;
        Check("multiplier overflow rejected", !DiscoveryRadiusService.TryGetCurrentRadius(out _));
        Console.WriteLine($"SUMMARY failures={failures}");
        return failures == 0 ? 0 : 1;
    }
}
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object? o) => o != null; public static bool operator !(Object? o) => o == null; }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; } }
    public sealed class Transform { public Vector3 position; }
    public static class Time { public static float time; }
}
public class Character : UnityEngine.Object { }
public sealed class Player : Character
{
    public static Player m_localPlayer = null!;
    public UnityEngine.Transform transform = new();
    public bool IsAttached() => false;
    public bool IsAttachedToShip() => false;
}
public sealed class Minimap : UnityEngine.Object
{
    public static Minimap instance = new();
    public float m_exploreRadius = 100;
}
public static class Utils
{
    public static float DistanceXZ(Vector3 a, Vector3 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z));
}
namespace BepInEx.Bootstrap
{
    public sealed class PluginInfo { public object Instance = new(); }
    public static class Chainloader { public static Dictionary<string,PluginInfo> PluginInfos = new(); }
}
namespace AutoMapPins.Common { internal class UnusedNamespaceAnchor { } }
namespace AutoMapPins.Model { public static class PinComponent { public static bool IsExploredReflectionAvailable => true; } }
namespace AutoMapPins
{
    internal sealed class Setting<T> { public T Value; public Setting(T value) { Value=value; } }
    internal sealed class TestLog { public void LogInfo(object message) { } public void LogWarning(object message) { } }
    internal static class AutoMapPinsPlugin
    {
        internal static TestLog Log = new();
        internal static Setting<float> FallbackDiscoveryRadius = new(0);
        internal static Setting<float> DiscoveryRadiusMultiplier = new(1);
        internal static Setting<MapDiscoveryRadiusSource> DiscoveryRadiusSource = new(MapDiscoveryRadiusSource.AutoDetectSkillMod);
        internal static Setting<string> SkillRadiusPluginGuid = new("");
        internal static Setting<string> SkillRadiusTypeName = new("");
        internal static Setting<string> SkillRadiusMemberName = new("");
    }
}
