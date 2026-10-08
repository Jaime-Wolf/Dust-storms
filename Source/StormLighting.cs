using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    // Camera-scoped changes leave Enviro's day/night cycle and weather presets intact.
    internal static class StormLighting
    {
        private static Light[] directional;
        private static Light[] headlights;
        private static bool needsEnvironmentRefresh;
        private static readonly string[] skyColors = { "_SkyColor", "_HorizonColor", "_SunColor", "_MoonColor",
            "_CloudColor", "_FlatCloudsLightColor", "_FlatCloudsAmbientColor" };
        private static readonly string[] skyFloats = { "_StarsIntensity", "_CloudsExposure" };
        internal static void Scan()
        {
            List<Light> found = new List<Light>(), vehicleSpots = new List<Light>();
            foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                if (light == null) continue;
                if (light.type == LightType.Directional) found.Add(light);
                else if (light.type == LightType.Spot && IsVehicleSpot(light))
                    vehicleSpots.Add(light);
            }
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional && !found.Contains(RenderSettings.sun))
                found.Add(RenderSettings.sun);
            directional = found.ToArray();
            headlights = vehicleSpots.ToArray();
        }
        internal static bool Recover(bool calm)
        {
            // Never bake the temporary dusty sky, or overwrite a live camera
            // scope. LateUpdate calls this only after the final clearing fade.
            if (!calm || !needsEnvironmentRefresh || StormFog.HasOverrides) return false;
            needsEnvironmentRefresh = false;
            try { DynamicGI.UpdateEnvironment(); return true; }
            catch (Exception e) { Plugin.Log.LogWarning("Native ambient refresh skipped: " + e.Message); return false; }
        }
        internal static void Clear(bool recover = false)
        {
            if (recover) Recover(true);
            directional = null; headlights = null; needsEnvironmentRefresh = false;
        }
        private static bool IsVehicleSpot(Light light)
        {
            if (light.GetComponentInParent<NWH.VehiclePhysics2.VehicleController>() != null) return true;
            for (Transform t = light.transform.parent; t != null; t = t.parent)
                if (t.Find("DriveTrigger") != null && t.GetComponentInChildren<NWH.VehiclePhysics2.VehicleController>(true) != null) return true;
            return false;
        }
        private static float Amount(float strength)
        { return Mathf.Clamp01(strength) * Mathf.Clamp01(Plugin.Value(Plugin.Darkness, 1) * 0.8f); }
        internal static float SkyFactor(float strength) { return 1 - 0.45f * Amount(strength); }
        private static Color Dim(Color color, float factor)
        { return new Color(color.r * factor, color.g * factor, color.b * factor, color.a); }

        internal sealed class Snapshot
        {
            private readonly Light[] lights;
            private readonly float[] intensities;
            private readonly Light[] spots;
            private readonly float[] spotIntensities, spotRanges;
            private readonly float ambientIntensity;
            private readonly Color ambient, sky, equator, ground;
            private readonly Material skybox;
            private readonly Color[] colors;
            private readonly float[] floats;
            private readonly bool[] hasColors, hasFloats;
            private readonly Vector4 weatherSky, enviroLight;
            internal Snapshot()
            {
                if (directional == null) Scan();
                lights = directional; intensities = new float[lights.Length];
                spots = headlights; spotIntensities = new float[spots.Length]; spotRanges = new float[spots.Length];
                for (int i = 0; i < spots.Length; i++) if (spots[i] != null)
                { spotIntensities[i] = spots[i].intensity; spotRanges[i] = spots[i].range; }
                ambientIntensity = RenderSettings.ambientIntensity; ambient = RenderSettings.ambientLight;
                sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor; ground = RenderSettings.ambientGroundColor;
                skybox = RenderSettings.skybox;
                colors = new Color[skyColors.Length]; floats = new float[skyFloats.Length];
                hasColors = new bool[colors.Length]; hasFloats = new bool[floats.Length];
                weatherSky = Shader.GetGlobalVector("_weatherSkyMod"); enviroLight = Shader.GetGlobalVector("_EnviroLighting");
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null) intensities[i] = lights[i].intensity;
                if (skybox != null)
                {
                    for (int i = 0; i < colors.Length; i++) if (skybox.HasProperty(skyColors[i]))
                    { hasColors[i] = true; colors[i] = skybox.GetColor(skyColors[i]); }
                    for (int i = 0; i < floats.Length; i++) if (skybox.HasProperty(skyFloats[i]))
                    { hasFloats[i] = true; floats[i] = skybox.GetFloat(skyFloats[i]); }
                }
            }
            internal void Apply(float strength)
            {
                // Retain diffuse daylight instead of turning a noon storm into night.
                float amount = Amount(strength), directFactor = 1 - 0.65f * amount;
                float ambientFactor = 1 - 0.45f * amount, skyFactor = SkyFactor(strength);
                if (amount > 0) needsEnvironmentRefresh = true;
                float tint = 0.9f * (float)StormModel.Smooth(amount);
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = intensities[i] * directFactor;
                float assist = (float)StormModel.Smooth(strength) * Mathf.Clamp(Plugin.Value(Plugin.Headlights, 1), 0, 2);
                for (int i = 0; i < spots.Length; i++) if (spots[i] != null && spots[i].isActiveAndEnabled)
                { spots[i].intensity = spotIntensities[i] * (1 + 0.35f * assist); spots[i].range = spotRanges[i] * (1 + 0.2f * assist); }
                RenderSettings.ambientIntensity = ambientIntensity * ambientFactor;
                RenderSettings.ambientLight = Dim(ambient, ambientFactor);
                RenderSettings.ambientSkyColor = Dim(sky, ambientFactor);
                RenderSettings.ambientEquatorColor = Dim(equator, ambientFactor);
                RenderSettings.ambientGroundColor = Dim(ground, ambientFactor);
                // Keep the generated ambient probe entirely native. GI can
                // complete a new sky/daylight readback during this draw; restoring
                // an old SH snapshot would overwrite that newer result. The
                // native ambient intensity/colors supply temporary darkening.
                if (skybox != null)
                {
                    for (int i = 0; i < colors.Length; i++) if (hasColors[i])
                    {
                        Color dimmed = Dim(colors[i], skyFactor);
                        if (skyColors[i] != "_SunColor" && skyColors[i] != "_MoonColor")
                        {
                            float brightness = Mathf.Max(dimmed.r, Mathf.Max(dimmed.g, dimmed.b));
                            Color dustSky = StormFog.Sand * (brightness * 1.2f); dustSky.a = dimmed.a;
                            dimmed = Color.Lerp(dimmed, dustSky, tint);
                        }
                        skybox.SetColor(skyColors[i], dimmed);
                    }
                    for (int i = 0; i < floats.Length; i++) if (hasFloats[i]) skybox.SetFloat(skyFloats[i], floats[i] * skyFactor);
                }
                // Enviro's atmospheric shader also uses these global colours.
                Color nativeFog = RenderSettings.fogColor;
                float fogLight = Mathf.Clamp(Mathf.Max(nativeFog.r, Mathf.Max(nativeFog.g, nativeFog.b)) * 1.5f, 0, 1);
                Color skyDust = StormFog.Sand * (fogLight * skyFactor);
                Shader.SetGlobalVector("_weatherSkyMod", StormFog.BlendWeather(new Vector4(weatherSky.x * skyFactor,
                    weatherSky.y * skyFactor, weatherSky.z * skyFactor, weatherSky.w), skyDust, Mathf.Max(weatherSky.w, 0.95f), tint));
                Shader.SetGlobalVector("_EnviroLighting", new Vector4(enviroLight.x * directFactor, enviroLight.y * directFactor, enviroLight.z * directFactor, enviroLight.w));
            }
            internal void Restore()
            {
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = intensities[i];
                for (int i = 0; i < spots.Length; i++) if (spots[i] != null)
                { spots[i].intensity = spotIntensities[i]; spots[i].range = spotRanges[i]; }
                RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                if (skybox != null)
                {
                    for (int i = 0; i < colors.Length; i++) if (hasColors[i]) skybox.SetColor(skyColors[i], colors[i]);
                    for (int i = 0; i < floats.Length; i++) if (hasFloats[i]) skybox.SetFloat(skyFloats[i], floats[i]);
                }
                Shader.SetGlobalVector("_weatherSkyMod", weatherSky); Shader.SetGlobalVector("_EnviroLighting", enviroLight);
            }
        }
    }
}
