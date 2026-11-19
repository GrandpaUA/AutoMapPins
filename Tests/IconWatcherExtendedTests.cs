using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using AutoMapPins.Data;
internal static class IconWatcherExtendedTests
{
    public static int Main()
    {
        string previous = BepInEx.Paths.ConfigPath;
        string root = Path.Combine(Path.GetTempPath(), "AMP-watch-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"nested"));
        BepInEx.Paths.ConfigPath = root;
        var storage = new IconConfigStorage("FixItFelix.AutoMapPins");
        var names = new[] { "FixItFelix.AutoMapPins.icons.yaml", "FixItFelix.AutoMapPins.icons.custom.yaml", "FixItFelix.AutoMapPins.iconsExtra.yaml", "nested/FixItFelix.AutoMapPins.icons.more.yaml", "nested/asset.png", "unrelated.yaml" };
        var events = new Dictionary<string, ManualResetEvent>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in names) events.Add(Path.GetFullPath(Path.Combine(root,name)), new ManualResetEvent(false));
        try
        {
            var watchers = (List<FileSystemWatcher>)typeof(IconConfigStorage).GetField("_configFileWatchers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(storage)!;
            foreach(var watcher in watchers) { watcher.SynchronizingObject = null; watcher.Created += (_, e) => { if(events.TryGetValue(e.FullPath,out var signal)) signal.Set(); }; }
            int passed = 0;
            foreach(var name in names)
            {
                string path = Path.GetFullPath(Path.Combine(root,name));
                using(var stream = new FileStream(path, FileMode.CreateNew)) { stream.WriteByte(32); }
                bool observed = events[path].WaitOne(name == "unrelated.yaml" ? 500 : 3000);
                bool expected = name != "unrelated.yaml";
                if(observed != expected) throw new Exception("Unexpected notification coverage: " + name);
                passed++; Console.WriteLine("PASS Created coverage " + name + "=" + observed);
            }
            storage.Dispose(); storage.Dispose();
            if(watchers.Count != 0) throw new Exception("Disposed watcher list not empty");
            Console.WriteLine("PASS repeated disposal clears watcher ownership");
            Console.WriteLine("SUMMARY " + (passed + 1) + " PASS"); return 0;
        }
        finally { storage.Dispose(); BepInEx.Paths.ConfigPath = previous; /* Keep signals alive for any in-flight callbacks. */ Console.WriteLine("Fixture retained: " + root); }
    }
}
