using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoMapPins.Discovery;

// An upper bound on both the outgoing batch and the *received* payload. Decode
// validates the count before allocating/reading any candidate entry.
internal static class GeneratedPinWireProtocol
{
    internal const int ProtocolVersion = 1;
    internal const int MaximumBatchSize = 32;
    internal const int MaximumPacketBytes = 8192;
    internal const int MaximumPrefabNameLength = 128;

    internal readonly struct Entry
    {
        internal readonly ZDOID Id;
        internal readonly string Prefab;
        internal readonly Vector3 Position;

        internal Entry(ZDOID id, string prefab, Vector3 position)
        {
            Id = id;
            Prefab = prefab;
            Position = position;
        }
    }

    internal static ZPackage Encode(int requestId, int sequence, bool final, bool truncated,
        IReadOnlyList<Entry> entries)
    {
        if (requestId <= 0 || sequence < 0 || entries.Count > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(entries));
        var packet = new ZPackage();
        packet.Write(ProtocolVersion);
        packet.Write(requestId);
        packet.Write(sequence);
        packet.Write(final);
        packet.Write(truncated);
        packet.Write(entries.Count);
        foreach (Entry entry in entries)
        {
            if (!Valid(entry)) throw new ArgumentException("Invalid remote pin entry", nameof(entries));
            packet.Write(entry.Id);
            packet.Write(entry.Prefab);
            packet.Write(entry.Position);
        }
        if (packet.Size() > MaximumPacketBytes)
            throw new ArgumentException("Remote pin packet exceeds maximum size", nameof(entries));
        return packet;
    }

    internal static bool TryDecode(ZPackage? packet, out int requestId, out int sequence,
        out bool final, out bool truncated, out List<Entry> entries)
    {
        requestId = 0;
        sequence = 0;
        final = false;
        truncated = false;
        entries = new List<Entry>();
        if (packet == null || packet.Size() > MaximumPacketBytes) return false;

        try
        {
            packet.SetPos(0);
            if (packet.ReadInt() != ProtocolVersion) return false;
            requestId = packet.ReadInt();
            sequence = packet.ReadInt();
            final = packet.ReadBool();
            truncated = packet.ReadBool();
            int count = packet.ReadInt();
            if (requestId <= 0 || sequence < 0 || count < 0 || count > MaximumBatchSize ||
                (truncated && !final)) return false;
            for (int i = 0; i < count; i++)
            {
                var entry = new Entry(packet.ReadZDOID(), packet.ReadString(), packet.ReadVector3());
                if (!Valid(entry)) return false;
                entries.Add(entry);
            }
            return packet.GetPos() == packet.Size();
        }
        catch (Exception)
        {
            entries.Clear();
            return false;
        }
    }

    private static bool Valid(Entry entry) =>
        !String.IsNullOrWhiteSpace(entry.Prefab) && entry.Prefab.Length <= MaximumPrefabNameLength &&
        Finite(entry.Position.x) && Finite(entry.Position.y) && Finite(entry.Position.z);

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
