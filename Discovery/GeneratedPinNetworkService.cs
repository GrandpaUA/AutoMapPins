using System;
using System.Collections;
using System.Collections.Generic;
using AutoMapPins.Model;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Dedicated-server test transport. Per-connection ZRpc callbacks identify the
// actual socket; vanilla ZRoutedRpc sender UIDs are packet-controlled and must
// not be used to authenticate scan requests or results.
internal sealed class GeneratedPinNetworkService
{
    private const string Hello = "AMP.GZP.Hello.1";
    private const string HelloAck = "AMP.GZP.HelloAck.1";
    private const string Request = "AMP.GZP.Request.1";
    private const string Response = "AMP.GZP.Response.1";
    private const float RequestInterval = 20f;
    private const float RequestTimeout = 45f;
    private const float HelloRetryInterval = 10f;
    private const int MaximumHelloAttempts = 3;

    private readonly MonoBehaviour _owner;
    private readonly GeneratedPinRequestLimiter _limiter = new();
    private readonly GeneratedPinResponseTracker _tracker = new();
    private readonly Dictionary<long, Coroutine> _serverScans = new();
    private readonly HashSet<ZRpc> _registeredConnections = new();
    private readonly HashSet<PositionedObject> _seen = new();
    private ZNet? _world;
    private ZNetPeer? _serverPeer;
    private ZRpc? _clientRpc;
    private GeneratedPinService? _pins;
    private int _helloAttempts;
    private float _nextHelloAt;
    private bool _supported;
    private bool _waiting;
    private float _nextRequestAt;
    private float _requestDeadline;
    private int _requestId;
    private float _nextPeerCleanupAt;

    internal GeneratedPinNetworkService(MonoBehaviour owner) => _owner = owner;

    internal void Tick(GeneratedPinService? pins)
    {
        ZNet? world = ZNet.instance;
        if (!world)
        {
            Reset();
            return;
        }
        if (world != _world)
        {
            Reset();
            _world = world;
        }
        if (world.IsServer())
        {
            foreach (ZNetPeer peer in world.GetPeers())
            {
                if (!peer.IsReady() || peer.m_rpc == null || !_registeredConnections.Add(peer.m_rpc)) continue;
                peer.m_rpc.Register<int>(Hello, OnHello);
                peer.m_rpc.Register<ZPackage>(Request, OnRequest);
            }
            if (Time.time >= _nextPeerCleanupAt)
            {
                _nextPeerCleanupAt = Time.time + 60f;
                var connected = new HashSet<long>();
                var connections = new HashSet<ZRpc>();
                foreach (ZNetPeer peer in world.GetPeers())
                {
                    connected.Add(peer.m_uid);
                    if (peer.m_rpc != null) connections.Add(peer.m_rpc);
                }
                _limiter.Prune(connected);
                _registeredConnections.IntersectWith(connections);
            }
            return;
        }

        ZNetPeer? server = world.GetServerPeer();
        if (!AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value || !Player.m_localPlayer ||
            !Minimap.instance || pins == null || server == null || !server.IsReady() ||
            server.m_rpc == null || !server.m_rpc.IsConnected())
        {
            ResetClient(pins);
            return;
        }
        if (_serverPeer != server || _clientRpc != server.m_rpc)
        {
            ResetClient(pins);
            _serverPeer = server;
            _clientRpc = server.m_rpc;
            _clientRpc.Register<int>(HelloAck, OnHelloAck);
            _clientRpc.Register<ZPackage>(Response, OnResponse);
        }
        _pins = pins;
        if (!_supported && _helloAttempts < MaximumHelloAttempts && Time.time >= _nextHelloAt)
        {
            _helloAttempts++;
            _nextHelloAt = Time.time + HelloRetryInterval;
            _clientRpc.Invoke(Hello, GeneratedPinWireProtocol.ProtocolVersion);
            if (_helloAttempts == MaximumHelloAttempts)
                AutoMapPinsPlugin.Log.LogWarning("generated-zone server capability not yet confirmed; no scan until acknowledged");
        }
        if (!_supported) return;
        if (_waiting && Time.time > _requestDeadline)
        {
            _tracker.Reset();
            _waiting = false;
            AutoMapPinsPlugin.Log.LogWarning("generated-zone server scan timed out; preserving existing pins");
        }
        if (_waiting || Time.time < _nextRequestAt ||
            !DiscoveryRadiusService.TryGetCurrentRadius(out float radius)) return;
        radius = Math.Min(radius, GeneratedZoneScanner.MaximumRadius);
        Vector3 center = Player.m_localPlayer.transform.position;
        if (!GeneratedZoneScanner.ValidCenter(center)) return;
        _requestId = _requestId == int.MaxValue ? 1 : _requestId + 1;
        _tracker.Begin(_requestId, center, radius);
        _seen.Clear();
        _waiting = true;
        _requestDeadline = Time.time + RequestTimeout;
        _nextRequestAt = Time.time + RequestInterval;
        var packet = new ZPackage();
        packet.Write(GeneratedPinWireProtocol.ProtocolVersion);
        packet.Write(_requestId);
        packet.Write(center);
        packet.Write(radius);
        _clientRpc.Invoke(Request, packet);
    }

