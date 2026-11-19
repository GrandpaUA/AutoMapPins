using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Keeps a remote scan isolated from older responses, malformed sequence numbers
// and objects outside the radius originally requested by this client.
internal sealed class GeneratedPinResponseTracker
{
    private int _requestId;
    private int _nextSequence;
    private int _received;
    private Vector3 _origin;
    private float _radius;
    private bool _active;

    internal void Begin(int requestId, Vector3 origin, float radius)
    {
        if (requestId <= 0 || !Valid(radius) || radius <= 0 || radius > GeneratedZoneScanner.MaximumRadius ||
            !Valid(origin.x) || !Valid(origin.z))
            throw new ArgumentException("Invalid remote scan request");
        _requestId = requestId;
        _nextSequence = 0;
        _received = 0;
        _origin = origin;
        _radius = radius;
        _active = true;
    }

    internal bool TryAccept(int requestId, int sequence, bool final, bool truncated,
        IReadOnlyList<GeneratedPinWireProtocol.Entry> entries, out bool complete)
    {
        complete = false;
        if (!_active || requestId != _requestId || sequence != _nextSequence ||
            (truncated && !final) || entries.Count > GeneratedPinWireProtocol.MaximumBatchSize ||
            _received + entries.Count > GeneratedZoneScanner.MaximumCandidates)
            return false;

        float radiusSquared = _radius * _radius;
        foreach (var entry in entries)
        {
            float dx = entry.Position.x - _origin.x;
            float dz = entry.Position.z - _origin.z;
            if (!Valid(dx) || !Valid(dz) || dx * dx + dz * dz > radiusSquared ||
                String.IsNullOrWhiteSpace(entry.Prefab) ||
                entry.Prefab.Length > GeneratedPinWireProtocol.MaximumPrefabNameLength)
                return false;
        }

        _received += entries.Count;
        ++_nextSequence;
        if (final)
        {
            _active = false;
            complete = !truncated;
        }
        return true;
    }

    internal void Reset()
    {
        _active = false;
        _requestId = 0;
        _received = 0;
    }

    private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
