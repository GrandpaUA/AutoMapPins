using System;
using System.Collections.Generic;
using System.Reflection;
using AutoMapPins.Data;
using AutoMapPins.Discovery;
using AutoMapPins.Model;
using UnityEngine;
internal static class IndividualDisableLifecycleTests
{
    private static int passed;
    private static void Check(string name, bool ok) { if(!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    public static int Main()
    {
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        PinComponent? component = null;
        try
        {
            Minimap.instance = new Minimap();
            DiscoveryRadiusService.ForceVisible = true;
            const string basis = "cat:\n  isActive: true\n  isPermanent: true\n  categoryConfiguredObjects: [Target, Sibling]\n";
            Action<string> reload = extra => Registry.InitializeRegistry(storage.DeserializeAndMergeFileData(new Dictionary<string,string> { ["base.yaml"] = basis, ["override.yaml"] = extra }));
            reload("");
            component = (PinComponent)Activator.CreateInstance(typeof(PinComponent), BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<object>(), null)!;
            component.gameObject = new GameObject("Target"); component.Awake();
            Check("permanent visible fixture created", Minimap.instance.m_pins.Count == 1 && Minimap.instance.m_pins[0].m_save);
            const string disable = "cat:\n  individualConfiguredObjects:\n    Target:\n      isActive: false\n";
            reload(disable);
            Check("hot disable removes existing permanent discovery", Minimap.instance.m_pins.Count == 0);
            Check("disabled ID remains known not missing", Registry.HasConfiguredObject("Target") && !Registry.ConfiguredPins.ContainsKey("Target"));
            Check("sibling unaffected", Registry.ConfiguredPins.ContainsKey("Sibling"));
            reload(disable);
            Check("repeated disable stays empty", Minimap.instance.m_pins.Count == 0);
            reload("cat:\n  individualConfiguredObjects:\n    Target:\n      isActive: true\n");
            Check("explicit reenable rediscovers registered visible object", Minimap.instance.m_pins.Count == 1 && Minimap.instance.m_pins[0].m_save);
            reload(disable); reload("");
            Check("overlay removal restores category inheritance", Minimap.instance.m_pins.Count == 1);
            reload(disable + "other:\n  isActive: true\n  categoryConfiguredObjects: [Target]\n");
            Check("another active category remains allowed", Registry.ConfiguredPins.ContainsKey("Target"));
            Console.WriteLine("SUMMARY " + passed + " PASS"); return 0;
        }
        finally { component?.OnDestroy(); storage.Dispose(); }
    }
}
