using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace ApocaDustStorm
{
    // Bounded translucent beam approximation for the game's built-in renderer.
    // No volumetric raymarch, additional depth texture, light or physics query.
    internal sealed class StormLightScatter : IDisposable
    {
        private const int Beams = 4, Slices = 8, Rings = 4, PerBeam = Slices * Rings;
        private readonly GameObject owner;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly MeshRenderer renderer;
        private readonly Vector3[] vertices = new Vector3[Beams * PerBeam];
        private readonly Vector3[] local = new Vector3[Beams * PerBeam];
        private readonly Color[] colours = new Color[Beams * PerBeam];
        private readonly Light[] chosen = new Light[Beams];
        private Light[] handheld;
        private GameObject player;
        private float clock, nextHandheld;
        private bool visible;
        private Vector3 position;
        internal StormLightScatter(Transform parent)
        {
            try
            {
                Shader shader = Shader.Find("Sprites/Default"); if (shader == null || !shader.isSupported) shader = Shader.Find("UI/Default");
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("No scatter shader");
                owner = new GameObject("ApocaDustStorm.LightScatter"); owner.hideFlags = HideFlags.HideAndDontSave; owner.transform.SetParent(parent, false);
                material = new Material(shader); material.hideFlags = HideFlags.HideAndDontSave; material.mainTexture = Texture2D.whiteTexture;
                material.SetColor("_Color", Color.white); material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual); material.renderQueue = 3002;
                mesh = new Mesh(); mesh.name = "ApocaDustStorm.FourDustBeams"; mesh.hideFlags = HideFlags.HideAndDontSave; mesh.MarkDynamic();
                int[] triangles = new int[Beams * (Rings - 1) * Slices * 6]; int n = 0;
                for (int beam = 0; beam < Beams; beam++) for (int ring = 0; ring < Rings - 1; ring++) for (int slice = 0; slice < Slices; slice++)
                {
                    int a = beam * PerBeam + ring * Slices + slice, b = beam * PerBeam + ring * Slices + (slice + 1) % Slices, c = a + Slices, d = b + Slices;
                    triangles[n++] = a; triangles[n++] = c; triangles[n++] = b; triangles[n++] = b; triangles[n++] = c; triangles[n++] = d;
                }
                mesh.vertices = vertices; mesh.colors = colours; mesh.triangles = triangles;
                owner.AddComponent<MeshFilter>().sharedMesh = mesh; renderer = owner.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; renderer.enabled = false;
            }
            catch { Dispose(); throw; }
        }
        private void Add(Light light, Vector3 eye, ref int count)
        {
            if (count >= Beams || light == null || !light.isActiveAndEnabled || light.type != LightType.Spot || light.intensity <= 0 || (light.transform.position - eye).sqrMagnitude > 900) return;
            for (int i = 0; i < count; i++) if (chosen[i] == light) return;
            chosen[count++] = light;
        }
        internal void Tick(Vector3 eye, float dt, float strength)
        {
            float amount = Mathf.Clamp(Plugin.Value(Plugin.LightScatter, 1), 0, 2);
            visible = Plugin.Active && strength > 0.25f && amount > 0 && !PlayerStormHazards.Sheltered;
            if (!visible) { Hide(); return; }
            clock += dt; if (clock < 0.1f) return; clock = 0;
            GameObject current = PlayerStormHazards.Player;
            if (player != current) { player = current; handheld = null; nextHandheld = 0; }
            if (!PlayerStormHazards.InVehicle && player != null && Time.unscaledTime >= nextHandheld)
            { nextHandheld = Time.unscaledTime + 2; handheld = player.GetComponentsInChildren<Light>(true); }
            int count = 0; Light[] headlights = StormLighting.HeadlightSources;
            if (headlights != null) foreach (Light light in headlights) Add(light, eye, ref count);
            if (!PlayerStormHazards.InVehicle && handheld != null) foreach (Light light in handheld) Add(light, eye, ref count);
            position = eye; owner.transform.position = position;
            Color sand = StormFog.DustColour(RenderSettings.fogColor, StormRunner.LightingStrength, NativeStormClock.NightStrength);
            for (int beam = 0; beam < Beams; beam++)
            {
                Light light = beam < count ? chosen[beam] : null;
                for (int ring = 0; ring < Rings; ring++) for (int slice = 0; slice < Slices; slice++)
                {
                    int index = beam * PerBeam + ring * Slices + slice;
                    if (light == null) { vertices[index] = Vector3.zero; colours[index] = new Color(0, 0, 0, 0); continue; }
                    float t = ring / (float)(Rings - 1), length = Mathf.Min(light.range, 14);
                    float distance = 0.25f + t * Mathf.Max(0, length - 0.25f);
                    float radius = Mathf.Tan(Mathf.Clamp(light.spotAngle, 5, 80) * Mathf.Deg2Rad * 0.5f) * distance;
                    float angle = slice * Mathf.PI * 2 / Slices;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, distance);
                    local[index] = offset;
                    Color colour = Color.Lerp(sand, light.color * 0.55f, 0.25f);
                    colour.a = Mathf.Sin(t * Mathf.PI) * 0.11f * strength * amount * Mathf.Clamp(light.intensity, 0, 1);
                    colours[index] = colour;
                }
            }
            for (int i = count; i < Beams; i++) chosen[i] = null;
            visible = count > 0; mesh.colors = colours;
        }
        internal void PreCull(Camera camera)
        {
            if (renderer == null) return;
            renderer.enabled = visible && !PlayerStormHazards.Sheltered && StormRunner.CanRender && camera == StormRunner.View;
            if (!renderer.enabled) return;
            position = StormView.Eye(camera); owner.transform.position = position;
            // Follow the final native light transforms, including chase-camera
            // LateUpdate and moving vehicles, without new searches or arrays.
            for (int beam = 0; beam < Beams; beam++)
            {
                Light light = chosen[beam];
                bool active = light != null && light.isActiveAndEnabled && light.intensity > 0;
                for (int i = 0; i < PerBeam; i++)
                {
                    int index = beam * PerBeam + i;
                    vertices[index] = !active ? Vector3.zero : light.transform.position - position + light.transform.TransformVector(local[index]);
                }
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
        }
        internal void Hide() { if (renderer != null) renderer.enabled = false; }
        internal void ShiftOrigin(Vector3 delta) { position += delta; if (owner != null) owner.transform.position = position; }
        public void Dispose()
        { Hide(); if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); } if (mesh != null) UnityEngine.Object.Destroy(mesh); if (material != null) UnityEngine.Object.Destroy(material); }
    }
}
