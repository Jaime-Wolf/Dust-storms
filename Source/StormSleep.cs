using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ApocaDustStorm
{
    internal static class StormSleep
    {
        private static GameObject waking;
        internal static bool Dangerous(PlayMakerFSM sleeper, float strength)
        {
            if (!Plugin.Active || !Apocasetter.GameMenu.InGame || Apocasetter.GameMenu.Paused || Time.timeScale <= 0 ||
                sleeper == null || !sleeper.isActiveAndEnabled || !sleeper.Fsm.Initialized || sleeper.FsmName != "Sleep" ||
                Single.IsNaN(strength) || Single.IsInfinity(strength)) return false;
            // Include an active storm's approach: starting sleep during build-up must
            // not jump the native clock far enough to bypass the entire storm.
            if (!StormRunner.Model.Active && StormHazardModel.Severity(strength) <= 0.001) return false;
            GameObject owner = sleeper.gameObject;
            if (owner == null || owner.name != "Player" || owner != GameObject.Find("Player")) return false;
            bool alive = false;
            foreach (PlayMakerFSM fsm in owner.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health" || !fsm.isActiveAndEnabled || !fsm.Fsm.Initialized || fsm.ActiveStateName != "playerHealth") continue;
                FsmFloat hp = fsm.FsmVariables.FindFsmFloat("Health");
                alive = hp != null && !Single.IsNaN(hp.Value) && !Single.IsInfinity(hp.Value) && hp.Value >= 0.4f;
                break;
            }
            return alive && !StormShelter.ContainsActor(owner);
        }
        private static bool HasTransition(Fsm fsm, string state, string eventName, string destination)
        {
            FsmState native = fsm.GetState(state);
            if (native == null || native.Transitions == null) return false;
            foreach (FsmTransition transition in native.Transitions)
                if (transition != null && transition.FsmEvent != null && transition.FsmEvent.Name == eventName && transition.ToState == destination) return true;
            return false;
        }
        private static string WakeState(PlayMakerFSM sleeper, string from)
        {
            Fsm fsm = sleeper.Fsm;
            // The native on-foot cancel restores Movement/GroundTest/Jump/Look in Enable.
            // Cab sleep never disables those FSMs and its native back event goes to Awake.
            if (fsm.GetState("Awake") == null || !HasTransition(fsm, "SleepOnFoot", "back", "Enable") ||
                !HasTransition(fsm, "SleepInCar", "back", "Awake") || !HasTransition(fsm, "Enable", "FINISHED", "Awake")) return null;
            return from == "SleepOnFoot" || from == "reset" ? "Enable" : "Awake";
        }
        internal static FsmState Redirect(Fsm fsm, FsmState next)
        {
            if (fsm == null || next == null || fsm.Name != "Sleep" ||
                (next.Name != "checkInCar" && next.Name != "SleepOnFoot" && next.Name != "SleepInCar" && next.Name != "reset" && next.Name != "reset 2")) return next;
            PlayMakerFSM sleeper = fsm.FsmComponent;
            if (sleeper == null || sleeper.Fsm != fsm || !Dangerous(sleeper, StormRunner.Strength)) return next;
            string wake = WakeState(sleeper, sleeper.ActiveStateName);
            if (wake == null) return next;
            waking = sleeper.gameObject;
            // Redirect BEFORE native checkInCar rewards or sleep time acceleration run.
            return fsm.GetState(wake);
        }
        internal static bool Interrupt(PlayMakerFSM sleeper, float strength)
        {
            if (sleeper == null || String.IsNullOrEmpty(sleeper.ActiveStateName) || sleeper.ActiveStateName == "Awake" ||
                sleeper.ActiveStateName == "Enable" || !Dangerous(sleeper, strength)) return false;
            string wake = WakeState(sleeper, sleeper.ActiveStateName);
            if (wake == null) return false;
            waking = sleeper.gameObject;
            if (sleeper.ActiveStateName == "SleepOnFoot" || sleeper.ActiveStateName == "SleepInCar") sleeper.SendEvent("back");
            else sleeper.SetState(wake);
            return true;
        }
        internal static bool ConsumeWake(GameObject owner)
        { if (waking == null || waking != owner) return false; waking = null; return true; }
        internal static void Reset() { waking = null; }
    }
    [HarmonyPatch(typeof(Fsm), "SwitchState")]
    internal static class StormSleepEntryPatch
    {
        internal static void Prefix(Fsm __instance, ref FsmState __0)
        { __0 = StormSleep.Redirect(__instance, __0); }
    }
    [HarmonyPatch(typeof(CallMethod), "DoMethodCall")]
    internal static class StormSleepClockPatch
    {
        internal static bool Prefix(CallMethod __instance)
        {
            if (__instance == null || __instance.Fsm == null || __instance.Fsm.Name != "Sleep" ||
                __instance.methodName == null || __instance.methodName.Value != "SetTimeline") return true;
            PlayMakerFSM sleeper = __instance.Fsm.FsmComponent;
            if (sleeper == null || sleeper.gameObject != __instance.Owner || !StormSleep.Interrupt(sleeper, StormRunner.Strength)) return true;
            __instance.Finish();
            return false;
        }
    }
}
