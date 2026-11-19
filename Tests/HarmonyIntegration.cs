using System;
using System.Collections.Generic;
using System.Reflection;
using AutoMapPins.Data;
using AutoMapPins.Model;
using AutoMapPins.Patches;
using HarmonyLib;
using UnityEngine;

internal static class HarmonyIntegration
{
    public static int Main()
    {
        var harmony = new Harmony("amp.audit.sharing");
        MethodInfo Hook(string name) => typeof(MinimapPatch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        harmony.Patch(typeof(Minimap).GetMethod("AuditShare"),
            new HarmonyMethod(Hook("GetSharedMapDataPrefix")),
            new HarmonyMethod(Hook("GetSharedMapDataPostfix")),
            finalizer: new HarmonyMethod(Hook("GetSharedMapDataFinalizer")));
        foreach (bool innerFail in new[] { false, true })
        foreach (bool outerFail in new[] { false, true })
        foreach (bool catchInner in new[] { false, true })
        {
            var map = new Minimap { FailInner=innerFail, FailOuter=outerFail, CatchInner=catchInner };
            Minimap.instance = map;
            var pin = new MapPin(new PositionedObject("Tree", new Vector3(0,0,0)),
                new Config.Pin { Name="Tree", IsActive=true, IsPermanent=true });
            var saved = new Minimap.PinData { m_type=Minimap.PinType.None, m_author=pin.m_author, m_ownerID=0, m_save=true };
            var user = new Minimap.PinData { m_type=Minimap.PinType.User };
            map.m_pins.AddRange(new[] { pin, saved, user });
            bool threw=false;
            try { if (map.AuditShare()!=1) throw new Exception("serialized AMP metadata"); }
            catch (InvalidOperationException) { threw=true; }
            if (threw != (outerFail || innerFail && !catchInner) || map.m_pins.Count!=3 ||
                !map.m_pins.Contains(pin) || !map.m_pins.Contains(saved) || !map.m_pins.Contains(user))
                throw new Exception("Harmony state restoration mismatch");
            Console.WriteLine($"PASS real Harmony nested sharing inner={innerFail} outer={outerFail} caught={catchInner}");
        }
        Console.WriteLine("SUMMARY real Harmony invocation PASS (stub world; not gameplay)");
        return 0;
    }
}
