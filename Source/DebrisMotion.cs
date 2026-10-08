using UnityEngine;

namespace ApocaDustStorm
{
    internal struct DebrisHit
    { internal float Distance; internal Vector3 Normal, SurfaceVelocity; }
    internal interface IDebrisWorld
    {
        bool Sweep(Vector3 position, float radius, Vector3 move, out DebrisHit hit);
        bool Penetration(Vector3 position, float radius, out Vector3 correction, out DebrisHit hit);
    }
    internal static class DebrisMotion
    {
        internal const float MaxSpeed = 22;
        internal static Vector3 Bounce(Vector3 velocity, Vector3 normal, Vector3 surface)
        {
            normal = normal.normalized; surface = Vector3.ClampMagnitude(surface, 18);
            Vector3 relative = velocity - surface;
            float into = Vector3.Dot(relative, normal);
            if (into >= 0) return Vector3.ClampMagnitude(velocity, MaxSpeed);
            Vector3 tangent = relative - normal * into;
            return Vector3.ClampMagnitude(surface + tangent * 0.7f - normal * into * 0.25f, MaxSpeed);
        }
        internal static void Step(ref Vector3 position, ref Vector3 velocity, float radius, float dt, Vector3 wind, float gust, IDebrisWorld world)
        {
            if (dt <= 0 || dt > 0.1f) return;
            // Gravity and drag operate only on our transient debris, never game objects.
            Vector3 target = wind * (7 + gust * 8);
            float blend = 1 - Mathf.Exp(-dt * 0.35f);
            velocity.x = Mathf.Lerp(velocity.x, target.x, blend); velocity.z = Mathf.Lerp(velocity.z, target.z, blend);
            velocity.y -= 9.81f * dt; velocity = Vector3.ClampMagnitude(velocity, MaxSpeed);
            DebrisHit hit; Vector3 correction;
            // Sphere casts exclude starting overlaps. Repair moving-car contacts first.
            for (int i = 0; i < 3 && world.Penetration(position, radius, out correction, out hit); i++)
            { position += correction; velocity = Bounce(velocity, hit.Normal, hit.SurfaceVelocity); }
            float remaining = dt;
            for (int i = 0; i < 3 && remaining > 0.00001f; i++)
            {
                Vector3 move = velocity * remaining; float distance = move.magnitude;
                if (distance < 0.0001f) break;
                if (!world.Sweep(position, radius, move, out hit)) { position += move; break; }
                float fraction = Mathf.Clamp01((hit.Distance - 0.005f) / distance);
                position += move * fraction + hit.Normal * 0.005f;
                velocity = Bounce(velocity, hit.Normal, hit.SurfaceVelocity);
                remaining *= 1 - fraction;
            }
            for (int i = 0; i < 2 && world.Penetration(position, radius, out correction, out hit); i++)
            { position += correction; velocity = Bounce(velocity, hit.Normal, hit.SurfaceVelocity); }
        }
    }
    internal sealed class DebrisWorld : IDebrisWorld
    {
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private readonly Collider[] overlaps = new Collider[48];
        private readonly SphereCollider probe;
        internal DebrisWorld(GameObject root)
        {
            GameObject go = new GameObject("Debris query shape"); go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetParent(root.transform, false);
            probe = go.AddComponent<SphereCollider>(); probe.isTrigger = true; probe.enabled = false;
            // A disabled query shape cannot generate Unity contact/damage callbacks.
        }
        public bool Sweep(Vector3 position, float radius, Vector3 move, out DebrisHit hit)
        {
            int count = Physics.SphereCastNonAlloc(position, radius, move.normalized, hits, move.magnitude + 0.005f, ~0, QueryTriggerInteraction.Ignore);
            hit = new DebrisHit(); float nearest = float.MaxValue; bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null || hits[i].collider == probe || hits[i].distance >= nearest) continue;
                if (Vector3.Dot(move, hits[i].normal) >= -0.00001f) continue;
                nearest = hits[i].distance; found = true;
                hit.Distance = nearest; hit.Normal = hits[i].normal;
                Rigidbody body = hits[i].collider.attachedRigidbody;
                hit.SurfaceVelocity = body == null ? Vector3.zero : body.GetPointVelocity(hits[i].point);
            }
            // Conservatively stop if a crowded query buffer filled before all hits could be returned.
            if (count == hits.Length) { hit.Distance = 0; hit.Normal = -move.normalized; hit.SurfaceVelocity = Vector3.zero; found = true; }
            return found;
        }
        public bool Penetration(Vector3 position, float radius, out Vector3 correction, out DebrisHit hit)
        {
            probe.radius = radius;
            int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            float deepest = 0; correction = Vector3.zero; hit = new DebrisHit();
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i]; if (c == null || c == probe || !c.enabled) continue;
                Vector3 normal; float depth;
                if (!Physics.ComputePenetration(probe, position, Quaternion.identity, c, c.transform.position, c.transform.rotation, out normal, out depth) || depth <= deepest) continue;
                deepest = depth; correction = normal * (depth + 0.006f); hit.Normal = normal;
                hit.SurfaceVelocity = c.attachedRigidbody == null ? Vector3.zero : c.attachedRigidbody.GetPointVelocity(position);
            }
            return deepest > 0;
        }
    }
}
