using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    // One depth-tested background shell, rather than a screen tint over the HUD.
    // Nearby opaque objects occlude it. Matching world fog removes the contrasting
    // blue sky behind fully fogged hills. No colliders, shadows or world particles.
    internal static class StormHorizon
    {
        private static GameObject owner;
        private static Mesh mesh;
        private static Material material;
        private static MeshRenderer renderer;
        private static bool warned;
        internal static bool Available = true;
        private static Shader FindShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported) shader = Shader.Find("UI/Default");
            return shader;
        }
        internal static void Scan()
        { Shader shader = FindShader(); Available = shader != null && shader.isSupported; }
        internal static void PreCull(Camera camera)
        {
            try { Draw(camera); }
            catch (Exception e)
            {
                Clear(); Available = false;
                if (!warned) { warned = true; Plugin.Log.LogWarning("Dust backdrop skipped: " + e.Message); }
            }
        }
        private static void Draw(Camera camera)
        {
            bool draw = StormRunner.CanRender && camera == StormRunner.View && StormRunner.Strength >= 0.001f;
            if (!draw) { Hide(); return; }
            if (owner == null && !Create()) return;
            owner.transform.position = StormView.Eye(camera);
            // Keep the backdrop at the sky, behind visible terrain. A small shell
            // intersected the ground and left a circular moving fog boundary.
            float radius = (float)HorizonMath.Radius(camera.farClipPlane);
            owner.transform.localScale = Vector3.one * radius;
            Color colour = RenderSettings.fogColor;
            colour.a = (float)HorizonMath.Opacity(StormRunner.Strength);
            material.SetColor("_Color", colour);
            renderer.enabled = true;
        }
        private static bool Create()
        {
            // Sprites/Default uses vertex colour, alpha blending and depth testing,
            // without lighting or an additional black particle-fog contribution.
            Shader shader = FindShader();
            if (shader == null || !shader.isSupported)
            {
                Available = false;
                if (!warned) { warned = true; Plugin.Log.LogWarning("Dust horizon shader unavailable; keeping distance fog without the backdrop."); }
                return false;
            }
            Available = true;
            owner = new GameObject("ApocaDustStorm.Horizon"); owner.hideFlags = HideFlags.HideAndDontSave;
            material = new Material(shader); material.hideFlags = HideFlags.HideAndDontSave;
            material.mainTexture = Texture2D.whiteTexture; material.renderQueue = 2990;
            // The UI fallback is drawn as ordinary world geometry: depth testing
            // must stay enabled so the near vehicle/buildings cover the backdrop.
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            mesh = new Mesh(); mesh.name = "ApocaDustStorm.HorizonShell"; mesh.hideFlags = HideFlags.HideAndDontSave;
            const int rings = 16, slices = 48;
            Vector3[] vertices = new Vector3[(rings + 1) * (slices + 1)];
            Vector2[] uv = new Vector2[vertices.Length]; Color[] colours = new Color[vertices.Length];
            int[] triangles = new int[rings * slices * 6]; int triangle = 0;
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= slices; x++)
            {
                float latitude = Mathf.PI * y / rings, longitude = Mathf.PI * 2 * x / slices;
                int i = y * (slices + 1) + x;
                vertices[i] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                uv[i] = new Vector2(0.5f, 0.5f); colours[i] = Color.white;
                if (y == rings || x == slices) continue;
                int next = i + slices + 1;
                triangles[triangle++] = i; triangles[triangle++] = i + 1; triangles[triangle++] = next;
                triangles[triangle++] = i + 1; triangles[triangle++] = next + 1; triangles[triangle++] = next;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colours; mesh.triangles = triangles; mesh.RecalculateBounds();
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = owner.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false; return true;
        }
        internal static void Hide() { if (renderer != null) renderer.enabled = false; }
        internal static void Clear()
        {
            Hide(); if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
            if (mesh != null) UnityEngine.Object.Destroy(mesh); if (material != null) UnityEngine.Object.Destroy(material);
            owner = null; mesh = null; material = null; renderer = null;
        }
    }
}
