using System;
using UnityEngine;
using UnityEngine.AzureSky;
namespace ApocaDustStorm
{
    internal static class LightingRecoveryChecks
    {
        private static bool Same(Color a,Color b) { return a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a; }
        private static void Dirty(Camera view)
        { StormRunner.View=view;StormRunner.Strength=1;StormFog.Push(view);StormFog.Restore();StormRunner.Strength=0; }
        internal static void Run(Action<bool,string> check)
        {
            StormFog.Restore();StormLighting.Clear();NativeStormClock.Reset();
            Plugin.Active=true;Plugin.Darkness=1f;Apocasetter.GameMenu.InGame=true;Time.unscaledTime=5000;
            Camera view=new GameObject("Lighting recovery view").Add<Camera>();
            AzureEnvironmentController native=new GameObject("Native current ambient").Add<AzureEnvironmentController>();
            native.environmentIntensity=.85f;native.environmentAmbientColor=new Color(.6f,.5f,.4f,1);
            native.environmentEquatorColor=new Color(.3f,.25f,.2f,1);native.environmentGroundColor=new Color(.15f,.12f,.1f,1);
            StormLighting.Scan();RenderSettings.ambientIntensity=.12f;RenderSettings.ambientSkyColor=new Color(.08f,.07f,.06f,1);
            Dirty(view);int requests=DynamicGI.Requests,accepted=DynamicGI.AcceptedRequests,writes=RenderSettings.ProbeWrites;
            DynamicGI.IgnoreNext=true;
            check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+1&&DynamicGI.AcceptedRequests==accepted,"A silent dropped readback is represented without throwing");
            check(RenderSettings.ambientIntensity==.85f&&Same(RenderSettings.ambientLight,native.environmentAmbientColor)&&Same(RenderSettings.ambientSkyColor,native.environmentAmbientColor)&&Same(RenderSettings.ambientEquatorColor,native.environmentEquatorColor)&&Same(RenderSettings.ambientGroundColor,native.environmentGroundColor),"Clear daylight restores all live native ambient values instead of leaving storm-darkened shadows");
            check(DynamicGI.AmbientAtRequest==.85f&&DynamicGI.SkyAtRequest.r==.6f&&RenderSettings.ProbeWrites==writes,"Clean refresh uses restored native light without installing an old SH probe");
            int searches=UnityEngine.Object.SceneQueries;
            for(int i=0;i<30;i++){Time.unscaledTime+=.016f;check(!StormLighting.Recover(true),"Spaced recovery does not request GI every frame");}
            Time.unscaledTime=5001;native.environmentIntensity=.91f;native.environmentAmbientColor=new Color(.7f,.6f,.5f,1);
            check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+2&&DynamicGI.AcceptedRequests==accepted+1,"A spaced retry recovers when the first request was silently ignored");
            check(RenderSettings.ambientIntensity==.91f&&RenderSettings.ambientSkyColor.r==.7f,"Recovery follows current daylight changes rather than a pre-storm snapshot");
            Time.unscaledTime=5003;
            check(!StormLighting.Recover(false)&&DynamicGI.Requests==requests+2,"A returning storm or residual clearing fade postpones recovery");
            check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+3,"Third clean request is spaced after the second");
            Time.unscaledTime=5006;
            check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+4,"Recovery performs its final bounded clean request");
            bool quiet=true;for(int i=0;i<1200;i++){Time.unscaledTime+=.016f;quiet&=!StormLighting.Recover(true);}
            check(quiet&&DynamicGI.Requests==requests+4&&UnityEngine.Object.SceneQueries==searches,"After four attempts clear-weather updates do no GI work or scene searches");

            native.environmentIntensity=.045f;native.environmentAmbientColor=new Color(.025f,.03f,.04f,1);
            native.environmentEquatorColor=new Color(.012f,.014f,.018f,1);native.environmentGroundColor=new Color(.006f,.007f,.009f,1);
            RenderSettings.ambientIntensity=.003f;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            Dirty(view);check(StormLighting.Recover(true)&&RenderSettings.ambientIntensity==.045f&&Same(RenderSettings.ambientSkyColor,native.environmentAmbientColor)&&Same(RenderSettings.ambientGroundColor,native.environmentGroundColor),"Night recovery restores native night brightness rather than daytime light or residual storm darkness");
            check(RenderSettings.ambientMode==UnityEngine.Rendering.AmbientMode.Trilight,"Recovery preserves the selected native ambient mode");
            requests=DynamicGI.Requests;Dirty(view);StormRunner.Strength=1;StormFog.Push(view);float dim=RenderSettings.ambientIntensity;
            check(!StormLighting.Recover(true)&&DynamicGI.Requests==requests&&RenderSettings.ambientIntensity==dim,"No recovery write or readback occurs while a live camera override is active");StormFog.Restore();StormRunner.Strength=0;
            DynamicGI.ThrowOnce=true;check(!StormLighting.Recover(true),"A failed environment refresh remains contained");
            Time.unscaledTime+=1;check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+2,"A failed refresh can recover on the next spaced attempt");

            native.enabled=false;RenderSettings.ambientIntensity=.37f;Dirty(view);StormLighting.Recover(true);
            check(RenderSettings.ambientIntensity==.37f,"A disabled native controller cannot overwrite another lighting owner");native.enabled=true;
            native.environmentIntensity=float.NaN;RenderSettings.ambientIntensity=.42f;Dirty(view);StormLighting.Recover(true);
            check(RenderSettings.ambientIntensity==.42f,"Invalid native intensity does not corrupt restored render settings");native.environmentIntensity=.045f;
            native.environmentGroundColor=new Color(float.NaN,0,0,1);RenderSettings.ambientIntensity=.43f;Dirty(view);StormLighting.Recover(true);
            check(RenderSettings.ambientIntensity==.43f,"Invalid native colour prevents partial ambient restoration");native.environmentGroundColor=new Color(.006f,.007f,.009f,1);
            native.environmentIntensity=0;native.environmentAmbientColor=new Color(0,0,0,1);Dirty(view);StormLighting.Recover(true);
            check(RenderSettings.ambientIntensity==0&&RenderSettings.ambientSkyColor.r==0,"Native completely dark lighting is respected without an artificial brightness floor");
            Dirty(view);StormLighting.Recover(true);requests=DynamicGI.Requests;StormLighting.Clear(true);
            check(DynamicGI.Requests==requests+1,"In-game cleanup makes one immediate clean request even while the next retry was waiting");Time.unscaledTime+=10;
            check(!StormLighting.Recover(true)&&DynamicGI.Requests==requests+1,"Cleanup cancels remaining scheduled work so it cannot leak into another session");
            native.enabled=false;UnityEngine.Object.Destroy(native);StormLighting.Scan();RenderSettings.ambientIntensity=.52f;Dirty(view);StormLighting.Recover(true);
            check(RenderSettings.ambientIntensity==.52f,"Without a native Azure controller recovery keeps the latest render settings");
            StormLighting.Clear();Plugin.Darkness=null;StormRunner.Strength=0;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Skybox;
        }
    }
}
