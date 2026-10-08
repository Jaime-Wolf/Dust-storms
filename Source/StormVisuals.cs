using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    internal sealed class StormVisuals : IDisposable
    {
        private readonly GameObject root;
        private readonly ParticleSystem dust, ground, front;
        private readonly Texture2D texture;
        private readonly Material particles, wood;
        private readonly Mesh branch;
        private readonly StormDebris debris;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly ParticleSystem.Particle[] airborne = new ParticleSystem.Particle[800], scud = new ParticleSystem.Particle[280];
        private sealed class SourcePatch { internal Vector3 Center; internal float Life, Radius; }
        private readonly SourcePatch[] patches = new SourcePatch[12];
        private readonly System.Random random = new System.Random();
        private float nextBatch, dustBudget, groundBudget, flowClock;
        private Vector3 lastCamera;
        private bool hasCamera;
        internal StormVisuals()
        {
            root = new GameObject("ApocaDustStorm.Visuals"); root.hideFlags = HideFlags.HideAndDontSave;
            // Construct in a try block so a stripped shader cannot leak half-built objects.
            try
            {
                texture = StormAssets.DustTexture(); particles = StormAssets.ParticleMaterial(texture);
                wood = StormAssets.WoodMaterial();
                branch = StormAssets.Branch();
                dust = Create("Lifting dust", 800, particles, null, 0);
                ground = Create("Ground dust", 280, particles, null, 0);
                front = Create("Approaching dust front", 64, particles, null, 0);
                debris = new StormDebris(root, wood, branch);
                Plugin.Log.LogInfo("Dust systems ready: ground pickup, rising downwind plumes and collision-aware twigs/sticks; no debris damage callbacks.");
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
        private bool Ground(Vector3 location, out Vector3 point)
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
        private bool OpenSky(Vector3 point)
        { return !Physics.Raycast(point + Vector3.up * 1.5f, Vector3.up, 25, ~0, QueryTriggerInteraction.Ignore); }
        private void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float life, Color color)
        {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = position; p.velocity = velocity; p.startSize = size; p.startLifetime = life; p.startColor = color;
            p.rotation3D = new Vector3(Range(0, 360), Range(0, 360), Range(0, 360)); ps.Emit(p, 1);
        }
        private bool Pickup(Vector3 camera, Vector3 wind, Vector3 cross, out Vector3 point)
        {
            int index = random.Next(patches.Length);
            SourcePatch patch = patches[index];
            if (patch == null) { patch = new SourcePatch(); patches[index] = patch; }
            if (patch.Life <= 0 || Vector3.Distance(patch.Center, camera) > 70)
            {
                // Thick distance haze hides far-away particles. Keep most pickup
                // patches close enough to read their drifting motion, with a
                // smaller background population for depth. This is only cosmetic
                // source placement; it does not create a clear weather bubble.
                bool near = random.NextDouble() < 0.7;
                Vector3 candidate = camera - wind * (near ? Range(4, 26) : Range(22, 52))
                    + cross * (near ? Range(-20, 20) : Range(-32, 32)), surface;
                if (!Ground(candidate, out surface) || !OpenSky(surface)) { point = Vector3.zero; return false; }
                patch.Center = surface; patch.Radius = Range(1.2f, 4.2f); patch.Life = Range(2.5f, 6);
            }
            // Resample the small offset so plumes originate on the actual local slope.
            Vector3 sample = patch.Center + wind * Range(-patch.Radius, patch.Radius) + cross * Range(-patch.Radius, patch.Radius);
            if (!Ground(sample, out point) || !OpenSky(point)) return false;
            return true;
        }
        private void Advect(ParticleSystem ps, ParticleSystem.Particle[] buffer, float dt, StormModel model, float speed, float gust, bool low)
        {
            int count = ps.GetParticles(buffer); float blend = (float)DustMotion.Blend(dt);
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = buffer[i];
                DustFlow flow = DustMotion.Flow(p.startLifetime - p.remainingLifetime, p.startLifetime,
                    (p.randomSeed % 10000) * 0.0127, model.WindX, model.WindZ, speed, gust, low);
                p.velocity = Vector3.Lerp(p.velocity, new Vector3((float)flow.X, (float)flow.Y, (float)flow.Z), blend);
                buffer[i] = p;
            }
            ps.SetParticles(buffer, count);
        }
        internal void Tick(Vector3 camera, float dt, float strength, StormModel model)
        {
            if (hasCamera && Vector3.Distance(lastCamera, camera) > 250)
            {
                dust.Clear(); ground.Clear(); front.Clear(); debris.Clear();
                foreach (SourcePatch patch in patches) if (patch != null) patch.Life = 0;
            }
            lastCamera = camera; hasCamera = true;
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ);
            Vector3 cross = new Vector3(-wind.z, 0, wind.x);
            float gust = StormRunner.Gust;
            float speed = (float)WindMath.Speed(gust, Plugin.Value(Plugin.Gusts, 1));
            float amount = Plugin.Value(Plugin.Dust, 1);
            // Wind targets change slowly; update at 20 Hz rather than processing every
            // particle every rendered frame. Unity still integrates/render them each frame.
            flowClock += dt;
            if (flowClock >= 0.05f)
            {
                Advect(dust, airborne, flowClock, model, speed, gust, false); Advect(ground, scud, flowClock, model, speed, gust, true);
                flowClock = 0;
            }
            foreach (SourcePatch patch in patches) if (patch != null) patch.Life -= dt;
            debris.Tick(camera, dt, strength, gust, model);
            nextBatch -= dt; if (nextBatch > 0) return;
            float step = Mathf.Clamp(0.1f - nextBatch, 0.1f, 0.3f); nextBatch = 0.1f;
            Color nativeFog = RenderSettings.fogColor;
            float light = Mathf.Clamp(Mathf.Max(nativeFog.r, Mathf.Max(nativeFog.g, nativeFog.b)) * 1.5f, 0.12f, 1)
                * StormLighting.SkyFactor(strength);
            Color sand = StormFog.Sand * light;
            float pickup = (float)DustMotion.Pickup(WindMath.Gain(gust, Plugin.Value(Plugin.Gusts, 1)));
            dustBudget = Mathf.Min(24, dustBudget + 92 * amount * strength * pickup * step);
            groundBudget = Mathf.Min(12, groundBudget + 26 * amount * strength * pickup * step);
            while (dustBudget >= 1)
            {
                dustBudget--;
                Vector3 surface; if (!Pickup(camera, wind, cross, out surface)) continue;
                Color plume = sand * Range(0.92f, 1.12f); plume.a = Range(0.22f, 0.28f);
                Emit(dust, surface + Vector3.up * Range(0.18f, 0.4f), wind * (speed * 0.12f) + Vector3.up * Range(1.3f, 2.1f), Range(7, 14), Range(6, 10), plume);
            }
            while (groundBudget >= 1)
            {
                groundBudget--;
                Vector3 surface; if (!Pickup(camera, wind, cross, out surface)) continue;
                Color scudColor = sand * Range(0.90f, 1.08f); scudColor.a = Range(0.28f, 0.35f);
                Emit(ground, surface + Vector3.up * Range(0.15f, 0.3f), wind * (speed * 0.24f) + Vector3.up * 0.05f, Range(4, 10), Range(3, 6), scudColor);
            }
            // A distant moving wall makes the approaching storm visible before local fog arrives.
            Vector3 heading = new Vector3((float)model.PrevailingX, 0, (float)model.PrevailingZ);
            Vector3 frontCross = new Vector3(-heading.z, 0, heading.x);
            Vector3 edge = new Vector3((float)model.OriginX, camera.y, (float)model.OriginZ) + heading * (float)model.Front;
            float signed = Vector3.Dot(camera - edge, heading);
            if (Mathf.Abs(signed) < 1800 && amount > 0)
            {
                float side = Vector3.Dot(camera - edge, frontCross);
                for (int i = 0; i < 1; i++)
                {
                    Vector3 p = edge + frontCross * (side + Range(-900, 900)) + heading * Range(-100, 50), surface;
                    float opacity = (float)model.FrontOpacity(p.x, p.z); if (opacity < 0.02f) continue;
                    if (!Ground(p, out surface)) continue;
                    float approach = (float)StormModel.Smooth((Mathf.Abs(signed) - 150) / 250);
                    Color wall = StormFog.Sand * light; wall.a = 0.22f * opacity * amount * approach;
                    Emit(front, surface + Vector3.up * Range(35, 110), heading * (float)model.Speed, Range(110, 220), Range(5, 8), wall);
                }
            }
        }
        internal void FixedTick(float strength, float gust, StormModel model) { debris.FixedTick(gust, model); }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            if (particles != null) UnityEngine.Object.Destroy(particles);
            if (wood != null) UnityEngine.Object.Destroy(wood);
            if (branch != null) UnityEngine.Object.Destroy(branch);
        }
    }
}
