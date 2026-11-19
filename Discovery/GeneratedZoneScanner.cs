using System;
using System.Collections;
using System.Collections.Generic;
using AutoMapPins.Data;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Read-only source of candidate pins in *already generated* sectors. In particular,
// do not call ZoneSystem.SpawnZone/PokeGhostZone: those generate and persist new world data.
internal static class GeneratedZoneScanner
{
    internal const float MaximumRadius = 1100f;
    internal const int SectorsPerFrame = 8;
    internal const int MaximumCandidates = 2000;
    internal const int CandidatesPerFrame = 32;
    internal const int ObjectsPerFrame = 256;
    internal const int MaximumExaminedObjects = 20000;
    internal const float MaximumWorldCoordinate = 32768f;
    private const float SectorSize = 64f;
    private const float HalfSector = SectorSize / 2f;

    internal readonly struct ScanStats
    {
        internal readonly int Sectors;
        internal readonly int Candidates;
        internal readonly int Examined;
        internal readonly bool Truncated;

        internal ScanStats(int sectors, int candidates, int examined, bool truncated)
        {
            Sectors = sectors;
            Candidates = candidates;
            Examined = examined;
            Truncated = truncated;
        }
    }

    internal readonly struct Candidate
    {
        internal readonly ZDOID Id;
        internal readonly string Name;
        internal readonly Vector3 Position;

        internal Candidate(ZDOID id, string name, Vector3 position)
        {
            Id = id;
            Name = name;
            Position = position;
        }
    }

    // Only sectors whose actual square overlaps the circle need to be inspected.
    // Clamp before float-to-int conversion so a broken/malicious radius cannot
    // make this enumerate the entire world.
    internal static bool ValidCenter(Vector3 center) =>
        !float.IsNaN(center.x) && !float.IsNaN(center.z) &&
        !float.IsInfinity(center.x) && !float.IsInfinity(center.z) &&
        Math.Abs(center.x) <= MaximumWorldCoordinate && Math.Abs(center.z) <= MaximumWorldCoordinate;

    internal static IEnumerable<Vector2s> EnumerateSectors(Vector3 center, float radius)
    {
        if (float.IsNaN(radius) || radius <= 0f || !ValidCenter(center)) yield break;
        radius = Math.Min(radius, MaximumRadius);
        int minX = (int)Math.Floor((center.x - radius + HalfSector) / SectorSize);
        int maxX = (int)Math.Floor((center.x + radius + HalfSector) / SectorSize);
        int minY = (int)Math.Floor((center.z - radius + HalfSector) / SectorSize);
        int maxY = (int)Math.Floor((center.z + radius + HalfSector) / SectorSize);

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            // The sector coordinate type is short; reject out-of-world coordinates
            // instead of wrapping them into unrelated sectors.
            if (x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue) continue;
            float dx = Math.Max(0f, Math.Abs(center.x - x * SectorSize) - HalfSector);
            float dz = Math.Max(0f, Math.Abs(center.z - y * SectorSize) - HalfSector);
            if (dx * dx + dz * dz <= radius * radius)
                yield return new Vector2s(x, y);
        }
    }

    // Run as a Unity coroutine on the server (also works in a local single-player
    // world). The callback processes small batches on the main thread. It never
    // instantiates prefabs or requests sectors the server has not generated.
    internal static IEnumerator Scan(Vector3 center, float radius, Action<Candidate> onCandidate,
        Action<ScanStats>? onComplete = null)
    {
        if (ZDOMan.instance == null || !ZNetScene.instance || onCandidate == null) yield break;
        if (float.IsNaN(radius) || radius <= 0f || !ValidCenter(center)) yield break;
        radius = Math.Min(radius, MaximumRadius);

        var sectorObjects = new List<ZDO>();
        var oneSector = new SimulationDistance(0, 0);
        int sectorsChecked = 0;
        int candidatesFound = 0;
        int objectsExamined = 0;
        foreach (Vector2s sector in EnumerateSectors(center, radius))
        {
            ++sectorsChecked;
            sectorObjects.Clear();
            ZDOMan.instance.FindSectorObjects(sector, oneSector, sectorObjects);
            foreach (ZDO zdo in sectorObjects)
            {
                if (objectsExamined >= MaximumExaminedObjects)
                {
                    onComplete?.Invoke(new ScanStats(sectorsChecked, candidatesFound, objectsExamined, truncated: true));
                    yield break;
                }
                ++objectsExamined;
                if (objectsExamined % ObjectsPerFrame == 0) yield return null;
                if (!zdo.IsValid()) continue;
                Vector3 position = zdo.GetPosition();
                float dx = position.x - center.x;
                float dz = position.z - center.z;
                if (dx * dx + dz * dz > radius * radius) continue;

                GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (!prefab) continue;
                string name = prefab.name;
                if (name.Length > GeneratedPinWireProtocol.MaximumPrefabNameLength ||
                    !Registry.ConfiguredPins.TryGetValue(name, out Config.Pin config) || config.IsActive != true)
                    continue;
                if (prefab.GetComponent<Pickable>() && zdo.GetBool(ZDOVars.s_picked)) continue;

                onCandidate(new Candidate(zdo.m_uid, name, position));
                if (++candidatesFound >= MaximumCandidates)
                {
                    onComplete?.Invoke(new ScanStats(sectorsChecked, candidatesFound, objectsExamined, truncated: true));
                    yield break;
                }
                if (candidatesFound % CandidatesPerFrame == 0) yield return null;
            }

            if (sectorsChecked % SectorsPerFrame == 0) yield return null;
        }
        onComplete?.Invoke(new ScanStats(sectorsChecked, candidatesFound, objectsExamined, truncated: false));
    }
}
