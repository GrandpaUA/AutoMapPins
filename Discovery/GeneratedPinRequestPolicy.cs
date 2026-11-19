using System;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Validate *before* scheduling any server-side world scan. Bound the requested
// center against the peer reference, then scan the SAME center the client tracks.
// Replacing it with a lagging peer position makes valid edge results fail client
// validation. Both positions are client-influenced in Valheim: this is NOT a
// tamper-proof location boundary; opt-in, admission limits and world bounds remain.
internal static class GeneratedPinRequestPolicy
{
    internal const float MaximumPeerOffset = 128f;
    internal const int MaximumConcurrentRequests = 2;
    internal const float MinimumSecondsBetweenRequests = 15f;

    internal static bool TryValidate(int requestId, Vector3 claimedCenter, Vector3 peerPosition,
        float requestedRadius, out Vector3 scanCenter)
    {
        scanCenter = claimedCenter;
        if (requestId <= 0 || !IsFinite(requestedRadius) || requestedRadius <= 0f ||
            requestedRadius > GeneratedZoneScanner.MaximumRadius ||
            !GeneratedZoneScanner.ValidCenter(claimedCenter) ||
            !GeneratedZoneScanner.ValidCenter(peerPosition))
            return false;

        float dx = claimedCenter.x - peerPosition.x;
        float dz = claimedCenter.z - peerPosition.z;
        return dx * dx + dz * dz <= MaximumPeerOffset * MaximumPeerOffset;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
