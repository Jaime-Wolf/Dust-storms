using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApocaDustStorm
{
    // A single native distance-only image pass covers far surfaces whose material
    // omits Unity fog. Depth/radial distance makes coverage independent of yaw.
    internal sealed class StormDistanceFog : MonoBehaviour
    {
        private static readonly List<StormDistanceFog> owned = new List<StormDistanceFog>();
        private static Shader shader;
        private static bool searched, warned;
        private Camera camera;
        private Material material;
        private float lastDensity = float.NaN;
        private float lastNight = float.NaN;
        private Color lastColour;
        private bool hasColour;
        private static bool Needed(Camera camera)
        {
            if (!StormRunner.CanRender || camera != StormRunner.View || StormRunner.Strength < 0.001f) return false;
            float density = StormFog.EffectiveDensity(RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogDensity,
                RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, StormRunner.Strength, StormRunner.Gust);
            return DistanceFogMath.CorrectionStart(density, NativeStormClock.NightStrength) < DistanceFogMath.FarDistance(camera.farClipPlane, camera.fieldOfView, camera.aspect);
        }
        internal static void PreCull(Camera camera)
        { if (camera == StormRunner.View) EnsureCamera(camera); }
        private void SetActive(bool active)
        {
            if (active) StormDepthRequest.Acquire(camera, this);
            else StormDepthRequest.Release(camera, this);
            enabled = active;
        }
        private void Configure()
        {
            material.EnableKeyword("ENVIRO_SIMPLE_FOG");
            material.SetVector("_SceneFogMode", new Vector4((float)FogMode.Exponential,1,0,0));
            material.SetVector("_EnviroParams", new Vector4(1,1,0,0));
            material.SetVector("_EnviroSkyFog", new Vector4(0,0,0,0));
            material.SetFloat("_distanceFogIntensity",1); material.SetFloat("_maximumFogDensity",0.005f);
        }
        internal static void EnsureCamera(Camera view)
        {
            if(view==null)return;
            bool needed = Needed(view);
            StormDistanceFog selected=null;
            foreach(StormDistanceFog item in owned)if(item!=null&&item.camera==view)selected=item;
            if(selected==null && needed)
            {
                if(!searched){shader=Shader.Find("Enviro/Lite/EnviroFogRenderingSimple");searched=true;}
                if(shader==null||!shader.isSupported)
                {
                    if(!warned){warned=true;Plugin.Log.LogWarning("Native distance-fog shader unavailable; keeping existing storm fog.");}
                    return;
                }
                selected=view.gameObject.AddComponent<StormDistanceFog>();
                selected.camera=view;StormDepthRequest.Register(selected);
                selected.material=new Material(shader);selected.material.hideFlags=HideFlags.HideAndDontSave;
                // Local keyword only; never alter the game's global shader state.
                selected.Configure();owned.Add(selected);
            }
            foreach(StormDistanceFog item in owned)if(item!=null)
            {
                item.SetActive(item.camera==view && needed);
            }
        }
        internal void Render(RenderTexture source,RenderTexture destination)
        {
            bool pushed=false,complete=false;
            RenderTexture previous=RenderTexture.active;
            try
            {
                if(material==null||camera==null||!enabled)return;
                pushed=StormFog.Push(camera);
                if(!pushed)return;
                float density=RenderSettings.fogDensity;
                float night=NativeStormClock.NightStrength;
                double start=DistanceFogMath.CorrectionStart(density,night);
                if(start>=DistanceFogMath.FarDistance(camera.farClipPlane,camera.fieldOfView,camera.aspect)||double.IsInfinity(start))return;
                // Same reconstruction and graphics-API Y correction as the
                // installed EnviroSkyRenderingLW.OnPreRender, with captured pose.
                Matrix4x4 world=StormView.World(camera),screen=StormView.Screen(camera);
                material.SetMatrix("_LeftWorldFromView",world);material.SetMatrix("_RightWorldFromView",world);
                material.SetMatrix("_LeftViewFromScreen",screen);material.SetMatrix("_RightViewFromScreen",screen);
                Vector3 eye=StormView.Eye(camera);
                material.SetVector("_WorldSpaceCameraPos",new Vector4(eye.x,eye.y,eye.z,1));
                Color colour = RenderSettings.fogColor;
                if (!hasColour || colour.r!=lastColour.r || colour.g!=lastColour.g || colour.b!=lastColour.b || colour.a!=lastColour.a)
                { material.SetColor("unity_FogColor",colour); lastColour=colour; hasColour=true; }
                if (density != lastDensity || night != lastNight)
                {
                    float correction=(float)DistanceFogMath.CorrectionDensity(density,night);
                    material.SetVector("_SceneFogParams",new Vector4(correction*1.2011224f,correction*1.442695f,0,0));
                    material.SetVector("_DistanceParams",new Vector4(-(float)start,0,0,0)); lastDensity=density; lastNight=night;
                }
                // Match native EnviroSkyRenderingLW: this shader is an opaque
                // image effect, before transparents and the post-processing stack.
                // Its compiled built-in vertex program already handles source Y
                // via _MainTex_TexelSize. A plain preparation copy loses that
                // source convention and cannot correct a late-stage invocation.
                material.SetTexture("_MainTex",source);
                Graphics.Blit(source,destination,material,0);complete=true;
            }
            catch(Exception e)
            {if(!warned){warned=true;Plugin.Log.LogWarning("Far dust pass skipped: "+e.Message);}}
            finally
            {
                try
                {
                    StormFog.FinishPass(pushed);
                    if(!complete)Graphics.Blit(source,destination);
                }
                finally
                {
                    RenderTexture.active=previous;
                }
            }
        }
        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            try { Render(source,destination); }
            // Unity's image-effect callback hands its destination to the next
            // stage. A null destination means the screen, not a missing target.
            finally { RenderTexture.active=destination; }
        }
        private void OnDisable() { StormDepthRequest.Release(camera, this); }
        private void OnDestroy()
        {
            StormDepthRequest.Release(camera, this); StormDepthRequest.Forget(this);
            owned.Remove(this);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
        internal static void Clear()
        {
            foreach(StormDistanceFog item in owned)if(item!=null)
            {
                item.SetActive(false); StormDepthRequest.Forget(item);
                if(item.material!=null){UnityEngine.Object.Destroy(item.material);item.material=null;}
                UnityEngine.Object.Destroy(item);
            }
            owned.Clear();shader=null;searched=false;
        }
    }
}
