using System;
using System.IO;
using System.Globalization;
using System.Threading;
using AutoMapPins.Data;
internal static class IconCultureOrderTests
{
    public static int Main()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        var previous = BepInEx.Paths.ConfigPath;
        var root = Path.Combine(Path.GetTempPath(), "AMP-icon-culture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        BepInEx.Paths.ConfigPath = root;
        IconConfigStorage? storage = null;
        try
        {
            File.WriteAllText(Path.Combine(root,"FixItFelix.AutoMapPins.icons.z.yaml"), "icons:\n  custom:\n    file: z.png\nvanillaOverrides:\n  Icon0:\n    file: z.png\n");
            File.WriteAllText(Path.Combine(root,"FixItFelix.AutoMapPins.icons.ä.yaml"), "icons:\n  custom:\n    file: a.png\nvanillaOverrides:\n  Icon0:\n    file: a.png\n");
            storage = new IconConfigStorage("FixItFelix.AutoMapPins");
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = storage.ReadIconConfigFiles();
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var swedish = storage.ReadIconConfigFiles();
            Console.WriteLine("Icons en-US=" + english.Icons!["custom"].File + ", sv-SE=" + swedish.Icons!["custom"].File);
            bool equal = english.Icons["custom"].File == swedish.Icons["custom"].File && english.VanillaOverrides!["Icon0"].File == swedish.VanillaOverrides!["Icon0"].File;
            Console.WriteLine(equal ? "PASS culture-independent custom and vanilla overrides" : "FAIL culture-dependent icon precedence");
            return equal ? 0 : 1;
        }
        finally { storage?.Dispose(); BepInEx.Paths.ConfigPath = previous; Thread.CurrentThread.CurrentCulture = original; Console.WriteLine("Fixture retained: " + root); }
    }
}
