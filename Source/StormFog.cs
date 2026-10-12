using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ApocaDustStorm
{
    internal static class StormFog
    {
        internal static readonly Color Sand = new Color(0.54f, 0.43f, 0.29f, 1);
        internal static readonly Color NightSand = new Color(0.32f, 0.215f, 0.12f, 1);
        private sealed class Snapshot
        {
            internal Camera Camera;
            internal bool Enabled, NightOnly;
            internal FogMode Mode;
            internal float Density, Start, End, EnviroDistance, EnviroMax;
            internal Color Color;
            internal Vector4 Weather;
            internal Vector4 SceneParams, SceneMode, HeightParams, DistanceParams;
            internal EnviroFogSettings Settings;
            internal float NativeStart;
            internal StormLighting.Snapshot Lighting;
            internal AzureAtmosphere.Snapshot Azure;
            internal bool LightingApplied, AzureApplied;
        }
        private static readonly Stack<Snapshot> stack = new Stack<Snapshot>();
        private static readonly Stack<Snapshot> pool = new Stack<Snapshot>();
        internal static bool HasOverrides { get { return stack.Count > 0; } }
        internal static Color DustColour(Color native, float strength, float night)
        {
            float light = Mathf.Max(native.r, Mathf.Max(native.g, native.b));
            float sky = Mathf.Lerp(StormLighting.SkyFactor(strength), StormLighting.SkyFactor(1),
                (float)HorizonMath.NightBlend(strength, night));
            float dayLight = Mathf.Clamp(light * 1.5f, 0.12f, 1);
            // Night haze has its own warm brown palette. Native fog RGB may be
            // white or nearly black; neither should wash out or erase its colour.
            // This tints fog only, without changing world-light intensities.
            Color sand = Color.Lerp(Sand * dayLight, NightSand, Mathf.Clamp01(night)) * sky; sand.a = 1;
            return sand;
        }
        internal static Vector4 NightWeather(Vector4 native, Color dust, float strength, float night)
        {
            // Change colour only: retain the native alpha and the existing fog
            // attenuation/opacity ramp instead of making night storms denser sooner.
            float amount = (float)HorizonMath.NightBlend(strength, night);
            return new Vector4(Mathf.Lerp(native.x, dust.r, amount), Mathf.Lerp(native.y, dust.g, amount),
                Mathf.Lerp(native.z, dust.b, amount), native.w);
        }
        internal static Vector4 BlendWeather(Vector4 native, Color dust, float opacity, float blend)
        {
            // Blend colour contributions with their weights. Native weather RGB
            // can be bright even when its alpha is zero; straight RGB blending
            // would briefly brighten a night storm as that alpha increases.
            float weight = Mathf.Lerp(native.w, opacity, blend);
            if (weight <= 0.000001f) return native;
            float a = native.w * (1 - blend), b = opacity * blend;
            return new Vector4((native.x * a + dust.r * b) / weight,
                (native.y * a + dust.g * b) / weight, (native.z * a + dust.b * b) / weight, weight);
        }
        internal static float EffectiveDensity(bool enabled, FogMode mode, float density, float start, float end, float strength, float gust)
        {
            float target = (float)(StormModel.FogDensity(strength * strength, Plugin.StormVisibility) * WindMath.Haze(gust, Plugin.Value(Plugin.Gusts, 1)));
            float baseline = density;
            if (mode == FogMode.Linear) baseline = -Mathf.Log(Mathf.Clamp01((end - 100) / Mathf.Max(1, end - start)) + 0.00001f) / 100;
            else if (mode == FogMode.ExponentialSquared) baseline = density * density * 100;
            return Mathf.Max(enabled ? Mathf.Max(0, baseline) : 0, target);
        }
        internal static bool Push(Camera camera, Material azureMaterial = null)
        {
            if (!StormRunner.CanRender || camera == null || camera != StormRunner.View) return false;
            float night = NativeStormClock.NightStrength;
            bool nightOnly=StormRunner.Strength<0.001f;
            if (nightOnly && night<=0) return false;
            Snapshot s = pool.Count > 0 ? pool.Pop() : new Snapshot();
            s.Camera=camera; s.NightOnly=nightOnly;
            s.Color=RenderSettings.fogColor; s.Weather=Shader.GetGlobalVector("_weatherFogMod");
            if (nightOnly)
            {
                stack.Push(s);
                try
                {
                    if (stack.Count==1)
                    {
                        RenderSettings.fogColor=NightFogColours.Colour(s.Color,night);
                        Shader.SetGlobalVector("_weatherFogMod",NightFogColours.Weather(s.Weather,night));
                    }
                    return true;
                }
                catch { Pop(); throw; }
            }
            s.Camera = camera; s.Enabled = RenderSettings.fog; s.Mode = RenderSettings.fogMode;
            s.Density = RenderSettings.fogDensity; s.Start = RenderSettings.fogStartDistance; s.End = RenderSettings.fogEndDistance;
            s.Color = RenderSettings.fogColor; s.Weather = Shader.GetGlobalVector("_weatherFogMod");
            s.EnviroDistance = Shader.GetGlobalFloat("_distanceFogIntensity"); s.EnviroMax = Shader.GetGlobalFloat("_maximumFogDensity");
            s.Settings = null; s.LightingApplied = s.AzureApplied = false;
            s.SceneParams = Shader.GetGlobalVector("_SceneFogParams"); s.SceneMode = Shader.GetGlobalVector("_SceneFogMode");
            s.HeightParams = Shader.GetGlobalVector("_HeightParams"); s.DistanceParams = Shader.GetGlobalVector("_DistanceParams");
            EnviroSkyLite sky = EnviroSkyLite.instance;
            if (sky != null && sky.fogSettings != null) { s.Settings = sky.fogSettings; s.NativeStart = s.Settings.startDistance; }
            // Nested image passes share the outer lighting, rather than dimming it again.
            bool outer = stack.Count == 0;
            stack.Push(s);
            try
            {
            if (!outer) return true;
            float strength = StormRunner.Strength;
            float blend = (float)StormModel.Smooth(strength);
            if (outer && (Plugin.Value(Plugin.Darkness, 1) > 0 || Plugin.Value(Plugin.Headlights, 1) > 0))
            {
                if (s.Lighting == null) s.Lighting = new StormLighting.Snapshot(); else s.Lighting.Capture();
                s.LightingApplied = true; s.Lighting.Apply(StormRunner.LightingStrength);
            }
            if (s.Azure == null) s.Azure = new AzureAtmosphere.Snapshot(azureMaterial); else s.Azure.Capture(azureMaterial);
            s.AzureApplied = true; s.Azure.Apply(strength);
            float gust = (float)StormRunner.Gust;
            float densityTarget = EffectiveDensity(s.Enabled, s.Mode, s.Density, s.Start, s.End, strength, gust);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = densityTarget;
            // White native fog is a daytime setting, not a measure of current
            // night light. Correct its colour before distance dust reveals it.
            Color sand = DustColour(s.Color, StormRunner.LightingStrength, night);
            // Retain the accepted daytime mix; at night, fully replace stale
            // white RGB once the handoff completes instead of retaining 5% white.
            RenderSettings.fogColor = Color.Lerp(NightFogColours.Colour(s.Color,night), sand, (float)HorizonMath.ColourBlend(Mathf.Max(strength, StormRunner.PaletteStrength), night) * Mathf.Lerp(0.95f, 1, night));
            Shader.SetGlobalVector("_weatherFogMod", BlendWeather(NightWeather(NightFogColours.Weather(s.Weather,night), sand, strength, night), sand, 0.95f, blend));
            Shader.SetGlobalFloat("_distanceFogIntensity", Mathf.Lerp(s.EnviroDistance, Mathf.Max(s.EnviroDistance, 1), blend));
            // Enviro's shader stores ONE MINUS maximum density (verified in SetMaterialsVariables).
            Shader.SetGlobalFloat("_maximumFogDensity", Mathf.Lerp(s.EnviroMax, Mathf.Min(s.EnviroMax, 0.005f), blend));
            // RenderFog reads this setting into _DistanceParams *after* its prefix.
            // Close the clear near-distance zone progressively, including while driving.
            if (s.Settings != null) s.Settings.startDistance = Mathf.Lerp(s.NativeStart, 0, (float)StormModel.Smooth(strength * 3));
            // World shaders need the same current fog parameters before drawing,
            // including chase cameras without an Enviro image-effect component.
            float density = RenderSettings.fogDensity;
            Shader.SetGlobalVector("_SceneFogParams", new Vector4(density * 1.2011224f, density * 1.442695f, 0, 0));
            Shader.SetGlobalVector("_SceneFogMode", new Vector4((float)FogMode.Exponential, s.SceneMode.y, 0, 0));
            float start = s.Settings != null ? -s.Settings.startDistance : Mathf.Lerp(s.DistanceParams.x, 0, (float)StormModel.Smooth(strength * 3));
            Shader.SetGlobalVector("_DistanceParams", new Vector4(start, s.DistanceParams.y, s.DistanceParams.z, s.DistanceParams.w));
            return true;
            }
            catch { Pop(); throw; }
        }
        private static void Pop()
        {
            if (stack.Count == 0) return;
            Snapshot s = stack.Pop();
            if (s.NightOnly)
            {
                RenderSettings.fogColor=s.Color; Shader.SetGlobalVector("_weatherFogMod",s.Weather);
                s.Camera=null; pool.Push(s); return;
            }
            if (s.LightingApplied) s.Lighting.Restore();
            if (s.AzureApplied) s.Azure.Restore();
            if (s.Settings != null) s.Settings.startDistance = s.NativeStart;
            RenderSettings.fog = s.Enabled; RenderSettings.fogMode = s.Mode; RenderSettings.fogDensity = s.Density;
            RenderSettings.fogStartDistance = s.Start; RenderSettings.fogEndDistance = s.End; RenderSettings.fogColor = s.Color;
            Shader.SetGlobalVector("_weatherFogMod", s.Weather); Shader.SetGlobalFloat("_distanceFogIntensity", s.EnviroDistance);
            Shader.SetGlobalFloat("_maximumFogDensity", s.EnviroMax);
            Shader.SetGlobalVector("_SceneFogParams", s.SceneParams); Shader.SetGlobalVector("_SceneFogMode", s.SceneMode);
            Shader.SetGlobalVector("_HeightParams", s.HeightParams); Shader.SetGlobalVector("_DistanceParams", s.DistanceParams);
            s.Camera = null; pool.Push(s);
        }
        internal static void PreCull(Camera camera) { StormEntryShake.PreCull(camera); StormView.Capture(camera); StormDistanceFog.PreCull(camera); Push(camera); StormHorizon.PreCull(camera); DustLightning.PreCull(camera); WindblownLizards.PreCull(camera); }
        internal static void PostRender(Camera camera) { DustLightning.Hide(); StormHorizon.Hide(); if (stack.Count > 0 && stack.Peek().Camera == camera) Pop(); StormEntryShake.PostRender(camera); }
        internal static void Restore() { while (stack.Count > 0) Pop(); }
        internal static void Clear() { Restore(); pool.Clear(); }
        internal static void FinishPass(bool pushed) { if (pushed) Pop(); }
    }
    // Enviro reads RenderSettings again during its image effect, after onPostRender.
    // Give that pass the same dust, then restore even if it throws.
    [HarmonyPatch(typeof(EnviroSkyRenderingLW), "RenderFog")]
    internal static class StormEnviroFogPatch
    {
        private static void Prefix(EnviroSkyRenderingLW __instance, ref bool __state)
        { __state = StormFog.Push(__instance.GetComponent<Camera>()); }
        private static Exception Finalizer(Exception __exception, bool __state)
        { StormFog.FinishPass(__state); return __exception; }
    }
    [HarmonyPatch(typeof(UnityEngine.AzureSky.AzureFogScattering), "OnRenderImage")]
    internal static class StormAzureFogPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo native=AccessTools.Method(typeof(Transform),"TransformVector",new[]{typeof(Vector3)});
            MethodInfo corrected=AccessTools.Method(typeof(StormView),"FogVector");
            int matches=0;List<CodeInstruction> result=new List<CodeInstruction>();
            foreach(CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(native)) { instruction.opcode=OpCodes.Call;instruction.operand=corrected;matches++; }
                result.Add(instruction);
            }
            if(matches!=4)throw new InvalidOperationException("Azure fog camera-ray layout changed; expected four native frustum transforms.");
            return result;
        }
        private static void Prefix(UnityEngine.AzureSky.AzureFogScattering __instance, ref bool __state)
        { __state = StormFog.Push(__instance.GetComponent<Camera>(), __instance.fogScatteringMaterial); }
        private static Exception Finalizer(Exception __exception, bool __state)
        { StormFog.FinishPass(__state); return __exception; }
    }
}
