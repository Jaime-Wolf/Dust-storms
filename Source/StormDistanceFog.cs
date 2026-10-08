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
        private DepthTextureMode originalDepth;
        internal static void EnsureCamera(Camera view)
        {
            if(view==null)return;
            StormDistanceFog selected=null;
            foreach(StormDistanceFog item in owned)if(item!=null&&item.camera==view)selected=item;
            if(selected==null && StormRunner.CanRender && StormRunner.Strength>=0.001f)
            {
                if(!searched){shader=Shader.Find("Enviro/Lite/EnviroFogRenderingSimple");searched=true;}
                if(shader==null||!shader.isSupported)
                {
                    if(!warned){warned=true;Plugin.Log.LogWarning("Native distance-fog shader unavailable; keeping existing storm fog.");}
                    return;
                }
                selected=view.gameObject.AddComponent<StormDistanceFog>();
                selected.camera=view;selected.originalDepth=view.depthTextureMode;
                selected.material=new Material(shader);selected.material.hideFlags=HideFlags.HideAndDontSave;
                // Local keyword only; never alter the game's global shader state.
                selected.material.EnableKeyword("ENVIRO_SIMPLE_FOG");owned.Add(selected);
            }
            foreach(StormDistanceFog item in owned)if(item!=null)
            {
                item.enabled=item.camera==view&&StormRunner.CanRender&&StormRunner.Strength>=0.001f;
                if(item.enabled)item.camera.depthTextureMode|=DepthTextureMode.Depth;
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
                double start=DistanceFogMath.Start(density);
                if(start>=DistanceFogMath.FarDistance(camera.farClipPlane,camera.fieldOfView,camera.aspect)||double.IsInfinity(start))return;
                // Same reconstruction and graphics-API Y correction as the
                // installed EnviroSkyRenderingLW.OnPreRender, with captured pose.
                Matrix4x4 world=StormView.World(camera),screen=StormView.Screen(camera);
                material.SetMatrix("_LeftWorldFromView",world);material.SetMatrix("_RightWorldFromView",world);
                material.SetMatrix("_LeftViewFromScreen",screen);material.SetMatrix("_RightViewFromScreen",screen);
                Vector3 eye=StormView.Eye(camera);
                material.SetVector("_WorldSpaceCameraPos",new Vector4(eye.x,eye.y,eye.z,1));
                material.SetColor("unity_FogColor",RenderSettings.fogColor);
                material.SetVector("_SceneFogParams",new Vector4(density*1.2011224f,density*1.442695f,0,0));
                material.SetVector("_SceneFogMode",new Vector4((float)FogMode.Exponential,1,0,0));
                material.SetVector("_DistanceParams",new Vector4(-(float)start,0,0,0));
                material.SetVector("_EnviroParams",new Vector4(1,1,0,0));
                material.SetVector("_EnviroSkyFog",new Vector4(0,0,0,0));
                material.SetFloat("_distanceFogIntensity",1);material.SetFloat("_maximumFogDensity",0.005f);
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
        internal static void Clear()
        {
            foreach(StormDistanceFog item in owned)if(item!=null)
            {
                item.enabled=false;
                if(item.camera!=null&&item.camera.depthTextureMode==(item.originalDepth|DepthTextureMode.Depth))
                    item.camera.depthTextureMode=item.originalDepth;
                if(item.material!=null)UnityEngine.Object.Destroy(item.material);
                UnityEngine.Object.Destroy(item);
            }
            owned.Clear();shader=null;searched=false;
        }
    }
}
