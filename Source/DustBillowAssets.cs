using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    // Uses the game's compiled shader; no runtime ShaderLab compilation or
    // redistribution of game assets. Soft intersections require opaque depth.
    internal sealed class DustBillowAssets : IDisposable
    {
        internal readonly Texture2D Texture;
        internal readonly Material Material;
        private readonly StormBillowDepth depth;
        private static Texture2D sharedAtlas;
        private static int atlasUsers;
        private bool disposed;
        internal DustBillowAssets(GameObject owner, float softness, float near, float far)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Compiled soft-particle shader unavailable");
            try
            {
                if (sharedAtlas==null) sharedAtlas=Atlas();
                Texture = sharedAtlas; atlasUsers++;
                Material = new Material(shader); Material.hideFlags = HideFlags.HideAndDontSave;
                Material.mainTexture = Texture; Material.SetColor("_Color", Color.white);
                Material.SetFloat("_Mode", 2); Material.SetFloat("_ColorMode", 0); Material.SetFloat("_FlipbookMode", 1);
                Material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); Material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                Material.SetInt("_BlendOp", (int)BlendOp.Add); Material.SetInt("_ZWrite", 0); Material.SetInt("_Cull", (int)CullMode.Off);
                Material.SetFloat("_SoftParticlesEnabled", 1); Material.SetFloat("_CameraFadingEnabled", 1);
                Material.SetFloat("_SoftParticlesNearFadeDistance", 0); Material.SetFloat("_SoftParticlesFarFadeDistance", softness);
                Material.SetFloat("_CameraNearFadeDistance", near); Material.SetFloat("_CameraFarFadeDistance", far);
                Material.SetVector("_SoftParticleFadeParams", new Vector4(0,1/softness,0,0));
                Material.SetVector("_CameraFadeParams", new Vector4(near,1/(far-near),0,0));
                Material.EnableKeyword("_ALPHABLEND_ON"); Material.EnableKeyword("SOFTPARTICLES_ON");
                Material.EnableKeyword("_FADING_ON"); Material.EnableKeyword("_REQUIRE_UV2");
                Material.SetShaderPassEnabled("Always", false); // No distortion GrabPass.
                Material.SetShaderPassEnabled("ShadowCaster", false); Material.renderQueue = 3000;
                depth = owner.AddComponent<StormBillowDepth>(); StormDepthRequest.Register(depth);
            }
            catch { Dispose(); throw; }
        }
        private static Texture2D Atlas()
        {
            const int tile = 128, size = tile*4;
            Texture2D texture = new Texture2D(size,size,TextureFormat.RGBA32,true);
            try
            {
                texture.name = "ApocaDustStorm.AnimatedBillows"; texture.hideFlags = HideFlags.HideAndDontSave;
                Color32[] pixels = new Color32[size*size];
                for (int frame=0; frame<16; frame++) for (int y=0; y<tile; y++) for (int x=0; x<tile; x++)
                {
                    double shade, alpha = HaboobVolumeMath.Billow(x/(double)(tile-1),y/(double)(tile-1),frame/16.0,out shade);
                    byte value = (byte)(255*shade);
                    // Unity whole-sheet animation reads the top-left tile first.
                    int px = (frame%4)*tile+x, py = (3-frame/4)*tile+y;
                    pixels[py*size+px] = new Color32(value,value,value,(byte)(255*alpha));
                }
                texture.SetPixels32(pixels); texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Trilinear; texture.Apply(true,true); return texture;
            }
            catch { UnityEngine.Object.Destroy(texture); throw; }
        }
        internal static void Configure(ParticleSystem system, ParticleSystemRenderer renderer, int capacity)
        {
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main; main.maxParticles = capacity;
            main.startSpeed = 0; main.loop = true; main.playOnAwake = false; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule emission = system.emission; emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape; shape.enabled = false;
            ParticleSystem.CollisionModule collision = system.collision; collision.enabled = false;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = sheet.numTilesY = 4; sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1)); sheet.cycleCount = 1;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 1; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.AnimBlend });
            renderer.enabled = false;
        }
        internal void Camera(Camera camera, bool active) { if (depth!=null) depth.Select(camera,active); }
        public void Dispose()
        {
            if (disposed) return; disposed=true;
            if (depth!=null) { depth.Select(null,false); StormDepthRequest.Forget(depth); UnityEngine.Object.Destroy(depth); }
            if (Material!=null) UnityEngine.Object.Destroy(Material);
            if (!ReferenceEquals(Texture,null) && atlasUsers>0 && --atlasUsers==0)
            { if (sharedAtlas!=null) UnityEngine.Object.Destroy(sharedAtlas); sharedAtlas=null; }
        }
    }
    // A depth lease only: no additional image effect or per-frame callback.
    internal sealed class StormBillowDepth : MonoBehaviour
    {
        private Camera current;
        internal void Select(Camera camera, bool active)
        {
            if (!active) camera = null;
            if (!ReferenceEquals(current,camera)) { if (!ReferenceEquals(current,null)) StormDepthRequest.Release(current,this); current = camera; }
            if (current!=null) StormDepthRequest.Acquire(current,this);
        }
        private void OnDestroy() { Select(null,false); StormDepthRequest.Forget(this); }
    }
}
