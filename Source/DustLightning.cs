using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
namespace ApocaDustStorm
{
    internal static class DustLightning
    {
        private const int MainPoints = 13, PrimaryBranches = 6, SecondaryForks = 3;
        private static readonly DustDischargeModel model = new DustDischargeModel();
        private static readonly System.Random random = new System.Random();
        private static GameObject owner;
        private static AudioSource source;
        private static AudioClip[] clips;
        private static LineRenderer[] arcs;
        private static Material material;
        private static Light strikeLight;
        private static bool pendingPlacement, favourView, probedChase, audibleDischarge;
        private static float screenX, screenY, depth, angle, height;
        private static MethodInfo chaseApply;
        internal static float Flare { get { return Plugin.DustLightningEnabled ? (float)model.Pulse * Mathf.Clamp(Plugin.Value(Plugin.FlashBrightness, 1), 0, 2) : 0; } }
        internal static float WindGain { get { return Plugin.DustLightningEnabled && audibleDischarge && Plugin.Value(Plugin.DischargeVolume, 1) > 0 ? (float)model.WindGain : 1; } }
        private static float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private static void Ensure()
        {
            if (owner != null) return;
            owner = new GameObject("ApocaDustStorm.StaticDischarge"); owner.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                source = owner.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = false;
                source.volume = 1; source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = 110;
                source.ignoreListenerVolume = false; source.ignoreListenerPause = false; source.outputAudioMixerGroup = null;
                strikeLight = owner.AddComponent<Light>(); strikeLight.type = LightType.Point;
                strikeLight.color = new Color(0.91f, 0.92f, 1, 1);
                strikeLight.range = 150; strikeLight.intensity = 0; strikeLight.enabled = false;
                strikeLight.renderMode = LightRenderMode.ForcePixel; strikeLight.bounceIntensity = 0;
                strikeLight.shadows = LightShadows.Soft; strikeLight.shadowStrength = 0.85f;
                strikeLight.shadowResolution = LightShadowResolution.Medium;
                strikeLight.shadowBias = 0.025f; strikeLight.shadowNormalBias = 0.25f;
                clips = new AudioClip[3];
                for (int i = 0; i < clips.Length; i++)
                {
                    float[] samples = DischargeSound.Generate(3118 + i * 97);
                    clips[i] = AudioClip.Create("ApocaDustStorm.LightningCrack" + i, samples.Length, 1, DischargeSound.SampleRate, false);
                    clips[i].hideFlags = HideFlags.HideAndDontSave; clips[i].SetData(samples, 0);
                }
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null || !shader.isSupported) shader = Shader.Find("UI/Default");
                if (shader == null || !shader.isSupported) return; // Local light and sound still work.
                material = new Material(shader); material.hideFlags = HideFlags.HideAndDontSave;
                material.mainTexture = Texture2D.whiteTexture; material.SetColor("_Color", Color.white);
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual); material.renderQueue = 3001;
                arcs = new LineRenderer[1 + PrimaryBranches + SecondaryForks];
                for (int i = 0; i < arcs.Length; i++)
                {
                    GameObject go = new GameObject("Dust static arc " + i); go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetParent(owner.transform, false);
                    LineRenderer line = go.AddComponent<LineRenderer>(); arcs[i] = line; line.sharedMaterial = material;
                    line.useWorldSpace = false; line.positionCount = i == 0 ? MainPoints : (i <= PrimaryBranches ? 5 : 4);
                    line.startWidth = i == 0 ? 0.070f : (i <= PrimaryBranches ? 0.035f : 0.022f);
                    line.endWidth = i == 0 ? 0.016f : (i <= PrimaryBranches ? 0.012f : 0.008f);
                    line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                    line.lightProbeUsage = LightProbeUsage.Off; line.reflectionProbeUsage = ReflectionProbeUsage.Off; line.enabled = false;
                }
            }
            catch { Clear(); throw; }
        }
        private static void Event(Camera view)
        {
            Ensure();
            audibleDischarge = source != null && Plugin.Value(Plugin.DischargeVolume, 1) > 0;
            // Roll once, then place from the final rendered camera on PreCull.
            // Chase cameras can override matrices without moving their transform.
            favourView = random.NextDouble() < 0.75;
            screenX = Range(0.24f, 0.76f); screenY = Range(0.55f, 0.70f);
            depth = Range(24, 38); angle = Range(-(float)Math.PI, (float)Math.PI);
            height = Range(4.16f, 8.32f); pendingPlacement = true;
        }
        internal static void AlignView(Camera view)
        {
            if (!probedChase)
            {
                probedChase = true;
                Type chase = AccessTools.TypeByName("ApocaChaseCamera.ChaseView");
                if (chase != null) chaseApply = AccessTools.Method(chase, "Apply", new Type[] { typeof(Camera) });
            }
            // Apply is idempotent for an already-rendered camera. This optional
            // bridge avoids depending on the plugins' PreCull subscription order.
            if (chaseApply != null)
            {
                try { chaseApply.Invoke(null, new object[] { view }); }
                catch { chaseApply = null; Plugin.Log.LogWarning("Lightning camera bridge unavailable; using the current camera projection."); }
            }
        }
        private static void Place(Camera view)
        {
            AlignView(view);
            Ray centre = RenderRay(view, 0.5f, 0.5f);
            Vector3 forward = centre.direction; forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward = forward.normalized; Vector3 right = new Vector3(forward.z, 0, -forward.x);
            owner.transform.position = favourView ? RenderRay(view, screenX, screenY).GetPoint(depth) :
                centre.origin + (forward * (float)Math.Cos(angle) + right * (float)Math.Sin(angle)) * depth + Vector3.up * Range(3, 7);
            model.SetDistance(Vector3.Distance(owner.transform.position, centre.origin));
            source.panStereo = favourView ? (screenX - 0.5f) * 1.4f : Mathf.Clamp((float)Math.Sin(angle), -0.35f, 0.35f);
            pendingPlacement = false;
            if (arcs == null) return;
            Vector3[] main = new Vector3[MainPoints];
            for (int i = 0; i < main.Length; i++) main[i] = right * Range(-0.55f, 0.55f) + Vector3.up * (height * (i / (float)(MainPoints - 1) - 0.5f)) + forward * Range(-0.35f, 0.35f);
            arcs[0].SetPositions(main);
            Vector3[][] branches = new Vector3[PrimaryBranches][];
            for (int branch = 0; branch < PrimaryBranches; branch++)
            {
                // Stagger roots along the trunk, then vary reach and slope so
                // the extra branches do not form a regular ladder.
                int root = 1 + (int)((branch + Range(0.1f, 0.9f)) * (MainPoints - 2) / PrimaryBranches);
                float side = branch % 2 == 0 ? 1 : -1;
                if (random.NextDouble() < 0.20) side = -side;
                branches[branch] = Branch(main[root], right, forward, height * Range(0.18f, 0.38f), side, Range(-0.35f, 0.60f), 5);
                arcs[1 + branch].SetPositions(branches[branch]);
            }
            for (int fork = 0; fork < SecondaryForks; fork++)
            {
                Vector3[] parent = branches[fork * 2 + random.Next(2)];
                Vector3 origin = parent[random.Next(2, parent.Length - 1)];
                arcs[1 + PrimaryBranches + fork].SetPositions(Branch(origin, right, forward,
                    height * Range(0.09f, 0.17f), random.Next(2) == 0 ? 1 : -1, Range(-0.50f, 0.60f), 4));
            }
        }
        private static Vector3[] Branch(Vector3 origin, Vector3 right, Vector3 forward, float length, float side, float slope, int count)
        {
            Vector3[] points = new Vector3[count]; points[0] = origin;
            for (int i = 1; i < count; i++)
            {
                float t = i / (float)(count - 1), jagged = (float)Math.Sin(Math.PI * t);
                points[i] = origin + right * (side * length * t + Range(-0.10f, 0.10f) * length * jagged)
                    + Vector3.up * (slope * length * t + Range(-0.08f, 0.08f) * length * jagged)
                    + forward * (Range(-0.08f, 0.08f) * length * jagged);
            }
            return points;
        }
        internal static Ray RenderRay(Camera view, float x, float y)
        {
            // Explicitly unproject through the rendered view matrix, so a
            // transform-only ray implementation cannot place behind the chase view.
            Matrix4x4 toWorld = view.worldToCameraMatrix.inverse, projection = view.projectionMatrix;
            Vector3 local = new Vector3((2 * x - 1 + projection.m02) / projection.m00,
                (2 * y - 1 + projection.m12) / projection.m11, -1);
            return new Ray(toWorld.MultiplyPoint3x4(Vector3.zero), toWorld.MultiplyVector(local).normalized);
        }
        internal static void Tick(Camera view, float dt, float strength, bool stormActive, bool preview, bool clockSkipped)
        {
            if (!Plugin.DustLightningEnabled || view == null || strength <= 0.001f) { Suspend(); model.Reset(); return; }
            if (clockSkipped) { Suspend(); model.Reset(); return; }
            bool triggered = model.Step(dt, stormActive && strength >= 0.70f, random.NextDouble);
            if (preview && strength >= 0.15f && model.Preview(random.NextDouble())) triggered = true;
            if (triggered) Event(view);
            float volume = Mathf.Clamp(Plugin.Value(Plugin.DischargeVolume, 1), 0, 2);
            if (volume <= 0) { audibleDischarge = false; if (source != null) source.Stop(); }
            if (model.TakeSound() && source != null && volume > 0)
                source.PlayOneShot(clips[random.Next(clips.Length)], 0.85f * volume * Range(0.85f, 1));
        }
        internal static void PreCull(Camera camera)
        {
            bool gameplay = StormRunner.CanRender && camera == StormRunner.View;
            if (gameplay && pendingPlacement) Place(camera);
            bool draw = gameplay && Flare > 0.001f;
            // Only light geometry near this strike during the gameplay draw.
            // Fog, sky, ambient light and the HUD keep their storm baseline.
            if (strikeLight != null)
            {
                strikeLight.intensity = draw ? 8 * Flare * (1 + 0.5f * NativeStormClock.NightStrength) : 0;
                strikeLight.enabled = draw;
                if (draw) strikeLight.cullingMask = camera.cullingMask;
            }
            Color colour = new Color(0.65f, 0.74f, 0.86f, Mathf.Clamp01(Flare * 0.30f * (1 + 0.25f * NativeStormClock.NightStrength)));
            if (arcs != null) foreach (LineRenderer line in arcs) if (line != null) { line.startColor = colour; line.endColor = colour; line.enabled = draw; }
        }
        internal static void Hide()
        {
            if (strikeLight != null) { strikeLight.enabled = false; strikeLight.intensity = 0; }
            if (arcs != null) foreach (LineRenderer line in arcs) if (line != null) line.enabled = false;
        }
        internal static void Suspend() { pendingPlacement = false; audibleDischarge = false; model.CancelPulse(); Hide(); if (source != null) source.Stop(); }
        internal static void Clear()
        {
            Suspend(); model.Reset();
            if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
            if (clips != null) foreach (AudioClip clip in clips) if (clip != null) UnityEngine.Object.Destroy(clip);
            if (material != null) UnityEngine.Object.Destroy(material);
            owner = null; source = null; clips = null; arcs = null; material = null; strikeLight = null;
        }
    }
}
