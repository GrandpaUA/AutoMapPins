using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using AutoMapPins.Patches;
using UnityEngine;

internal static class ExploreHarmonyIntegration
{
    private static float bodyRadius;
    private static Vector3 bodyPosition;
    private static int calls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void NamedP(Vector3 p, float radius) { bodyPosition=p; bodyRadius=radius; calls++; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void NamedCenter(Vector3 center, float radius) { bodyPosition=center; bodyRadius=radius; calls++; }
    private static void Exploration(ref float radius) { radius *= 3.5f; }
    private static void Sailing(ref float radius) { radius *= 6f; }
    private static void Legacy(Vector3 center, float radius) { }
    private static MethodInfo Method(string name) => typeof(ExploreHarmonyIntegration).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    public static int Main()
    {
        var capture = typeof(MinimapPatch).GetMethod("ExplorePostfix", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (string target in new[] { "NamedP", "NamedCenter" })
        foreach (bool reverse in new[] { false, true })
        {
            var harmony = new Harmony("amp.audit.explore." + target + reverse);
            var original = Method(target);
            try
            {
                // Apply the observer first, then rebuild the patch chain as other mods load.
                harmony.Patch(original, postfix: new HarmonyMethod(capture));
                harmony.Patch(original, prefix: new HarmonyMethod(Method(reverse ? "Sailing" : "Exploration")) { priority=Priority.High });
                harmony.Patch(original, prefix: new HarmonyMethod(Method(reverse ? "Exploration" : "Sailing")) { priority=Priority.Low });
                calls=0;
                foreach (float basis in new[] { 50f, 20f })
                {
                    var position=new Vector3(17,23,-42);
                    AutoMapPins.Discovery.DiscoveryRadiusService.CapturedRadius=-1;
                    if (target=="NamedP") NamedP(position,basis); else NamedCenter(position,basis);
                    if (bodyRadius!=basis*21 || AutoMapPins.Discovery.DiscoveryRadiusService.CapturedRadius!=bodyRadius ||
                        !bodyPosition.Equals(position) || !AutoMapPins.Discovery.DiscoveryRadiusService.CapturedPosition.Equals(position))
                        throw new Exception("Explore final argument capture mismatch");
                }
                if (calls!=2) throw new Exception("Explore original invocation count mismatch");
                Console.WriteLine($"PASS Explore real Harmony target={target} reverse={reverse}, radii=1050/420");
            }
            finally { harmony.UnpatchSelf(); }
        }
        var legacy=new Harmony("amp.audit.explore.legacy-negative");
        bool rejected=false;
        try { legacy.Patch(Method("NamedP"),postfix:new HarmonyMethod(Method("Legacy"))); }
        catch (Exception e) { rejected=e.ToString().Contains("center") && e.ToString().Contains("not found"); }
        finally { legacy.UnpatchSelf(); }
        if (!rejected) throw new Exception("Legacy center-binding negative control did not reproduce");
        Console.WriteLine("PASS legacy center binding rejected on p parameter");
        Console.WriteLine("SUMMARY 5 Explore Harmony scenarios PASS; CLR/net48 stub world, NOT Valheim/Mono gameplay");
        return 0;
    }
}
namespace AutoMapPins.Discovery
{
    // Recorder only; actual radius validation/provider behavior is tested separately.
    internal static class DiscoveryRadiusService
    {
        internal static float CapturedRadius;
        internal static Vector3 CapturedPosition;
        internal static void CaptureGameExploreRadius(Vector3 position,float radius) { CapturedPosition=position; CapturedRadius=radius; }
    }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
    }
}
namespace JetBrains.Annotations { internal sealed class UsedImplicitlyAttribute : Attribute { } }
