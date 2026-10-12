using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class StormOptimizationChecks
    {
        private sealed class GroundWorld : IDustPickupWorld
        {
            internal int Queries; internal bool Blocked;
            internal readonly List<Vector3> Validated = new List<Vector3>();
            public bool GroundAt(Vector3 p,out Vector3 result) { Queries++; result=new Vector3(p.x,p.x*.2f,p.z); return !Blocked; }
            public bool Ground(Vector3 p,out Vector3 result) { return GroundAt(p,out result); }
            public bool OpenSky(Vector3 p) { Queries++; Validated.Add(p); return !Blocked; }
        }
        private sealed class Effect : MonoBehaviour {}
        private sealed class ExternalEffect : MonoBehaviour
        { private void OnRenderImage(RenderTexture a,RenderTexture b) {} }
        private static bool Same(Vector3 a,Vector3 b) { return a.x==b.x&&a.y==b.y&&a.z==b.z; }
        internal static void Run(Action<bool,string> check)
        {
            DustPickupSources sources=new DustPickupSources(new System.Random(2)); GroundWorld world=new GroundWorld();
            int emitted=0; bool valid=true, bounded=true; Vector3 point;
            for(int batch=0;batch<200;batch++)
            {
                sources.Advance(.1f); sources.BeginBatch(); int before=world.Queries;
                for(int i=0;i<36;i++) if(sources.TryGet(Vector3.zero,Vector3.forward,new Vector3(1,0,0),world,out point))
                { emitted++; valid &= world.Validated.Exists(delegate(Vector3 p){return Same(p,point);})&&Math.Abs(point.y-point.x*.2f)<.0001f; }
                bounded &= world.Queries-before<=12;
            }
            check(bounded,"Dust source placement never exceeds six validated locations/twelve casts per batch");
            check(valid&&emitted>5000,"Reused dust locations are exact tested points on a slope, with sustained emissions");
            check(world.Queries < emitted,"Cached ground/sky work is below one query per emitted particle in the seeded workload");
            world.Blocked=true;sources.Advance(1);sources.BeginBatch();bool underRoof=false;
            for(int i=0;i<36;i++) underRoof|=sources.TryGet(Vector3.zero,Vector3.forward,new Vector3(1,0,0),world,out point);
            check(!underRoof,"Expired anchors cannot emit when renewed ground/sky checks reject them");
            sources.Clear();world.Blocked=false;sources.BeginBatch();bool teleported=false;
            for(int i=0;i<36;i++) if(sources.TryGet(new Vector3(1000,0,1000),Vector3.forward,new Vector3(1,0,0),world,out point)) teleported|=point.x>900&&point.z>900;
            check(teleported,"Clearing source anchors after a teleport produces validated locations near the new view");
            float distance=DustVisibility.RetireDistance(1,45,1,1);
            check(!DustVisibility.Hidden(Vector3.forward*20,Vector3.zero,14,distance),"Nearby visible dust survives retirement checks");
            check(!DustVisibility.Hidden(Vector3.forward*(distance+15),Vector3.zero,14,distance),"Growing stretched plume edges survive past the center visibility limit");
            check(DustVisibility.Hidden(Vector3.forward*(distance+40),Vector3.zero,14,distance),"Obsolete twenty-metre plus double-size padding no longer retains invisible dust");
            check(DustVisibility.Hidden(Vector3.forward*(distance+80),Vector3.zero,14,distance),"Dust wholly beyond 99.9 percent haze attenuation can retire");
            check(float.IsPositiveInfinity(DustVisibility.RetireDistance(0,45,1,1)),"Clear weather cannot abruptly distance-cull the remaining particle fade");
            check(DustVisibility.RetireDistance(.2f,45,1,1)>distance*20,"Thin approaching weather retains its visible distant dust");
            check(DustVisibility.RetireDistance(1,90,1,1)>distance*1.9f,"Visibility setting still expands the particle range");
            check(!DustVisibility.Hidden(Vector3.forward*200,Vector3.zero,220,distance),"A large arrival-wall billboard survives when its near edge remains visible");
            check(DustVisibility.Hidden(Vector3.forward*1000,Vector3.zero,220,distance),"Arrival-wall dust entirely beyond opaque peak haze can retire");
            check(DustVisibility.Hidden(new Vector3(100,0,0),Vector3.zero,14,distance)==DustVisibility.Hidden(new Vector3(0,0,100),Vector3.zero,14,distance),"Distance retirement is independent of view rotation");

            DustOccupancy occupancy=new DustOccupancy(3);occupancy.Begin(Vector3.zero);
            Vector3 anchor=new Vector3(1,1,1);
            bool reservations=occupancy.TryReserve(anchor)&&occupancy.TryReserve(anchor)&&occupancy.TryReserve(anchor);
            check(reservations&&!occupancy.TryReserve(anchor),"Duplicate cached-anchor emissions stop after three reservations");
            check(occupancy.TryReserve(new Vector3(7,1,1)),"A nearby empty cell can still form a new plume");
            check(occupancy.TryReserve(new Vector3(1,7,1)),"Lifted dust does not permanently block its ground-level source");
            occupancy.Begin(Vector3.zero);occupancy.Observe(anchor);occupancy.Observe(anchor);occupancy.Observe(anchor);
            check(!occupancy.TryReserve(anchor),"Living particles counted by the existing scan prevent crowded births");
            occupancy.Begin(Vector3.zero);
            check(occupancy.TryReserve(anchor),"Particles leaving a cell free it on the next scan");
            occupancy.Begin(Vector3.zero);for(int i=0;i<300;i++)occupancy.Observe(anchor);
            check(!occupancy.TryReserve(anchor),"Extreme crowds saturate their counter instead of wrapping");
            check(occupancy.TryReserve(new Vector3(500,0,0)),"The bounded grid adds no artificial distant emission boundary");
            occupancy.Begin(new Vector3(-1000,0,-1000));
            Vector3 shifted=new Vector3(-1001,1,-1001);
            check(occupancy.TryReserve(shifted)&&occupancy.TryReserve(shifted)&&occupancy.TryReserve(shifted)&&!occupancy.TryReserve(shifted),"Negative and shifted world coordinates have independent stable cells");
            occupancy.Begin(Vector3.zero);
            check(occupancy.TryReserve(anchor),"Origin shifts clear occupancy before new local emissions");

            Camera camera=new GameObject("Shared depth test").Add<Camera>();Effect first=camera.gameObject.Add<Effect>(),second=camera.gameObject.Add<Effect>();
            StormDepthRequest.Register(first);StormDepthRequest.Register(second);
            StormDepthRequest.Acquire(camera,first);StormDepthRequest.Acquire(camera,second);StormDepthRequest.Release(camera,first);
            check(camera.depthTextureMode==DepthTextureMode.Depth,"One storm effect cannot release depth still needed by another");
            StormDepthRequest.Release(camera,second);check(camera.depthTextureMode==DepthTextureMode.None,"Last owned request restores an otherwise unused depth flag");
            camera.depthTextureMode=DepthTextureMode.Depth;StormDepthRequest.Acquire(camera,first);StormDepthRequest.Release(camera,first);
            check(camera.depthTextureMode==DepthTextureMode.Depth,"A preexisting native depth request is preserved");
            camera.depthTextureMode=DepthTextureMode.None;StormDepthRequest.Acquire(camera,first);camera.depthTextureMode|=DepthTextureMode.DepthNormals;StormDepthRequest.Release(camera,first);
            check(camera.depthTextureMode==(DepthTextureMode.Depth|DepthTextureMode.DepthNormals),"New foreign depth features cause conservative retention");
            camera.depthTextureMode=DepthTextureMode.None;ExternalEffect foreign=camera.gameObject.Add<ExternalEffect>();
            StormDepthRequest.Acquire(camera,first);StormDepthRequest.Release(camera,first);
            check(camera.depthTextureMode==DepthTextureMode.Depth,"An active external image effect keeps shared camera depth");
            foreign.enabled=false;StormDepthRequest.Forget(first);StormDepthRequest.Forget(second);

            Camera fogView=new GameObject("Fog scheduling test").Add<Camera>();
            StormRunner.View=fogView;StormRunner.Strength=.001f;StormRunner.Gust=0;Plugin.Active=true;
            RenderSettings.fog=false;fogView.farClipPlane=20;StormDistanceFog.Clear();StormDistanceFog.EnsureCamera(fogView);
            check(fogView.GetComponent<StormDistanceFog>()==null&&fogView.depthTextureMode==DepthTextureMode.None,"Imperceptible far-only fog installs no image effect and requests no depth");
            StormRunner.Strength=1;fogView.farClipPlane=3000;StormDistanceFog.EnsureCamera(fogView);
            StormDistanceFog pass=fogView.GetComponent<StormDistanceFog>();RenderTexture input=new RenderTexture(),output=new RenderTexture();
            pass.Render(input,output);Material mat=Graphics.LastMaterial;int writes=mat.UniformWrites;
            pass.Render(input,output);
            check(mat.UniformWrites-writes==6,"Stable distance fog only updates four pose matrices, eye and source texture");
            StormRunner.Strength=0;StormDistanceFog.EnsureCamera(fogView);
            check(!pass.enabled&&fogView.depthTextureMode==DepthTextureMode.None,"Storm fade completion disables the pass and releases its depth immediately");
            StormRunner.Strength=1;StormDistanceFog.EnsureCamera(fogView);
            Camera other=new GameObject("Fog switch target").Add<Camera>();StormRunner.View=other;StormDistanceFog.EnsureCamera(other);
            check(!pass.enabled&&fogView.depthTextureMode==DepthTextureMode.None&&other.depthTextureMode==DepthTextureMode.Depth,"Switching views transfers owned fog/depth instead of leaving the old camera enabled");
            StormDistanceFog.Clear();

            WindblownLizards.Reset(); PlayerStormHazards.Reset();
            StormModel storm=new StormModel();storm.Begin(0,0,0,600,90,75,1);StormRunner.View=other;StormRunner.Strength=.25f;
            int scans=UnityEngine.Object.SceneQueries;
            for(int i=0;i<600;i++)WindblownLizards.Tick(.1f,0,storm,false);
            check(UnityEngine.Object.SceneQueries==scans,"Calm lizard updates perform no prefab discovery");
            for(int i=0;i<420;i++){Time.unscaledTime+=.1f;WindblownLizards.Tick(.1f,.25f,storm,false);}
            scans=UnityEngine.Object.SceneQueries;int objects=UnityEngine.Object.All.Count;
            PlayerStormHazards.Sheltered=true;WindblownLizards.PreCull(other);
            check(UnityEngine.Object.All.Count==objects,"Shelter defers a due lizard without spawning it indoors");
            PlayerStormHazards.Sheltered=false;WindblownLizards.PreCull(other);
            check(UnityEngine.Object.All.Count>objects,"Approaching visible dust can produce its first edible lizard without peak-strength exposure");
            for(int i=0;i<300;i++){Time.unscaledTime+=.1f;WindblownLizards.Tick(.1f,.25f,storm,false);}
            check(UnityEngine.Object.SceneQueries==scans,"A resolved native lizard prefab causes no repeated scene searches");
            MethodInfo prefix=typeof(WindblownLizardPrefabPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
            check(prefix.ReturnType==typeof(void),"Prefab observation cannot suppress the native CreateObject action");
            WindblownLizards.Reset();PlayerStormHazards.Reset();StormRunner.Strength=0;
        }
    }
}
