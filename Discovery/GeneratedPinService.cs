using System.Collections;
using System.Collections.Generic;
using AutoMapPins.Model;
using AutoMapPins.Patches;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Experimental local-world path for pins in previously generated sectors. The
// dedicated-server RPC path must be implemented separately; a remote client only
// receives nearby ZDOs and must never pretend this scan covers a whole world.
internal sealed class GeneratedPinService
{
    private const float RefreshSeconds = 15f;
    private readonly MonoBehaviour _owner;
    private readonly Dictionary<PositionedObject, MapPin> _ownedPins = new();
    private readonly HashSet<MapPin> _ownedPinLookup = new();
    private readonly List<PositionedObject> _expiredPins = new();
    private const float CullIntervalSeconds = 0.5f;
    private const float ImmediateCullMovement = 8f;
    private float _nextCullAt;
    private float _lastCullRadius;
    private Vector3 _lastCullCenter;
    private Coroutine? _scan;
    private float _nextScanAt;
    private ZNet? _world;

    internal GeneratedPinService(MonoBehaviour owner) => _owner = owner;

    internal void Tick()
    {
        bool enabled = AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value;
        ZNet? world = ZNet.instance;
        if (!enabled || !world || !Minimap.instance || !Player.m_localPlayer ||
            !DiscoveryRadiusService.TryGetCurrentRadius(out float radius))
        {
            Reset();
            return;
        }

        if (world != _world)
        {
            Reset();
            _world = world;
        }

        // Cull at a bounded interval, but react immediately to a large move or a
        // shrinking radius. Never allocate a copy of every owned pin per frame.
        Vector3 playerPosition = Player.m_localPlayer.transform.position;
        if (Time.time >= _nextCullAt || radius < _lastCullRadius ||
            DistanceSquaredXZ(playerPosition, _lastCullCenter) >= ImmediateCullMovement * ImmediateCullMovement)
        {
            _nextCullAt = Time.time + CullIntervalSeconds;
            _lastCullRadius = radius;
            _lastCullCenter = playerPosition;
            CullOutOfRangePins(playerPosition, radius);
        }

        // Remote clients receive bounded scan results from the server transport;
        // their local ZDOMan does not contain the whole generated world.
        if (!world.IsServer() || _scan != null || Time.time < _nextScanAt) return;
        _nextScanAt = Time.time + RefreshSeconds;
        _scan = _owner.StartCoroutine(Scan(playerPosition, radius, world));
    }

    private IEnumerator Scan(Vector3 center, float radius, ZNet world)
    {
        // StartCoroutine can execute synchronously until the first yield. Always
        // yield before completing a tiny scan so Tick does not retain a finished
        // Coroutine handle and permanently suppress subsequent scans.
        yield return null;
        try
        {
            var seen = new HashSet<PositionedObject>();
            GeneratedZoneScanner.ScanStats? result = null;
            int added = 0;
            IEnumerator sectors = GeneratedZoneScanner.Scan(center, radius, candidate =>
            {
                if (world != ZNet.instance || !AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value ||
                    !DiscoveryRadiusService.IsWithinDiscoveryRadius(candidate.Position)) return;
                var position = new PositionedObject(candidate.Name, candidate.Position);
                seen.Add(position);
                if (_ownedPins.TryGetValue(position, out MapPin owned))
                {
                    if (MinimapPatch.ContainsDistantPin(owned)) return;
                    _ownedPins.Remove(position);
                    _ownedPinLookup.Remove(owned);
                }
                MapPin? pin = MinimapPatch.UpsertDistantPin(position);
                if (pin == null) return;
                added++;
                if (pin.Config.IsPermanent != true)
                {
                    _ownedPins[position] = pin;
                    _ownedPinLookup.Add(pin);
                }
            }, stats => result = stats);
            while (sectors.MoveNext())
            {
                if (world != ZNet.instance || !AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value)
                {
                    _scan = null;
                    yield break;
                }
                yield return sectors.Current;
            }

            if (result.HasValue)
            {
                // A capped scan is not a complete snapshot. Do not remove distant pins
                // that simply fell after the cap in the current scan order.
                if (!result.Value.Truncated)
                {
                    RemoveUnseenPins(seen);
                }
                AutoMapPinsPlugin.Log.LogInfo($"generated-zone scan: radius {radius:F0}m, sectors {result.Value.Sectors}, " +
                    $"examined {result.Value.Examined}, candidates {result.Value.Candidates}, " +
                    $"new pins {added}, temporary pins {_ownedPins.Count}, " +
                    $"truncated {result.Value.Truncated}");
            }
        }
        finally
        {
            // A callback/Unity failure must not leave a completed handle blocking all later scans.
            // Let the original exception propagate; the next scheduled Tick may retry.
            _scan = null;
        }
    }

    internal void ApplyRemoteCandidate(GeneratedPinWireProtocol.Entry entry,
        HashSet<PositionedObject> seen)
    {
        if (!AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value || !Minimap.instance ||
            !DiscoveryRadiusService.IsWithinDiscoveryRadius(entry.Position)) return;
        var position = new PositionedObject(entry.Prefab, entry.Position);
        seen.Add(position);
        if (_ownedPins.TryGetValue(position, out MapPin existing))
        {
            if (MinimapPatch.ContainsDistantPin(existing)) return;
            _ownedPins.Remove(position);
            _ownedPinLookup.Remove(existing);
        }
        MapPin? created = MinimapPatch.UpsertDistantPin(position);
        if (created != null && created.Config.IsPermanent != true)
        {
            _ownedPins[position] = created;
            _ownedPinLookup.Add(created);
        }
    }

    internal void CommitRemoteSnapshot(HashSet<PositionedObject> seen) => RemoveUnseenPins(seen);

    private void RemoveUnseenPins(HashSet<PositionedObject> seen)
    {
        _expiredPins.Clear();
        foreach (var entry in _ownedPins)
            if (!seen.Contains(entry.Key)) _expiredPins.Add(entry.Key);
        RemoveExpiredPins();
    }

    private void CullOutOfRangePins(Vector3 center, float radius)
    {
        _expiredPins.Clear();
        float radiusSquared = radius * radius;
        foreach (var entry in _ownedPins)
            if (DistanceSquaredXZ(center, entry.Key.ObjectPosition) > radiusSquared ||
                !ExplorationSkillGate.IsUnlocked(entry.Value.Config))
                _expiredPins.Add(entry.Key);
        RemoveExpiredPins();
    }

    private void RemoveExpiredPins()
    {
        foreach (PositionedObject position in _expiredPins)
        {
            if (!_ownedPins.TryGetValue(position, out MapPin pin)) continue;
            MinimapPatch.RemoveDistantPin(pin);
            _ownedPins.Remove(position);
            _ownedPinLookup.Remove(pin);
        }
        _expiredPins.Clear();
    }

    internal bool Owns(MapPin pin) => _ownedPinLookup.Contains(pin);

    internal void Reset()
    {
        if (_scan != null)
        {
            _owner.StopCoroutine(_scan);
            _scan = null;
        }
        foreach (MapPin pin in _ownedPins.Values)
            MinimapPatch.RemoveDistantPin(pin);
        _ownedPins.Clear();
        _ownedPinLookup.Clear();
        _expiredPins.Clear();
        _nextCullAt = 0f;
        _lastCullRadius = 0f;
        _lastCullCenter = default;
        _nextScanAt = 0f;
        _world = null;
    }

    private static float DistanceSquaredXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
