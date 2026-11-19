// World-only stubs for real Harmony execution on .NET Framework 4.8.
// The sharing hook implementation is extracted verbatim from production at test time.
using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapPins.Model;

public class Minimap
{
    public enum PinType { None, User }
    public class PinData
    {
        public PinType m_type;
        public string m_author;
        public long m_ownerID;
        public bool m_save;
    }
    public static Minimap instance;
    public List<PinData> m_pins = new List<PinData>();
    public int ShareDepth;
    public bool FailInner, FailOuter, CatchInner;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public int AuditShare()
    {
        if (m_pins.Any(p => p is MapPin || p.m_author == "amp"))
            throw new Exception("AMP pin or serialized metadata leaked into sharing");
        if (ShareDepth == 0)
        {
            ShareDepth++;
            try { AuditShare(); }
            catch (InvalidOperationException) { if (!CatchInner) throw; }
            finally { ShareDepth--; }
            if (FailOuter) throw new InvalidOperationException("outer");
        }
        else if (FailInner) throw new InvalidOperationException("inner");
        return m_pins.Count;
    }
}
namespace UnityEngine
{
    public struct Vector3 { public Vector3(float x, float y, float z) { } }
}
namespace AutoMapPins.Data
{
    public class Config
    {
        public class Pin { public string Name; public bool IsActive, IsPermanent; }
    }
}
namespace AutoMapPins.Model
{
    public class PositionedObject { public PositionedObject(string name, UnityEngine.Vector3 position) { } }
    public class MapPin : Minimap.PinData
    {
        public MapPin(PositionedObject position, AutoMapPins.Data.Config.Pin config)
        { m_author = "amp"; m_save = config.IsPermanent; }
    }
}
namespace JetBrains.Annotations
{
    public class UsedImplicitlyAttribute : Attribute { }
}
