using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AutoMapPins.Data;
internal static class CategoryCultureOrderTests
{
    public static int Main()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        var storage = new YamlFileStorage("FixItFelix.AutoMapPins");
        try
        {
            var files = new Dictionary<string,string> {
                ["FixItFelix.AutoMapPins.categories.z.yaml"] = "cat:\n  isActive: true\n  skillRequire: 10\n",
                ["FixItFelix.AutoMapPins.categories.ä.yaml"] = "cat:\n  isActive: true\n  skillRequire: 70\n"
            };
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = storage.DeserializeAndMergeFileData(files)["cat"].SkillRequire;
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var swedish = storage.DeserializeAndMergeFileData(files)["cat"].SkillRequire;
            Console.WriteLine("Identical synced YAML: en-US skill=" + english + ", sv-SE skill=" + swedish);
            return english == swedish ? 0 : 1;
        }
        finally { Thread.CurrentThread.CurrentCulture = original; storage.Dispose(); }
    }
}
