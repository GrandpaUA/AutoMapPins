using System;
using System.Reflection;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Model;
using UnityEngine;
internal static class MovedObjectLifecycleTests
{
    public static int Main()
    {
        Minimap.instance = new Minimap();
        Registry.ConfiguredPins.Clear();
        Registry.ConfiguredPins["MovedFixture"] = new Config.Pin { IsActive=true, IsPermanent=false, Name="MovedFixture", IconName="NoConfig" };
        DiscoveryRadiusService.ForceVisible = true;
        var component = (PinComponent)Activator.CreateInstance(typeof(PinComponent), BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<object>(), null)!;
        component.gameObject = new GameObject("MovedFixture");
        component.gameObject.transform.position = new Vector3(10,0,0);
        component.Awake();
        if (Minimap.instance.m_pins.Count != 1) throw new Exception("Fixture did not create pin");
        component.gameObject.transform.position = new Vector3(11,0,0);
        component.OnDestroy();
        Console.WriteLine("Temporary pins after moved object's OnDestroy: " + Minimap.instance.m_pins.Count);
        return Minimap.instance.m_pins.Count == 0 ? 0 : 1;
    }
}
