using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using AutoMapPins.Data;
internal static class IconWatcherCoverageTests
{
    public static int Main()
    {
        string previous = BepInEx.Paths.ConfigPath;
        string root = Path.Combine(Path.GetTempPath(), "AMP-watcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); BepInEx.Paths.ConfigPath = root;
        var storage = new IconConfigStorage("FixItFelix.AutoMapPins");
        try
        {
            var watchers = (List<FileSystemWatcher>)typeof(IconConfigStorage).GetField("_configFileWatchers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(storage)!;
            using var observed = new ManualResetEvent(false);
            foreach(var watcher in watchers) { watcher.SynchronizingObject = null; watcher.Created += (_, e) => { if(e.Name == "FixItFelix.AutoMapPins.iconsExtra.yaml") observed.Set(); }; }
            File.WriteAllText(Path.Combine(root,"FixItFelix.AutoMapPins.iconsExtra.yaml"), "icons:\n  probe:\n    file: probe.png\n");
            bool eventSeen = observed.WaitOne(2000);
            bool read = storage.ReadIconConfigFiles().Icons!.ContainsKey("probe");
            Console.WriteLine("Non-dot suffix discovered=" + read + "; watched=" + eventSeen);
            return read == eventSeen ? 0 : 1;
        }
        finally { storage.Dispose(); BepInEx.Paths.ConfigPath = previous; Console.WriteLine("Fixture retained: " + root); }
    }
}
