using System;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class PlayerStormHazards
    {
        private static readonly VehicleProtectionModel cover = new VehicleProtectionModel();
        private static bool wasSleeping;
        private static GameObject player;
        private static PlayMakerFSM health, inCar, sleepFsm;
        private static FsmFloat amount;
        private static float nextShelterScan;
        private static Vector3 shelterPosition;
        private static bool suspended;
        internal static bool Sheltered, InVehicle;
        internal static GameObject Player { get { return player; } }
        internal static bool Ready { get { return !suspended && player != null && health != null && health.isActiveAndEnabled && health.Fsm.Initialized && health.ActiveStateName == "playerHealth" && amount != null && amount.Value >= 0.4f && !float.IsNaN(amount.Value) && !float.IsInfinity(amount.Value); } }
        internal static float VehicleProtection { get { return (float)cover.Protection; } }
        internal static bool Sleeping { get { return sleepFsm != null && sleepFsm.isActiveAndEnabled && sleepFsm.Fsm.Initialized && !String.IsNullOrEmpty(sleepFsm.ActiveStateName) && sleepFsm.ActiveStateName != "Awake"; } }
        internal static StormCover State(float strength)
        {
            if (!Plugin.Active || !Ready || Sleeping || Apocasetter.GameMenu.Paused || Time.timeScale <= 0 ||
                float.IsNaN(strength) || float.IsInfinity(strength) ||
                StormHazardModel.Severity(strength) <= 0.001 || Plugin.Value(Plugin.ExposureDamage, 1) <= 0) return StormCover.None;
            if (Sheltered) return StormCover.Shelter;
            if (InVehicle) return StormCover.Vehicle;
            return StormCover.Exposed;
        }
        internal static void Tick(GameObject current, float dt, float strength, bool skipped)
        {
            if (!Plugin.Active || current == null) { Reset(); return; }
            if (Apocasetter.GameMenu.Paused || Time.timeScale <= 0) { Suspend(); return; }
            suspended = false;
            if (player != current)
            {
                Reset(); suspended = false; player = current;
                // Only the runner's exact native Player can own exposure damage.
                if (current.name != "Player") { Reset(); return; }
                foreach (PlayMakerFSM fsm in player.GetComponents<PlayMakerFSM>())
                { if (fsm.FsmName == "Health") health = fsm; if (fsm.FsmName == "InCar") inCar = fsm; if (fsm.FsmName == "Sleep") sleepFsm = fsm; }
            }
            if (amount == null && health != null && health.Fsm.Initialized) amount = health.FsmVariables.FindFsmFloat("Health");
            if (!Ready) { nextShelterScan = 0; Sheltered = false; InVehicle = false; return; }
            bool wasInVehicle = InVehicle;
            InVehicle = inCar != null && inCar.ActiveStateName == "InCar";
            Vector3 position = player.transform.position;
            // Moving across a shelter boundary or leaving a cab must not reuse stale cover.
            if (Time.unscaledTime >= nextShelterScan || skipped || wasInVehicle != InVehicle ||
                (position - shelterPosition).sqrMagnitude > 0.000001f)
            { nextShelterScan = Time.unscaledTime + 0.25f; shelterPosition = position; Sheltered = StormShelter.ContainsActor(player); }
            if (Sleeping)
            {
                wasSleeping = true;
                StormSleep.Interrupt(sleepFsm, strength);
                return;
            }
            bool interrupted = StormSleep.ConsumeWake(player);
            bool woke = wasSleeping || interrupted;
            wasSleeping = false;
            float damage = 0;
            if (!woke && !skipped)
            {
                bool hazardous = State(strength) != StormCover.None;
                cover.Step(dt, hazardous, Sheltered, InVehicle);
                damage = (float)StormHazardModel.Damage(dt, strength, Sheltered, InVehicle, Plugin.Value(Plugin.ExposureDamage, 1), cover.Protection);
            }
            if (damage > 0) amount.Value = Mathf.Max(0, amount.Value - damage);
        }
        internal static void AdvanceShelteredSleep(double elapsed)
        {
            if (!Plugin.Active || !Ready || !Sleeping || Apocasetter.GameMenu.Paused || Time.timeScale <= 0 ||
                Double.IsNaN(elapsed) || Double.IsInfinity(elapsed) || elapsed <= 0 || !StormShelter.ContainsActor(player)) return;
            // Safe native sleep can restore cab cover. No health accounting or wake penalty.
            double remaining = Math.Min(elapsed, 120);
            while (remaining > 0) { double step = Math.Min(remaining, 0.2); cover.Step(step, false, true, false); remaining -= step; }
        }
        internal static float MovementGain(float strength, float gust)
        {
            if (!Plugin.Active || !Ready || Sheltered || InVehicle || Apocasetter.GameMenu.Paused || Time.timeScale <= 0) return 1;
            return (float)StormHazardModel.MovementGain(strength, gust, Plugin.Value(Plugin.Gusts, 1), Plugin.Value(Plugin.MovementResistance, 1));
        }
        internal static void Suspend() { suspended = true; nextShelterScan = 0; }
        internal static void Reset() { Suspend(); cover.Reset(); wasSleeping = false; shelterPosition = Vector3.zero; Sheltered = false; InVehicle = false; player = null; health = null; inCar = null; sleepFsm = null; amount = null; }
    }
}
