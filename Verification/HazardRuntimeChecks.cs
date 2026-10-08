using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class HazardRuntimeChecks
    {
        private static GameObject Actor(string name, bool npc)
        {
            GameObject actor = new GameObject(name); actor.Add<Rigidbody>();
            PlayMakerFSM health = actor.Add<PlayMakerFSM>(); health.FsmName = "Health"; health.ActiveStateName = "playerHealth";
            actor.Add<PlayMakerFSM>().FsmName = "Movement";
            if (npc) { actor.Add<PlayMakerFSM>().FsmName = "Detection"; actor.Add<PlayMakerFSM>().FsmName = "Attack"; }
            else { PlayMakerFSM cab = actor.Add<PlayMakerFSM>(); cab.FsmName = "InCar"; cab.ActiveStateName = "OnFoot"; }
            return actor;
        }
        private static List<CodeInstruction> NativeAssignment()
        { return new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Callvirt, typeof(Rigidbody).GetProperty("velocity").GetSetMethod()), new CodeInstruction(OpCodes.Ret) }; }
        private static Action<T, Rigidbody, Vector3> Executable<T>(IEnumerable<CodeInstruction> instructions)
        {
            DynamicMethod method = new DynamicMethod("CheckedStormMove", typeof(void), new Type[] { typeof(T), typeof(Rigidbody), typeof(Vector3) }, typeof(HazardRuntimeChecks).Module, true);
            ILGenerator il = method.GetILGenerator();
            foreach (CodeInstruction code in instructions)
            { if (code.operand == null) il.Emit(code.opcode); else if (code.operand is MethodInfo) il.Emit(code.opcode, (MethodInfo)code.operand); else throw new Exception("Unexpected fixture operand"); }
            return (Action<T, Rigidbody, Vector3>)method.CreateDelegate(typeof(Action<T, Rigidbody, Vector3>));
        }
        internal static void Run(Action<bool, string> check)
        {
            Physics.RaycastFixture = null; PlayerStormHazards.Reset(); StormAIMovement.Reset();
            Plugin.Active = true; Plugin.ExposureDamage = null; Plugin.MovementResistance = null;
            Time.unscaledTime = 100; Time.timeScale = 1; Apocasetter.GameMenu.Paused = false;
            GameObject player = Actor("Player", false), npc = Actor("Test pursuer", true);
            PlayMakerFSM health = player.GetComponents<PlayMakerFSM>()[0], inCar = player.GetComponents<PlayMakerFSM>()[2];
            FsmFloat hp = health.FsmVariables.Health, aiHp = npc.GetComponents<PlayMakerFSM>()[0].FsmVariables.Health;
            PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(Math.Abs(hp.Value - 99.97f) < 0.0001 && PlayerStormHazards.State(1) == StormCover.Exposed, "First outdoor frame reports EXPOSED and starts full-rate damage");
            for (int i = 1; i < 600; i++) { Time.unscaledTime += 0.1f; PlayerStormHazards.Tick(player, 0.1f, 1, false); }
            check(Math.Abs(hp.Value - 82) < 0.01 && aiHp.Value == 100, "The first minute damages only the exact native Player health, leaving AI untouched");
            check(PlayerStormHazards.Ready, "Native health state and variable are bound before exposure damage");
            float before = hp.Value; inCar.ActiveStateName = "InCar";
            for (int i = 0; i < 100; i++) { Time.unscaledTime += 0.1f; PlayerStormHazards.Tick(player, 0.1f, 1, false); }
            check(Math.Abs(before - hp.Value - 0.332825f) < 0.002 && PlayerStormHazards.InVehicle, "Entering a vehicle starts near ninety percent protection and progressively wears it down");
            check(PlayerStormHazards.State(1) == StormCover.Vehicle, "Vehicle status follows native InCar state immediately");
            check(PlayerStormHazards.MovementGain(1, 1) == 1, "Driving does not reduce throttle or alter vehicle controls");
            Apocasetter.GameMenu.Paused = true; before = hp.Value; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && !PlayerStormHazards.Ready, "Paused menus suspend damage and movement effects");
            Apocasetter.GameMenu.Paused = false; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value < before && PlayerStormHazards.State(1) != StormCover.None, "Unpausing immediately resumes current cover and damage without a grace timer");
            before = hp.Value; PlayerStormHazards.Tick(player, 0.1f, 1, true);
            check(hp.Value == before, "Sleeping/time jumps do not inflict catch-up damage");
            inCar.ActiveStateName = "OnFoot"; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(Math.Abs(before - hp.Value - 0.03f) < 0.0001 && PlayerStormHazards.State(1) == StormCover.Exposed, "Leaving a vehicle after sleeping immediately shows EXPOSED and starts full damage");
            Plugin.ExposureDamage = 0f; before = hp.Value; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && PlayerStormHazards.State(1) == StormCover.None, "Zero damage slider suppresses both health loss and exposure warning"); Plugin.ExposureDamage = null;
            PlayerStormHazards.Tick(player, 0.1f, 0.3f, false);
            check(hp.Value == before && PlayerStormHazards.State(0.3f) == StormCover.None, "Light dust has neither exposure warning nor damage");
            PlayerStormHazards.Tick(player, 0.1f, 0.6f, false);
            check(Math.Abs(before - hp.Value - 0.015f) < 0.0001 && PlayerStormHazards.State(0.6f) == StormCover.Exposed, "Approaching dangerous dust reports exposure at its current reduced damage rate");
            GetAxisKeyAxis axis = new GetAxisKeyAxis { Owner = player, Fsm = new Fsm { Name = "Movement" }, store = new FsmFloat { Value = 5 } };
            StormRunner.Strength = 1; StormRunner.Gust = 1;
            PlayerStormMovementPatch.Postfix(axis);
            check(Math.Abs(axis.store.Value - 3.5f) < 0.0001, "Fresh native walk/run input receives the tuned storm multiplier");
            axis.store.Value = 5; axis.Fsm.Name = "UseLadder"; PlayerStormMovementPatch.Postfix(axis);
            check(axis.store.Value == 5, "Ladder climbing and flight FSMs retain native movement");
            axis.Owner = npc; axis.Fsm.Name = "Movement"; PlayerStormMovementPatch.Postfix(axis);
            check(axis.store.Value == 5, "Player input hook cannot alter AI or unrelated input");
            axis.Owner = player; axis.store.IsNone = true; PlayerStormMovementPatch.Postfix(axis);
            check(axis.store.Value == 5, "Absent input variables are ignored");
            health.ActiveStateName = "playerDeath"; before = hp.Value; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && PlayerStormHazards.MovementGain(1, 1) == 1, "Death stops damage and player movement effects"); health.ActiveStateName = "playerHealth";
            hp.Value = float.NaN; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(float.IsNaN(hp.Value) && !PlayerStormHazards.Ready, "Invalid native health is never written"); hp.Value = 100;
            PlayerStormHazards.Tick(npc, 0.1f, 1, false);
            check(PlayerStormHazards.Player == null && aiHp.Value == 100, "NPC cannot be bound as the player damage target");

            GameObject cave = new GameObject("Cave_3"); Collider roof = cave.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return direction.y > 0.5f ? new RaycastHit[] { new RaycastHit { collider = roof, distance = 4 } } : new RaycastHit[0]; };
            check(StormShelter.ContainsActor(player), "Native cave roof provides shelter");
            cave.name = "Building_7"; check(StormShelter.ContainsActor(player), "Every recognized building roof can shelter the player");
            cave.name = "container_long_1"; check(StormShelter.ContainsActor(player), "Conex/container geometry provides full shelter");
            Time.unscaledTime += 1; PlayerStormHazards.Tick(player, 0.1f, 1, false); before = hp.Value;
            for (int i = 0; i < 700; i++) PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && PlayerStormHazards.Sheltered && PlayerStormHazards.State(1) == StormCover.Shelter && PlayerStormHazards.MovementGain(1, 1) == 1, "Shelter immediately stops damage and movement resistance and reports SHELTERED");
            Physics.RaycastFixture = null; player.transform.position += Vector3.forward * 0.02f;
            PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(!PlayerStormHazards.Sheltered && Math.Abs(before - hp.Value - 0.03f) < 0.0001 && PlayerStormHazards.State(1) == StormCover.Exposed, "Walking beyond shelter refreshes cover on the same frame without waiting for the stationary cache");
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return direction.y > 0.5f ? new RaycastHit[] { new RaycastHit { collider = roof, distance = 4 } } : new RaycastHit[0]; };
            player.transform.position -= Vector3.forward * 0.02f; before = hp.Value;
            PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && PlayerStormHazards.State(1) == StormCover.Shelter, "Walking back into shelter immediately stops damage");
            inCar.ActiveStateName = "InCar"; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && PlayerStormHazards.State(1) == StormCover.Shelter, "A vehicle parked under structural shelter receives full shelter protection");
            Physics.RaycastFixture = null; inCar.ActiveStateName = "OnFoot"; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(Math.Abs(before - hp.Value - 0.03f) < 0.0001 && !PlayerStormHazards.Sheltered && PlayerStormHazards.State(1) == StormCover.Exposed, "Exiting a vehicle rechecks geometry immediately even if actor position did not change");
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return direction.y > 0.5f ? new RaycastHit[] { new RaycastHit { collider = roof, distance = 4 } } : new RaycastHit[0]; };
            cave.name = "tree_big"; check(!StormShelter.ContainsActor(player), "Tree canopy does not count as full shelter");
            cave.name = "car_wreck_roof"; check(!StormShelter.ContainsActor(player), "Wreck panels cannot masquerade as a building roof");
            cave.name = "building"; roof.isTrigger = true; check(!StormShelter.ContainsActor(player), "Trigger volumes are excluded from shelter"); roof.isTrigger = false;
            roof.attachedRigidbody = cave.Add<Rigidbody>(); check(!StormShelter.ContainsActor(player), "Loose physics objects cannot provide full shelter"); roof.attachedRigidbody = null;
            cave.Add<NWH.VehiclePhysics2.VehicleController>(); check(!StormShelter.ContainsActor(player), "An intact vehicle roof is excluded from full structural shelter");
            GameObject unnamed = new GameObject("Imported shelter mesh"); Collider mesh = unnamed.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return new RaycastHit[] { new RaycastHit { collider = mesh, distance = 3 } }; };
            check(StormShelter.ContainsActor(player), "Unnamed/modded roof and two walls also provide shelter");
            GameObject ruin = new GameObject("old_building"); Collider wall = ruin.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return Math.Abs(direction.y) < 0.1f ? new RaycastHit[] { new RaycastHit { collider = wall, distance = 4 } } : new RaycastHit[0]; };
            check(StormShelter.ContainsActor(player), "An enclosed building also protects through its walls when the roof is absent");
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return direction.y > 0.5f ? new RaycastHit[] { new RaycastHit { collider = mesh, distance = 3 } } : new RaycastHit[0]; };
            check(!StormShelter.ContainsActor(player), "A lone unnamed overhead prop is insufficient shelter");
            Collider self = player.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance) { return new RaycastHit[] { new RaycastHit { collider = self, distance = 0.3f } }; };
            check(!StormShelter.ContainsActor(player), "Player's own colliders cannot shelter them");
            Physics.RaycastFixture = null; Time.unscaledTime += 1; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(!PlayerStormHazards.Sheltered, "Shelter refresh detects leaving the structure");

            StormRunner.View = new GameObject("Hazard view").Add<Camera>(); StormAIMovement.Reset();
            check(Math.Abs(StormAIMovement.Gain(npc) - 0.7f) < 0.00001 && aiHp.Value == 100, "AI share player's peak movement resistance while staying damage immune");
            GameObject item = new GameObject("Moving item"); item.Add<PlayMakerFSM>().FsmName = "Movement";
            check(StormAIMovement.Gain(item) == 1, "Movement-named item FSM is insufficient to qualify as an AI actor");
            SetVelocity action = new SetVelocity { Owner = npc, Fsm = new Fsm { Name = "Movement" } };
            Vector3 motion = new Vector3(5, -4, 10), filtered = StormAIMovement.FilterActionVelocity(motion, action);
            check(Math.Abs(filtered.x - 3.5f) < 0.0001 && Math.Abs(filtered.z - 7) < 0.0001 && filtered.y == -4, "AI slowdown changes only planar movement, preserving fall/jump motion");
            action.Fsm.Name = "Attack"; filtered = StormAIMovement.FilterActionVelocity(motion, action);
            check(filtered.x == 5 && filtered.z == 10, "Non-movement impulses retain their native behavior"); action.Fsm.Name = "Movement";
            List<CodeInstruction> original = NativeAssignment();
            List<CodeInstruction> rewritten = new List<CodeInstruction>(NativeAIStormMovementPatch.Transpiler(original));
            Action<SetVelocity, Rigidbody, Vector3> nativeMove = Executable<SetVelocity>(rewritten);
            Rigidbody body = npc.GetComponent<Rigidbody>(); nativeMove(action, body, motion);
            check(rewritten.Count == original.Count + 2 && Math.Abs(body.velocity.z - 7) < 0.00001 && body.velocity.y == -4, "Executable native movement IL applies resistance exactly once before its existing assignment");
            Action<SetVelocity, Rigidbody, Vector3> npcMove = Executable<SetVelocity>(StormAIMovement.NpcCombatTranspiler(NativeAssignment()));
            npcMove(action, body, motion);
            check(Math.Abs(body.velocity.z - 7) < 0.00001, "Executable NPCAI override IL receives the same single multiplier");
            bool rejected = false; try { StormAIMovement.Rewrite(new CodeInstruction[0], typeof(StormAIMovement).GetMethod("FilterActionVelocity", BindingFlags.NonPublic | BindingFlags.Static)); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Changed/absent native assignment rejects the adapter instead of silently mispatching");
            List<CodeInstruction> doubled = NativeAssignment(); doubled.AddRange(NativeAssignment()); rejected = false;
            try { StormAIMovement.NpcCombatTranspiler(doubled); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Unexpected multiple assignments are rejected for compatibility safety");
            Harmony harmony = new Harmony(); StormAIMovement.InstallNpcBridge(harmony);
            check(StormAIMovement.NpcBridgeInstalled && harmony.Patches == 2, "Optional bridge checks and installs both NPCAI combat and wandering layouts");
            NPCAI.Idle.Ctl ctl = new NPCAI.Idle.Ctl { A = new NPCAI.Senses.Agent { T = npc.transform } };
            Action<object, Rigidbody, Vector3> idleMove = Executable<object>(StormAIMovement.NpcIdleTranspiler(NativeAssignment())); idleMove(ctl, body, motion);
            check(Math.Abs(body.velocity.z - 7) < 0.00001 && body.velocity.y == -4, "NPCAI wandering applies one multiplier through the verified actor bridge");
            action.Owner = player; nativeMove(action, body, motion);
            check(body.velocity.z == 10, "AI adapter never double-slows the player's already scaled input"); action.Owner = npc;
            Plugin.MovementResistance = 0f; nativeMove(action, body, motion); idleMove(ctl, body, motion);
            check(body.velocity.z == 10 && StormAIMovement.Gain(npc) == 1, "Zero movement slider restores native and NPCAI speeds immediately"); Plugin.MovementResistance = null;
            Apocasetter.GameMenu.Paused = true; check(StormAIMovement.Gain(npc) == 1, "Pause cannot alter AI movement"); Apocasetter.GameMenu.Paused = false;
            StormRunner.Strength = 0; check(StormAIMovement.Gain(npc) == 1, "Clearing restores AI's native speed without retained speed writes"); StormRunner.Strength = 1;
            Plugin.Active = false; before = hp.Value; PlayerStormHazards.Tick(player, 0.1f, 1, false);
            check(hp.Value == before && StormAIMovement.Gain(npc) == 1 && PlayerStormHazards.Player == null, "Disabling immediately stops hazards and clears player ownership"); Plugin.Active = true;
            check(aiHp.Value == 100, "Complete player hazard and AI adapter tests leave AI health untouched");
            GameObject passive = new GameObject("Lizard"); passive.Add<PlayMakerFSM>().FsmName = "Health"; passive.Add<PlayMakerFSM>().FsmName = "Rotate"; passive.Add<PlayMakerFSM>().FsmName = "Bodypart";
            check(Math.Abs(StormAIMovement.Gain(passive) - 0.7f) < 0.00001, "Passive native creatures also receive movement resistance without requiring attack detection");
            WindblownLizards.Reset(); GameObject foodPrefab = new GameObject("Lizard_Dead"); foodPrefab.IsPrefab = true; foodPrefab.Add<Rigidbody>();
            foreach (string name in new string[] { "Food", "ID", "ItemName", "Cook", "Perishable", "saveItemVar" }) foodPrefab.Add<PlayMakerFSM>().FsmName = name;
            check(WindblownLizards.NativeFoodPrefab(foodPrefab), "Lizard source requires an actual native food prefab with item identity");
            foodPrefab.IsPrefab = false; check(!WindblownLizards.NativeFoodPrefab(foodPrefab), "Existing world loot is never cloned"); foodPrefab.IsPrefab = true;
            GameObject live = new GameObject("Lizard"); live.IsPrefab = true; live.Add<Rigidbody>();
            check(!WindblownLizards.NativeFoodPrefab(live), "Live lizard NPCs cannot be used as food spawner templates");
            StormModel weather = new StormModel(); weather.Begin(0, 0, 0, 900, 90, 75, 1);
            int beforeFood = UnityEngine.Object.FindObjectsOfType<GameObject>().Length;
            for (int i = 0; i < 2000; i++) WindblownLizards.Tick(0.1f, 1, weather, false);
            Camera camera = StormRunner.View; WindblownLizards.PreCull(camera);
            GameObject dropped = null; foreach (GameObject candidate in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (candidate.name == "Lizard_Dead" && candidate != foodPrefab) dropped = candidate;
            check(dropped != null && UnityEngine.Object.FindObjectsOfType<GameObject>().Length == beforeFood + 1 && dropped.GetComponents<PlayMakerFSM>().Length == 6, "Wind event creates one native food item retaining pickup/cooking/spoilage/save FSMs");
            check(dropped.GetComponent<Rigidbody>().velocity.y == 1.5f && dropped.GetComponent<Rigidbody>().velocity.magnitude < 10 && live.GetComponent<Rigidbody>().velocity.sqrMagnitude == 0, "Only the newly spawned food receives a modest initial wind toss");
            WindblownLizards.Reset(); check(!dropped.Destroyed && dropped.activeInHierarchy, "Clearing effects leaves real edible loot under native game ownership");
            PlayerStormHazards.Reset(); StormAIMovement.Reset(); Physics.RaycastFixture = null; StormRunner.Strength = 0; StormRunner.View = null;
        }
    }
}

