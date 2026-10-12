using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    internal sealed class StormVisuals : IDisposable, IDustPickupWorld
    {
        private readonly GameObject root;
        private readonly ParticleSystem dust, ground;
        private readonly HaboobWall wall;
        private readonly StormTerrainSampler frontGround = new StormTerrainSampler();
        private readonly StormNearDust nearDust;
        private readonly StormBillows billows;
        private readonly StormLightScatter scatter;
        private readonly Texture2D texture;
        private readonly Material particles, wood;
        private readonly Mesh branch;
        private readonly StormDebris debris;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly ParticleSystem.Particle[] airborne = new ParticleSystem.Particle[560], scud = new ParticleSystem.Particle[196];
        private readonly DustOccupancy airborneOccupancy = new DustOccupancy(3), scudOccupancy = new DustOccupancy(2);
        private readonly System.Random random = new System.Random();
        private readonly DustPickupSources sources;
        private float retireDistance;
        private float nextBatch, dustBudget, groundBudget, flowClock;
        private Vector3 lastCamera;
        private bool hasCamera;
        internal StormVisuals()
        {
            sources = new DustPickupSources(random);
            root = new GameObject("ApocaDustStorm.Visuals"); root.hideFlags = HideFlags.HideAndDontSave;
            // Construct in a try block so a stripped shader cannot leak half-built objects.
            try
            {
                texture = StormAssets.DustTexture(); particles = StormAssets.ParticleMaterial(texture);
                wood = StormAssets.WoodMaterial();
                branch = StormAssets.Branch();
                dust = Create("Lifting dust", airborne.Length, particles, null, 0);
                ground = Create("Ground dust", scud.Length, particles, null, 0);
                try { wall = new HaboobWall(root.transform); }
                catch (Exception e) { Plugin.Log.LogWarning("Dust wall unavailable; retaining local dust and haze: " + e.Message); }
                try { nearDust = new StormNearDust(root.transform, texture); }
                catch (Exception e) { Plugin.Log.LogWarning("Near dust unavailable: " + e.Message); }
                try { billows = new StormBillows(root.transform); }
                catch (Exception e) { Plugin.Log.LogWarning("Animated billows unavailable: " + e.Message); }
                try { scatter = new StormLightScatter(root.transform); }
                catch (Exception e) { Plugin.Log.LogWarning("Dust light scatter unavailable: " + e.Message); }
                debris = new StormDebris(root, wood, branch);
                Plugin.Log.LogInfo("Dust systems ready: ground pickup, rising downwind plumes and cosmetic windblown twigs/sticks; no debris damage callbacks.");
            }
            catch { Dispose(); throw; }
        }
        private ParticleSystem Create(string name, int limit, Material material, Mesh mesh, float gravity)
        {
            GameObject go = new GameObject(name); go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetParent(root.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit; main.loop = true; main.playOnAwake = false; main.startSpeed = 0;
            main.gravityModifier = gravity; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule emission = ps.emission; emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = false;
            ParticleSystem.CollisionModule collision = ps.collision; collision.enabled = false;
            if (name != "Approaching dust front")
            {
                // Cosmetic particle queries only: no force or collision/damage messages.
                collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D; collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.enableDynamicColliders = false; collision.sendCollisionMessages = false;
                collision.colliderForce = 0; collision.radiusScale = 0.06f; collision.lifetimeLoss = 0.65f;
                collision.bounce = 0; collision.dampen = 0.5f;
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime; size.enabled = true;
                bool low = name == "Ground dust";
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                    new Keyframe(0, low ? 0.18f : 0.08f), new Keyframe(0.25f, low ? 0.65f : 0.45f),
                    new Keyframe(0.7f, 1), new Keyframe(1, 1.25f)));
            }
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime; color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0, 1) });
            color.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance; renderer.maxParticleSize = 0.45f;
            if (name != "Approaching dust front")
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.cameraVelocityScale = 0; renderer.velocityScale = name == "Ground dust" ? 0.085f : 0.040f;
                renderer.lengthScale = name == "Ground dust" ? 1.35f : 1.22f;
            }
            if (mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; main.startRotation3D = true;
                ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
                rotation.x = new ParticleSystem.MinMaxCurve(0.6f, 2.0f); rotation.y = new ParticleSystem.MinMaxCurve(-1.3f, 1.7f);
                rotation.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            }
            ps.Play(); return ps;
        }
        private float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        public bool Ground(Vector3 location, out Vector3 point)
        {
            // Ground dust originates on terrain, rather than a moving car/player/loose part.
            int n = Physics.RaycastNonAlloc(location + Vector3.up * 100, Vector3.down, hits, 250, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; point = location; bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.attachedRigidbody != null || hits[i].normal.y < 0.45f) continue;
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; found = true; }
            }
            return found;
        }
        public bool OpenSky(Vector3 point)
        { return !Physics.Raycast(point + Vector3.up * 1.5f, Vector3.up, 25, ~0, QueryTriggerInteraction.Ignore); }
        private void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float life, Color color)
        {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = position; p.velocity = velocity; p.startSize = size; p.startLifetime = life; p.startColor = color;
            p.rotation3D = new Vector3(Range(0, 360), Range(0, 360), Range(0, 360)); ps.Emit(p, 1);
        }
        private void Advect(ParticleSystem ps, ParticleSystem.Particle[] buffer, DustOccupancy occupancy, float dt, StormModel model, float speed, float gust, bool low)
        {
            int count = ps.GetParticles(buffer); float blend = (float)DustMotion.Blend(dt);
            occupancy.Begin(lastCamera);
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = buffer[i];
                if (DustVisibility.Hidden(p.position, lastCamera, p.startSize, retireDistance)) continue;
                DustFlow flow = DustMotion.Flow(p.startLifetime - p.remainingLifetime, p.startLifetime,
                    (p.randomSeed % 10000) * 0.0127, model.WindX, model.WindZ, speed, gust, low);
                p.velocity = Vector3.Lerp(p.velocity, new Vector3((float)flow.X, (float)flow.Y, (float)flow.Z), blend);
                occupancy.Observe(p.position);
                buffer[kept++] = p;
            }
            if (count > 0) ps.SetParticles(buffer, kept);
        }
        internal void Tick(Vector3 camera, float dt, float strength, StormModel model)
        {
            if (hasCamera && Vector3.Distance(lastCamera, camera) > 250)
            {
                dust.Clear(); ground.Clear(); debris.Clear();
                sources.Clear();
                airborneOccupancy.Begin(camera); scudOccupancy.Begin(camera);
                dustBudget = groundBudget = 0;
            }
            lastCamera = camera; hasCamera = true;
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ);
            Vector3 cross = new Vector3(-wind.z, 0, wind.x);
            float gust = StormRunner.Gust;
            float speed = (float)WindMath.Speed(gust, Plugin.Value(Plugin.Gusts, 1));
            float amount = Plugin.Value(Plugin.Dust, 1);
            retireDistance = DustVisibility.RetireDistance(strength, Plugin.StormVisibility, gust, Plugin.Value(Plugin.Gusts, 1));
            // Wind targets change slowly; update at 20 Hz rather than processing every
            // particle every rendered frame. Unity still integrates/render them each frame.
            flowClock += dt;
            if (flowClock >= 0.05f)
            {
                Advect(dust, airborne, airborneOccupancy, flowClock, model, speed, gust, false);
                Advect(ground, scud, scudOccupancy, flowClock, model, speed, gust, true);
                flowClock = 0;
            }
            sources.Advance(dt);
            debris.Tick(camera, dt, strength, gust, model);
            if (wall != null) wall.Tick(camera, dt, strength, model, frontGround);
            if (nearDust != null) nearDust.Tick(camera, dt, strength, gust, model, StormRunner.Entered);
            if (billows != null) billows.Tick(camera, dt, strength, gust, model, StormRunner.Entered);
            if (scatter != null) scatter.Tick(camera, dt, strength);
            nextBatch -= dt; if (nextBatch > 0) return;
            float step = Mathf.Clamp(0.1f - nextBatch, 0.1f, 0.3f); nextBatch = 0.1f;
            sources.BeginBatch();
            Color nativeFog = RenderSettings.fogColor;
            float light = Mathf.Clamp(Mathf.Max(nativeFog.r, Mathf.Max(nativeFog.g, nativeFog.b)) * 1.5f, 0.12f, 1)
                * StormLighting.SkyFactor(strength);
            Color sand = StormFog.Sand * light;
            float pickup = (float)DustMotion.Pickup(WindMath.Gain(gust, Plugin.Value(Plugin.Gusts, 1)));
            // Do not query ground/shelter for particles a full system cannot accept.
            // Discard overflow rather than releasing a catch-up burst as slots free.
            dustBudget = DustEmissionBudget.Add(dustBudget, 64 * amount * strength * pickup, step, dust.particleCount, airborne.Length, 24);
            groundBudget = DustEmissionBudget.Add(groundBudget, 18 * amount * strength * pickup, step, ground.particleCount, scud.Length, 12);
            while (dustBudget >= 1)
            {
                dustBudget--;
                Vector3 surface; if (!sources.TryGet(camera, wind, cross, this, out surface)) continue;
                if (DustVisibility.Hidden(surface, camera, 14, retireDistance) || !airborneOccupancy.TryReserve(surface)) continue;
                Color plume = sand * Range(0.92f, 1.12f); plume.a = Range(0.22f, 0.28f);
                Emit(dust, surface + Vector3.up * Range(0.18f, 0.4f), wind * (speed * 0.12f) + Vector3.up * Range(1.3f, 2.1f), Range(7, 14), Range(6, 10), plume);
            }
            while (groundBudget >= 1)
            {
                groundBudget--;
                Vector3 surface; if (!sources.TryGet(camera, wind, cross, this, out surface)) continue;
                if (DustVisibility.Hidden(surface, camera, 10, retireDistance) || !scudOccupancy.TryReserve(surface)) continue;
                Color scudColor = sand * Range(0.90f, 1.08f); scudColor.a = Range(0.28f, 0.35f);
                Emit(ground, surface + Vector3.up * Range(0.15f, 0.3f), wind * (speed * 0.24f) + Vector3.up * 0.05f, Range(4, 10), Range(3, 6), scudColor);
            }
        }
        internal void PreCullWall(Camera camera) { if (wall != null) wall.PreCull(camera); if (nearDust != null) nearDust.PreCull(camera); if (billows != null) billows.PreCull(camera); if (scatter != null) scatter.PreCull(camera); }
        internal void HideWall() { if (wall != null) wall.Hide(); if (nearDust != null) nearDust.Hide(); if (billows != null) billows.Hide(); if (scatter != null) scatter.Hide(); }
        internal void ShiftOrigin(Vector3 delta)
        {
            lastCamera += delta;
            dust.Clear(); ground.Clear(); debris.Clear(); sources.Clear();
            airborneOccupancy.Begin(lastCamera); scudOccupancy.Begin(lastCamera); dustBudget = groundBudget = 0;
            if (wall != null) wall.ShiftOrigin(delta); frontGround.Reset();
            if (nearDust != null) nearDust.Clear();
            if (billows != null) billows.Clear();
            if (scatter != null) scatter.ShiftOrigin(delta);
        }
        internal void FixedTick(float strength, float gust, StormModel model) { debris.FixedTick(gust, model); }
        public void Dispose()
        {
            if (wall != null) wall.Dispose();
            if (nearDust != null) nearDust.Dispose();
            if (billows != null) billows.Dispose();
            if (scatter != null) scatter.Dispose();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            if (particles != null) UnityEngine.Object.Destroy(particles);
            if (wood != null) UnityEngine.Object.Destroy(wood);
            if (branch != null) UnityEngine.Object.Destroy(branch);
        }
    }
}
