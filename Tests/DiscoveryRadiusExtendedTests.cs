using System;
using AutoMapPins;
using AutoMapPins.Discovery;
using BepInEx.Bootstrap;
using UnityEngine;

public static class AuditRadiusProvider { public static float Radius { get; set; } }
internal static class DiscoveryRadiusExtendedTests
{
    private static int failures;
    private static void Check(string name, bool ok) { Console.WriteLine((ok?"PASS ":"FAIL ")+name); if(!ok)failures++; }
    public static int Main()
    {
        Player.m_localPlayer = new Player();
        AutoMapPinsPlugin.DiscoveryRadiusSource.Value=MapDiscoveryRadiusSource.ConfigOnly;
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value=20;
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,0f})
        {
            Time.time+=10;
            AutoMapPinsPlugin.DiscoveryRadiusMultiplier.Value=value;
            Check("invalid multiplier disables radius: "+value,!DiscoveryRadiusService.TryGetCurrentRadius(out _));
        }
        Time.time+=10;
        AutoMapPinsPlugin.DiscoveryRadiusMultiplier.Value=2;
        Check("valid multiplier recovery",DiscoveryRadiusService.TryGetCurrentRadius(out float r)&&r==40);
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value=30;
        Time.time+=0.6f;
        Check("config change after cache expiry",DiscoveryRadiusService.TryGetCurrentRadius(out r)&&r==60);
        Chainloader.PluginInfos["audit.radius"]=new PluginInfo();
        AutoMapPinsPlugin.SkillRadiusPluginGuid.Value="audit.radius";
        AutoMapPinsPlugin.SkillRadiusTypeName.Value="AuditRadiusProvider";
        AutoMapPinsPlugin.SkillRadiusMemberName.Value="Radius";
        AutoMapPinsPlugin.DiscoveryRadiusSource.Value=MapDiscoveryRadiusSource.SpecificIntegration;
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,0f})
        {
            Time.time+=10; AuditRadiusProvider.Radius=value;
            Check("invalid integration falls back: "+value,DiscoveryRadiusService.TryGetCurrentRadius(out r)&&r==60);
        }
        Time.time+=10; AuditRadiusProvider.Radius=70;
        Check("integration recovers without restart",DiscoveryRadiusService.TryGetCurrentRadius(out r)&&r==140);
        Time.time+=10; AutoMapPinsPlugin.DiscoveryRadiusSource.Value=MapDiscoveryRadiusSource.ConfigOnly;
        AutoMapPinsPlugin.FallbackDiscoveryRadius.Value=float.PositiveInfinity;
        string debug=DiscoveryRadiusService.GetDebugBreakdown();
        Check("debug reports rejected radius instead of effective Infinity",debug.Contains("effective radius: 0.0 (valid: False"));
        Console.WriteLine($"SUMMARY extended radius failures={failures}"); return failures==0?0:1;
    }
}
