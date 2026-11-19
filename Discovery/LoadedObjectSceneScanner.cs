using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoMapPins.Discovery;

// Diagnostic over already-loaded Unity scene objects. Never generate or visit world sectors.
internal static class LoadedObjectSceneScanner
{
    internal const int MaximumNearbyCandidates = 20000;

    internal static LoadedObjectScanSummary Scan(IEnumerable<GameObject> objects, Vector3 center,
        Transform playerTransform, float radius, bool includeChildren,
        Func<string, string> parseId, Func<string, bool> isConfigured)
    {
        var summary = new LoadedObjectScanSummary();
        foreach (GameObject obj in objects)
        {
            if (!obj || obj.transform == playerTransform || obj.transform.IsChildOf(playerTransform) ||
                obj.transform is RectTransform) continue;
            float distance = Vector3.Distance(center, obj.transform.position);
            if (Single.IsNaN(distance) || distance > radius) continue;
            if (!includeChildren && obj.transform.parent != null && !obj.GetComponent<ZNetView>()) continue;
            // Count nearby eligible candidates, not far away objects, players or UI.
            if (++summary.ExaminedObjects > MaximumNearbyCandidates)
            {
                summary.Capped = true;
                break;
            }
            string id = parseId(obj.name);
            summary.Add(id, distance, isConfigured(id));
        }
        return summary;
    }
}
