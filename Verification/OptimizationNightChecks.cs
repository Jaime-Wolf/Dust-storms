using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AzureSky;
namespace ApocaDustStorm
{
    internal static class OptimizationNightChecks
    {
        internal static void Run(Action<bool,string> check)
        {
            Assets(check); Uploads(check); Night(check);
        }
        private static void Assets(Action<bool,string> check)
        {
            DustBillowAssets first=new DustBillowAssets(new GameObject("Shared front atlas"),32,8,35);
            DustBillowAssets second=new DustBillowAssets(new GameObject("Shared near atlas"),3,1.5f,7);
            Texture2D texture=first.Texture;
            check(ReferenceEquals(texture,second.Texture)&&first.Material!=second.Material,"Front and nearby dust share the immutable animation atlas while retaining independent materials");
            check(first.Material.GetVector("_SoftParticleFadeParams").y==1f/32&&second.Material.GetVector("_SoftParticleFadeParams").y==1f/3,"Shared texture does not merge different terrain fade distances");
            first.Dispose();check(!texture.Destroyed&&first.Material.Destroyed&&!second.Material.Destroyed,"Disposing one effect leaves the other effect's texture/material alive");
            first.Dispose();check(!texture.Destroyed,"Repeated cleanup cannot consume another effect's atlas ownership");
            second.Dispose();check(texture.Destroyed,"Last atlas owner releases the shared generated texture");
        }
        private sealed class Flat : IDustPickupWorld
        {
            public bool Ground(Vector3 p,out Vector3 result){result=new Vector3(p.x,0,p.z);return true;}
            public bool OpenSky(Vector3 p){return true;}
        }
        private static void Uploads(Action<bool,string> check)
        {
            Plugin.Active=true;Camera view=new GameObject("Single upload view").Add<Camera>();StormRunner.View=view;
            StormRunner.Model.BeginFrom(0,0,WindFrom.North,600,90,75,9);StormRunner.Model.Tick(20);
            HaboobWall wall=new HaboobWall(new GameObject("Single upload root").transform);
            ParticleSystem ps=(ParticleSystem)typeof(HaboobWall).GetField("system",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wall);
            wall.Tick(Vector3.zero,.1f,.1f,StormRunner.Model,new Flat());
            check(ps.UploadCalls==0,"Wall update builds geometry without an early duplicate upload");
            wall.PreCull(view);check(ps.UploadCalls==1&&ps.ParticleCount>0&&ps.ParticleCount<2058,"Active view uploads once and excludes fully transparent wall cards");
            bool visible=true;for(int i=0;i<ps.ParticleCount;i++)visible&=ps.Particles[i].startColor.a>0;
            check(visible,"Every submitted wall particle has nonzero final alpha");
            wall.PreCull(new GameObject("Inactive upload view").Add<Camera>());check(ps.UploadCalls==1,"Other cameras do not trigger wall uploads");
            wall.Dispose();StormRunner.Model.Reset();
        }
        private static void Night(Action<bool,string> check)
        {
            StormFog.Clear();NativeStormClock.Reset();Plugin.Active=true;StormRunner.Strength=0;
            Camera view=new GameObject("Calm night view").Add<Camera>();StormRunner.View=view;
            AzureTimeController clock=new GameObject("Calm night clock").Add<AzureTimeController>();clock.SunElevation=-1;NativeStormClock.Scan();
            Color native=new Color(1,.9f,.8f,1);Vector4 weather=new Vector4(1,.8f,.6f,.37f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogDensity=.004f;
            RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=5000;RenderSettings.fogColor=native;
            Shader.SetGlobalVector("_weatherFogMod",weather);Shader.SetGlobalFloat("_Azure_GlobalFogDensity",.19f);
            int gi=DynamicGI.Requests;float ambient=RenderSettings.ambientIntensity;
            check(StormFog.Push(view)&&RenderSettings.fogColor.r<=.080001f&&Shader.GetGlobalVector("_weatherFogMod").x<=.080001f,"Clear night corrects white fog even with zero storm strength");
            check(RenderSettings.fog&&RenderSettings.fogMode==FogMode.Linear&&RenderSettings.fogDensity==.004f&&RenderSettings.fogStartDistance==100&&RenderSettings.fogEndDistance==5000,"Clear-night correction preserves all native visibility/density settings");
            check(Shader.GetGlobalVector("_weatherFogMod").w==weather.w&&Shader.GetGlobalFloat("_Azure_GlobalFogDensity")==.19f&&DynamicGI.Requests==gi&&RenderSettings.ambientIntensity==ambient,"Clear-night colour leaves native alpha, Azure density, GI and lighting alone");
            float outer=RenderSettings.fogColor.r;StormFog.Push(view);StormFog.FinishPass(true);
            check(RenderSettings.fogColor.r==outer,"Nested night scopes restore the outer colour instead of leaking native white into an image pass");
            StormFog.Restore();check(RenderSettings.fogColor.r==1&&Shader.GetGlobalVector("_weatherFogMod").x==1&&!StormFog.HasOverrides,"Night scope cleanup restores actual native colours exactly");
            RenderSettings.fog=false;StormFog.Push(view);check(!RenderSettings.fog,"Clear-night correction never enables disabled fog");StormFog.Restore();
            float previous=0;bool continuous=true;
            for(int i=0;i<=200;i++)
            {
                StormRunner.Strength=i/1000f;StormFog.Push(view);float value=RenderSettings.fogColor.r;
                if(i>0)continuous&=Math.Abs(value-previous)<.005;previous=value;StormFog.Restore();
            }
            check(continuous,"Clearing blends from warm storm dust to dark native night haze without a white endpoint");
            StormRunner.Strength=0;Camera menu=new GameObject("Night menu").Add<Camera>();check(!StormFog.Push(menu)&&RenderSettings.fogColor.r==1,"Other cameras retain their native colours");
            Plugin.Active=false;check(!StormFog.Push(view)&&!StormFog.HasOverrides,"Disabled mod leaves native fog untouched");Plugin.Active=true;
            clock.SunElevation=1;check(!StormFog.Push(view)&&RenderSettings.fogColor.r==1,"Clear daytime fog remains unchanged");
            Color dark=new Color(.03f,.02f,.01f,.5f),actual=NightFogColours.Colour(dark,1);
            check(actual.r==dark.r&&actual.g==dark.g&&actual.a==dark.a,"Already dark native fog is never brightened or made more opaque");
            clock.SunElevation=-1;RenderSettings.fogColor=new Color(.5f,.4f,.3f,1);StormFog.Push(view);StormFog.Restore();
            check(RenderSettings.fogColor.r==.5f,"Later frames restore current native weather rather than an old pre-storm snapshot");
            UnityEngine.Object.Destroy(clock);NativeStormClock.Reset();StormFog.Clear();StormRunner.Strength=0;
        }
    }
}
