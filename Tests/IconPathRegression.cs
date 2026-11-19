using System;
using System.IO;
using System.Reflection;
internal static class IconPathRegression
{
    private static readonly MethodInfo Resolve = typeof(AutoMapPins.Icons.Assets).GetMethod("TryResolveIconPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static void Check(string path, bool expected)
    {
        object[] args = { path, "" };
        bool actual = (bool)Resolve.Invoke(null, args)!;
        if (actual != expected || (!actual && (string)args[1] != ""))
            throw new Exception("Unexpected resolution: " + path);
        Console.WriteLine("PASS " + path + " accepted=" + actual);
    }
    public static int Main()
    {
        // Additive fixtures remain under the test output; no user files touched.
        string root = Path.Combine(AppContext.BaseDirectory, "fixture-" + Guid.NewGuid().ToString("N"));
        string config = Path.Combine(root, "config");
        string outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(Path.Combine(config, "normal"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "probe.png"), "synthetic");
        File.WriteAllText(Path.Combine(config, "normal", "probe.png"), "synthetic");
        Directory.CreateSymbolicLink(Path.Combine(config, "linked"), outside);
        File.CreateSymbolicLink(Path.Combine(config, "file-link.png"), Path.Combine(outside, "probe.png"));
        BepInEx.Paths.ConfigPath = config;
        Check("normal/probe.png", true);
        Check("normal/missing.png", true); // Existence is checked later by loader.
        Check("linked/probe.png", false);
        Check("linked/missing.png", false);
        Check("file-link.png", false);
        Check("../outside/probe.png", false);
        Check(Path.Combine(outside, "probe.png"), false);
        string rootLink = Path.Combine(root, "profile-config");
        Directory.CreateSymbolicLink(rootLink, config);
        BepInEx.Paths.ConfigPath = rootLink;
        Check("normal/probe.png", true); // Explicit root is trusted, descendants are not.
        Check("linked/probe.png", false);
        Console.WriteLine("SUMMARY 9 PASS; source resolver, not Unity rendering");
        return 0;
    }
}
