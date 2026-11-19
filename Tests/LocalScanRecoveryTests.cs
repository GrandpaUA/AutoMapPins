using System;
using System.Collections.Generic;
using AutoMapPins;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using UnityEngine;

// Compile with the bounded gameplay fixture and select this StartupObject.
internal static class LocalScanRecoveryTests
{
    public static void Main()
    {
        Minimap.instance = new Minimap();
        ZNetScene.instance = new ZNetScene();
        ZNetScene.instance.Prefabs[101] = new GameObject("FaultFixture");
        ZDOMan.instance = new ZDOMan();
        ZDOMan.instance.Sectors[new Vector2s(0, 0)] = new List<ZDO> { new ZDO(101, new Vector3()) };
        Player.m_localPlayer = new Player();
        ZNet.instance = new ZNet(true);
        Time.time = 1;
        AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value = true;
        DiscoveryRadiusService.ForceVisible = true;
        // Fault injection only: the YAML validator does not normally accept null configs.
        Registry.ConfiguredPins["FaultFixture"] = null!;
        var owner = new MonoBehaviour();
        var service = new GeneratedPinService(owner);
        service.Tick();
        var first = owner.LastStarted!;
        bool threw = false;
        try { while (first.MoveNext()) { } }
        catch (NullReferenceException) { threw = true; }
        if (!threw) throw new Exception("Fault injection did not propagate");
        Console.WriteLine("PASS original scan exception propagates");
        Registry.ConfiguredPins.Clear();
        Time.time = 2;
        service.Tick();
        if (!ReferenceEquals(first, owner.LastStarted)) throw new Exception("Failure bypassed refresh interval");
        Console.WriteLine("PASS failure preserves retry interval");
        Time.time = 60;
        service.Tick();
        if (ReferenceEquals(first, owner.LastStarted)) throw new Exception("Stale scan handle blocked retry");
        while (owner.LastStarted!.MoveNext()) { }
        Console.WriteLine("PASS scan retries after removing injected fault");
        var successful = owner.LastStarted;
        Time.time = 80;
        service.Tick();
        if (ReferenceEquals(successful, owner.LastStarted)) throw new Exception("Successful scan blocked refresh");
        while (owner.LastStarted!.MoveNext()) { }
        Console.WriteLine("PASS successful retry permits later refresh");
        service.Reset();
    }
}
