using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace ApocaDustStorm
{
    // Small camera-following volume, not a full-screen overlay or camera shake.
    // Relative simulation avoids inheriting vehicle velocity/origin translations.
    internal sealed class StormNearDust : IDisposable
    {
        private readonly GameObject owner;
        private readonly ParticleSystem system;
        private readonly ParticleSystemRenderer renderer;
        private readonly Material material;
        private readonly System.Random random = new System.Random();
        private float budget;
        private bool visible;
        internal StormNearDust(Transform parent, Texture2D texture)
        {
            owner = new GameObject("ApocaDustStorm.NearDust"); owner.hideFlags = HideFlags.HideAndDontSave; owner.transform.SetParent(parent, false);
            try
            {
                // An unlit alpha material avoids the legacy particle shader's
                // extra built-in fog contribution washing nearby dust white.
                Shader shader = Shader.Find("Sprites/Default"); if (shader == null || !shader.isSupported) shader = Shader.Find("UI/Default");
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("No near-dust shader");
                material = new Material(shader); material.hideFlags = HideFlags.HideAndDontSave;
                material.mainTexture = texture; material.SetColor("_Color", Color.white); material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual); material.renderQueue = 3001;
                system = owner.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 96;
                main.startSpeed = 0; main.loop = true; main.playOnAwake = false; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                ParticleSystem.EmissionModule emission = system.emission; emission.enabled = false;
                ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false;
                ParticleSystem.CollisionModule collision = system.collision; collision.enabled = false;
                ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime; color.enabled = true;
                Gradient gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0.7f, 0.65f), new GradientAlphaKey(0, 1) });
                color.color = new ParticleSystem.MinMaxGradient(gradient);
                renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.025f; renderer.lengthScale = 1.8f; renderer.maxParticleSize = 0.06f;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false; system.Play();
            }
            catch { Dispose(); throw; }
        }
        private float Range(float from, float to) { return from + (float)random.NextDouble() * (to - from); }
        internal void Tick(Vector3 eye, float dt, float strength, float gust, StormModel model, bool entered)
        {
            float amount = Mathf.Clamp(Plugin.Value(Plugin.MicroDust, 1), 0, 2) * Mathf.Clamp(Plugin.Value(Plugin.Dust, 1), 0, 2);
            Vector3 actualEye = StormRunner.View != null ? StormView.Eye(StormRunner.View) : eye;
            bool inCab = PlayerStormHazards.InVehicle && StormRunner.OriginAnchor != null && (actualEye - StormRunner.OriginAnchor.position).sqrMagnitude < 4;
            visible = Plugin.Active && amount > 0 && strength > 0.02f && !PlayerStormHazards.Sheltered && !inCab;
            if (!visible) { Clear(); return; }
            owner.transform.position = eye; owner.transform.rotation = Quaternion.identity;
            int occupied = system.particleCount;
            float impact = Mathf.Clamp01(StormRunner.ImpactStrength);
            budget = DustEmissionBudget.Add(budget, 26 * amount * strength * (1 + impact), dt, occupied, 96, 12);
            int count = Mathf.Min((int)budget, Mathf.Max(0, 96 - occupied)); budget -= count;
            if (entered) count = Mathf.Min(96 - occupied, count + 18);
            Color colour = StormFog.DustColour(RenderSettings.fogColor, StormRunner.LightingStrength, NativeStormClock.NightStrength);
            colour.a = 0.14f + 0.1f * impact;
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ), cross = new Vector3(-wind.z, 0, wind.x);
            float speed = Mathf.Lerp(7, 16, Mathf.Clamp01(gust));
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams();
                particle.position = -wind * Range(3, 7) + cross * Range(-6, 6) + Vector3.up * Range(-2, 3);
                particle.velocity = wind * speed + Vector3.up * Range(-0.5f, 1);
                particle.startSize = Range(0.035f, entered ? 0.22f : 0.12f); particle.startLifetime = Range(0.45f, 0.9f); particle.startColor = colour;
                system.Emit(particle, 1);
            }
        }
        internal void PreCull(Camera camera)
        {
            if (renderer == null) return;
            renderer.enabled = visible && StormRunner.CanRender && camera == StormRunner.View;
            if (renderer.enabled && (PlayerStormHazards.Sheltered || (PlayerStormHazards.InVehicle && StormRunner.OriginAnchor != null && (StormView.Eye(camera) - StormRunner.OriginAnchor.position).sqrMagnitude < 4))) renderer.enabled = false;
            if (renderer.enabled) owner.transform.position = StormView.Eye(camera);
        }
        internal void Hide() { if (renderer != null) renderer.enabled = false; }
        internal void Clear() { budget = 0; visible = false; Hide(); if (system != null) system.Clear(); }
        public void Dispose() { Clear(); if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); } if (material != null) UnityEngine.Object.Destroy(material); }
    }
}
