using System;
using UnityEngine;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
namespace ApocaDustStorm
{
    internal static class WindblownLizards
    {
        private static readonly WindLootSchedule schedule = new WindLootSchedule();
        private static readonly System.Random random = new System.Random();
        private static GameObject prefab;
        private static bool pending, warned;
        private static Vector3 wind;
        private static int lookupAttempts;
        private static float nextLookup;
        internal static void CapturePrefab(GameObject candidate)
        { if (prefab == null && NativeFoodPrefab(candidate)) prefab = candidate; }
        private static void PreparePrefab()
        {
            if (prefab != null || lookupAttempts >= 3 || Time.unscaledTime < nextLookup) return;
            lookupAttempts++; nextLookup = Time.unscaledTime + 30;
            // Search the actual GameObject asset, not just initialized FSM components.
            // This also finds inactive food assets referenced by native creature death.
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            { CapturePrefab(candidate); if (prefab != null) break; }
            if (prefab != null) Plugin.Log.LogInfo("Windblown lizard source ready: native Lizard_Dead food prefab.");
        }
        internal static bool NativeFoodPrefab(GameObject candidate)
        {
            if (candidate == null || candidate.name != "Lizard_Dead" || candidate.scene.IsValid() || candidate.GetComponent<Rigidbody>() == null) return false;
            bool food = false, id = false, itemName = false;
            foreach (PlayMakerFSM fsm in candidate.GetComponents<PlayMakerFSM>())
            { if (fsm.FsmName == "Food") food = true; if (fsm.FsmName == "ID") id = true; if (fsm.FsmName == "ItemName") itemName = true; }
            return food && id && itemName;
        }
        internal static void Tick(float dt, float strength, StormModel storm, bool skipped)
        {
            if (!Plugin.Active || Plugin.WindblownLizards == null || !Plugin.WindblownLizards.Value || skipped || !storm.Active)
            { pending = false; return; }
            if (strength >= 0.20f) PreparePrefab();
            wind = new Vector3((float)storm.WindX, 0, (float)storm.WindZ);
            // A storm timer, not an outdoor player-exposure timer. Defer placement
            // in proper shelter, with at most one pending item and two per storm.
            if (pending || !schedule.Tick(dt, strength >= 0.20f, random.NextDouble)) return;
            pending = true;
        }
        internal static void PreCull(Camera view)
        {
            if (!pending || !StormRunner.CanRender || view != StormRunner.View) return;
            if (Plugin.WindblownLizards == null || !Plugin.WindblownLizards.Value || Time.timeScale <= 0 || Apocasetter.GameMenu.Paused)
            { pending = false; return; }
            if (PlayerStormHazards.Sheltered) return;
            pending = false;
            if (prefab == null)
            {
                if (!warned) { warned = true; Plugin.Log.LogWarning("Native raw-lizard food prefab is not loaded; skipping windblown lizard instead of cloning a live NPC or existing loot."); }
                return;
            }
            DustLightning.AlignView(view);
            Ray eye = DustLightning.RenderRay(view, 0.5f, 0.5f);
            Vector3 forward = eye.direction; forward.y = 0; forward = forward.normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 side = new Vector3(wind.z, 0, -wind.x);
            // The old 9m upwind offset could put the spawn behind the camera.
            // Aim the wind path through a point ten metres ahead instead.
            Vector3 position = eye.origin + forward * 10 - wind * 4 + side * ((float)random.NextDouble() * 2 - 1) + Vector3.up * 1.5f;
            GameObject spawned = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
            spawned.name = prefab.name; spawned.SetActive(true);
            Rigidbody body = spawned.GetComponent<Rigidbody>();
            // Initialize only this newly spawned native food item. Leave all existing
            // creatures/items/vehicles untouched; its ordinary gravity/collisions take over.
            body.isKinematic = false;
            body.velocity = wind * (6 + (float)random.NextDouble() * 3) + Vector3.up * 1.5f;
            body.angularVelocity = new Vector3(1.5f, 0.8f, 2.5f);
            schedule.Spawned();
            Plugin.Log.LogInfo("An occasional native lizard food item blew past; ordinary pickup, eating, cooking and spoilage remain native.");
        }
        internal static void Suspend() { pending = false; }
        internal static void Reset() { Suspend(); schedule.Reset(); prefab = null; warned = false; lookupAttempts = 0; nextLookup = 0; }
    }
    [HarmonyPatch(typeof(CreateObject), "OnEnter")]
    internal static class WindblownLizardPrefabPatch
    {
        private static void Prefix(CreateObject __instance)
        {
            // Observe only the referenced prefab. The native action still runs.
            if (Plugin.Active && __instance.gameObject != null)
                WindblownLizards.CapturePrefab(__instance.gameObject.Value);
        }
    }
}
