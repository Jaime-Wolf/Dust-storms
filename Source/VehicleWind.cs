using HutongGames.PlayMaker;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace ApocaDustStorm
{
    internal static class VehicleWind
    {
        private static GameObject player;
        private static PlayMakerFSM inCar, health;
        private static VehicleController vehicle;
        private static Rigidbody body;
        private static float acceleration;
        private static float phase;
        internal static void Reset() { player = null; inCar = null; health = null; vehicle = null; body = null; acceleration = 0; phase = 0; }
        internal static bool IsDriving(GameObject current)
        {
            if (current == null) return false;
            if (current != player)
            {
                Reset(); player = current;
                foreach (PlayMakerFSM fsm in player.GetComponents<PlayMakerFSM>())
                { if (fsm.FsmName == "InCar") inCar = fsm; if (fsm.FsmName == "Health") health = fsm; }
            }
            if (inCar == null || inCar.ActiveStateName != "InCar" ||
                (health != null && (health.ActiveStateName == "playerDeath" || health.ActiveStateName == "backToMenu"))) return false;
            VehicleController found = null;
            for (Transform t = player.transform.parent; t != null; t = t.parent)
                if (t.Find("DriveTrigger") != null)
                { found = t.GetComponent<VehicleController>(); if (found == null) found = t.GetComponentInChildren<VehicleController>(true); break; }
            if (found == null || !found.isActiveAndEnabled) return false;
            if (found != vehicle)
            {
                vehicle = found; body = found.GetComponent<Rigidbody>();
                if (body == null) body = found.GetComponentInParent<Rigidbody>(); acceleration = 0;
                if (body != null) Plugin.Log.LogInfo("Wind buffeting bound to " + vehicle.name + "; chassis mass " + body.mass + "kg.");
            }
            return body != null && !body.isKinematic;
        }
        internal static void Tick(GameObject current, StormModel model, float strength, float gust)
        {
            float setting = Mathf.Clamp(Plugin.Value(Plugin.Buffeting, 1), 0, 2);
            if (!Plugin.Active || !model.Active || strength < 0.005f || setting <= 0 ||
                (PlayerStormHazards.Player == current && PlayerStormHazards.Sheltered) || !IsDriving(current) || !vehicle.IsGrounded()) { acceleration = 0; return; }
            float mass = body.mass;
            Vector3 center = body.worldCenterOfMass;
            if (float.IsNaN(mass) || float.IsInfinity(mass) || mass < 100 ||
                float.IsNaN(center.x) || float.IsNaN(center.y) || float.IsNaN(center.z) ||
                float.IsInfinity(center.x) || float.IsInfinity(center.y) || float.IsInfinity(center.z)) { acceleration = 0; return; }
            // No force while airborne, heavily tilted or already rotating rapidly.
            if (Vector3.Dot(body.transform.up, Vector3.up) < 0.8f || body.angularVelocity.sqrMagnitude > 0.64f) { acceleration = 0; return; }
            float goal = (float)WindMath.Acceleration(strength, gust, Plugin.Value(Plugin.Gusts, 1), setting, mass);
            acceleration = Mathf.MoveTowards(acceleration, goal, Time.fixedDeltaTime * 0.3f);
            phase += Time.fixedDeltaTime;
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ);
            float force = Mathf.Min(6000, acceleration * mass);
            // Apply the same capped horizontal pressure slightly higher and off-center:
            // suspension lean and a changing yaw moment, without lifting the chassis.
            // No velocity edits, tornado force, vertical lift, individual-part force or damage messages.
            float lever = (float)(0.32 * System.Math.Sin(phase * 1.4) + 0.12 * System.Math.Sin(phase * 2.7));
            body.AddForceAtPosition(wind * force, center + Vector3.up * 0.55f + body.transform.forward * lever, ForceMode.Force);
        }
    }
}
