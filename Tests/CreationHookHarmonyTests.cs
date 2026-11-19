using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

internal static class CreationHookHarmonyTests
{
    private static int checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
        Console.WriteLine("PASS " + label);
    }

    public static void Main()
    {
        var harmony = new Harmony("amp.audit.creation." + Guid.NewGuid().ToString("N"));
        try
        {
            harmony.PatchAll(typeof(CreationHookHarmonyTests).Assembly);
            var targets = new (Type type, string method)[] {
                (typeof(Destructible), "Start"), (typeof(TreeBase), "Awake"),
                (typeof(Beehive), "Awake"), (typeof(SpawnOnDamaged), "Start"),
                (typeof(ResourceRoot), "Awake"), (typeof(MineRock), "Start"),
                (typeof(MineRock5), "Awake"), (typeof(Location), "Awake"),
                (typeof(Leviathan), "Awake"), (typeof(TeleportWorld), "Awake"),
                (typeof(PickableItem), "Awake"), (typeof(Container), "Awake") };
            foreach (var target in targets)
            {
                var go = new GameObject();
                var component = go.Add(target.type);
                var method = target.type.GetMethod(target.method)!;
                var info = Harmony.GetPatchInfo(method);
                Check(info != null && info.Owners.Contains(harmony.Id), "registered " + target.type.Name);
                method.Invoke(component, null);
                Check(go.PinAdds == 1 && component.Calls == 1, "original and postfix " + target.type.Name);
                method.Invoke(component, null);
                Check(go.PinAdds == 1 && component.Calls == 2, "no duplicate " + target.type.Name);
            }
            foreach (float height in new[] { 99f, 100f, 101f, float.NaN, float.PositiveInfinity })
            {
                var go = new GameObject();
                go.transform.position = new Vector3 { y = height };
                var tree = (TreeBase)go.Add(typeof(TreeBase));
                tree.Awake();
                Check(go.PinAdds == (height < 100f ? 1 : 0), "strict height " + height);
            }
            var overlap = new GameObject();
            var treeOverlap = (TreeBase)overlap.Add(typeof(TreeBase));
            var destructibleOverlap = (Destructible)overlap.Add(typeof(Destructible));
            treeOverlap.Awake(); destructibleOverlap.Start();
            Check(overlap.PinAdds == 1, "cross-hook duplicate suppression");

            var pickable = new GameObject(); pickable.Add(typeof(Pickable));
            ((Destructible)pickable.Add(typeof(Destructible))).Start();
            Check(pickable.PinAdds == 0, "Destructible defers to Pickable");

            var built = new GameObject(); built.Add(typeof(Piece));
            ((Beehive)built.Add(typeof(Beehive))).Awake();
            Check(built.PinAdds == 0, "Beehive hook excludes Piece");

            harmony.UnpatchSelf();
            var control = new GameObject();
            ((TreeBase)control.Add(typeof(TreeBase))).Awake();
            Check(control.PinAdds == 0, "unpatched negative control");
            Console.WriteLine("PASS total=" + checks + " (CLR/net48; Unity components are stubs)");
        }
        finally { harmony.UnpatchSelf(); }
    }
}

namespace JetBrains.Annotations { [AttributeUsage(AttributeTargets.All)] public sealed class UsedImplicitlyAttribute : Attribute { } }
namespace AutoMapPins
{
    internal static class AutoMapPinsPlugin { internal static readonly Setting MaxDetectionHeight = new Setting(); }
    internal sealed class Setting { internal float Value = 100f; }
}
namespace AutoMapPins.Model { public sealed class PinComponent : Component { } }
namespace UnityEngine
{
    public struct Vector3 { public float y; }
    public sealed class Transform { public Vector3 position; }
    public class Component
    {
        public GameObject gameObject = null!;
        public int Calls;
        public bool TryGetComponent<T>(out T found) where T : Component => gameObject.TryGetComponent(out found);
    }
    public sealed class GameObject
    {
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public readonly Transform transform = new Transform();
        public int PinAdds;
        public Component Add(Type type)
        {
            var component = (Component)Activator.CreateInstance(type)!;
            component.gameObject = this;
            components.Add(type, component);
            if (type == typeof(AutoMapPins.Model.PinComponent)) PinAdds++;
            return component;
        }
        public T AddComponent<T>() where T : Component, new() => (T)Add(typeof(T));
        public bool TryGetComponent<T>(out T found) where T : Component
        {
            bool exists = components.TryGetValue(typeof(T), out var component);
            found = (T)component!;
            return exists;
        }
    }
}
public class Destructible : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Start() { Calls++; } }
public class TreeBase : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class Beehive : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class SpawnOnDamaged : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Start() { Calls++; } }
public class ResourceRoot : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class MineRock : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Start() { Calls++; } }
public class MineRock5 : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class Location : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class Leviathan : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class TeleportWorld : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class PickableItem : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class Container : Component { [MethodImpl(MethodImplOptions.NoInlining)] public void Awake() { Calls++; } }
public class Pickable : Component { }
public class Piece : Component { }