    private ZNetPeer? ConnectedPeer(ZRpc rpc)
    {
        ZNet? world = ZNet.instance;
        if (!world || world != _world || !world.IsServer()) return null;
        foreach (ZNetPeer peer in world.GetPeers())
            if (peer.m_rpc == rpc && peer.IsReady() && rpc.IsConnected()) return peer;
        return null;
    }

    private void OnHello(ZRpc rpc, int version)
    {
        if (!AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value ||
            version != GeneratedPinWireProtocol.ProtocolVersion || ConnectedPeer(rpc) == null) return;
        rpc.Invoke(HelloAck, GeneratedPinWireProtocol.ProtocolVersion);
    }

    private void OnHelloAck(ZRpc rpc, int version)
    {
        if (ZNet.instance != _world || ZNet.instance == null || ZNet.instance.IsServer() ||
            _serverPeer == null || rpc != _clientRpc || rpc != _serverPeer.m_rpc ||
            _helloAttempts == 0 || version != GeneratedPinWireProtocol.ProtocolVersion) return;
        if (!_supported)
            AutoMapPinsPlugin.Log.LogInfo("generated-zone dedicated server supports remote pin scans");
        _supported = true;
    }

    private void OnRequest(ZRpc rpc, ZPackage packet)
    {
        ZNet? world = ZNet.instance;
        ZNetPeer? peer = ConnectedPeer(rpc);
        if (world == null || peer == null || !AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value ||
            packet == null || packet.Size() > 64) return;
        int id;
        Vector3 claimed;
        float radius;
        try
        {
            packet.SetPos(0);
            if (packet.ReadInt() != GeneratedPinWireProtocol.ProtocolVersion) return;
            id = packet.ReadInt();
            claimed = packet.ReadVector3();
            radius = packet.ReadSingle();
            if (packet.GetPos() != packet.Size()) return;
        }
        catch (Exception) { return; }
        if (!GeneratedPinRequestPolicy.TryValidate(id, claimed, peer.GetRefPos(), radius, out Vector3 center) ||
            !_limiter.TryBegin(peer.m_uid, Time.time)) return;
        // ServerScan starts with a yield so the handle exists before cleanup.
        _serverScans[peer.m_uid] = _owner.StartCoroutine(ServerScan(world, peer, id, center, radius));
    }

