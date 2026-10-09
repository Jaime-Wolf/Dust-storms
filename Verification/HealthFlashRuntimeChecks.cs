using System;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;
namespace ApocaDustStorm
{
    internal static class HealthFlashRuntimeChecks
    {
        private static bool Same(Color a, Color b)
        { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }
        internal static void Run(Action<bool, string> check)
        {
            PlayerStormHazards.Reset(); StormSleep.Reset(); Physics.RaycastFixture = null;
            Plugin.Active = true; Plugin.ExposureDamage = Plugin.ExposureIndicators = null;
            Time.unscaledTime = 1000; Time.timeScale = 1; Apocasetter.GameMenu.Paused = false;
            GameObject player = new GameObject("Player");
            PlayMakerFSM health = player.Add<PlayMakerFSM>(); health.FsmName = "Health"; health.ActiveStateName = "playerHealth";
            PlayMakerFSM cab = player.Add<PlayMakerFSM>(); cab.FsmName = "InCar"; cab.ActiveStateName = "OnFoot";
            Text label = new GameObject("Canvas/SurvivalCanvas/HealthLable").Add<Text>();
            Text number = new GameObject("Canvas/SurvivalCanvas/HealthLable/HealthText").Add<Text>();
            Color original = new Color(.6f, .7f, .8f, .5f), other = new Color(.8f, .3f, .2f, .75f);
            label.color = number.color = original;
            PlayerStormHazards.Tick(player, .1f, 0, false); StormHealthFlash.Tick();
            check(Same(label.color, original) && Same(number.color, original), "Clear weather does not tint the native health readout");
            float hp = health.FsmVariables.Health.Value;
            int finds = GameObject.Finds;
            PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(health.FsmVariables.Health.Value < hp && label.color.r == 1 && number.color.g == .85f && label.color.a == original.a,
                "Actual storm health loss pulses the native label and number yellow while preserving transparency");
            check(GameObject.Finds == finds + 2, "The pulse binds only the two specific player HUD paths");
            finds = GameObject.Finds;
            Time.unscaledTime += .3f; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(label.color.r < 1 && label.color.r > original.r, "Continuous per-frame damage does not restart the fade");
            Time.unscaledTime += .31f; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(Same(label.color, original) && Same(number.color, original), "The pulse restores exact native colors after six tenths of a second");
            Time.unscaledTime = 1001.99f; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(Same(label.color, original), "Sustained exposure has a clear gap rather than a constant yellow glow");
            Time.unscaledTime = 1002; cab.ActiveStateName = "InCar"; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(label.color.r == 1 && number.color.r == 1, "Reduced but actual vehicle exposure damage starts the next two-second pulse");
            check(GameObject.Finds == finds, "Repeated pulses reuse native HUD references without scene searches");
            label.color = other; Time.unscaledTime += .2f; StormHealthFlash.Tick();
            check(label.color.a == other.a, "A newer native or other-mod tint becomes the current baseline");
            Time.unscaledTime += .5f; StormHealthFlash.Tick();
            check(Same(label.color, other), "Fade completion preserves the updated native baseline");
            Time.unscaledTime = 1004; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            number.color = other; StormHealthFlash.Clear();
            check(Same(number.color, other) && Same(label.color, other), "Cleanup restores owned tints and preserves newer foreign writes");
            Time.unscaledTime = 1006; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            Apocasetter.GameMenu.Paused = true; StormHealthFlash.Tick();
            check(Same(label.color, other) && Same(number.color, other), "Pause clears the visible pulse without leaving a stuck HUD tint");
            Apocasetter.GameMenu.Paused = false; StormHealthFlash.Clear();
            Plugin.ExposureIndicators = 0f; Time.unscaledTime = 1008; hp = health.FsmVariables.Health.Value;
            PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(health.FsmVariables.Health.Value < hp && Same(label.color, other), "Hiding indicators suppresses the pulse without changing storm damage");
            Plugin.ExposureIndicators = null; StormHealthFlash.Clear(); Plugin.ExposureDamage = 0f;
            Time.unscaledTime = 1010; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(Same(label.color, other), "Disabling storm health damage causes no misleading health pulse");
            Plugin.ExposureDamage = null;
            GameObject cave = new GameObject("Building_health_test"); Collider roof = cave.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 p, Vector3 d, float distance) { return d.y > .5f ? new[] { new RaycastHit { collider = roof, distance = 4 } } : new RaycastHit[0]; };
            Time.unscaledTime = 1012; hp = health.FsmVariables.Health.Value; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(hp == health.FsmVariables.Health.Value && Same(label.color, other), "Sheltered players have neither storm damage nor a damage pulse");
            Physics.RaycastFixture = null; StormShelter.Reset(); Time.unscaledTime = 1014;
            PlayerStormHazards.Tick(player, .1f, 1, true); StormHealthFlash.Tick();
            check(Same(label.color, other), "Clock skips do not produce catch-up damage or a health pulse");
            Time.unscaledTime = 1016; PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            Plugin.Active = false; StormHealthFlash.Tick();
            check(Same(label.color, other) && Same(number.color, other), "Disabling the mod restores health display colors");
            Plugin.Active = true; PlayerStormHazards.Reset(); Time.unscaledTime = 1018; StormHealthFlash.Tick();
            check(Same(label.color, other), "Player/session reset cannot retain or replay a previous damage pulse");
            label.gameObject.SetActive(false); number.gameObject.SetActive(false);
            PlayerStormHazards.Tick(player, .1f, 1, false); StormHealthFlash.Tick();
            check(Same(label.color, other), "Hidden native HUD elements are never recolored");
            PlayerStormHazards.Reset(); StormHealthFlash.Clear();
            UnityEngine.Object.Destroy(label); UnityEngine.Object.Destroy(number);
            UnityEngine.Object.Destroy(label.gameObject); UnityEngine.Object.Destroy(number.gameObject);
            Physics.RaycastFixture = null; Plugin.ExposureIndicators = Plugin.ExposureDamage = null;
        }
    }
}
