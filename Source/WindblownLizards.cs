using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class WindblownLizards
    {
        private static readonly WindLootSchedule schedule = new WindLootSchedule();
        private static readonly System.Random random = new System.Random();
        private static GameObject prefab;
        private static bool pending, warned;
        private static Vector3 wind;
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
            if (pending || PlayerStormHazards.Sheltered || !schedule.Tick(dt, strength >= 0.75f, random.NextDouble)) return;
            wind = new Vector3((float)storm.WindX, 0, (float)storm.WindZ); pending = true;
        }
        internal static void PreCull(Camera view)
        {
            if (!pending || !StormRunner.CanRender || view != StormRunner.View) return;
            if (Plugin.WindblownLizards == null || !Plugin.WindblownLizards.Value || Time.timeScale <= 0 || Apocasetter.GameMenu.Paused || PlayerStormHazards.Sheltered)
            { pending = false; return; }
            pending = false;
            if (prefab == null)
                foreach (PlayMakerFSM fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    if (fsm.FsmName == "Food" && NativeFoodPrefab(fsm.gameObject)) { prefab = fsm.gameObject; break; }
            if (prefab == null)
            {
                if (!warned) { warned = true; Plugin.Log.LogWarning("Native raw-lizard food prefab is not loaded; skipping windblown lizard instead of cloning a live NPC or existing loot."); }
                return;
            }
            DustLightning.AlignView(view);
            Ray eye = DustLightning.RenderRay(view, 0.5f, 0.5f);
            Vector3 forward = eye.direction; forward.y = 0; forward = forward.normalized;
            Vector3 side = new Vector3(wind.z, 0, -wind.x);
            Vector3 position = eye.origin + forward * 9 - wind * 9 + side * ((float)random.NextDouble() * 4 - 2) + Vector3.up * 2;
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
        internal static void Reset() { Suspend(); schedule.Reset(); prefab = null; warned = false; }
    }
}
