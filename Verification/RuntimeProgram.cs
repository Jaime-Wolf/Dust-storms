using System;
using System.Reflection;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
namespace ApocaDustStorm
{
    internal static class RuntimeProgram
    {
        private static int count;
        private static void Check(bool ok,string description){count++;if(!ok)throw new Exception("FAILED: "+description);}
        private static bool Prefix(Type patch,object instance){return (bool)patch.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{instance});}
        private static void Main()
        {
            GameObject storm=new GameObject("SandStorm123"); PlayMakerFSM marker=storm.Add<PlayMakerFSM>();marker.FsmName="SandPlayer";
            PlayMakerFSM move=storm.Add<PlayMakerFSM>();move.FsmName="Move";
            GameObject physics=new GameObject("TornadoPhysics");physics.transform.parent=storm.transform;
            PlayMakerFSM damage=physics.Add<PlayMakerFSM>();damage.FsmName="TornadoDamage";
            PlayMakerFSM force=physics.Add<PlayMakerFSM>();force.FsmName="Tornado";
            Tornado native=physics.Add<Tornado>(); Collider collider=physics.Add<Collider>(); Renderer renderer=physics.Add<Renderer>();AudioSource nativeSound=physics.Add<AudioSource>();
            Renderer originallyHidden=physics.Add<Renderer>();originallyHidden.enabled=false;
            GameObject spawn=new GameObject("Sandstorm_1"); PlayMakerFSM spawner=spawn.Add<PlayMakerFSM>();spawner.FsmName="ItemSpawner";
            GameObject bomb=new GameObject("CarExplosion"); PlayMakerFSM bombFsm=bomb.Add<PlayMakerFSM>();bombFsm.FsmName="Move";
            Check(NativeStormGuard.Root(physics.transform)==storm.transform,"Find storm through ancestor marker");
            Check(NativeStormGuard.Root(spawn.transform)==null,"Never classify storm spawner as storm prefab");
            Check(!NativeStormGuard.Hazard(bombFsm),"Ordinary Move FSM is untouched");
            Explosion wind=new Explosion{Owner=physics,Fsm=new Fsm{Name="Tornado"}};
            Explosion explosion=new Explosion{Owner=bomb,Fsm=new Fsm{Name="Explosion"}};
            Check(!Prefix(typeof(StormForcePatch),wind),"Stock storm force blocked before scan");
            Check(Prefix(typeof(StormForcePatch),explosion),"Combat explosion still runs");
            SendEvent sandDamage=new SendEvent{Owner=physics,Fsm=new Fsm{Name="TornadoDamage"}};
            SendEvent collisionDamage=new SendEvent{Owner=bomb,Fsm=new Fsm{Name="Bodypart"}};
            Check(!Prefix(typeof(StormDamageEnterPatch),sandDamage)&&sandDamage.Finished,"Storm damage action finishes without sending damage");
            Check(Prefix(typeof(StormDamageEnterPatch),collisionDamage),"Other gameplay damage remains native");
            Check(!Prefix(typeof(StormDamageUpdatePatch),sandDamage),"Repeating storm damage stays blocked");
            Check(!Prefix(typeof(StormEnablePatch),damage),"Early storm enable is blocked");
            Check(!damage.enabled&&!force.enabled&&!marker.enabled&&!move.enabled&&!native.enabled,"All native storm force and damage components disabled");
            Check(!collider.enabled&&!renderer.enabled,"Native trigger collider and tornado visuals suppressed");
            Check(!nativeSound.enabled,"Native tornado loop is muted while replaced");
            Check(spawner.enabled&&bombFsm.enabled,"Spawner and ordinary gameplay preserved");
            NativeStormGuard.Suppress(storm.transform);Plugin.Active=false;NativeStormGuard.Restore();
            Check(damage.enabled&&force.enabled&&marker.enabled&&move.enabled&&native.enabled,"Disabling mod restores owned native behaviour");
            Check(collider.enabled&&renderer.enabled&&!originallyHidden.enabled,"Restore preserves originally disabled renderer");
            Check(nativeSound.enabled,"Disabling mod restores native storm sound component");
            Check(Prefix(typeof(StormForcePatch),wind),"Disabled mod leaves stock storm physics native");
            Plugin.Active=true;
            NativeStormGuard.Suppress(storm.transform);NativeStormGuard.ResetScene();
            Check(!damage.enabled&&!collider.enabled&&!renderer.enabled,"Scene change keeps surviving native storms harmless");
            Plugin.Active=false;NativeStormGuard.Restore();Plugin.Active=true;
            GameObject manager=new GameObject("__GameManager__");PlayMakerFSM disable=manager.Add<PlayMakerFSM>();disable.FsmName="DisableSandstorm";
            NativeStormGuard.Scan();Check(NativeStormGuard.StormsDisabled(),"Honor native Disable Sandstorm switch");
            disable.enabled=false;Check(!NativeStormGuard.StormsDisabled(),"Storm chance rolls allowed with native switch off");
            Plugin.Active=false;NativeStormGuard.Restore();Plugin.Active=true;
            Camera camera=new GameObject("PlayerCamera").Add<Camera>();Camera menu=new GameObject("MenuCamera").Add<Camera>();
            StormRunner.View=camera;StormRunner.Strength=0.8f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogDensity=0.007f;
            RenderSettings.fogStartDistance=10;RenderSettings.fogEndDistance=1000;RenderSettings.fogColor=new Color(0.2f,0.3f,0.4f,1);
            Shader.SetGlobalVector("_weatherFogMod",new Vector4(0.1f,0.2f,0.3f,0.2f));Shader.SetGlobalFloat("_maximumFogDensity",0.5f);Shader.SetGlobalFloat("_distanceFogIntensity",0.3f);
            Check(!StormFog.Push(menu)&&RenderSettings.fogMode==FogMode.Linear,"Menu camera never receives storm override");
            Check(StormFog.Push(camera)&&RenderSettings.fogMode==FogMode.Exponential&&RenderSettings.fogDensity>0.02f,"Gameplay gets distance fog");
            Check(Shader.GetGlobalFloat("_maximumFogDensity")<0.06f,"Enviro opacity progressively conceals distant terrain");
            StormFog.PostRender(menu);Check(RenderSettings.fogMode==FogMode.Exponential,"Other camera cannot pop active snapshot");
            StormFog.PostRender(camera);
            Check(RenderSettings.fogMode==FogMode.Linear&&RenderSettings.fogDensity==0.007f&&RenderSettings.fogEndDistance==1000,"Native fog restored after draw");
            Check(Shader.GetGlobalVector("_weatherFogMod").w==0.2f&&Shader.GetGlobalFloat("_distanceFogIntensity")==0.3f,"Native shader weather restored");
            StormFog.Push(camera);StormFog.Push(camera);StormFog.FinishPass(true);Check(RenderSettings.fogMode==FogMode.Exponential,"Nested image effect preserves outer scope");
            StormFog.Restore();Check(RenderSettings.fogMode==FogMode.Linear,"Scene cleanup restores nested scopes");
            RenderSettings.fogColor=new Color(0.01f,0.015f,0.02f,1);StormFog.Push(camera);
            Check(RenderSettings.fogColor.r<0.1f&&RenderSettings.fogColor.g<0.1f,"Night storms retain subdued dust colour");StormFog.Restore();
            Exception renderError=new Exception("Simulated image-effect error");StormFog.Push(camera);
            MethodInfo finalizer=typeof(StormEnviroFogPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static);
            Check(ReferenceEquals(finalizer.Invoke(null,new object[]{renderError,true}),renderError)&&RenderSettings.fogMode==FogMode.Linear,"Image-effect exception restores fog and preserves native exception");
            StormRunner.Strength=0;Check(!StormFog.Push(camera),"No active dust leaves rendering alone");
            Plugin.Active=false;StormRunner.Strength=1;Check(!StormFog.Push(camera),"Disabled mod never overrides camera fog");
            Plugin.Active=true;TransitionChecks(camera);LightingChecks(camera,menu);AmbientRecoveryChecks(camera);AzureChecks(camera,menu);NightTransitionChecks(camera,menu);NativeClockChecks();WindChecks();DebrisChecks();HorizonChecks(camera,menu);SandAudioChecks();LightningRuntimeChecks(camera,menu);LightningWindChecks(camera);
            DistanceFogChecks(camera,menu);
            HazardRuntimeChecks.Run(Check);
            ExposureRuntimeChecks.Run(Check);
            HealthFlashRuntimeChecks.Run(Check);
            PerformanceRuntimeChecks.Run(Check);
            Console.WriteLine(count+" simulated storm guard, fog, lighting, collision and vehicle-wind checks passed. Unity rendering/PhysX are not simulated.");
        }
        private static void DistanceFogChecks(Camera camera,Camera menu)
        {
            StormRunner.View=camera;StormRunner.Strength=1;Plugin.Active=true;StormDistanceFog.Clear();
            camera.depthTextureMode=DepthTextureMode.None;camera.UseRenderedPose=true;camera.RenderedPosition=new Vector3(100,8,50);
            camera.fieldOfView=60;Time.frameCount++;StormView.Capture(camera);
            StormDistanceFog.EnsureCamera(camera);StormDistanceFog pass=camera.GetComponent<StormDistanceFog>();
            Check(pass!=null&&pass.enabled&&camera.depthTextureMode==DepthTextureMode.Depth,"Storm installs one distance pass and requests depth before geometry");
            StormDistanceFog.EnsureCamera(camera);Check(camera.gameObject.GetComponents<StormDistanceFog>().Length==1,"Repeated updates reuse the same pass/material");
            RenderTexture source=new RenderTexture(),destination=new RenderTexture();
            Color original=RenderSettings.fogColor;int draw=Graphics.FogBlits,plain=Graphics.PlainBlits;
            camera.UseRenderedPose=false;camera.fieldOfView=20;pass.Render(source,destination);Material material=Graphics.LastMaterial;
            Check(Graphics.FogBlits==draw+1&&Graphics.PlainBlits==plain&&material!=null&&Graphics.LastPass==0,"Far coverage draws native pass0 once without a preparation or redundant output copy");
            Check(ReferenceEquals(Graphics.LastFogSource,source)&&ReferenceEquals(material.BoundTexture,source)&&RenderTexture.active==null,"Native fog receives the original scene texture directly and restores the previous target");
            MethodInfo imageEffect=typeof(StormDistanceFog).GetMethod("OnRenderImage",BindingFlags.NonPublic|BindingFlags.Instance);
            Check(imageEffect.IsDefined(typeof(ImageEffectOpaque),false),"Distance fog uses native Enviro's opaque stage before transparents and post-processing");
            RenderTexture preparedFixture=new RenderTexture();Graphics.Blit(source,preparedFixture);Graphics.Blit(preparedFixture,destination,material,0);
            Check(destination.Top==source.Bottom&&destination.Bottom==source.Top,"Decoded native vertex rule reproduces the previous preparation-copy inversion fixture");
            RenderTexture priorTarget=new RenderTexture();RenderTexture.active=priorTarget;
            foreach(bool alternate in new[]{false,true})foreach(int samples in new[]{1,4})
            {
                source.TexelSizeY=(alternate?1f:-1f)/1198;
                source.Top="sky above horizon";source.Bottom="road below horizon";
                source.AtV0=alternate?source.Top:source.Bottom;source.AtV1=alternate?source.Bottom:source.Top;
                source.descriptor=new RenderTextureDescriptor{width=1920,height=1198,depthBufferBits=24,msaaSamples=samples,colorFormat=42,bindMS=true,useMipMap=true,autoGenerateMips=true};
                int rented=RenderTexture.Rents,returned=RenderTexture.Returns;
                pass.Render(source,destination);
                Check(destination.Top==source.Top&&destination.Bottom==source.Bottom,"Native vertex sampler-row convention handles texel sign "+alternate+" and AA fixture "+samples);
                Check(ReferenceEquals(Graphics.LastFogSource,source)&&source.descriptor.msaaSamples==samples&&source.descriptor.depthBufferBits==24&&source.descriptor.width==1920&&source.descriptor.height==1198&&source.descriptor.colorFormat==42,"Source descriptor and orientation metadata reach native shader without a conversion");
                Check(RenderTexture.Rents==rented&&RenderTexture.Returns==returned&&RenderTexture.active==priorTarget,"Successful native draw needs no borrowed texture and restores caller target");
            }
            RenderTexture.active=null;
            Check(material.Keywords.Contains("ENVIRO_SIMPLE_FOG")&&material.GetVector("_SceneFogMode").y==1&&material.GetVector("_EnviroParams").z==0&&material.GetVector("_EnviroParams").w==0,"Pass enables radial distance locally without height fog or extra tonemapping");
            float density=material.GetVector("_SceneFogParams").y/1.442695f;
            Check(Math.Abs(material.GetVector("_DistanceParams").x+DistanceFogMath.Start(density))<0.0001,"Image pass starts beyond the accepted nearby visibility range");
            Check(material.GetMatrix("_LeftWorldFromView").MultiplyPoint3x4(Vector3.zero).x==100&&material.GetVector("_WorldSpaceCameraPos").x==100,"Depth fog uses captured rendered eye after chase matrices reset");
            Check(Math.Abs(material.GetMatrix("_LeftViewFromScreen")[1,1]+Math.Tan(Math.PI/6))<0.0001,"Projection keeps the geometry frame and installed Direct3D Y convention");
            Check(material.GetVector("_EnviroSkyFog").y==0&&material.GetColor("unity_FogColor").r<1,"Far geometry uses dust colour without introducing another bright sky layer");
            Check(RenderSettings.fogColor.r==original.r&&RenderSettings.fogColor.b==original.b,"Distance pass restores native fog after drawing");
            Graphics.ThrowOnce=true;plain=Graphics.PlainBlits;int faultRents=RenderTexture.Rents,faultReturns=RenderTexture.Returns;pass.Render(source,destination);
            Check(Graphics.PlainBlits==plain+1&&RenderSettings.fogColor.r==original.r&&RenderTexture.Rents==faultRents&&RenderTexture.Returns==faultReturns&&RenderTexture.active==null&&destination.Top==source.Top,"Render failure copies the ordinary original frame, restores fog and requires no temporary resource");
            int loopRents=RenderTexture.Rents,loopReturns=RenderTexture.Returns;bool upright=true;
            for(int i=0;i<120;i++){pass.Render(source,destination);upright&=destination.Top==source.Top&&destination.Bottom==source.Bottom&&RenderTexture.active==null;}
            Check(upright&&RenderTexture.Rents==loopRents&&RenderTexture.Returns==loopReturns,"Repeated sampler-row fixtures preserve the native convention without temporary texture allocation");
            RenderTexture.active=priorTarget;imageEffect.Invoke(pass,new object[]{source,destination});
            Check(RenderTexture.active==destination&&destination.Top==source.Top,"Actual image callback leaves its destination active for Unity's next stage");
            RenderTexture.active=priorTarget;Graphics.ThrowOnce=true;imageEffect.Invoke(pass,new object[]{source,null});
            Check(RenderTexture.active==null&&Graphics.ScreenTarget.Top==source.Top,"Failed image callback safely copies to the screen and leaves the null screen target active");
            StormRunner.Strength=0;StormDistanceFog.EnsureCamera(camera);plain=Graphics.PlainBlits;pass.Render(source,destination);
            Check(!pass.enabled&&Graphics.PlainBlits==plain+1,"Clear weather disables added fog and uses native passthrough");
            StormRunner.Strength=1;StormRunner.View=menu;StormDistanceFog.EnsureCamera(menu);
            Check(!pass.enabled&&menu.GetComponent<StormDistanceFog>().enabled,"Camera switching enables only the selected view");
            StormDistanceFog.Clear();Check(pass.Destroyed&&material.Destroyed&&camera.depthTextureMode==DepthTextureMode.None,"Cleanup frees owned pass/material and restores its original depth request");
            Shader.Available=false;StormRunner.View=camera;StormDistanceFog.EnsureCamera(camera);
            Check(camera.GetComponent<StormDistanceFog>()==null,"Missing native shader retains existing effects without a partial component");
            StormDistanceFog.Clear();Shader.Available=true;StormDistanceFog.EnsureCamera(camera);
            camera.depthTextureMode|=DepthTextureMode.DepthNormals;StormDistanceFog.Clear();
            Check((camera.depthTextureMode&DepthTextureMode.DepthNormals)!=0,"Cleanup preserves depth features requested by other effects");
            camera.depthTextureMode=DepthTextureMode.None;camera.fieldOfView=60;
            SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore;Time.frameCount++;StormView.Capture(camera);
            Check(StormView.Screen(camera)[1,1]>0,"OpenGL projection keeps the native Y convention");
            SystemInfo.graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Direct3D11;StormView.Clear();
        }
        private static void AzureChecks(Camera camera,Camera menu)
        {
            Material sky=new Material(),fog=new Material(),image=new Material();
            sky.SetFloat("_Azure_Exposure",2);sky.SetFloat("_Azure_Rayleigh",1);sky.SetFloat("_Azure_Mie",0.2f);
            sky.SetColor("_Azure_RayleighColor",new Color(0.6f,0.8f,1,0.4f));
            foreach(Material material in new[]{fog,image})
            {material.SetFloat("_Azure_GlobalFogDistance",5000);material.SetFloat("_Azure_GlobalFogDensity",0.2f);material.SetFloat("_Azure_GlobalFogSmooth",1);}
            UnityEngine.AzureSky.AzureSkyRenderController controller=new GameObject("ActualSky").Add<UnityEngine.AzureSky.AzureSkyRenderController>();
            controller.m_skyMaterial=sky;controller.m_fogMaterial=fog;
            UnityEngine.AzureSky.AzureFogScattering effect=camera.gameObject.Add<UnityEngine.AzureSky.AzureFogScattering>();effect.fogScatteringMaterial=image;
            Shader.SetGlobalFloat("_Azure_GlobalFogDistance",5000);Shader.SetGlobalFloat("_Azure_GlobalFogDensity",0.2f);
            Shader.SetGlobalFloat("_Azure_Exposure",2);Shader.SetGlobalFloat("_Azure_Rayleigh",1);Shader.SetGlobalFloat("_Azure_Mie",0.2f);
            Shader.SetGlobalVector("_Azure_RayleighColor",new Vector4(0.6f,0.8f,1,0.4f));
            AzureAtmosphere.Scan();StormRunner.Strength=1;
            Check(!StormFog.Push(menu)&&fog.GetFloat("_Azure_GlobalFogDistance")==5000,"Menu camera leaves actual Azure materials untouched");
            StormFog.Push(camera);
            Check(fog.GetFloat("_Azure_GlobalFogDistance")==5000&&image.GetFloat("_Azure_GlobalFogDistance")==5000&&
                Shader.GetGlobalFloat("_Azure_GlobalFogDistance")==5000,"Matched distance fog retains native Azure distances instead of pulling pale scattering toward the camera");
            Check(fog.GetFloat("_Azure_GlobalFogDensity")==0&&Shader.GetGlobalFloat("_Azure_GlobalFogDensity")==0,"Storm fog and background replace the mismatched Azure scattering layer at peak");
            Check(sky.GetFloat("_Azure_Exposure")>1.27f&&sky.GetFloat("_Azure_Exposure")<1.29f,"Azure sky exposure retains the chosen daylight floor");
            Check(sky.GetColor("_Azure_RayleighColor").r>sky.GetColor("_Azure_RayleighColor").b&&sky.GetColor("_Azure_RayleighColor").a==0.4f,
                "Actual Azure sky tint warms blue scattering without changing material alpha");
            float mie=Shader.GetGlobalFloat("_Azure_Mie");StormFog.Push(camera);StormFog.FinishPass(true);
            Check(Shader.GetGlobalFloat("_Azure_Mie")==mie,"Nested passes do not compound actual sky scattering");StormFog.Restore();
            Check(fog.GetFloat("_Azure_GlobalFogDistance")==5000&&image.GetFloat("_Azure_GlobalFogDensity")==0.2f&&sky.GetFloat("_Azure_Exposure")==2,
                "Azure sky, controller fog and image-effect materials restore exactly");
            Check(Shader.GetGlobalFloat("_Azure_GlobalFogDistance")==5000&&Shader.GetGlobalVector("_Azure_RayleighColor").z==1,
                "Azure global uniforms restore exactly");
            Plugin.Darkness=0f;StormFog.Push(camera);
            Check(sky.GetFloat("_Azure_Exposure")==2&&Shader.GetGlobalFloat("_Azure_Exposure")==2,"Darkening zero retains native Azure exposure while allowing dust haze");StormFog.Restore();Plugin.Darkness=null;
            MethodInfo prefix=typeof(StormAzureFogPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
            object[] arguments={effect,false};prefix.Invoke(null,arguments);
            Check((bool)arguments[1]&&image.GetFloat("_Azure_GlobalFogDensity")==0,"Azure image-effect prefix reapplies matched fog after camera post-render");
            Exception failure=new Exception("Azure render error");MethodInfo finalizer=typeof(StormAzureFogPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static);
            Check(ReferenceEquals(finalizer.Invoke(null,new object[]{failure,true}),failure)&&image.GetFloat("_Azure_GlobalFogDistance")==5000,
                "Azure image-effect error restores its material and preserves native exception");
            sky.ThrowOnceOnWrite=true;bool failed=false;try{StormFog.Push(camera);}catch(Exception){failed=true;}
            Check(failed&&sky.GetFloat("_Azure_Exposure")==2&&Shader.GetGlobalFloat("_Azure_GlobalFogDistance")==5000,
                "Partially applied Azure material failure unwinds all camera overrides");
            sky.SetColor("_Azure_RayleighColor",new Color(0.01f,0.008f,0.005f,1));StormFog.Push(camera);
            Check(sky.GetColor("_Azure_RayleighColor").r<=0.01f&&sky.GetColor("_Azure_RayleighColor").b<=0.01f,"Native night sky is not brightened into daytime by dust tint");StormFog.Restore();
            Camera chase=new GameObject("ChaseWithoutFog").Add<Camera>();StormRunner.View=chase;AzureAtmosphere.EnsureCamera(chase);
            UnityEngine.AzureSky.AzureFogScattering added=chase.GetComponent<UnityEngine.AzureSky.AzureFogScattering>();
            Check(added!=null&&added.enabled&&added.InitCalls==1,"Missing chase-camera depth fog gets an initialized storm effect before first render");
            Check(!ReferenceEquals(added.fogScatteringMaterial,image)&&added.fogScatteringMaterial.GetFloat("_Azure_GlobalFogDistance")==5000,"Chase effect clones native fog material rather than editing the original");
            StormRunner.Strength=0;AzureAtmosphere.EnsureCamera(chase);Check(!added.enabled,"Added chase fog is disabled in clear weather");
            StormRunner.Strength=1;StormRunner.View=camera;AzureAtmosphere.EnsureCamera(camera);
            Check(!added.enabled&&effect.enabled,"Camera switching disables added effect and preserves native first-person effect");
            chase.depthTextureMode=DepthTextureMode.Depth;
            AzureAtmosphere.Clear();Check(added.Destroyed&&added.fogScatteringMaterial.Destroyed&&!effect.Destroyed&&!image.Destroyed,"Cleanup destroys only owned chase effect and cloned material");
            Check(chase.depthTextureMode==DepthTextureMode.None,"Owned chase depth-texture request restores on cleanup");
            AzureAtmosphere.Scan();AzureAtmosphere.EnsureCamera(chase);
            Check(chase.GetComponent<UnityEngine.AzureSky.AzureFogScattering>()!=null,"Storm can recreate its chase fog after disable and re-enable");AzureAtmosphere.Clear();
            StormRunner.View=camera;StormRunner.Strength=.3f;AzureAtmosphere.Scan();StormFog.Push(camera);
            Check(image.GetFloat("_Azure_GlobalFogDistance")==5000&&image.GetFloat("_Azure_GlobalFogSmooth")==1,"Approach never compresses or hardens the remaining pale native scattering layer");StormFog.Restore();
            StormHorizon.Available=false;StormFog.Push(camera);
            Check(image.GetFloat("_Azure_GlobalFogDistance")<5000,"Missing horizon shader retains native fog fallback coverage");StormFog.Restore();StormHorizon.Available=true;
        }
        private static void NightTransitionChecks(Camera camera,Camera menu)
        {
            // Unlike the old night fixture, native fog is bright white despite
            // the sun being below the horizon, as seen in the user's recording.
            var clock=new GameObject("NightFogClock").Add<UnityEngine.AzureSky.AzureTimeController>();
            Color savedColour=RenderSettings.fogColor;Vector4 savedWeather=Shader.GetGlobalVector("_weatherFogMod");
            float savedStrength=StormRunner.Strength;
            NativeStormClock.Reset();NativeStormClock.Scan();StormRunner.View=camera;StormHorizon.Available=true;
            RenderSettings.fogColor=Color.white;Shader.SetGlobalVector("_weatherFogMod",new Vector4(1,1,1,0.4f));
            Shader.SetGlobalFloat("_Azure_GlobalFogDensity",0.2f);Shader.SetGlobalFloat("_Azure_HeightFogDensity",0.12f);
            float peak=0;Color dayPeak=new Color();double maxStep=0;float previous=1;
            foreach(float strength in new[]{0.05f,0.1f,0.2f,0.4f,0.7f,1f})
            {
                StormRunner.Strength=strength;clock.SunElevation=1;StormFog.Push(camera);
                Color day=RenderSettings.fogColor;Vector4 weather=Shader.GetGlobalVector("_weatherFogMod");
                float density=RenderSettings.fogDensity,azure=Shader.GetGlobalFloat("_Azure_GlobalFogDensity");
                if(strength==1)dayPeak=day;StormFog.Restore();
                clock.SunElevation=-1;StormFog.Push(camera);
                Color night=RenderSettings.fogColor;Vector4 nightWeather=Shader.GetGlobalVector("_weatherFogMod");
                Check(night.r<=day.r+0.000001&&night.g<=day.g+0.000001,"White native night fog dims before dense dust at strength "+strength);
                Check(density==RenderSettings.fogDensity&&weather.w==nightWeather.w,"Night colour correction preserves distance density and weather opacity at "+strength);
                Check(Shader.GetGlobalFloat("_Azure_GlobalFogDensity")<=azure&&Shader.GetGlobalFloat("_Azure_HeightFogDensity")<=0.12f,"Night handoff also removes residual native scattering at "+strength);
                if(strength==0.2f){peak=night.r;Check(night.r<0.39f&&night.g<0.32f&&night.b<0.24f&&Shader.GetGlobalFloat("_Azure_GlobalFogDensity")==0&&Shader.GetGlobalFloat("_Azure_HeightFogDensity")==0,"White horizon is replaced by muted earth colour early in a night storm");}
                if(strength==1)Check(Math.Abs(night.r-dayPeak.r)<0.000001&&Math.Abs(night.g-dayPeak.g)<0.000001&&Math.Abs(night.r-peak)<0.000001,"Accepted full-strength fog colour is unchanged for day and night");
                StormFog.Restore();
                Check(RenderSettings.fogColor.r==1&&Shader.GetGlobalVector("_weatherFogMod").w==0.4f&&Shader.GetGlobalVector("_weatherFogMod").x==1&&Shader.GetGlobalFloat("_Azure_GlobalFogDensity")==0.2f,"Camera cleanup restores native white fog and material globals at "+strength);
            }
            for(int i=1;i<=1000;i++)
            {
                StormRunner.Strength=i/1000f;StormFog.Push(camera);float value=RenderSettings.fogColor.r;
                maxStep=Math.Max(maxStep,Math.Abs(value-previous));previous=value;StormFog.Restore();
            }
            Check(maxStep<0.006,"Night colour transition ramps smoothly from native fog without a one-frame brightness switch");
            clock.SunElevation=1;StormRunner.Strength=0.1f;StormFog.Push(camera);Color dayTwilight=RenderSettings.fogColor;StormFog.Restore();
            clock.SunElevation=-1;StormFog.Push(camera);Color nightTwilight=RenderSettings.fogColor;StormFog.Restore();
            clock.SunElevation=-0.075f;StormFog.Push(camera);Color twilight=RenderSettings.fogColor;StormFog.Restore();
            Check(twilight.r>nightTwilight.r&&twilight.r<dayTwilight.r,"Live sun elevation blends night palette continuously through twilight");
            clock.SunElevation=-1;StormRunner.Strength=0.4f;StormFog.PreCull(camera);
            MeshRenderer backdrop=null;foreach(MeshRenderer item in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(item.name=="ApocaDustStorm.Horizon")backdrop=item;
            Check(backdrop!=null&&backdrop.enabled&&Math.Abs(backdrop.sharedMaterial.GetColor("_Color").r-RenderSettings.fogColor.r)<0.000001&&RenderSettings.fogColor.r<0.39f,"Part-strength night backdrop uses corrected earth colour rather than a separate white material");
            StormFog.PostRender(camera);StormHorizon.Clear();
            StormRunner.Strength=0.1f;StormFog.Push(camera);
            typeof(StormEnviroFogPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{new Exception("Night render failure"),true});
            Check(RenderSettings.fogColor.r==1&&Shader.GetGlobalVector("_weatherFogMod").x==1,"Night render failures restore native colours without leaking overrides");
            StormRunner.Strength=0.2f;Check(!StormFog.Push(menu)&&RenderSettings.fogColor.r==1,"Other cameras never inherit the night colour correction");
            Plugin.Active=false;Check(!StormFog.Push(camera)&&RenderSettings.fogColor.r==1,"Disabling mod restores native night fog");Plugin.Active=true;
            Vector4 native=new Vector4(1,0.9f,0.8f,0.4f);Color dust=new Color(0.1f,0.08f,0.05f,1);
            Check(StormFog.NightWeather(native,dust,0.5f,0).x==native.x&&StormFog.NightWeather(native,dust,0.5f,1).w==native.w,"Day weather colour and native alpha remain intact");
            Check(Math.Abs(StormFog.NightWeather(native,dust,0.5f,1).x-dust.r)<0.000001&&Math.Abs(StormFog.NightWeather(native,dust,0.5f,1).z-dust.b)<0.000001,"Nonzero native white weather fog cannot remain as a night halo");
            UnityEngine.Object.Destroy(clock);NativeStormClock.Reset();RenderSettings.fogColor=savedColour;
            Shader.SetGlobalVector("_weatherFogMod",savedWeather);StormRunner.Strength=savedStrength;
        }
        private static void HorizonChecks(Camera camera,Camera menu)
        {
            StormRunner.View=camera;StormRunner.Strength=1;Plugin.Active=true;
            camera.UseRenderedPose=true;camera.RenderedPosition=new Vector3(100,8,50);camera.RenderedForward=new Vector3(0,0,-1);Time.frameCount++;
            StormFog.PreCull(camera);
            MeshRenderer renderer=null;foreach(MeshRenderer item in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(item.name=="ApocaDustStorm.Horizon")renderer=item;
            Check(renderer!=null&&renderer.enabled,"Active gameplay camera draws the dust backdrop");
            Check(renderer.sharedMaterial.GetColor("_Color").r==RenderSettings.fogColor.r&&renderer.sharedMaterial.GetColor("_Color").a>0.99,"Backdrop matches current distance-fog colour and strongly masks the blue sky");
            Check(renderer.transform.localScale.x>=60&&renderer.GetComponent<Collider>()==null,"Backdrop stays beyond near objects and has no collision");
            Check(renderer.transform.position.x==100&&renderer.transform.position.y==8&&renderer.transform.localScale.x==2850,"Backdrop follows the rendered chase eye at far-sky distance regardless of transform/visibility reference");
            Check(renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&!renderer.receiveShadows,"Horizon cannot cast a dark shadow over the vehicle");
            StormFog.PostRender(camera);Check(!renderer.enabled,"Backdrop hides after the gameplay draw");
            camera.UseRenderedPose=false;
            Vector3 ray=StormView.FogVector(camera.transform,new Vector3(0,0,10));
            Check(ray.z==-10,"Image-effect rays retain rendered heading after the camera resets its live matrix");
            Check(StormView.Eye(camera).x==100,"Post-render fog keeps the same captured eye position as geometry");
            Vector3 native=StormView.FogVector(menu.transform,new Vector3(0,0,10));Check(native.z==10,"Other cameras preserve native frustum transforms");
            Time.frameCount++;Check(StormView.FogVector(camera.transform,new Vector3(0,0,10)).z==10,"A stale frame cannot reuse the preceding chase projection");
            System.Collections.Generic.List<HarmonyLib.CodeInstruction> corners=new System.Collections.Generic.List<HarmonyLib.CodeInstruction>();
            MethodInfo transform=typeof(Transform).GetMethod("TransformVector");
            for(int i=0;i<4;i++)corners.Add(new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Callvirt,transform));
            int corrected=0;foreach(HarmonyLib.CodeInstruction code in StormAzureFogPatch.Transpiler(corners))if(((MethodInfo)code.operand).Name=="FogVector"&&code.opcode==System.Reflection.Emit.OpCodes.Call)corrected++;
            Check(corrected==4,"Azure transpiler corrects all four native frustum corners");
            corners.RemoveAt(0);bool rejected=false;try{foreach(var code in StormAzureFogPatch.Transpiler(corners)){} }catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Changed native fog-ray layout rejects the adapter rather than partially correcting the image");StormView.Clear();
            StormHorizon.PreCull(menu);Check(!renderer.enabled,"Menu and other cameras never draw storm backdrop");
            StormRunner.Strength=0;StormHorizon.PreCull(camera);Check(!renderer.enabled,"Clear weather hides the backdrop");
            Material material=renderer.sharedMaterial;Mesh mesh=renderer.GetComponent<MeshFilter>().sharedMesh;GameObject owner=renderer.gameObject;
            StormHorizon.Clear();Check(owner.Destroyed&&material.Destroyed&&mesh.Destroyed,"Scene cleanup frees the backdrop, material and mesh");
            int renderersBefore=UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length;
            Shader.Available=false;StormRunner.Strength=1;StormHorizon.PreCull(camera);Check(UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Length==renderersBefore,"Missing backdrop shader fails without allocating a partial object");Shader.Available=true;
        }
        private static void SandAudioChecks()
        {
            GameObject car=new GameObject("SandAudioVehicle");NWH.VehiclePhysics2.VehicleController vehicle=car.Add<NWH.VehiclePhysics2.VehicleController>();car.Add<Rigidbody>();new GameObject("DriveTrigger").transform.parent=car.transform;
            GameObject player=new GameObject("SandAudioPlayer");player.transform.parent=car.transform;
            PlayMakerFSM inCar=player.Add<PlayMakerFSM>();inCar.FsmName="InCar";inCar.ActiveStateName="InCar";
            PlayMakerFSM health=player.Add<PlayMakerFSM>();health.FsmName="Health";health.ActiveStateName="OnFoot";
            SandAudio audio=new SandAudio();AudioSource source=null;
            foreach(AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())if(item.name=="ApocaDustStorm.SandOnMetal")source=item;
            Check(source!=null&&!source.playOnAwake&&source.volume==0,"New bodywork layer starts silent");
            Check(!source.ignoreListenerVolume&&!source.ignoreListenerPause&&source.outputAudioMixerGroup==null,"Bodywork audio respects listener volume and pause");
            audio.Tick(player,1,1,0.016f);Check(source.isPlaying&&source.volume>0&&source.volume<0.01,"Sand starts with a gentle fade while driving");
            for(int i=0;i<300;i++)audio.Tick(player,1,1,0.016f);Check(source.volume>0.3&&source.volume<0.321,"Default sand mix reaches its bounded level beneath wind");
            vehicle.Grounded=false;audio.Tick(player,1,1,0.016f);Check(source.isPlaying,"Brief airborne driving does not interrupt wind hitting bodywork");
            inCar.ActiveStateName="OnFoot";audio.Tick(player,1,1,0.016f);Check(!source.isPlaying&&source.volume==0,"Leaving vehicle immediately stops bodywork sound");
            inCar.ActiveStateName="InCar";Plugin.SandVolume=0f;audio.Tick(player,1,1,0.016f);Check(!source.isPlaying,"Sand slider zero mutes the new layer");Plugin.SandVolume=null;
            audio.Tick(player,1,1,0.1f);audio.Silence();Check(!source.isPlaying&&source.volume==0,"Pause silence resets the source");
            health.ActiveStateName="playerDeath";audio.Tick(player,1,1,0.1f);Check(!source.isPlaying,"Death prevents bodywork sound");health.ActiveStateName="OnFoot";
            Plugin.Active=false;audio.Tick(player,1,1,0.1f);Check(!source.isPlaying,"Disabled mod prevents bodywork sound");Plugin.Active=true;
            AudioClip clip=source.clip;GameObject owner=source.gameObject;audio.Dispose();Check(owner.Destroyed&&clip.Destroyed&&!owner.activeInHierarchy,"Cleanup stops sound and frees the generated clip");VehicleWind.Reset();
        }
        private static void LightningRuntimeChecks(Camera camera,Camera menu)
        {
            Plugin.Active=true;Plugin.Lightning=true;StormRunner.View=camera;StormRunner.Strength=1;
            UnityEngine.AzureSky.AzureTimeController lightningClock=UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureTimeController>();if(lightningClock==null)lightningClock=new GameObject("LightningClock").Add<UnityEngine.AzureSky.AzureTimeController>();lightningClock.SunElevation=1;NativeStormClock.Reset();NativeStormClock.Scan();
            DustLightning.Clear();StormFog.Push(camera);Color baseline=RenderSettings.fogColor;float baselineAmbient=RenderSettings.ambientIntensity;StormFog.Restore();Color native=RenderSettings.fogColor;
            DustLightning.Tick(camera,0.016f,1,true,true,false);
            DustLightning.PreCull(camera);
            AudioSource source=null;foreach(AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())if(item.name=="ApocaDustStorm.StaticDischarge")source=item;
            Check(source!=null&&!source.loop&&!source.playOnAwake&&!source.ignoreListenerPause&&!source.ignoreListenerVolume,"Discharge source is a listener-aware one-shot, never a repeated loop");
            Check(source.Shots==0&&DustLightning.Flare==0,"Preview arc starts gently and its sound is delayed");
            Light strikeLight=source.gameObject.GetComponent<Light>();
            Check(strikeLight!=null&&strikeLight.type==LightType.Point&&!strikeLight.enabled&&strikeLight.shadows==LightShadows.Soft&&strikeLight.shadowResolution==UnityEngine.Rendering.LightShadowResolution.Medium,"One bounded local shadow light starts disabled with clearer shadows");
            for(int i=0;i<8;i++)DustLightning.Tick(camera,0.016f,1,true,false,false);
            Check(source.Shots==1&&source.LastShotGain>=0.7225f&&source.LastShotGain<=0.85f,"Exactly one louder bounded lightning crack follows the flash");
            StormFog.PreCull(camera);
            Check(RenderSettings.fogColor.r==baseline.r&&RenderSettings.fogColor.b==baseline.b&&RenderSettings.ambientIntensity==baselineAmbient,"Lightning no longer brightens fog or ambient light across the screen");
            Check(strikeLight.enabled&&strikeLight.intensity>0&&strikeLight.intensity<=21.6f&&strikeLight.range==150&&strikeLight.cullingMask==camera.cullingMask&&strikeLight.bounceIntensity==0,"Gameplay draw enables a stronger farther-reaching local light without global indirect illumination");
            int visible=0;foreach(LineRenderer arc in UnityEngine.Object.FindObjectsOfType<LineRenderer>())if(arc.enabled)visible++;
            Check(visible==10,"Gameplay camera displays a taller arc with six primary branches and three secondary forks");
            StormFog.PostRender(camera);Check(RenderSettings.fogColor.r==native.r&&RenderSettings.fogColor.b==native.b,"Native fog colour restores after the camera draw");
            Check(!strikeLight.enabled&&strikeLight.intensity==0,"Point light switches off immediately after the gameplay draw");
            visible=0;foreach(LineRenderer arc in UnityEngine.Object.FindObjectsOfType<LineRenderer>())if(arc.enabled)visible++;Check(visible==0,"Arcs hide outside the camera rendering scope");
            DustLightning.PreCull(menu);visible=0;foreach(LineRenderer arc in UnityEngine.Object.FindObjectsOfType<LineRenderer>())if(arc.enabled)visible++;Check(visible==0&&!strikeLight.enabled,"Menu camera never sees static arc geometry or the local flash light");
            DustLightning.PreCull(camera);float daylight=strikeLight.intensity;lightningClock.SunElevation=-0.2f;DustLightning.PreCull(camera);
            Check(Math.Abs(strikeLight.intensity-daylight*1.5f)<0.0001,"Native sun elevation automatically boosts the local light by fifty percent at night");
            lightningClock.SunElevation=-0.075f;DustLightning.PreCull(camera);Check(Math.Abs(strikeLight.intensity-daylight*1.25f)<0.0001,"Night boost blends smoothly through twilight instead of switching abruptly");
            lightningClock.SunElevation=float.NaN;Check(NativeStormClock.NightStrength==0,"Invalid native sun elevation safely disables the automatic boost");lightningClock.SunElevation=1;
            Plugin.FlashBrightness=0f;DustLightning.PreCull(camera);Check(DustLightning.Flare==0&&!strikeLight.enabled,"Flash brightness zero hides bolt and light independently of sound");Plugin.FlashBrightness=null;
            Plugin.DischargeVolume=0f;DustLightning.Tick(camera,0.016f,1,true,false,false);Check(!source.isPlaying&&DustLightning.Flare>0,"Discharge volume zero stops sound while allowing the glow");Plugin.DischargeVolume=null;
            DustLightning.Suspend();Check(DustLightning.Flare==0&&!source.isPlaying&&!strikeLight.enabled&&DustLightning.WindGain==1,"Pause suspension clears flash, light and audio and restores wind gain");
            int shots=source.Shots;DustLightning.Tick(camera,0.016f,1,true,true,false);DustLightning.Tick(camera,0.016f,1,true,false,true);
            for(int i=0;i<10;i++)DustLightning.Tick(camera,0.016f,1,true,false,false);
            Check(source.Shots==shots&&DustLightning.Flare==0,"Sleep/time skip discards pending sound and cannot create a catch-up flash");
            Plugin.Lightning=false;DustLightning.Tick(camera,0.016f,1,true,true,false);Check(DustLightning.Flare==0&&source.Shots==shots,"Dust lightning switch disables preview and automatic effects");Plugin.Lightning=true;
            DustLightning.Tick(camera,0.016f,1,true,true,false);DustLightning.Tick(camera,0.016f,0,false,false,false);Check(DustLightning.Flare==0&&!source.isPlaying,"Clear weather discards discharge effects");
            Plugin.Active=false;DustLightning.Tick(camera,0.016f,1,true,true,false);Check(source.Shots==shots,"Disabled mod cannot create discharge sound");Plugin.Active=true;
            AudioClip[] clips=UnityEngine.Object.FindObjectsOfType<AudioClip>();GameObject owner=source.gameObject;DustLightning.Clear();bool freed=true;
            foreach(AudioClip clip in clips)if(clip.name.StartsWith("ApocaDustStorm.LightningCrack")&&!clip.Destroyed)freed=false;
            Check(owner.Destroyed&&!owner.activeInHierarchy&&freed&&!strikeLight.enabled,"Cleanup destroys the light/discharge owner and all generated clips");
            Shader.Available=false;DustLightning.Tick(camera,0.016f,1,true,true,false);
            DustLightning.PreCull(camera);
            AudioSource fallback=null;foreach(AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())if(item.name=="ApocaDustStorm.StaticDischarge"&&item.gameObject!=owner)fallback=item;
            for(int i=0;i<8;i++)DustLightning.Tick(camera,0.016f,1,true,false,false);DustLightning.PreCull(camera);
            Check(fallback!=null&&fallback.Shots==1&&fallback.gameObject.GetComponent<Light>().enabled,"Missing arc shader still permits the local shadow light and one-shot sound");
            DustLightning.Clear();Shader.Available=true;
            ApocaChaseCamera.ChaseView.Override=true;int inside=0,behind=0;AudioSource placement=null;
            for(int i=0;i<240;i++)
            {
                DustLightning.Suspend();DustLightning.Tick(camera,0.016f,1,true,true,false);DustLightning.PreCull(camera);
                foreach(AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())if(item.name=="ApocaDustStorm.StaticDischarge"&&item.gameObject.activeInHierarchy)placement=item;
                Vector3 projected=camera.WorldToViewportPoint(placement.transform.position);
                if(projected.z>0&&projected.x>=0&&projected.x<=1&&projected.y>=0&&projected.y<=1)inside++;
                if(projected.z<0)behind++;
            }
            Check(inside>140&&behind>1,"Strikes favour the rendered camera view while still allowing natural surrounding/behind events");
            Check(ApocaChaseCamera.ChaseView.Calls>0&&camera.UseRenderedPose&&placement.transform.position.x>50,"Optional chase bridge places from its custom render pose even when the transform points the opposite way");
            DustLightning.Clear();ApocaChaseCamera.ChaseView.Override=false;camera.UseRenderedPose=false;UnityEngine.Object.Destroy(lightningClock);NativeStormClock.Reset();
            StormHorizon.Clear();
        }
        private static void LightningWindChecks(Camera camera)
        {
            Plugin.Active=true;Plugin.Lightning=true;Plugin.DischargeVolume=null;StormRunner.View=camera;StormRunner.Strength=1;DustLightning.Clear();
            AudioClip bedClip=AudioClip.Create("Medium Wind Sound",48000,1,24000,false),gustClip=AudioClip.Create("Heavy Wind Sound",48000,1,24000,false);
            WindAudio wind=new WindAudio();AudioSource bed=null,gust=null,engine=new GameObject("UnrelatedEngineAudio").Add<AudioSource>();engine.volume=0.6f;
            for(int i=0;i<500;i++)wind.Tick(1,0.6f,0.02f);
            foreach(AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())if(item.name=="Wind bed")bed=item;else if(item.name=="Wind gusts")gust=item;
            Check(bed!=null&&gust!=null&&bed.isPlaying&&gust.isPlaying&&!bed.ignoreListenerVolume&&!gust.ignoreListenerPause,"Storm wind loads native recordings and respects listener controls");
            float bedLevel=bed.volume,gustLevel=gust.volume;
            DustLightning.Tick(camera,0.02f,1,true,true,false);DustLightning.PreCull(camera);
            for(int i=0;i<50;i++){DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);}
            Check(Math.Abs(bed.volume-bedLevel*0.75f)<0.00001&&Math.Abs(gust.volume-gustLevel*0.75f)<0.00001&&engine.volume==0.6f,"Both storm wind layers dip while unrelated engine audio retains its volume");
            for(int i=0;i<200;i++){DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);}
            Check(Math.Abs(bed.volume-bedLevel)<0.00001&&Math.Abs(gust.volume-gustLevel)<0.00001,"Wind returns to its original gust mix after the thunder dip");
            DustLightning.Suspend();Plugin.DischargeVolume=0f;DustLightning.Tick(camera,0.02f,1,true,true,false);DustLightning.PreCull(camera);
            for(int i=0;i<40;i++){DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);}
            Check(DustLightning.WindGain==1&&Math.Abs(bed.volume-bedLevel)<0.00001,"Muted lightning does not reduce wind");
            DustLightning.Suspend();Plugin.DischargeVolume=null;DustLightning.Tick(camera,0.02f,1,true,true,false);DustLightning.PreCull(camera);
            for(int i=0;i<25;i++){DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);}
            Plugin.DischargeVolume=0f;DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);
            Plugin.DischargeVolume=null;DustLightning.Tick(camera,0.02f,1,true,false,false);wind.Tick(1,0.6f,0.02f);
            Check(DustLightning.WindGain==1&&Math.Abs(bed.volume-bedLevel)<0.00001,"Muting a strike cancels its dip; unmuting cannot resurrect a cancelled thunder effect");
            Plugin.WindVolume=0f;wind.Tick(1,0.6f,0.02f);Check(!bed.isPlaying&&!gust.isPlaying&&bed.volume==0&&gust.volume==0,"Wind slider zero remains muted independently of lightning");Plugin.WindVolume=null;
            GameObject windOwner=bed.transform.parent.gameObject;wind.Dispose();
            Check(!windOwner.activeInHierarchy&&!bedClip.Destroyed&&!gustClip.Destroyed,"Wind cleanup preserves borrowed native clips");
            DustLightning.Clear();UnityEngine.Object.Destroy(bedClip);UnityEngine.Object.Destroy(gustClip);UnityEngine.Object.Destroy(engine.gameObject);
        }
        private static void NativeClockChecks()
        {
            UnityEngine.AzureSky.AzureTimeController time=new GameObject("SleepClock").Add<UnityEngine.AzureSky.AzureTimeController>();
            time.Timeline=12;time.SetLength(60);NativeStormClock.Reset();NativeStormClock.Scan();
            NativeStormClock.Step(0.016);time.Timeline=20;
            Check(Math.Abs(NativeStormClock.Step(0.016)-1200)<0.001,"Sleep clock adapter reads native timeline skip and day-length conversion");
            Check(time.Timeline==20&&time.ReadLength()==60,"Storm clock reads native time without writing timeline or day length");
            NativeStormClock.Reset();NativeStormClock.Scan();time.Timeline=23;NativeStormClock.Step(0);
            time.Timeline=1;time.Date=new Vector3Int(2026,10,8);
            Check(Math.Abs(NativeStormClock.Step(0.016)-300)<0.001,"Native adapter follows sleep across a calendar day");
            time.Date=new Vector3Int(0,0,0);Check(NativeStormClock.Step(0.016)==0.016,"Invalid native date safely falls back to normal weather time");
            NativeStormClock.Reset();
        }
        private static void TransitionChecks(Camera camera)
        {
            EnviroSkyLite.instance.fogSettings.startDistance=800;
            Shader.SetGlobalVector("_SceneFogParams",new Vector4(1,2,3,4));
            Shader.SetGlobalVector("_SceneFogMode",new Vector4(5,6,7,8));
            Shader.SetGlobalVector("_HeightParams",new Vector4(9,10,11,12));
            Shader.SetGlobalVector("_DistanceParams",new Vector4(13,14,15,16));
            double maxDistanceStep=0,maxCapStep=0,maxWeatherStep=0;
            float previousDistance=0.3f,previousCap=0.5f,previousWeight=0.2f;
            for(int i=1;i<=1000;i++)
            {
                StormRunner.Strength=i/1000f;StormFog.Push(camera);
                float distance=Shader.GetGlobalFloat("_distanceFogIntensity"),cap=Shader.GetGlobalFloat("_maximumFogDensity");
                float weight=Shader.GetGlobalVector("_weatherFogMod").w;
                maxDistanceStep=Math.Max(maxDistanceStep,Math.Abs(distance-previousDistance));
                maxCapStep=Math.Max(maxCapStep,Math.Abs(cap-previousCap));
                maxWeatherStep=Math.Max(maxWeatherStep,Math.Abs(weight-previousWeight));
                previousDistance=distance;previousCap=cap;previousWeight=weight;
                if(i==1)Check(distance<0.301f&&cap>0.499f,"First trace of dust does not abruptly enable full shader fog");
                if(i==1000)
                {
                    Check(cap<=0.0051f,"Peak inverse-density ceiling reaches 99.5 percent far opacity");
                    Check(EnviroSkyLite.instance.fogSettings.startDistance==0,"Peak fog removes native clear-distance zone");
                    Check(Math.Abs(Shader.GetGlobalVector("_SceneFogParams").y-RenderSettings.fogDensity*1.442695f)<0.000001f&&
                        Shader.GetGlobalVector("_SceneFogMode").x==(float)FogMode.Exponential&&Shader.GetGlobalVector("_DistanceParams").x==0,
                        "Chase-camera world shaders receive current exponential fog before drawing");
                }
                // Simulate globals written by the installed Enviro RenderFog body.
                Shader.SetGlobalVector("_SceneFogParams",new Vector4());Shader.SetGlobalVector("_SceneFogMode",new Vector4());
                Shader.SetGlobalVector("_HeightParams",new Vector4());Shader.SetGlobalVector("_DistanceParams",new Vector4());
                StormFog.PostRender(camera);
                if(EnviroSkyLite.instance.fogSettings.startDistance!=800)throw new Exception("Native fog start distance leaked after draw");
            }
            for(int i=999;i>=1;i--)
            {
                StormRunner.Strength=i/1000f;StormFog.Push(camera);
                float distance=Shader.GetGlobalFloat("_distanceFogIntensity"),cap=Shader.GetGlobalFloat("_maximumFogDensity");
                float weight=Shader.GetGlobalVector("_weatherFogMod").w;
                maxDistanceStep=Math.Max(maxDistanceStep,Math.Abs(distance-previousDistance));
                maxCapStep=Math.Max(maxCapStep,Math.Abs(cap-previousCap));maxWeatherStep=Math.Max(maxWeatherStep,Math.Abs(weight-previousWeight));
                previousDistance=distance;previousCap=cap;previousWeight=weight;StormFog.PostRender(camera);
            }
            Check(maxDistanceStep<0.002&&maxCapStep<0.002&&maxWeatherStep<0.002,"Shader distance, opacity and colour weight blend continuously over approach and reversed clearing");
            Check(Shader.GetGlobalVector("_SceneFogParams").w==4&&Shader.GetGlobalVector("_SceneFogMode").w==8&&
                Shader.GetGlobalVector("_HeightParams").w==12&&Shader.GetGlobalVector("_DistanceParams").w==16,"All four Enviro-derived fog globals restore after image effect");
            StormRunner.Strength=1;StormFog.Push(camera);StormFog.Push(camera);
            Check(EnviroSkyLite.instance.fogSettings.startDistance==0,"Nested draw does not compound or reopen clear-distance override");
            StormFog.Restore();Check(EnviroSkyLite.instance.fogSettings.startDistance==800,"Cleanup restores native fog start distance through nested scopes");
            StormFog.Push(camera);
            MethodInfo finalizer=typeof(StormEnviroFogPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static);
            finalizer.Invoke(null,new object[]{new Exception("Fog error"),true});
            Check(EnviroSkyLite.instance.fogSettings.startDistance==800,"Image-effect error restores native fog settings");
            Vector4 night=StormFog.BlendWeather(new Vector4(1,1,1,0),new Color(0.01f,0.008f,0.005f,1),0.95f,0.1f);
            Check(night.x<0.011f&&night.w>0,"Invisible bright native weather colour cannot flash into night dust");
            EnviroSkyLite savedSky=EnviroSkyLite.instance;EnviroSkyLite.instance=null;
            Check(StormFog.Push(camera),"Camera haze tolerates absent Enviro instance");StormFog.Restore();EnviroSkyLite.instance=savedSky;
        }
        private static void LightingChecks(Camera camera,Camera menu)
        {
            Light sun=new GameObject("Sun").Add<Light>();sun.type=LightType.Directional;sun.intensity=2;
            Light moon=new GameObject("Moon").Add<Light>();moon.type=LightType.Directional;moon.intensity=0.3f;
            Light headlight=new GameObject("Headlight").Add<Light>();headlight.type=LightType.Spot;headlight.intensity=4;
            GameObject car=new GameObject("LitVehicle");car.Add<NWH.VehiclePhysics2.VehicleController>();
            Light vehicleLight=new GameObject("VehicleHeadlight").Add<Light>();vehicleLight.transform.parent=car.transform;vehicleLight.type=LightType.Spot;vehicleLight.intensity=4;vehicleLight.range=40;
            Light switchedOff=new GameObject("OffVehicleHeadlight").Add<Light>();switchedOff.transform.parent=car.transform;switchedOff.type=LightType.Spot;switchedOff.intensity=4;switchedOff.enabled=false;
            RenderSettings.sun=sun;RenderSettings.ambientIntensity=1.2f;
            RenderSettings.ambientLight=new Color(0.5f,0.4f,0.3f,1);RenderSettings.ambientSkyColor=new Color(0.7f,0.6f,0.5f,1);
            RenderSettings.ambientEquatorColor=new Color(0.4f,0.3f,0.2f,1);RenderSettings.ambientGroundColor=new Color(0.2f,0.15f,0.1f,1);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Skybox;
            UnityEngine.Rendering.SphericalHarmonicsL2 probe=new UnityEngine.Rendering.SphericalHarmonicsL2();probe[0,0]=0.8f;probe[1,4]=-0.2f;probe[2,8]=0.5f;RenderSettings.ambientProbe=probe;
            Material skybox=new Material();skybox.SetColor("_SkyColor",new Color(0.5f,0.6f,0.7f,0.4f));skybox.SetColor("_SunColor",new Color(1,0.9f,0.8f,1));skybox.SetFloat("_StarsIntensity",2);
            RenderSettings.skybox=skybox;Shader.SetGlobalVector("_EnviroLighting",new Vector4(1,0.9f,0.8f,1));Shader.SetGlobalVector("_weatherSkyMod",new Vector4(0.6f,0.5f,0.4f,0.3f));
            StormLighting.Scan();StormRunner.View=camera;StormRunner.Strength=1;Plugin.Darkness=1f;
            int probeWrites=RenderSettings.ProbeWrites;
            Check(!StormFog.Push(menu)&&sun.intensity==2,"Menu draw leaves actual world lighting untouched");
            StormFog.Push(camera);
            Check(sun.intensity>0.9f&&sun.intensity<1&&moon.intensity>0.14f&&moon.intensity<0.15f,"Peak storm dims directional lights while retaining diffuse daylight");
            Check(headlight.intensity==4,"Vehicle headlights retain native brightness");
            Check(Math.Abs(vehicleLight.intensity-5.4f)<0.00001f&&vehicleLight.range==48,"Vehicle spotlights get the modest storm intensity and range boost");
            Check(!switchedOff.enabled&&switchedOff.intensity==4,"Storm assist cannot turn switched-off headlights on");
            Check(RenderSettings.ambientIntensity>0.75f&&RenderSettings.ambientIntensity<0.8f&&RenderSettings.ambientSkyColor.r<0.5f&&RenderSettings.ambientGroundColor.r<0.15f,"Ambient retains a dark-daylight floor during storm draw");
            Check(RenderSettings.ambientProbe[0,0]==0.8f&&RenderSettings.ambientProbe[1,4]==-0.2f&&RenderSettings.ambientProbe[2,8]==0.5f&&RenderSettings.ProbeWrites==probeWrites,"Temporary intensity/colors dim daylight while the generated ambient probe remains untouched");
            Check(skybox.GetColor("_SkyColor").b<0.3f&&skybox.GetColor("_SkyColor").r>skybox.GetColor("_SkyColor").b&&skybox.GetFloat("_StarsIntensity")<1.3f,"Actual Enviro sky changes from blue to an earthy overcast");
            Check(skybox.GetColor("_SkyColor").a==0.4f&&Shader.GetGlobalVector("_weatherSkyMod").w>0.8f,"Sky material alpha is preserved while the temporary weather tint blends in");
            float dimSun=sun.intensity;StormFog.Push(camera);StormFog.FinishPass(true);
            Check(sun.intensity==dimSun,"Nested camera/image-effect passes do not compound dimming");
            StormFog.Restore();
            Check(sun.intensity==2&&moon.intensity==0.3f&&headlight.intensity==4,"Directional lights restore exactly after draw");
            Check(vehicleLight.intensity==4&&vehicleLight.range==40,"Vehicle light intensity and range restore after drawing");
            Plugin.Headlights=0f;StormFog.Push(camera);Check(vehicleLight.intensity==4&&vehicleLight.range==40,"Headlight assist zero keeps native vehicle lights");StormFog.Restore();Plugin.Headlights=null;
            Check(RenderSettings.ambientIntensity==1.2f&&RenderSettings.ambientSkyColor.r==0.7f&&RenderSettings.ambientGroundColor.r==0.2f,"Native ambient values restore exactly");
            Check(RenderSettings.ambientProbe[0,0]==0.8f&&RenderSettings.ambientProbe[1,4]==-0.2f&&RenderSettings.ambientProbe[2,8]==0.5f&&RenderSettings.ProbeWrites==probeWrites,"Restoring camera lighting never reassigns the native ambient probe");
            Check(skybox.GetColor("_SkyColor").b==0.7f&&skybox.GetFloat("_StarsIntensity")==2&&Shader.GetGlobalVector("_EnviroLighting").x==1,"Sky material and Enviro globals restore exactly");
            StormRunner.Strength=0.1f;StormFog.Push(camera);Check(sun.intensity>1.8f,"Storm approach begins with gentle lighting reduction");StormFog.Restore();
            Plugin.Darkness=2f;StormRunner.Strength=1;StormFog.Push(camera);
            Check(sun.intensity>=0.69f&&RenderSettings.ambientIntensity>=0.65f&&StormLighting.SkyFactor(1)>=0.54f,"Maximum darkening retains bounded native daylight fractions");StormFog.Restore();Plugin.Darkness=1f;
            sun.intensity=0.8f;StormRunner.Strength=1;
            for(int i=0;i<100;i++){StormFog.Push(camera);StormFog.PostRender(camera);}
            Check(sun.intensity==0.8f,"Repeated frames preserve changing native day/night light instead of accumulating darkness");
            Plugin.Darkness=0f;StormFog.Push(camera);Check(sun.intensity==0.8f&&skybox.GetColor("_SkyColor").b==0.7f,"Darkening zero preserves lighting while allowing storm haze");StormFog.Restore();
            Plugin.Darkness=1f;StormFog.Push(camera);
            MethodInfo finalizer=typeof(StormEnviroFogPatch).GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static);
            finalizer.Invoke(null,new object[]{new Exception("Simulated render failure"),true});
            Check(sun.intensity==0.8f&&RenderSettings.ambientIntensity==1.2f,"Image-effect error restores actual lighting as well as fog");
            skybox.ThrowOnceOnWrite=true;bool failed=false;
            try{StormFog.Push(camera);}catch(Exception){failed=true;}
            Check(failed&&sun.intensity==0.8f&&RenderSettings.ambientIntensity==1.2f&&RenderSettings.fogMode==FogMode.Linear,"Partially applied lighting failure unwinds both lighting and fog");
            Plugin.Darkness=null;StormLighting.Clear();
        }
        private static void AmbientRecoveryChecks(Camera camera)
        {
            StormFog.Restore();StormLighting.Clear();Plugin.Darkness=1f;StormRunner.View=camera;StormRunner.Strength=1;
            UnityEngine.Rendering.SphericalHarmonicsL2 before=new UnityEngine.Rendering.SphericalHarmonicsL2();before[0,0]=0.1f;
            RenderSettings.ambientProbe=before;int writes=RenderSettings.ProbeWrites,requests=DynamicGI.Requests;
            StormFog.Push(camera);
            Check(RenderSettings.ProbeWrites==writes,"Storm camera draw cannot install a custom ambient-probe snapshot");
            UnityEngine.Rendering.SphericalHarmonicsL2 latest=new UnityEngine.Rendering.SphericalHarmonicsL2();latest[0,0]=0.9f;latest[1,4]=-0.35f;latest[2,8]=0.7f;
            RenderSettings.ambientProbe=latest;
            Check(!StormLighting.Recover(true)&&DynamicGI.Requests==requests,"Environment recovery waits until camera overrides have unwound");
            StormFog.Restore();
            Check(RenderSettings.ambientProbe[0,0]==0.9f&&RenderSettings.ambientProbe[1,4]==-0.35f&&RenderSettings.ambientProbe[2,8]==0.7f&&RenderSettings.ProbeWrites==writes+1,"A native GI completion during rendering survives camera restoration instead of reverting to the older probe");
            Check(!StormLighting.Recover(false)&&DynamicGI.Requests==requests,"Active weather and its residual fade cannot request a dusty environment bake");
            StormRunner.Strength=0;RenderSettings.ambientIntensity=0.85f;RenderSettings.ambientSkyColor=new Color(0.63f,0.5f,0.4f,1);
            Check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+1,"Fully clear weather schedules one clean native environment refresh");
            Check(DynamicGI.AmbientAtRequest==0.85f&&DynamicGI.SkyAtRequest.r==0.63f&&DynamicGI.ProbeAtRequest[0,0]==0.9f,"Recovery uses current native daylight and the latest probe rather than a pre-storm time-of-day snapshot");
            bool quiet=true;for(int i=0;i<120;i++)quiet&=!StormLighting.Recover(true);
            Check(quiet&&DynamicGI.Requests==requests+1,"Repeated calm updates do not flood GI readback requests");
            StormRunner.Strength=1;StormFog.Push(camera);StormFog.Restore();
            Check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+2,"A later storm independently marks its environment for one recovery");
            StormFog.Push(camera);StormFog.Restore();requests=DynamicGI.Requests;StormLighting.Clear(true);
            Check(DynamicGI.Requests==requests+1&&!StormLighting.Recover(true),"In-game disable/cleanup refreshes once after restoring native lighting");
            StormFog.Push(camera);StormFog.Restore();requests=DynamicGI.Requests;StormLighting.Clear();
            Check(!StormLighting.Recover(true)&&DynamicGI.Requests==requests,"Scene cleanup without recovery discards pending work instead of applying an old scene's sky");
            Plugin.Darkness=0f;StormFog.Push(camera);StormFog.Restore();
            Check(!StormLighting.Recover(true)&&DynamicGI.Requests==requests,"Darkening zero does not create an unnecessary environment refresh");
            Plugin.Darkness=1f;StormFog.Push(camera);StormFog.Restore();DynamicGI.ThrowOnce=true;
            Check(!StormLighting.Recover(true)&&!StormLighting.Recover(true)&&DynamicGI.Requests==requests+1,"A failed GI request is contained and cannot retry every frame");
            requests=DynamicGI.Requests;StormFog.Push(camera);float dim=RenderSettings.ambientIntensity;
            Check(StormFog.HasOverrides&&dim<0.85f,"Interrupted camera fixture holds an actual temporary lighting override");
            StormFog.Restore();
            Check(!StormFog.HasOverrides&&RenderSettings.ambientIntensity==0.85f,"Next-frame restoration unwinds an interrupted draw without leaving shaded areas dimmed");
            Check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+1,"Interrupted draw recovery refreshes only after all temporary state is gone");
            bool native=true;writes=RenderSettings.ProbeWrites;requests=DynamicGI.Requests;
            foreach(UnityEngine.Rendering.AmbientMode mode in new[]{UnityEngine.Rendering.AmbientMode.Skybox,UnityEngine.Rendering.AmbientMode.Trilight,UnityEngine.Rendering.AmbientMode.Flat})
            {
                RenderSettings.ambientMode=mode;
                for(int frame=0;frame<40;frame++)
                {
                    float intensity=0.2f+frame*0.03f;RenderSettings.ambientIntensity=intensity;
                    UnityEngine.Rendering.SphericalHarmonicsL2 current=new UnityEngine.Rendering.SphericalHarmonicsL2();
                    for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)current[channel,coefficient]=(frame+1)*(coefficient%2==0?1f:-1f)*(channel+1)*0.001f;
                    RenderSettings.ambientProbe=current;StormRunner.Strength=1-frame/40f;StormFog.Push(camera);StormFog.PostRender(camera);
                    native&=RenderSettings.ambientIntensity==intensity&&RenderSettings.ambientMode==mode;
                    for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)native&=RenderSettings.ambientProbe[channel,coefficient]==current[channel,coefficient];
                    native&=!StormLighting.Recover(false);
                }
            }
            Check(native&&RenderSettings.ProbeWrites==writes+120&&DynamicGI.Requests==requests,"Changing native daylight and all27 positive/negative SH coefficients survive120 storm draws across all ambient modes without mod probe writes or mid-storm baking");
            StormRunner.Strength=0;Check(StormLighting.Recover(true)&&DynamicGI.Requests==requests+1,"Clearing after changing daylight still requests only one clean recovery");
            StormLighting.Clear();Plugin.Darkness=null;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Skybox;
        }
        private static void WindChecks()
        {
            GameObject car=new GameObject("Truck");GameObject drive=new GameObject("DriveTrigger");drive.transform.parent=car.transform;
            NWH.VehiclePhysics2.VehicleController vehicle=car.Add<NWH.VehiclePhysics2.VehicleController>();Rigidbody body=car.Add<Rigidbody>();
            GameObject player=new GameObject("WindTestPlayer");player.transform.parent=car.transform;
            PlayMakerFSM inCar=player.Add<PlayMakerFSM>();inCar.FsmName="InCar";inCar.ActiveStateName="InCar";
            PlayMakerFSM health=player.Add<PlayMakerFSM>();health.FsmName="Health";health.ActiveStateName="OnFoot";
            StormModel model=new StormModel();model.Begin(0,0,0,420,90,75,9);
            VehicleWind.Tick(player,model,0.8f,0.9f);Check(body.Forces==1&&body.LastForce.x>0&&body.LastForce.y==0,"Driver gets gentle planar pressure, no lift");
            Check(body.LastForce.magnitude/body.mass<=0.45f&&body.LastPoint.y<=0.56f&&Math.Abs(body.LastPoint.z)<=0.44f,"Force and roll/yaw lever arms remain capped");
            for(int i=0;i<200;i++)VehicleWind.Tick(player,model,1,1);
            Check(body.LastForce.magnitude<=6000&&body.LastForce.magnitude/body.mass<=0.45f,"Repeated peak gusts remain bounded");
            Vector3 torque=Vector3.Cross(body.LastPoint-body.worldCenterOfMass,body.LastForce);
            Check(Math.Abs(torque.z)>0&&Math.Abs(torque.y)>0&&body.LastForce.y==0,"Horizontal pressure produces suspension lean and a modest yaw moment without lift");
            int calls=body.Forces;inCar.ActiveStateName="OnFoot";VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"On-foot player cannot buffet nearby car");
            inCar.ActiveStateName="InCar";vehicle.Grounded=false;VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Airborne vehicle gets no wind force");
            vehicle.Grounded=true;body.angularVelocity=new Vector3(0,1,0);VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Unstable rotating vehicle gets no added buffet");
            body.angularVelocity=Vector3.zero;body.transform.up=new Vector3(1,0,0);VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Tipped vehicle gets no wind force");
            body.transform.up=Vector3.up;Plugin.Buffeting=0f;VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Buffeting zero immediately stops force");
            Plugin.Buffeting=null;Plugin.Active=false;VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Disabled mod never applies vehicle pressure");
            Plugin.Active=true;body.mass=float.NaN;VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Invalid chassis mass gets no force");
            body.mass=1600;body.worldCenterOfMass=new Vector3(float.PositiveInfinity,0,0);VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Invalid center of mass gets no force");
            body.worldCenterOfMass=Vector3.zero;health.ActiveStateName="playerDeath";VehicleWind.Tick(player,model,1,1);Check(body.Forces==calls,"Death stops driver wind force");VehicleWind.Reset();
        }
        private sealed class PlaneWorld : IDebrisWorld
        {
            internal Vector3 Surface=Vector3.zero;
            public bool Sweep(Vector3 p,float radius,Vector3 move,out DebrisHit hit)
            {
                hit=new DebrisHit();float fraction=2;bool found=false;
                if(move.y<0&&p.y+move.y<radius){fraction=Math.Max(0,(radius-p.y)/move.y);hit.Normal=Vector3.up;found=true;}
                if(move.x>0&&p.x+move.x>5-radius){float f=Math.Max(0,(5-radius-p.x)/move.x);if(f<fraction){fraction=f;hit.Normal=new Vector3(-1,0,0);found=true;}}
                hit.Distance=fraction*move.magnitude;hit.SurfaceVelocity=Surface;return found;
            }
            public bool Penetration(Vector3 p,float radius,out Vector3 correction,out DebrisHit hit)
            {
                correction=Vector3.zero;hit=new DebrisHit();hit.SurfaceVelocity=Surface;
                if(p.y<radius){hit.Normal=Vector3.up;correction=Vector3.up*(radius-p.y+0.001f);return true;}
                if(p.x>5-radius){hit.Normal=new Vector3(-1,0,0);correction=hit.Normal*(p.x-(5-radius)+0.001f);return true;}return false;
            }
        }
        private static void DebrisChecks()
        {
            Vector3 bounce=DebrisMotion.Bounce(new Vector3(10,-5,0),Vector3.up,Vector3.zero);
            Check(bounce.y>0&&bounce.x<10,"Twig floor impact bounces with friction");
            Check(bounce.magnitude<new Vector3(10,-5,0).magnitude,"Static collision loses energy");
            bounce=DebrisMotion.Bounce(new Vector3(0,2,0),Vector3.up,Vector3.zero);Check(bounce.y==2,"Separating twig is not reflected back into surface");
            bounce=DebrisMotion.Bounce(new Vector3(10,0,0),new Vector3(-1,0,0),new Vector3(-60,0,0));
            Check(bounce.x<0&&bounce.magnitude<=DebrisMotion.MaxSpeed+0.001f,"Moving vehicle surface redirects twig at capped speed");
            PlaneWorld world=new PlaneWorld();Vector3 p=new Vector3(4.6f,1,0),v=new Vector3(22,0,0);
            DebrisMotion.Step(ref p,ref v,0.1f,0.02f,new Vector3(1,0,0),1,world);
            Check(p.x<=4.9f&&v.x<0,"Fast twig sweep bounces before crossing thin wall");
            p=new Vector3(0,1,0);v=new Vector3(0,-10,0);
            for(int i=0;i<200;i++)DebrisMotion.Step(ref p,ref v,0.2f,0.02f,new Vector3(1,0,0),0.8f,world);
            Check(p.y>=0.199f&&p.x<=4.801f,"Repeatedly bouncing twig stays above ground and outside wall over many steps");
            p=new Vector3(5.1f,0.05f,0);v=new Vector3(3,-1,0);
            DebrisMotion.Step(ref p,ref v,0.2f,0.02f,new Vector3(1,0,0),0.5f,world);
            Check(p.x<=4.801f&&p.y>=0.199f,"Initial overlaps are repaired instead of passing through geometry");
            float previous=p.x;DebrisMotion.Step(ref p,ref v,0.2f,0,new Vector3(1,0,0),1,world);Check(p.x==previous,"Paused debris does not move");
            GameObject root=new GameObject("QueryTest");new DebrisWorld(root);SphereCollider probe=root.GetComponentsInChildren<SphereCollider>(true)[0];
            Check(!probe.enabled&&probe.isTrigger,"Collision query shape cannot generate damaging native contacts");
        }
    }
}
