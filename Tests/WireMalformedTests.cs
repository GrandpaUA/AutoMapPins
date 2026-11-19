using System;
using AutoMapPins.Discovery;
using UnityEngine;
internal static class WireMalformedTests
{
    private static int count;
    private static ZPackage Header(int version=1, int id=1, int sequence=0, bool final=true, bool truncated=false, int entries=0)
    {
        var p = new ZPackage(); p.Write(version); p.Write(id); p.Write(sequence);
        p.Write(final); p.Write(truncated); p.Write(entries); return p;
    }
    private static void Reject(string name, ZPackage p)
    {
        if (GeneratedPinWireProtocol.TryDecode(p, out _, out _, out _, out _, out _))
            throw new Exception("Accepted malformed packet: " + name);
        count++; Console.WriteLine("PASS " + name);
    }
    private static ZPackage Entry(string name, Vector3 pos)
    {
        var p = Header(entries:1); p.Write(new ZDOID()); p.Write(name); p.Write(pos); return p;
    }
    public static int Main()
    {
        Reject("empty", new ZPackage());
        Reject("unknown version", Header(version:2));
        Reject("zero request", Header(id:0));
        Reject("negative request", Header(id:-1));
        Reject("negative sequence", Header(sequence:-1));
        Reject("negative count", Header(entries:-1));
        Reject("count 33", Header(entries:33));
        Reject("truncated nonfinal", Header(final:false, truncated:true));
        Reject("missing entry", Header(entries:1));
        var trailing = Header(); trailing.Write(42); Reject("trailing bytes", trailing);
        Reject("blank prefab", Entry(" ", new Vector3()));
        Reject("prefab length129", Entry(new string('a',129), new Vector3()));
        Reject("NaN y", Entry("Tree", new Vector3(0,float.NaN,0)));
        Reject("infinite z", Entry("Tree", new Vector3(0,0,float.PositiveInfinity)));
        var valid = Entry(new string('a',128), new Vector3(0,0,0));
        if (!GeneratedPinWireProtocol.TryDecode(valid, out _, out _, out _, out _, out var entries) || entries.Count != 1)
            throw new Exception("Boundary-valid packet rejected");
        Console.WriteLine("PASS prefab length128 positive control");
        Console.WriteLine("SUMMARY " + count + " malformed rejections + 1 positive control");
        return 0;
    }
}
