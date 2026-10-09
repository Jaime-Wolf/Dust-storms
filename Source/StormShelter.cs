using System;
using System.Collections.Generic;
using UnityEngine;
using NWH.VehiclePhysics2;
namespace ApocaDustStorm
{
    // Geometry checks, not trigger events: shelters need no mod-specific component.
    internal static class StormShelter
    {
        private static readonly RaycastHit[] hits = new RaycastHit[32];
        private static readonly Vector3[] sides = { Vector3.forward, new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(-1, 0, 0) };
        private sealed class Structure { internal Transform Parent; internal bool Solid, Known; internal float Until; }
        private static readonly Dictionary<Collider, Structure> structures = new Dictionary<Collider, Structure>();
        internal static void Reset() { structures.Clear(); }
        internal static bool Descendant(Transform child, Transform ancestor)
        { for (; child != null; child = child.parent) if (child == ancestor) return true; return false; }
        private static bool Contains(string name, string word) { return name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static bool Structural(Collider collider, Transform actor, out bool known)
        {
            known = false;
            if (collider == null || !collider.enabled || collider.isTrigger || Descendant(collider.transform, actor)) return false;
            if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic) return false;
            Structure cached;
            if (structures.TryGetValue(collider, out cached) && cached.Parent == collider.transform.parent && Time.unscaledTime < cached.Until)
            { known = cached.Known; return cached.Solid; }
            bool solid = Classify(collider.transform, actor, out known);
            // Bound references and periodically reclassify movable/modded hierarchies.
            if (structures.Count >= 2048) structures.Clear();
            if (cached == null) cached = new Structure();
            cached.Parent = collider.transform.parent; cached.Solid = solid; cached.Known = known; cached.Until = Time.unscaledTime + 5;
            structures[collider] = cached;
            return solid;
        }
        private static bool Classify(Transform root, Transform actor, out bool known)
        {
            known = false;
            bool boatHull = false;
            for (Transform t = root; t != null; t = t.parent)
            {
                string name = t.name;
                // Native boat hulls use shipwreck_* beneath Wreck_* POIs.
                // Exempt only hull geometry and its POI wrapper, not the car
                // wrecks, loose props or vegetation placed at the same site.
                bool hullPart = name.StartsWith("shipwreck_", StringComparison.OrdinalIgnoreCase);
                if (hullPart) boatHull = true;
                if (t == actor || t.GetComponent<VehicleController>() != null || t.Find("DriveTrigger") != null ||
                    (Contains(name, "wreck") && !hullPart && !(boatHull && name.StartsWith("Wreck_", StringComparison.OrdinalIgnoreCase))) ||
                    Contains(name, "tree") || Contains(name, "grass") ||
                    Contains(name, "bush") || Contains(name, "twig") || Contains(name, "branch") || Contains(name, "leaf")) return false;
                if (hullPart || Contains(name, "cave") || Contains(name, "building") || Contains(name, "container") ||
                    Contains(name, "conex") || Contains(name, "house") || Contains(name, "garage") || Contains(name, "slum")) known = true;
            }
            return true;
        }
        private static bool Covered(Vector3 origin, Vector3 direction, float distance, Transform actor, out bool known)
        {
            known = false; float nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            // The caller treats an overfull query as no shelter, rather than guessing from truncated geometry.
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                bool structure;
                if (hits[i].distance < 0.05f || hits[i].distance >= nearest || !Structural(hits[i].collider, actor, out structure)) continue;
                nearest = hits[i].distance; known = structure;
            }
            return !float.IsInfinity(nearest);
        }
        internal static bool ContainsActor(GameObject actor)
        {
            if (actor == null) return false;
            Vector3 origin = actor.transform.position + Vector3.up * 0.6f;
            bool known;
            bool roof = Covered(origin, Vector3.up, 45, actor.transform, out known);
            // Native caves, buildings, conex and boat hulls identify their own structure.
            if (roof && known) return true;
            // Also support unnamed/modded structures when a roof and two walls enclose the actor.
            int walls = 0, knownWalls = 0;
            foreach (Vector3 side in sides)
                if (Covered(origin, side, 20, actor.transform, out known))
                { walls++; if (known) knownWalls++; if ((roof && walls >= 2) || knownWalls >= 3) return true; }
            return false;
        }
    }
}
