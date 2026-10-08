using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ApocaDustStorm
{
    // The installed game's sky and sleep FSM use AzureSky. Override its actual
    // material/global uniforms only for the gameplay camera, then restore them.
    internal static class AzureAtmosphere
    {
        private static readonly string[] floatKeys = { "_Azure_GlobalFogDistance", "_Azure_GlobalFogSmooth", "_Azure_GlobalFogDensity",
            "_Azure_Exposure", "_Azure_Rayleigh", "_Azure_Mie", "_Azure_SunTextureIntensity", "_Azure_MoonTextureIntensity",
            "_Azure_StarsIntensity", "_Azure_MilkyWayIntensity", "_Azure_HeightFogDensity" };
        private static readonly string[] colorKeys = { "_Azure_RayleighColor", "_Azure_MieColor", "_Azure_ScatteringColor",
            "_Azure_DynamicCloudColor1", "_Azure_DynamicCloudColor2", "_Azure_StaticCloudColor" };
        private static readonly FieldInfo skyField = typeof(AzureSkyRenderController).GetField("m_skyMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo fogField = typeof(AzureSkyRenderController).GetField("m_fogMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static Material[] cached;
        private static Material fogTemplate;
        private sealed class OwnedEffect { internal Camera Camera; internal AzureFogScattering Effect; internal Material Material; internal DepthTextureMode Depth; }
        private static readonly List<OwnedEffect> owned = new List<OwnedEffect>();
        private static readonly MethodInfo initialize = typeof(AzureFogScattering).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static void Add(List<Material> list, Material material)
        { if (material != null && !list.Contains(material)) list.Add(material); }
        internal static void Scan()
        {
            StormHorizon.Scan();
            List<Material> list = new List<Material>(); Add(list, RenderSettings.skybox);
            foreach (AzureSkyRenderController sky in UnityEngine.Object.FindObjectsOfType<AzureSkyRenderController>())
            {
                if (skyField != null) Add(list, skyField.GetValue(sky) as Material);
                if (fogField != null) Add(list, fogField.GetValue(sky) as Material);
            }
            cached = list.ToArray();
            foreach (AzureFogScattering effect in UnityEngine.Object.FindObjectsOfType<AzureFogScattering>())
            {
                bool added = false; foreach (OwnedEffect item in owned) if (item.Effect == effect) added = true;
                if (!added && effect.fogScatteringMaterial != null) { fogTemplate = effect.fogScatteringMaterial; break; }
            }
        }
        internal static void EnsureCamera(Camera camera)
        {
            if (camera == null) return;
            AzureFogScattering existing = camera.GetComponent<AzureFogScattering>();
            if (existing == null && fogTemplate != null)
            {
                OwnedEffect item = new OwnedEffect { Camera = camera, Material = new Material(fogTemplate), Depth = camera.depthTextureMode };
                owned.Add(item);
                item.Effect = camera.gameObject.AddComponent<AzureFogScattering>();
                item.Effect.fogScatteringMaterial = item.Material;
                // Runtime-added image effects can render this frame. Start only
                // caches camera references; initialize them before that first draw.
                if (initialize != null) initialize.Invoke(item.Effect, null);
            }
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                OwnedEffect item = owned[i];
                if (item.Camera == null || item.Effect == null)
                { if (item.Material != null) UnityEngine.Object.Destroy(item.Material); owned.RemoveAt(i); continue; }
                item.Effect.enabled = item.Camera == camera && StormRunner.Strength >= 0.001f;
            }
        }
        internal static void Clear()
        {
            foreach (OwnedEffect item in owned)
            {
                if (item.Effect != null) { item.Effect.enabled = false; UnityEngine.Object.Destroy(item.Effect); }
                if (item.Camera != null && item.Camera.depthTextureMode == (item.Depth | DepthTextureMode.Depth))
                    item.Camera.depthTextureMode = item.Depth;
                if (item.Material != null) UnityEngine.Object.Destroy(item.Material);
            }
            owned.Clear(); cached = null; fogTemplate = null;
        }
        private static float ChangeFloat(int index, float native, float strength)
        {
            float blend = (float)StormModel.Smooth(strength), light = StormLighting.SkyFactor(strength);
            if (index == 0)
            {
                // Matched world fog supplies the dust. Do not compress the native
                // pale scattering layer into a nearby white band during approach.
                if (StormHorizon.Available) return native;
                float distance = Mathf.Max(20, Plugin.StormVisibility / (float)WindMath.Haze(StormRunner.Gust, Plugin.Value(Plugin.Gusts, 1)));
                float baseline = native > 0 ? native : 5000;
                return 1 / Mathf.Lerp(1 / Mathf.Max(1, baseline), 1 / distance, blend);
            }
            if (index == 1) return StormHorizon.Available ? native : Mathf.Lerp(native, 0.05f, blend);
            // The storm owns distance fog and the matching dust backdrop. Fade
            // Azure's separate scattering fog out to avoid differently coloured
            // fogged terrain being composited against an untouched sky.
            if (index == 2 || index == 10)
                return StormHorizon.Available ? (float)HorizonMath.NativeFog(native, strength, NativeStormClock.NightStrength) : Mathf.Lerp(native, Mathf.Max(native, 0.995f), blend);
            if (index == 3) return native * light;
            if (index == 4) return native * (1 - 0.8f * blend);
            if (index == 5) return native * (1 + 2 * blend);
            return native * (1 - 0.8f * blend);
        }
        private static Color ChangeColor(Color native, float strength)
        {
            float blend = (float)StormModel.Smooth(strength) * 0.9f;
            float brightness = Mathf.Max(native.r, Mathf.Max(native.g, native.b));
            Color dust = StormFog.Sand * (brightness * 1.2f); dust.a = native.a;
            return Color.Lerp(native, dust, blend);
        }
        private sealed class MaterialState
        {
            internal Material Material;
            internal readonly float[] Floats = new float[floatKeys.Length];
            internal readonly Color[] Colors = new Color[colorKeys.Length];
            internal readonly bool[] HasFloats = new bool[floatKeys.Length], HasColors = new bool[colorKeys.Length];
            internal void Capture(Material material)
            {
                Material = material;
                Array.Clear(HasFloats, 0, HasFloats.Length); Array.Clear(HasColors, 0, HasColors.Length);
                for (int i = 0; i < floatKeys.Length; i++) if (material.HasProperty(floatKeys[i]))
                { HasFloats[i] = true; Floats[i] = material.GetFloat(floatKeys[i]); }
                for (int i = 0; i < colorKeys.Length; i++) if (material.HasProperty(colorKeys[i]))
                { HasColors[i] = true; Colors[i] = material.GetColor(colorKeys[i]); }
            }
            internal void Apply(float strength)
            {
                if (Material == null) return;
                for (int i = 0; i < Floats.Length; i++) if (HasFloats[i]) Material.SetFloat(floatKeys[i], ChangeFloat(i, Floats[i], strength));
                for (int i = 0; i < Colors.Length; i++) if (HasColors[i]) Material.SetColor(colorKeys[i], ChangeColor(Colors[i], strength));
            }
            internal void Restore()
            {
                if (Material == null) return;
                for (int i = 0; i < Floats.Length; i++) if (HasFloats[i]) Material.SetFloat(floatKeys[i], Floats[i]);
                for (int i = 0; i < Colors.Length; i++) if (HasColors[i]) Material.SetColor(colorKeys[i], Colors[i]);
            }
        }
        internal sealed class Snapshot
        {
            private readonly float[] floats = new float[floatKeys.Length];
            private readonly Vector4[] colors = new Vector4[colorKeys.Length];
            private readonly List<Material> list = new List<Material>();
            private readonly List<MaterialState> materials = new List<MaterialState>();
            private int count;
            internal Snapshot(Material imageMaterial) { Capture(imageMaterial); }
            internal void Capture(Material imageMaterial)
            {
                if (cached == null) Scan();
                list.Clear(); foreach (Material material in cached) Add(list, material);
                Add(list, RenderSettings.skybox); Add(list, imageMaterial);
                AzureFogScattering effect = StormRunner.View == null ? null : StormRunner.View.GetComponent<AzureFogScattering>();
                if (effect != null) Add(list, effect.fogScatteringMaterial);
                count = list.Count;
                while (materials.Count < count) materials.Add(new MaterialState());
                for (int i = 0; i < count; i++) materials[i].Capture(list[i]);
                for (int i = count; i < materials.Count; i++) materials[i].Material = null;
                for (int i = 0; i < floats.Length; i++) floats[i] = Shader.GetGlobalFloat(floatKeys[i]);
                for (int i = 0; i < colors.Length; i++) colors[i] = Shader.GetGlobalVector(colorKeys[i]);
            }
            internal void Apply(float strength)
            {
                for (int i = 0; i < count; i++) materials[i].Apply(strength);
                for (int i = 0; i < floats.Length; i++) Shader.SetGlobalFloat(floatKeys[i], ChangeFloat(i, floats[i], strength));
                for (int i = 0; i < colors.Length; i++)
                {
                    Color color = ChangeColor(new Color(colors[i].x, colors[i].y, colors[i].z, colors[i].w), strength);
                    Shader.SetGlobalVector(colorKeys[i], new Vector4(color.r, color.g, color.b, color.a));
                }
            }
            internal void Restore()
            {
                for (int i = 0; i < count; i++) materials[i].Restore();
                for (int i = 0; i < floats.Length; i++) Shader.SetGlobalFloat(floatKeys[i], floats[i]);
                for (int i = 0; i < colors.Length; i++) Shader.SetGlobalVector(colorKeys[i], colors[i]);
            }
        }
    }
}
