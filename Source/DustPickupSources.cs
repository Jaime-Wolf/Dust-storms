using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal interface IDustPickupWorld
    {
        bool Ground(Vector3 location, out Vector3 point);
        bool OpenSky(Vector3 point);
    }
    // Cache actual tested points, never an estimated height on nearby slopes.
    internal sealed class DustPickupSources
    {
        private sealed class Patch
        { internal Vector3 Center, Sample; internal float Life, SampleLife, Radius; internal bool HasSample; }
        private readonly Patch[] patches = new Patch[12];
        private readonly System.Random random;
        private int remaining;
        internal DustPickupSources(System.Random random) { this.random = random; }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        internal void Advance(float dt)
        { foreach (Patch p in patches) if (p != null) { p.Life -= dt; p.SampleLife -= dt; } }
        internal void BeginBatch() { remaining = 6; }
        internal void Clear() { Array.Clear(patches, 0, patches.Length); remaining = 0; }
        private bool Validate(IDustPickupWorld world, Vector3 candidate, out Vector3 point)
        {
            point = Vector3.zero;
            if (remaining == 0) return false;
            remaining--;
            return world.Ground(candidate, out point) && world.OpenSky(point);
        }
        internal bool TryGet(Vector3 camera, Vector3 wind, Vector3 cross, IDustPickupWorld world, out Vector3 point)
        {
            int index = random.Next(patches.Length);
            Patch p = patches[index];
            if (p == null) { p = new Patch(); patches[index] = p; }
            if (p.Life <= 0 || (p.Center - camera).sqrMagnitude > 4900)
            {
                p.HasSample = false;
                bool near = random.NextDouble() < 0.7;
                Vector3 candidate = camera - wind * (near ? Range(4, 26) : Range(22, 52))
                    + cross * (near ? Range(-20, 20) : Range(-32, 32));
                Vector3 center;
                if (!Validate(world, candidate, out center)) { point = Vector3.zero; return false; }
                p.Center = center;
                p.Radius = Range(1.2f, 4.2f); p.Life = Range(2.5f, 6);
            }
            if (!p.HasSample || p.SampleLife <= 0)
            {
                Vector3 candidate = p.Center + wind * Range(-p.Radius, p.Radius) + cross * Range(-p.Radius, p.Radius);
                if (!Validate(world, candidate, out p.Sample)) { p.HasSample = false; point = Vector3.zero; return false; }
                p.HasSample = true; p.SampleLife = 0.35f;
            }
            point = p.Sample; return true;
        }
    }
}
