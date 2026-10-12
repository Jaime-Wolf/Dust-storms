using HarmonyLib;
using UnityEngine;

namespace ApocaDustStorm
{
    // Observe native translations, without moving the player, vehicles or world.
    internal static class StormOriginObserver
    {
        internal struct Snapshot { internal Transform Anchor; internal Vector3 Position; }
        internal static void Before(out Snapshot state)
        {
            Transform anchor = StormRunner.CanRender && (StormRunner.Model.Active || StormRunner.Strength > 0.001f) ? StormRunner.OriginAnchor : null;
            state = new Snapshot { Anchor = anchor, Position = anchor != null ? anchor.position : Vector3.zero };
        }
        internal static void After(Snapshot state)
        {
            if (state.Anchor == null || state.Anchor != StormRunner.OriginAnchor) return;
            Vector3 delta = state.Anchor.position - state.Position;
            if (delta.sqrMagnitude <= 0.000001f || !Finite(delta.x) || !Finite(delta.y) || !Finite(delta.z)) return;
            StormRunner.ShiftOrigin(state.Anchor, delta);
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
    [HarmonyPatch(typeof(FloatingOrigin), "LateUpdate")]
    internal static class StormOriginPatch
    {
        private static void Prefix(out StormOriginObserver.Snapshot __state) { StormOriginObserver.Before(out __state); }
        private static void Postfix(StormOriginObserver.Snapshot __state) { StormOriginObserver.After(__state); }
    }
    [HarmonyPatch(typeof(FloatingOriginAdvanced), "LateUpdate")]
    internal static class StormAdvancedOriginPatch
    {
        private static void Prefix(out StormOriginObserver.Snapshot __state) { StormOriginObserver.Before(out __state); }
        private static void Postfix(StormOriginObserver.Snapshot __state) { StormOriginObserver.After(__state); }
    }
    [HarmonyPatch(typeof(FloatingOriginNew), "LateUpdate")]
    internal static class StormNewOriginPatch
    {
        private static void Prefix(out StormOriginObserver.Snapshot __state) { StormOriginObserver.Before(out __state); }
        private static void Postfix(StormOriginObserver.Snapshot __state) { StormOriginObserver.After(__state); }
    }
}
