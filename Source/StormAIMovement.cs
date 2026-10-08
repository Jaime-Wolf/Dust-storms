using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class StormAIMovement
    {
        private sealed class Actor { internal bool Eligible, Sheltered; internal float NextScan; }
        private static readonly Dictionary<GameObject, Actor> actors = new Dictionary<GameObject, Actor>();
        private static readonly List<GameObject> removed = new List<GameObject>();
        private static float nextPrune;
        private static FieldInfo idleAgent, agentTransform;
        internal static bool NpcBridgeInstalled;
        internal static void Reset() { actors.Clear(); removed.Clear(); nextPrune = 0; }
        internal static float Gain(GameObject owner)
        {
            if (!Plugin.Active || !StormRunner.CanRender || owner == null || owner == PlayerStormHazards.Player ||
                owner.name == "Player" || Apocasetter.GameMenu.Paused || Time.timeScale <= 0 || StormRunner.Strength <= 0.35f) return 1;
            if (Time.unscaledTime >= nextPrune)
            {
                nextPrune = Time.unscaledTime + 5; removed.Clear();
                foreach (GameObject candidate in actors.Keys) if (candidate == null) removed.Add(candidate);
                foreach (GameObject candidate in removed) actors.Remove(candidate);
            }
            Actor actor;
            if (!actors.TryGetValue(owner, out actor))
            {
                bool health = false, detection = false, attack = false, rotate = false, bodypart = false;
                foreach (PlayMakerFSM fsm in owner.GetComponents<PlayMakerFSM>())
                { if (fsm.FsmName == "Health") health = true; if (fsm.FsmName == "Detection") detection = true; if (fsm.FsmName == "Attack") attack = true; if (fsm.FsmName == "Rotate") rotate = true; if (fsm.FsmName == "Bodypart") bodypart = true; }
                actor = new Actor { Eligible = health && ((detection && attack) || (rotate && bodypart)) }; actors[owner] = actor;
            }
            if (!actor.Eligible) return 1;
            if (Time.unscaledTime >= actor.NextScan)
            { actor.NextScan = Time.unscaledTime + 0.75f; actor.Sheltered = StormShelter.ContainsActor(owner); }
            return actor.Sheltered ? 1 : (float)StormHazardModel.MovementGain(StormRunner.Strength, StormRunner.Gust,
                Plugin.Value(Plugin.Gusts, 1), Plugin.Value(Plugin.MovementResistance, 1));
        }
        internal static Vector3 FilterActionVelocity(Vector3 velocity, SetVelocity action)
        {
            if (action == null || action.Fsm == null || action.Fsm.Name != "Movement") return velocity;
            float gain = Gain(action.Owner); velocity.x *= gain; velocity.z *= gain; return velocity;
        }
        internal static Vector3 FilterIdleVelocity(Vector3 velocity, object controller)
        {
            if (controller == null || idleAgent == null || agentTransform == null) return velocity;
            object agent = idleAgent.GetValue(controller);
            Transform transform = agent == null ? null : agentTransform.GetValue(agent) as Transform;
            float gain = Gain(transform == null ? null : transform.gameObject);
            velocity.x *= gain; velocity.z *= gain; return velocity;
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> input, MethodInfo filter)
        {
            int matches = 0; List<CodeInstruction> output = new List<CodeInstruction>();
            foreach (CodeInstruction original in input)
            {
                CodeInstruction instruction = new CodeInstruction(original.opcode, original.operand);
                instruction.labels.AddRange(original.labels); instruction.blocks.AddRange(original.blocks);
                MethodInfo call = instruction.operand as MethodInfo;
                if (call != null && call.DeclaringType == typeof(Rigidbody) && call.Name == "set_velocity")
                {
                    CodeInstruction owner = new CodeInstruction(OpCodes.Ldarg_0);
                    owner.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    output.Add(owner); output.Add(new CodeInstruction(OpCodes.Call, filter)); matches++;
                }
                output.Add(instruction);
            }
            if (matches != 1) throw new InvalidOperationException("Storm movement adapter expected exactly one native velocity assignment, found " + matches);
            return output;
        }
        internal static void InstallNpcBridge(Harmony harmony)
        {
            Type brain = AccessTools.TypeByName("NPCAI.Brain"), idle = AccessTools.TypeByName("NPCAI.Idle");
            if (brain == null && idle == null) return;
            try
            {
                MethodInfo combat = brain == null ? null : AccessTools.Method(brain, "BeforeSetVelocity", new Type[] { typeof(SetVelocity) });
                Type controller = idle == null ? null : idle.GetNestedType("Ctl", BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo wander = controller == null ? null : AccessTools.Method(idle, "Drive", new Type[] { controller, typeof(float) });
                idleAgent = controller == null ? null : controller.GetField("A", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                agentTransform = idleAgent == null ? null : idleAgent.FieldType.GetField("T", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (combat == null || combat.ReturnType != typeof(bool) || !combat.IsStatic || wander == null || !wander.IsStatic ||
                    agentTransform == null || agentTransform.FieldType != typeof(Transform)) throw new InvalidOperationException("NPCAI movement layout differs from the checked 1.2.0 build.");
                harmony.Patch(combat, transpiler: new HarmonyMethod(typeof(StormAIMovement), "NpcCombatTranspiler"));
                try { harmony.Patch(wander, transpiler: new HarmonyMethod(typeof(StormAIMovement), "NpcIdleTranspiler")); }
                catch { harmony.Unpatch(combat, HarmonyPatchType.Transpiler, Plugin.GUID); throw; }
                NpcBridgeInstalled = true;
                Plugin.Log.LogInfo("Optional NPCAI movement bridge active: combat and wandering use the same storm resistance; no AI health changes.");
            }
            catch (Exception e) { NpcBridgeInstalled = false; idleAgent = null; agentTransform = null; Plugin.Log.LogWarning("NPCAI storm movement bridge unavailable: " + e.Message); }
        }
        internal static IEnumerable<CodeInstruction> NpcCombatTranspiler(IEnumerable<CodeInstruction> instructions)
        { return Rewrite(instructions, AccessTools.Method(typeof(StormAIMovement), "FilterActionVelocity")); }
        internal static IEnumerable<CodeInstruction> NpcIdleTranspiler(IEnumerable<CodeInstruction> instructions)
        { return Rewrite(instructions, AccessTools.Method(typeof(StormAIMovement), "FilterIdleVelocity")); }
    }
    [HarmonyPatch(typeof(SetVelocity), "DoSetVelocity")]
    internal static class NativeAIStormMovementPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return StormAIMovement.Rewrite(instructions, AccessTools.Method(typeof(StormAIMovement), "FilterActionVelocity")); }
    }
}
