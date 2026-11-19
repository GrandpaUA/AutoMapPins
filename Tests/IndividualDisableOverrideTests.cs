using System;
using System.Collections.Generic;
using AutoMapPins.Data;
internal static class IndividualDisableOverrideTests
{
    public static int Main()
    {
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            var merged = storage.DeserializeAndMergeFileData(new Dictionary<string,string> {
                ["base.yaml"] = "cat:\n  isActive: true\n  categoryConfiguredObjects: [DisabledObject, KeptObject]\n",
                ["override.yaml"] = "cat:\n  individualConfiguredObjects:\n    DisabledObject:\n      isActive: false\n"
            });
            Registry.InitializeRegistry(merged);
            bool disabled = !Registry.ConfiguredPins.ContainsKey("DisabledObject");
            bool retained = Registry.ConfiguredPins.ContainsKey("KeptObject");
            Console.WriteLine("Explicit disabled individual absent=" + disabled + "; sibling retained=" + retained);
            return disabled && retained ? 0 : 1;
        }
        finally { storage.Dispose(); }
    }
}