    private IEnumerator ServerScan(ZNet world, ZNetPeer peer, int id, Vector3 center, float radius)
    {
        long peerId = peer.m_uid;
        yield return null;
        int sequence = 0;
        var batch = new List<GeneratedPinWireProtocol.Entry>(GeneratedPinWireProtocol.MaximumBatchSize);
        GeneratedZoneScanner.ScanStats? stats = null;
        int sent = 0;
        try
        {
            IEnumerator scan = GeneratedZoneScanner.Scan(center, radius, candidate =>
            {
                batch.Add(new GeneratedPinWireProtocol.Entry(candidate.Id, candidate.Name, candidate.Position));
                if (batch.Count < GeneratedPinWireProtocol.MaximumBatchSize) return;
                if (SendBatch(world, peer, id, sequence++, final: false, truncated: false, batch))
                    sent += batch.Count;
                batch.Clear();
            }, done => stats = done);
            while (scan.MoveNext())
            {
                if (world != ZNet.instance || !AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value ||
                    world.GetPeer(peerId) != peer || !peer.IsReady() || !peer.m_rpc.IsConnected()) yield break;
                yield return scan.Current;
            }
            if (world == ZNet.instance && AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value &&
                world.GetPeer(peerId) == peer && peer.IsReady())
            {
                SendBatch(world, peer, id, sequence, final: true, truncated: stats?.Truncated ?? true, batch);
                AutoMapPinsPlugin.Log.LogInfo($"remote generated-zone scan peer {peerId}: " +
                    $"sectors {stats?.Sectors ?? 0}, examined {stats?.Examined ?? 0}, " +
                    $"candidates {stats?.Candidates ?? 0}, sent {sent + batch.Count}, " +
                    $"truncated {stats?.Truncated ?? true}");
            }
        }
        finally
        {
            _serverScans.Remove(peerId);
            if (world.GetPeer(peerId) == peer) _limiter.End(peerId);
            else _limiter.Forget(peerId);
        }
    }

    private static bool SendBatch(ZNet world, ZNetPeer peer, int id, int sequence, bool final,
        bool truncated, List<GeneratedPinWireProtocol.Entry> batch)
    {
        if (world != ZNet.instance || world.GetPeer(peer.m_uid) != peer ||
            !peer.IsReady() || !peer.m_rpc.IsConnected()) return false;
        peer.m_rpc.Invoke(Response, GeneratedPinWireProtocol.Encode(id, sequence, final, truncated, batch));
        return true;
    }

    private void OnResponse(ZRpc rpc, ZPackage packet)
    {
        if (!_waiting || ZNet.instance != _world || ZNet.instance == null || ZNet.instance.IsServer() ||
            _serverPeer == null || rpc != _clientRpc || rpc != _serverPeer.m_rpc || _pins == null ||
            !AutoMapPinsPlugin.DiscoverGeneratedZonePins.Value ||
            !GeneratedPinWireProtocol.TryDecode(packet, out int id, out int sequence,
                out bool final, out bool truncated, out var entries) ||
            !_tracker.TryAccept(id, sequence, final, truncated, entries, out bool complete)) return;
        foreach (var entry in entries) _pins.ApplyRemoteCandidate(entry, _seen);
        if (!final) return;
        if (complete) _pins.CommitRemoteSnapshot(_seen);
        _waiting = false;
        AutoMapPinsPlugin.Log.LogInfo($"remote generated-zone scan complete: items {_seen.Count}, " +
            $"truncated {truncated}");
    }

    private void ResetClient(GeneratedPinService? pins)
    {
        if (_serverPeer != null) pins?.Reset();
        _tracker.Reset();
        _seen.Clear();
        _serverPeer = null;
        _clientRpc = null;
        _pins = null;
        _helloAttempts = 0;
        _nextHelloAt = 0f;
        _supported = false;
        _waiting = false;
        _nextRequestAt = 0f;
    }

    internal void Reset()
    {
        ResetClient(_pins);
        foreach (Coroutine scan in new List<Coroutine>(_serverScans.Values)) _owner.StopCoroutine(scan);
        _serverScans.Clear();
        _limiter.Reset();
        _registeredConnections.Clear();
        _nextPeerCleanupAt = 0f;
        _world = null;
    }
}
