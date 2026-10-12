using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
namespace ApocaDustStorm
{
    internal static class PhasedRuntimeChecks
    {
        private static T Field<T>(object instance,string name){return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
        private static T Static<T>(Type type,string name){return (T)type.GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}
        internal static void Run(Action<bool,string> check)
        {
            Entry(check);Curves(check);Grading(check);Wind(check);NearDust(check);Scatter(check);
            StormBrownout.Clear();PlayerStormHazards.Reset();StormLighting.Clear();StormView.Clear();
            Plugin.Brownout=Plugin.MicroDust=Plugin.LightScatter=Plugin.Dust=Plugin.Gusts=null;
            StormRunner.ImpactStrength=0;StormRunner.OriginAnchor=null;StormRunner.Model.Reset();
        }
        private static void Entry(Action<bool,string> check)
        {
            HaboobEntryModel e=new HaboobEntryModel();
            check(!e.Step(true,0,200,.1,false)&&!e.Step(true,1,20,.1,false)&&e.Step(true,2,-1,.1,false),"Entry fires when an armed front physically crosses the player");
            check(!e.Step(true,3,2,.1,false)&&!e.Step(true,4,-2,.1,false),"Small boundary oscillations cannot repeat entry bursts");
            check(!e.Step(true,12,180,9,false)&&e.Step(true,13,-10,1,false),"Leaving well ahead of the front and waiting rearms a later physical entry");
            e.Reset();e.Step(true,1,200,.1,false);check(!e.Step(true,2,-400,.1,false),"Large teleports across the front do not trigger an impact");
            e.Reset();e.Step(true,1,200,.1,false);check(!e.Step(true,2,-1,.1,true),"Sleep/time skipping cannot trigger a late entry burst");
            e.Reset();check(!e.Step(true,50,-10,.1,false),"Starting already inside a storm cannot fabricate a wall impact");
            e.Step(true,51,200,9,false);check(!e.Step(true,0,-10,.1,false),"A new storm resets the previous boundary sample");
            e.Reset();e.Step(true,1,200,.1,false);e.Step(true,2,20,double.NaN,false);check(e.Step(true,3,-1,.1,false),"Invalid frame time cannot permanently poison the entry cooldown");
            check(!e.Step(true,double.NaN,-2,.1,false)&&!e.Step(false,4,-2,.1,false),"Invalid storm clocks and inactive storms reset entry state");
        }
        private static void Curves(Action<bool,string> check)
        {
            StormModel m=new StormModel();HaboobTransition t=new HaboobTransition();
            m.BeginFrom(0,0,WindFrom.West,600,90,75,9);m.Tick(50);
            double edge=m.SignedDistance(0,0);Vector3 ahead=new Vector3((float)(600-edge),0,0);
            HaboobFrame early=t.Step(m,ahead,.1f,false);
            check(early.Wind>.5f&&early.Fog<.03f,"Gust-front wind rises before nearby fog becomes dense");
            check(early.Palette>early.Fog&&early.Light>early.Fog&&early.Grade==0,"Warm sky and dimming begin ahead of brownout grading");
            bool same=true,bounds=true;double maxStep=0;float last=-1,min=1,max=0;
            for(int d=1800;d>=-600;d-=2)
            {
                Vector3 p=new Vector3((float)(d-edge),0,0);HaboobFrame f=t.Step(m,p,.01f,false);
                same&=Math.Abs(f.Fog-m.Intensity(p.x,p.z))<.000002;
                foreach(float value in new[]{f.Fog,f.Wind,f.Light,f.Palette,f.Grade,f.Impact}){bounds&=value>=0&&value<=1.00001;min=Math.Min(min,value);max=Math.Max(max,value);}
                if(last>=0)maxStep=Math.Max(maxStep,Math.Abs(f.Fog-last));last=f.Fog;
            }
            check(same,"Separate phase curves preserve the existing local peak-fog and visibility model");
            check(bounds&&maxStep<.01,"Phase outputs stay bounded and continuous through the front: min="+min+", max="+max+", largest fog step="+maxStep);
            t.Reset();t.Step(m,new Vector3((float)(200-edge),0,0),.1f,false);
            HaboobFrame impact=t.Step(m,new Vector3((float)(-1-edge),0,0),.1f,false);
            check(impact.Entered&&impact.Impact==1,"A physical crossing starts the impact curve exactly once");
            HaboobFrame trail=t.Step(m,new Vector3((float)(-2-edge),0,0),.3f,false);
            check(!trail.Entered&&trail.Impact>0&&trail.Impact<1,"Entry impact decays after the one-shot burst");
            check(t.Step(m,ahead,.1f,true).Impact==0,"Skipped time cancels residual impact");
            Vector3 position=new Vector3((float)(-250-edge),0,0);HaboobFrame before=t.Step(m,position,.1f,false);
            m.ShiftOrigin(-1000,1000);position+=new Vector3(-1000,0,1000);HaboobFrame shifted=t.Step(m,position,.1f,false);
            check(Math.Abs(before.Fog-shifted.Fog)<.00001&&Math.Abs(before.Wind-shifted.Wind)<.00001,"Origin shifts preserve local phase progress without a spurious crossing");
            HaboobFrame invalid=t.Step(m,new Vector3(float.NaN,0,0),.1f,false);check(invalid.Fog==0&&invalid.Wind==0&&invalid.Impact==0,"Invalid coordinates cannot poison smooth phase state");
            m.Stop();m.Tick(29);check(t.Step(m,position,.1f,false).Wind<.01,"Manual clearing fades even the early wind phase");
            m.Tick(2);HaboobFrame clear=t.Step(m,position,.1f,false);check(clear.Fog==0&&clear.Wind==0&&clear.Light==0&&clear.Palette==0&&clear.Grade==0&&clear.Impact==0,"Cleared storms return every phase to zero");
        }
        private static void Grading(Action<bool,string> check)
        {
            StormBrownout.Clear();Plugin.Active=true;PlayerStormHazards.Sheltered=false;
            Camera view=new GameObject("Grading gameplay").Add<Camera>();StormRunner.View=view;
            PostProcessLayer layer=view.gameObject.Add<PostProcessLayer>();layer.volumeLayer=new LayerMask{value=1<<7};
            StormBrownout.Tick(view,.8f);PostProcessVolume volume=Static<PostProcessVolume>(typeof(StormBrownout),"volume");
            PostProcessProfile profile=volume.sharedProfile;ColorGrading grading=(ColorGrading)profile.settings[0];
            check(volume.gameObject.layer==7&&volume.isGlobal&&volume.weight==0&&volume.priority==100,"Private grading uses an existing camera volume layer and stays weightless outside blending");
            check(grading.enabled.value&&grading.temperature.value==8&&grading.saturation.value==-24&&grading.contrast.value==-8,"Owned brownout palette has bounded warm/desaturation settings");
            float saved=StormBrownout.Before(layer,view);check(Math.Abs(volume.weight-.8f)<.00001,"Active gameplay view receives its requested brownout weight");
            StormBrownout.After(saved);check(volume.weight==0,"Volume weight restores after active-camera blending");
            Camera other=new GameObject("Grading menu").Add<Camera>();PostProcessLayer otherLayer=other.gameObject.Add<PostProcessLayer>();
            saved=StormBrownout.Before(otherLayer,other);check(volume.weight==0,"Non-gameplay cameras do not receive the global brownout volume");StormBrownout.After(saved);
            PlayerStormHazards.Sheltered=true;StormBrownout.Tick(view,1);saved=StormBrownout.Before(layer,view);check(Math.Abs(volume.weight-.3f)<.00001,"Proper shelter reduces grading while outside dust remains visible");StormBrownout.After(saved);PlayerStormHazards.Sheltered=false;
            Plugin.Brownout=0f;StormBrownout.Tick(view,1);saved=StormBrownout.Before(layer,view);check(volume.weight==0,"Brownout slider zero removes grading immediately");StormBrownout.After(saved);Plugin.Brownout=null;
            StormBrownout.Tick(view,1);Plugin.Active=false;saved=StormBrownout.Before(layer,view);check(volume.weight==0,"Disabled mod cannot grade a camera");StormBrownout.After(saved);Plugin.Active=true;
            StormBrownout.Suspend();saved=StormBrownout.Before(layer,view);check(volume.weight==0,"Suspending a missing gameplay view removes its grading");StormBrownout.After(saved);
            StormBrownout.Clear();check(profile.Destroyed&&grading.Destroyed&&!volume.gameObject.activeInHierarchy,"Cleanup destroys owned profile/settings and deactivates its owner");
            StormRunner.View=other;StormBrownout.Tick(other,1);check(Static<PostProcessVolume>(typeof(StormBrownout),"volume")==null,"A camera with no usable volume mask adds no new render layer");
            Camera noLayer=new GameObject("No post-processing").Add<Camera>();StormBrownout.Tick(noLayer,1);check(Static<PostProcessVolume>(typeof(StormBrownout),"volume")==null,"Absent native post-processing safely retains existing brown fog");StormBrownout.Clear();
        }
        private static void Wind(Action<bool,string> check)
        {
            StormVegetationWind wind=new StormVegetationWind();WindZone zone=Field<WindZone>(wind,"zone");GameObject owner=Field<GameObject>(wind,"owner");
            StormModel m=new StormModel();m.BeginFrom(0,0,WindFrom.North,600,90,75,2);
            wind.Tick(m,1,1);check(owner.activeSelf&&zone.windMain==1.6f&&zone.windTurbulence==.8f&&zone.windPulseMagnitude==.2f,"Gust-front vegetation wind uses bounded wind strength and turbulence");
            int objects=UnityEngine.Object.All.Count,queries=UnityEngine.Object.SceneQueries;for(int i=0;i<100;i++)wind.Tick(m,.7f,.5f);
            check(objects==UnityEngine.Object.All.Count&&queries==UnityEngine.Object.SceneQueries,"Vegetation wind updates create no scene objects or scene searches");
            Plugin.Gusts=0f;wind.Tick(m,1,1);check(!owner.activeSelf&&zone.windMain==0,"Wind slider zero disables the owned WindZone");Plugin.Gusts=null;
            wind.Tick(m,1,1);wind.Silence();check(!owner.activeSelf&&zone.windTurbulence==0,"Pause or view loss silences vegetation wind");wind.Dispose();check(owner.Destroyed,"WindZone owner is removed on cleanup");
        }
        private static void NearDust(Action<bool,string> check)
        {
            PlayerStormHazards.Reset();Plugin.Active=true;StormRunner.Strength=1;StormRunner.ImpactStrength=1;
            Camera view=new GameObject("Near dust view").Add<Camera>();view.transform.position=new Vector3(0,3,0);StormRunner.View=view;
            StormNearDust dust=new StormNearDust(new GameObject("Near dust root").transform,Texture2D.whiteTexture);
            ParticleSystem ps=Field<ParticleSystem>(dust,"system");ParticleSystemRenderer renderer=Field<ParticleSystemRenderer>(dust,"renderer");Material material=Field<Material>(dust,"material");
            StormModel m=new StormModel();m.BeginFrom(0,0,WindFrom.North,600,90,75,2);
            dust.Tick(view.transform.position,.1f,1,1,m,true);
            check(ps.main.maxParticles==96&&!ps.collision.enabled&&!ps.emission.enabled&&ps.main.simulationSpace==ParticleSystemSimulationSpace.Local,"Micro dust is capped, manually emitted, relative and collision-free");
            check(ps.particleCount>=18&&ps.particleCount<=30,"Boundary entry adds one bounded close dust burst");
            bool direction=true,life=true;foreach(ParticleSystem.EmitParams p in ps.Live){direction&=p.velocity.z<0&&Math.Abs(p.velocity.x)<.01;life&=p.startLifetime>=.45f&&p.startLifetime<=.9f&&p.startSize<=.22f&&p.startColor.a<=.24001f;}
            check(direction&&life,"Near dust travels downwind with short lifetimes and restrained opacity");
            dust.PreCull(view);check(renderer.enabled&&material.GetFloat("unity_GUIZTestMode")==4,"Near dust renders only through the active depth-tested view");
            dust.PreCull(new GameObject("Near dust other").Add<Camera>());check(!renderer.enabled,"Other cameras cannot draw close storm streaks");
            int queries=UnityEngine.Object.SceneQueries,rays=Physics.Raycasts;for(int i=0;i<100;i++)dust.Tick(view.transform.position,.2f,1,1,m,false);
            check(ps.particleCount==96&&queries==UnityEngine.Object.SceneQueries&&rays==Physics.Raycasts,"Saturated close dust pool stays bounded without queries");
            PlayerStormHazards.Sheltered=true;dust.Tick(view.transform.position,.1f,1,1,m,false);dust.PreCull(view);check(ps.particleCount==0&&!renderer.enabled,"Shelter clears close dust instead of letting streaks pass through rooms");PlayerStormHazards.Sheltered=false;
            PlayerStormHazards.InVehicle=true;StormRunner.OriginAnchor=new GameObject("Cab player").transform;StormRunner.OriginAnchor.position=view.transform.position;
            dust.Tick(view.transform.position,.1f,1,1,m,true);check(ps.particleCount==0,"First-person cab view has no dust streaks inside the player");
            view.transform.position+=new Vector3(0,3,-6);Time.frameCount++;StormView.Clear();dust.Tick(view.transform.position,.1f,1,1,m,true);check(ps.particleCount>0,"Exterior chase view still sees windblown close dust");PlayerStormHazards.InVehicle=false;
            Plugin.MicroDust=0f;dust.Tick(view.transform.position,.1f,1,1,m,true);check(ps.particleCount==0,"Micro dust slider zero immediately clears its particles");Plugin.MicroDust=null;
            dust.Dispose();check(material.Destroyed&&Field<GameObject>(dust,"owner").Destroyed,"Close dust cleanup removes its owned material and object");StormRunner.ImpactStrength=0;
        }
        private static void Scatter(Action<bool,string> check)
        {
            PlayerStormHazards.Reset();Plugin.Active=true;StormView.Clear();Camera view=new GameObject("Scatter view").Add<Camera>();StormRunner.View=view;
            Light[] lights=new Light[6];for(int i=0;i<lights.Length;i++){lights[i]=new GameObject("Beam "+i).Add<Light>();lights[i].type=LightType.Spot;lights[i].intensity=1;lights[i].range=50;lights[i].color=Color.white;}
            typeof(StormLighting).GetField("headlights",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,lights);
            StormLightScatter scatter=new StormLightScatter(new GameObject("Scatter root").transform);Mesh mesh=Field<Mesh>(scatter,"mesh");MeshRenderer renderer=Field<MeshRenderer>(scatter,"renderer");
            scatter.Tick(Vector3.zero,.1f,1);scatter.PreCull(view);
            check(mesh.vertices.Length==128&&mesh.triangles.Length==576&&renderer.enabled,"Scattering is limited to four short eight-sided beam meshes");
            bool bounded=true;foreach(Vector3 p in mesh.vertices)bounded&=p.z<=14.001;foreach(Color c in mesh.colors)bounded&=c.a>=-.00001&&c.a<=.11001;
            check(bounded&&!renderer.receiveShadows,"Beam lengths and opacity stay bounded without added shadow maps");
            Vector3 before=mesh.vertices[0];lights[0].transform.position=new Vector3(1,2,3);scatter.PreCull(view);check((mesh.vertices[0]-before-new Vector3(1,2,3)).sqrMagnitude<.00001,"Beam geometry follows final light transforms without waiting for its next search tick");
            int queries=UnityEngine.Object.SceneQueries,rays=Physics.Raycasts,objects=UnityEngine.Object.All.Count;for(int i=0;i<100;i++)scatter.PreCull(view);
            check(queries==UnityEngine.Object.SceneQueries&&rays==Physics.Raycasts&&objects==UnityEngine.Object.All.Count,"Scatter render callbacks add no queries or Unity objects");
            lights[0].enabled=false;scatter.PreCull(view);check(mesh.vertices[0].sqrMagnitude==0&&mesh.vertices[31].sqrMagnitude==0,"Switching a light off hides its beam immediately");
            scatter.PreCull(new GameObject("Scatter other").Add<Camera>());check(!renderer.enabled,"Beam approximation is gated to the active gameplay camera");
            PlayerStormHazards.Sheltered=true;scatter.PreCull(view);check(!renderer.enabled,"Entering proper shelter immediately hides local dust beams");PlayerStormHazards.Sheltered=false;
            Plugin.LightScatter=0f;scatter.Tick(Vector3.zero,.1f,1);scatter.PreCull(view);check(!renderer.enabled,"Scatter slider zero removes beam drawing");Plugin.LightScatter=null;
            scatter.Tick(Vector3.zero,.1f,1);Vector3 old=Field<GameObject>(scatter,"owner").transform.position;Vector3 delta=new Vector3(-1000,0,1000);Field<GameObject>(scatter,"owner").transform.position+=delta;scatter.ShiftOrigin(delta);
            check((Field<GameObject>(scatter,"owner").transform.position-old-delta).sqrMagnitude<.00001,"Beam cache applies a world-origin translation once");
            scatter.Dispose();check(mesh.Destroyed&&Field<Material>(scatter,"material").Destroyed,"Scatter cleanup destroys owned mesh and material");
        }
    }
}
