using System;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocaDustStorm
{
    internal sealed class StormRunner : MonoBehaviour
    {
        internal static readonly StormModel Model = new StormModel();
        private static readonly StormSchedule schedule = new StormSchedule();
        internal static float Strength, Gust;
        private static StormVisuals visuals;
        private static WindAudio audio;
        private static SandAudio sand;
        private static GameObject player;
        private static Camera view;
        private static bool preview, inSession;
        private static float nextScan, nextWarning, noticeUntil;
        private static string notice;
        private static GUIStyle noticeStyle;
        private static GUISkin noticeSkin;
        private static readonly System.Random random = new System.Random();
        internal static Camera View { get { return view; } }
        internal static bool CanRender { get { return Plugin.Active && inSession && view != null; } }
        private void Update()
        {
            try
            {
                // A camera that stops rendering can miss its closing callback.
                // Camera-only overrides must never survive into a later frame.
                StormFog.Restore();
                bool ingame = Plugin.Active && Apocasetter.GameMenu.InGame;
                if (!ingame) { if (inSession || Model.Active) ClearAll(); inSession = false; return; }
                inSession = true;
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 1;
                    if (player == null) player = GameObject.Find("Player");
                }
                StormDiscovery.EnsureScene();
                view = SelectView();
                StormDiscovery.BindVehicle(StormCameraCache.VehicleRoot);
                if (player == null || view == null) { ExposureHud.Hide(); PlayerStormHazards.Suspend(); WindblownLizards.Suspend(); DustLightning.Suspend(); if (sand != null) sand.Silence(); if (audio != null) audio.Silence(); return; }
                bool playing = Time.timeScale > 0 && !Apocasetter.GameMenu.Paused;
#if APOCA_DEV
                bool input = playing && Application.isFocused && !Apocasetter.InputBlocker.Active;
                if (input && Plugin.PreviewKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.PreviewKey.Value))
                { if (Model.Active) { Model.Stop(); Announce("Dust storm passing"); } else Begin(true); }
#endif
                if (!playing) { ExposureHud.Hide(); PlayerStormHazards.Suspend(); WindblownLizards.Suspend(); DustLightning.Suspend(); if (audio != null) audio.Silence(); if (sand != null) sand.Silence(); return; }
                bool disabled = Plugin.FollowNativeSwitch.Value && NativeStormGuard.StormsDisabled();
                if (!preview && (!Plugin.Automatic.Value || disabled) && Model.Active) Model.Stop();
                float dt = Mathf.Min(Time.deltaTime, 0.2f);
                double elapsed = NativeStormClock.Step(Time.deltaTime);
                bool skipped = elapsed > Math.Max(2, dt * 4);
                PlayerStormHazards.AdvanceShelteredSleep(elapsed);
                bool wasActive = Model.Active; Model.Tick(elapsed);
                if (wasActive && !Model.Active)
                {
                    preview = false; schedule.Reset();
                    Announce("Dust storm cleared"); Plugin.Log.LogInfo("Visual dust storm finished; all effects cleared.");
                }
                if (!wasActive && !Model.Active && Plugin.Automatic.Value && !disabled &&
                    schedule.Tick(dt, Plugin.Value(Plugin.MinimumCalm, 5), Plugin.Value(Plugin.Chance, 6), random.NextDouble)) Begin(false);
                Vector3 p = player.transform.position;
                float target = (float)Model.Intensity(p.x, p.z);
                Strength = skipped ? target : Mathf.Lerp(Strength, target, 1 - Mathf.Exp(-dt / 3.0f));
                Gust = Mathf.Lerp(Gust, (float)Model.Gust(p.x, p.z), 1 - Mathf.Exp(-dt / 0.65f));
                if (!Model.Active && Strength < 0.001f) Strength = 0;
                PlayerStormHazards.Tick(player, dt, Strength, skipped);
                ExposureHud.Tick(dt, Strength);
                WindblownLizards.Tick(dt, Strength, Model, skipped);
                AzureAtmosphere.EnsureCamera(view);
                StormDistanceFog.EnsureCamera(view);
                if (Model.Active || Strength > 0.001f)
                {
                    if (visuals == null) visuals = new StormVisuals();
                    if (audio == null) audio = new WindAudio();
                    if (sand == null) sand = new SandAudio();
                    visuals.Tick(view.transform.position, dt, Strength, Model);
                }
                else if (visuals != null) { visuals.Dispose(); visuals = null; }
#if APOCA_DEV
                bool dischargePreview = input && Plugin.DischargeKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.DischargeKey.Value);
#else
                bool dischargePreview = false;
#endif
                DustLightning.Tick(view, dt, Strength, Model.Active, dischargePreview, skipped);
                if (audio != null) audio.Tick(Strength, Gust, dt);
                if (sand != null) sand.Tick(player, Strength, Gust, dt);
            }
            catch (Exception e)
            {
                if (Time.unscaledTime >= nextWarning)
                { nextWarning = Time.unscaledTime + 10; Plugin.Log.LogWarning("Storm visuals stopped: " + e); }
                ClearAll();
            }
        }
        private static Camera SelectView()
        { return StormCameraCache.Select(player); }
        private static void Begin(bool test)
        {
            StormDiscovery.StormStarted(); StormShelter.Reset(); StormAIMovement.Reset();
            Vector3 p = player.transform.position;
            double duration = StormModel.RollDuration(Plugin.Value(Plugin.MinimumDuration, 5), Plugin.Value(Plugin.MaximumDuration, 15), random.NextDouble());
            Model.BeginFrom(p.x, p.z, (WindFrom)random.Next(4), duration,
                Plugin.Value(Plugin.BuildTime, 90), Plugin.Value(Plugin.ClearTime, 75), random.NextDouble() * 100);
            preview = test; schedule.Reset();
            WindblownLizards.Reset();
            Announce("Dust storm approaching from the " + Model.Source.ToString().ToLowerInvariant());
            Plugin.Log.LogInfo("Started " + (test ? "preview" : "automatic") + " worldwide storm from " + Model.Source + "; duration " + duration.ToString("F0") + "s, build-up " + Model.BuildTime + "s, clearing " + Model.ClearTime + "s; player exposure only, no AI/part storm damage.");
        }
        private static void Announce(string text) { notice = text; noticeUntil = Time.unscaledTime + 8; }
        internal static void ClearAll()
        {
            DustLightning.Clear(); StormHorizon.Clear(); StormDistanceFog.Clear(); StormView.Clear(); StormFog.Clear(); StormLighting.Clear(Apocasetter.GameMenu.InGame); AzureAtmosphere.Clear(); NativeStormClock.Reset(); Model.Reset(); Strength = 0; Gust = 0; preview = false;
            if (visuals != null) { visuals.Dispose(); visuals = null; }
            if (audio != null) { audio.Dispose(); audio = null; }
            if (sand != null) { sand.Dispose(); sand = null; }
            ExposureHud.Clear(); VehicleWind.Reset(); PlayerStormHazards.Reset(); StormSleep.Reset(); StormAIMovement.Reset(); WindblownLizards.Reset();
            StormDiscovery.Reset(); StormCameraCache.Clear(); StormShelter.Reset();
            player = null; view = null; inSession = false; nextScan = 0; noticeStyle = null; noticeSkin = null;
            schedule.Reset(); noticeUntil = 0;
        }
        private void OnGUI()
        {
            if (!Plugin.Active || !inSession || Apocasetter.GameMenu.Paused) return;
            ExposureHud.Draw();
            if (!Plugin.Status.Value) return;
            string label = Time.unscaledTime < noticeUntil ? notice : null;
#if APOCA_DEV
            if (label == null && preview && Model.Active) label = "DUST STORM TEST  |  " + Plugin.PreviewKey.Value + " TO CLEAR";
#endif
            if (label == null) return;
            if (noticeStyle == null || noticeSkin != GUI.skin)
            {
                noticeSkin = GUI.skin; noticeStyle = new GUIStyle(GUI.skin.label);
                noticeStyle.alignment = TextAnchor.MiddleCenter; noticeStyle.normal.textColor = new Color(0.87f, 0.77f, 0.57f);
            }
            GUIStyle style = noticeStyle;
            style.fontSize = Mathf.Clamp(Screen.height / 65, 14, 23); style.normal.textColor = new Color(0.87f, 0.77f, 0.57f);
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.1f, Screen.width * 0.6f, 45), label, style);
        }
        private void OnDisable() { ClearAll(); }
        private void LateUpdate()
        {
            if (inSession) StormLighting.Recover(!Model.Active && Strength == 0);
            StormHealthFlash.Tick();
        }
        private void FixedUpdate()
        {
            try
            {
                if (CanRender && Model.Active && Time.timeScale > 0 && !Apocasetter.GameMenu.Paused)
                {
                    VehicleWind.Tick(player, Model, Strength, Gust);
                    if (visuals != null) visuals.FixedTick(Strength, Gust, Model);
                }
            }
            catch (Exception e)
            { if (Time.unscaledTime >= nextWarning) { nextWarning = Time.unscaledTime + 10; Plugin.Log.LogWarning("Storm motion paused: " + e.Message); } }
        }
    }
}
