using System;
using System.Linq;
using System.Reflection;
using AutoMapPins;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Model;
using UnityEngine;
internal static class MovedObjectExtendedTests
{
    private static int passed;
    private static void Check(string name, bool value)
    {
        if (!value) throw new Exception(name);
        passed++; Console.WriteLine("PASS " + name);
    }
    private static PinComponent Spawn(float x)
    {
        var c = (PinComponent)Activator.CreateInstance(typeof(PinComponent), BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<object>(), null)!;
        c.gameObject = new GameObject("MovedFixture");
        c.gameObject.transform.position = new Vector3(x,0,0);
        c.Awake(); return c;
    }
    private static void Setup(bool permanent=false, bool grouped=false)
    {
        Minimap.instance = new Minimap();
        Registry.ConfiguredPins.Clear();
        Registry.ConfiguredPins["MovedFixture"] = new Config.Pin { IsActive=true, IsPermanent=permanent, Groupable=grouped, GroupingDistance=30, Name="MovedFixture", IconName="NoConfig" };
        DiscoveryRadiusService.ForceVisible = true;
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = false;
    }
    public static int Main()
    {
        Setup();
        var moving = Spawn(10);
        moving.gameObject.transform.position = new Vector3(20,0,0);
        PinComponent.RescanRegisteredVisiblePins();
        Check("rescan relocates temporary membership without ghost", Minimap.instance!.m_pins.Count == 1 && Minimap.instance.m_pins[0].m_pos.x == 20);
        PinComponent.RescanRegisteredVisiblePins();
        Check("repeated rescan idempotent", Minimap.instance.m_pins.Count == 1);
        moving.gameObject.transform.position = new Vector3(21,0,0);
        moving.OnDestroy();
        Check("destroy removes latest inserted identity", Minimap.instance.m_pins.Count == 0);
        moving.OnDestroy();
        Check("repeated destroy harmless", Minimap.instance.m_pins.Count == 0);

        Setup(grouped:true);
        var first = Spawn(10); var second = Spawn(12);
        Check("fixture starts grouped", Minimap.instance!.m_pins.Count == 1 && ((MapPin)Minimap.instance.m_pins[0]).InstanceCount == 2);
        first.gameObject.transform.position = new Vector3(100,0,0);
        first.OnDestroy();
        Check("moved member removal preserves other member", Minimap.instance.m_pins.Count == 1 && ((MapPin)Minimap.instance.m_pins[0]).InstanceCount == 1 && Minimap.instance.m_pins[0].m_pos.x == 12);
        second.OnDestroy();
        Check("last grouped member cleanup", Minimap.instance.m_pins.Count == 0);

        Setup();
        var hidden = Spawn(10);
        hidden.gameObject.transform.position = new Vector3(15,0,0);
        DiscoveryRadiusService.ForceVisible = false;
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = true;
        PinComponent.RefreshVisiblePinsAfterConfigChange();
        Check("hide after move removes original identity", Minimap.instance!.m_pins.Count == 0);
        hidden.OnDestroy();
        Check("destroy after hide harmless", Minimap.instance.m_pins.Count == 0);

        Setup(permanent:true);
        var saved = Spawn(10);
        saved.gameObject.transform.position = new Vector3(15,0,0);
        saved.OnDestroy();
        Check("moved permanent discovery remains at original position", Minimap.instance!.m_pins.Count == 1 && Minimap.instance.m_pins[0].m_save && Minimap.instance.m_pins[0].m_pos.x == 10);
        AutoMapPinsPlugin.TemporaryPinsWithinDiscoveryRadiusOnly.Value = false;
        Console.WriteLine("SUMMARY " + passed + " PASS");
        return 0;
    }
}
