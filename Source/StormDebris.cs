using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    internal sealed class StormDebris
    {
        private sealed class Body
        {
            internal GameObject Object;
            internal Vector3 Position, Velocity, Spin;
            internal float Radius, Scale, Age, Life;
        }
        private readonly Body[] bodies = new Body[22];
        private readonly DebrisWorld world;
        private readonly System.Random random = new System.Random();
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private float twigBudget;
        internal StormDebris(GameObject root, Material wood, Mesh branch)
        {
            world = new DebrisWorld(root);
            for (int i = 0; i < bodies.Length; i++)
            {
                GameObject go = new GameObject("Colliding twig"); go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(root.transform, false); go.AddComponent<MeshFilter>().sharedMesh = branch;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = wood;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                go.SetActive(false); bodies[i] = new Body { Object = go };
            }
        }
        private float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private bool Ground(Vector3 location, out Vector3 point)
        {
            int n = Physics.RaycastNonAlloc(location + Vector3.up * 100, Vector3.down, hits, 250, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; point = location; bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider; if (c == null || c.attachedRigidbody != null || hits[i].normal.y < 0.45f) continue;
                if (hits[i].distance < nearest) { point = hits[i].point; nearest = hits[i].distance; found = true; }
            }
            return found;
        }
        internal void Tick(Vector3 camera, float dt, float strength, float gust, StormModel model)
        {
            float amount = Mathf.Clamp(Plugin.Value(Plugin.Debris, 1), 0, 2);
            if (amount <= 0) { Clear(); return; }
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ);
            Vector3 cross = new Vector3(-wind.z, 0, wind.x);
            twigBudget = Mathf.Min(2, twigBudget + 0.8f * amount * strength * dt);
            while (twigBudget >= 1) { twigBudget--; Spawn(camera, wind, cross); }
            foreach (Body body in bodies)
            {
                if (!body.Object.activeSelf) continue;
                if (Vector3.Distance(body.Position, camera) > 160) { body.Object.SetActive(false); continue; }
                body.Object.transform.position = body.Position;
                float fade = Mathf.Min((float)StormModel.Smooth(body.Age / 0.4f), (float)StormModel.Smooth((body.Life - body.Age) / 1.2f));
                body.Object.transform.localScale = Vector3.one * (body.Scale * fade);
            }
        }
        private void Spawn(Vector3 camera, Vector3 wind, Vector3 cross)
        {
            Body body = null;
            foreach (Body candidate in bodies) if (!candidate.Object.activeSelf) { body = candidate; break; }
            if (body == null) return;
            Vector3 p = camera - wind * Range(15, 45) + cross * Range(-25, 25), surface;
            if (!Ground(p, out surface) || Physics.Raycast(surface + Vector3.up * 1.5f, Vector3.up, 25, ~0, QueryTriggerInteraction.Ignore)) return;
            body.Radius = Range(0.10f, 0.18f);
            body.Scale = body.Radius * 2.4f;
            body.Position = surface + Vector3.up * Range(1.2f, 3.0f);
            body.Velocity = wind * Range(7, 12) + Vector3.up * Range(0.3f, 1.0f);
            body.Spin = new Vector3(Range(-90, 90), Range(-130, 130), Range(-90, 90));
            body.Age = 0; body.Life = Range(7, 12);
            // Avoid spawning inside the cab/hood, a wall, or a part near the camera.
            DebrisHit hit; Vector3 correction;
            if (world.Penetration(body.Position, body.Radius, out correction, out hit)) return;
            body.Object.transform.position = body.Position;
            body.Object.transform.rotation = Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360));
            body.Object.transform.localScale = Vector3.zero; body.Object.SetActive(true);
        }
        internal void FixedTick(float gust, StormModel model)
        {
            if (Plugin.Value(Plugin.Debris, 1) <= 0) { Clear(); return; }
            Vector3 wind = new Vector3((float)model.WindX, 0, (float)model.WindZ);
            float localGust = Mathf.Clamp((float)WindMath.Gain(gust, Plugin.Value(Plugin.Gusts, 1)), 0, 2);
            foreach (Body body in bodies)
            {
                if (!body.Object.activeSelf) continue;
                body.Age += Time.fixedDeltaTime;
                if (body.Age >= body.Life) { body.Object.SetActive(false); continue; }
                DebrisMotion.Step(ref body.Position, ref body.Velocity, body.Radius, Time.fixedDeltaTime, wind, localGust, world);
                body.Object.transform.Rotate(body.Spin * Time.fixedDeltaTime, Space.World);
            }
        }
        internal void Clear()
        { foreach (Body body in bodies) if (body != null) body.Object.SetActive(false); twigBudget = 0; }
    }
}
