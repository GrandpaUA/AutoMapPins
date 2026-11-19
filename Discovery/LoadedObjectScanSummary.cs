using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoMapPins.Discovery;

// Parse console arguments without consulting game state, so invalid requests never scan a scene.
internal static class LoadedObjectScanOptions
{
    internal static bool TryParse(string[] args, out float radius, out bool all)
    {
        radius = 30f;
        all = false;
        if (args.Length < 2 || args.Length > 4) return false;
        if (args.Length >= 3)
        {
            if (String.Equals(args[2], "all", StringComparison.OrdinalIgnoreCase)) all = true;
            else if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) ||
                     Single.IsNaN(radius) || Single.IsInfinity(radius) || radius < 1f || radius > 100f)
                return false;
        }
        if (args.Length == 4)
        {
            if (all || !String.Equals(args[3], "all", StringComparison.OrdinalIgnoreCase)) return false;
            all = true;
        }
        return true;
    }

    internal const float MaximumLegacyRadius = 1000f;

    // 'amp scan_nearby [radius] [all]': same culture-invariant, finite parsing as 'amp scan',
    // with a wider limit kept for existing tester habits (loaded objects only either way).
    internal static bool TryParseLegacy(string[] args, out float radius, out bool all)
    {
        radius = 30f;
        all = false;
        if (args.Length < 2 || args.Length > 4) return false;
        for (int index = 2; index < args.Length; index++)
        {
            if (String.Equals(args[index], "all", StringComparison.OrdinalIgnoreCase))
            {
                if (all) return false;
                all = true;
            }
            else if (index != 2 ||
                     !float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) ||
                     Single.IsNaN(radius) || Single.IsInfinity(radius) || radius < 1f || radius > MaximumLegacyRadius)
                return false;
        }
        return true;
    }
}

// A diagnostic of loaded scene objects only; never walk world sectors or generate zones.
internal sealed class LoadedObjectScanSummary
{
    private sealed class Entry
    {
        internal int Count;
        internal float Nearest;
        internal bool Configured;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    internal int TotalObjects { get; private set; }
    internal int ExaminedObjects { get; set; }
    internal bool Capped { get; set; }

    internal void Add(string name, float distance, bool configured)
    {
        if (String.IsNullOrWhiteSpace(name) || Single.IsNaN(distance) || Single.IsInfinity(distance) || distance < 0f)
            return;
        if (!_entries.TryGetValue(name, out Entry entry))
            _entries[name] = entry = new Entry { Nearest = distance };
        entry.Count++;
        entry.Nearest = Math.Min(entry.Nearest, distance);
        entry.Configured |= configured;
        TotalObjects++;
    }

    internal string Render(float radius, bool all, int maxLines = 80)
    {
        string header = $"loaded object ID scan ({(all ? "all scene objects" : "root/networked objects")}, " +
                        $"radius {radius.ToString("F1", CultureInfo.InvariantCulture)}m): " +
                        $"{TotalObjects} matched, {_entries.Count} distinct, {ExaminedObjects} examined" +
                        (Capped ? " [INCOMPLETE: scan cap reached]" : "") +
                        ". This lists loaded scene objects ONLY, not unexplored/remote zones.";
        var lines = _entries.OrderBy(kv => kv.Value.Nearest).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(Math.Max(0, maxLines))
            .Select(kv => $"{kv.Key.Replace('\r', ' ').Replace('\n', ' ')}: count={kv.Value.Count}, " +
                          $"nearest={kv.Value.Nearest.ToString("F1", CultureInfo.InvariantCulture)}m, " +
                          (kv.Value.Configured ? "configured" : "NOT configured"));
        int omitted = Math.Max(0, _entries.Count - Math.Max(0, maxLines));
        return header + "\n" + String.Join("\n", lines) +
               (omitted > 0 ? $"\n... {omitted} farther IDs omitted; narrow the radius to see more." : "");
    }
}
