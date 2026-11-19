using System;
using System.Collections.Generic;

namespace AutoMapPins.Discovery;

// Dedicated-server admission gate. The RPC handler must check ZNet.GetPeer(sender)
// and GeneratedPinRequestPolicy before TryBegin, and always call End in finally.
internal sealed class GeneratedPinRequestLimiter
{
    private readonly Dictionary<long, float> _lastRequestAt = new();
    private readonly HashSet<long> _activePeers = new();

    internal bool TryBegin(long peerId, float now)
    {
        if (peerId <= 0 || float.IsNaN(now) || float.IsInfinity(now) ||
            _activePeers.Contains(peerId) ||
            _activePeers.Count >= GeneratedPinRequestPolicy.MaximumConcurrentRequests)
            return false;

        if (_lastRequestAt.TryGetValue(peerId, out float lastAt) &&
            (now < lastAt || now - lastAt < GeneratedPinRequestPolicy.MinimumSecondsBetweenRequests))
            return false;

        _activePeers.Add(peerId);
        _lastRequestAt[peerId] = now;
        return true;
    }

    internal void End(long peerId) => _activePeers.Remove(peerId);

    // Call after disconnect so stale peer identities do not consume server memory.
    internal void Forget(long peerId)
    {
        _activePeers.Remove(peerId);
        _lastRequestAt.Remove(peerId);
    }

    internal void Prune(ISet<long> connectedPeers)
    {
        foreach (long peerId in new List<long>(_lastRequestAt.Keys))
        {
            if (!connectedPeers.Contains(peerId)) Forget(peerId);
        }
    }

    internal void Reset()
    {
        _activePeers.Clear();
        _lastRequestAt.Clear();
    }
}
