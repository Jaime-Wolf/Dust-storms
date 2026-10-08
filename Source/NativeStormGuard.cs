using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ApocaDustStorm
{
    internal static class NativeStormGuard
    {
        private static readonly HashSet<GameObject> roots = new HashSet<GameObject>();
        private static readonly List<Behaviour> behaviours = new List<Behaviour>();
        private static readonly List<Renderer> renderers = new List<Renderer>();
        private static readonly List<Collider> colliders = new List<Collider>();
        private static PlayMakerFSM disableFsm;
        internal static Material NativeDustMaterial;
        internal static Transform Root(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                // Spawn-point names also contain Sandstorm. Require the actual prefab's FSM marker.
                if (!t.name.StartsWith("SandStorm", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (PlayMakerFSM fsm in t.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "SandPlayer") return t;
            }
            return null;
        }
        internal static bool IsStorm(Transform t) { return Plugin.Active && Root(t) != null; }
        internal static bool Hazard(PlayMakerFSM fsm)
        {
            if (fsm == null || !Plugin.Active) return false;
            string name = fsm.FsmName;
            return (name == "Tornado" || name == "TornadoDamage" || name == "SandPlayer" || name == "Move") && Root(fsm.transform) != null;
        }
        internal static void Suppress(Transform t)
        {
            if (!Plugin.Active || t == null) return;
            GameObject root = t.gameObject;
            if (!roots.Add(root)) return;
            foreach (ParticleSystemRenderer r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (NativeDustMaterial == null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex")) NativeDustMaterial = r.sharedMaterial;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                if (c.enabled) { colliders.Add(c); c.enabled = false; }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.enabled) { renderers.Add(r); r.enabled = false; }
            foreach (PlayMakerFSM fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                if (Hazard(fsm) && fsm.enabled) { behaviours.Add(fsm); fsm.enabled = false; }
            foreach (Tornado tornado in root.GetComponentsInChildren<Tornado>(true))
                if (tornado.enabled) { behaviours.Add(tornado); tornado.enabled = false; }
            foreach (AudioSource sound in root.GetComponentsInChildren<AudioSource>(true))
                if (sound.enabled) { behaviours.Add(sound); sound.enabled = false; }
            Plugin.Log.LogInfo("Suppressed native storm visuals, colliders, force and damage FSMs: " + root.name);
        }
        internal static void Scan()
        {
            foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (fsm.FsmName == "DisableSandstorm" && fsm.gameObject.name == "__GameManager__") disableFsm = fsm;
                if (Hazard(fsm)) Suppress(Root(fsm.transform));
            }
        }
        internal static bool StormsDisabled() { return disableFsm != null && disableFsm.enabled; }
        internal static void Restore()
        {
            foreach (Collider c in colliders) if (c != null && !c.enabled) c.enabled = true;
            foreach (Renderer r in renderers) if (r != null && !r.enabled) r.enabled = true;
            foreach (Behaviour b in behaviours) if (b != null && !b.enabled) b.enabled = true;
            behaviours.Clear(); renderers.Clear(); colliders.Clear(); roots.Clear(); NativeDustMaterial = null;
        }
        internal static void ResetScene()
        {
            // Keep surviving/persistent storms suppressed across additive loads.
            // Re-enabling them here would briefly expose the old tornado visuals.
            if (!Plugin.Active) Restore();
            else
            {
                behaviours.RemoveAll(b => b == null); renderers.RemoveAll(r => r == null);
                colliders.RemoveAll(c => c == null); roots.RemoveWhere(r => r == null);
            }
            disableFsm = null;
        }
    }
    [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")]
    internal static class StormEnablePatch
    {
        private static bool Prefix(PlayMakerFSM __instance)
        {
            if (!NativeStormGuard.Hazard(__instance)) return true;
            NativeStormGuard.Suppress(NativeStormGuard.Root(__instance.transform));
            return false;
        }
    }
    // These early guards also cover restored storms before the first scene scan.
    // Ordinary explosions and Bodypart damage outside the storm remain native.
    [HarmonyPatch(typeof(Explosion), "DoExplosion")]
    internal static class StormForcePatch
    { private static bool Prefix(Explosion __instance) { return __instance.Owner == null || !NativeStormGuard.IsStorm(__instance.Owner.transform); } }
    [HarmonyPatch(typeof(Tornado), "OnTriggerStay")]
    internal static class StormTornadoPatch
    { private static bool Prefix(Tornado __instance) { return !NativeStormGuard.IsStorm(__instance.transform); } }
    [HarmonyPatch(typeof(SendEvent), "OnEnter")]
    internal static class StormDamageEnterPatch
    {
        private static bool Prefix(SendEvent __instance)
        {
            if (__instance.Fsm.Name != "TornadoDamage" || __instance.Owner == null || !NativeStormGuard.IsStorm(__instance.Owner.transform)) return true;
            __instance.Finish(); return false;
        }
    }
    [HarmonyPatch(typeof(SendEvent), "OnUpdate")]
    internal static class StormDamageUpdatePatch
    {
        private static bool Prefix(SendEvent __instance)
        { return __instance.Fsm.Name != "TornadoDamage" || __instance.Owner == null || !NativeStormGuard.IsStorm(__instance.Owner.transform); }
    }
}
