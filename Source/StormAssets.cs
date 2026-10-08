using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApocaDustStorm
{
    internal static class StormAssets
    {
        internal static Texture2D DustTexture()
        {
            const int n = 96;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.name = "ApocaDustStorm.SoftDust"; texture.hideFlags = HideFlags.HideAndDontSave;
            Color32[] pixels = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / n * 2 - 1, fy = (y + 0.5f) / n * 2 - 1;
                float coarse = Mathf.PerlinNoise(x * 0.065f + 3.4f, y * 0.065f + 7.1f);
                float fine = Mathf.PerlinNoise(x * 0.17f + 11.2f, y * 0.17f + 2.6f);
                float edge = Mathf.Max(0, 1 - fx * fx - fy * fy + (coarse - 0.5f) * 0.35f);
                float noise = Mathf.Clamp01(0.18f + 0.82f * coarse + 0.24f * (fine - 0.5f));
                byte alpha = (byte)(255 * Mathf.Clamp01(edge * edge * noise));
                pixels[y * n + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        internal static Material ParticleMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Particles/Standard Unlit");
            Material material = null;
            if (shader != null && shader.isSupported) material = new Material(shader);
            else if (NativeStormGuard.NativeDustMaterial != null) material = new Material(NativeStormGuard.NativeDustMaterial);
            else
            {
                // A native material is a final fallback when Unity stripped unused shaders.
                foreach (ParticleSystemRenderer r in UnityEngine.Object.FindObjectsOfType<ParticleSystemRenderer>())
                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex"))
                    { material = new Material(r.sharedMaterial); break; }
            }
            if (material == null) throw new InvalidOperationException("No supported dust-particle material was found in this scene.");
            material.name = "ApocaDustStorm.DustMaterial"; material.hideFlags = HideFlags.HideAndDontSave;
            material.mainTexture = texture;
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000;
            return material;
        }
        internal static Material WoodMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("No supported wood material was found.");
            Material material = new Material(shader); material.hideFlags = HideFlags.HideAndDontSave;
            material.name = "ApocaDustStorm.DryWood"; material.color = new Color(0.27f, 0.20f, 0.105f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.05f);
            return material;
        }
        internal static Mesh Branch()
        {
            Tubes mesh = new Tubes();
            mesh.Add(new Vector3(-0.55f, 0, 0), new Vector3(0.5f, 0.08f, 0.06f), 0.025f, 5);
            mesh.Add(new Vector3(-0.10f, 0.035f, 0.025f), new Vector3(0.20f, 0.34f, 0.11f), 0.013f, 4);
            mesh.Add(new Vector3(0.17f, 0.055f, 0.04f), new Vector3(0.48f, -0.18f, -0.03f), 0.010f, 4);
            return mesh.Build("ApocaDustStorm.Branch");
        }
        private sealed class Tubes
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            internal void Add(Vector3 from, Vector3 to, float radius, int sides)
            {
                Vector3 direction = (to - from).normalized;
                Vector3 right = Vector3.Cross(direction, Math.Abs(direction.y) < 0.9 ? Vector3.up : Vector3.right).normalized;
                Vector3 up = Vector3.Cross(right, direction).normalized; int first = vertices.Count;
                for (int end = 0; end < 2; end++) for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    vertices.Add((end == 0 ? from : to) + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius * (end == 0 ? 1 : 0.6f));
                }
                for (int s = 0; s < sides; s++)
                {
                    int a = first + s, b = first + (s + 1) % sides, c = a + sides, d = b + sides;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            internal Mesh Build(string name)
            {
                Mesh mesh = new Mesh(); mesh.name = name; mesh.hideFlags = HideFlags.HideAndDontSave;
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
