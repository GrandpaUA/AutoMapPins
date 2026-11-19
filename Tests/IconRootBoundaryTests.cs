using System;
using System.IO;
using System.Reflection;
internal static class IconRootBoundaryTests
{
    public static int Main()
    {
        var resolver = typeof(AutoMapPins.Icons.Assets).GetMethod("TryResolveIconPath", BindingFlags.Static | BindingFlags.NonPublic)!;
        string root = Path.Combine(AppContext.BaseDirectory, "root-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int failures = 0;
        foreach (string suffix in new[] { "", Path.DirectorySeparatorChar.ToString(), Path.AltDirectorySeparatorChar.ToString() })
        {
            BepInEx.Paths.ConfigPath = root + suffix;
            object[] args = { "missing.png", "" };
            bool accepted = (bool)resolver.Invoke(null, args)!;
            Console.WriteLine((accepted ? "PASS" : "FAIL") + " normal path with root suffix '" + suffix + "'");
            if (!accepted) failures++;
        }
        string driveRoot = Path.GetPathRoot(root)!;
        BepInEx.Paths.ConfigPath = driveRoot;
        object[] driveArgs = { "nonexistent-amp-test-" + Guid.NewGuid().ToString("N") + ".png", "" };
        bool driveAccepted = (bool)resolver.Invoke(null, driveArgs)!;
        Console.WriteLine((driveAccepted ? "PASS" : "FAIL") + " filesystem root terminates ancestor walk");
        if (!driveAccepted) failures++;
        return failures == 0 ? 0 : 1;
    }
}
