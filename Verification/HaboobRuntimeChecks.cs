using System;
using System.Reflection;
using UnityEngine;
namespace ApocaDustStorm
{
    // Controlled lifecycle, geometry and depth-ownership checks, not GPU tests.
    internal static class HaboobRuntimeChecks
    {
        private sealed class GroundWorld : IDustPickupWorld
        {
            internal int Calls;internal float Height=40;
            public bool Ground(Vector3 input,out Vector3 point){Calls++;point=new Vector3(input.x,Height,input.z);return true;}
            public bool OpenSky(Vector3 point){throw new Exception("Wall must not perform sky probes");}
        }
        private static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
        internal static void Run(Action<bool,string> check)
        {
            Plugin.Active=true;Plugin.Dust=null;Plugin.WallStrength=null;PlayerStormHazards.Reset();StormView.Clear();
            Camera view=new GameObject("Volume camera").Add<Camera>();StormRunner.View=view;StormRunner.Strength=.5f;
            StormRunner.Model.BeginFrom(0,0,WindFrom.West,600,90,75,9);StormRunner.Model.Tick(20);
            HaboobWall wall=new HaboobWall(new GameObject("Volume root").transform);GroundWorld world=new GroundWorld();
            ParticleSystem ps=Field<ParticleSystem>(wall,"system");ParticleSystemRenderer renderer=Field<ParticleSystemRenderer>(wall,"renderer");
            GameObject owner=Field<GameObject>(wall,"owner");DustBillowAssets assets=Field<DustBillowAssets>(wall,"assets");
            check(ps.main.maxParticles==2058&&ps.Paused&&!ps.collision.enabled&&!ps.emission.enabled,"Front has 2058 manually controlled particles with no collision or automatic emission");
            check(owner.GetComponent<MeshFilter>()==null&&owner.GetComponent<Collider>()==null&&owner.GetComponent<Rigidbody>()==null,"Volume contains no sheet mesh, collider or rigidbody");
            check(ps.textureSheetAnimation.enabled&&ps.textureSheetAnimation.numTilesX==4&&ps.textureSheetAnimation.numTilesY==4,"Front uses sixteen animated atlas frames");
            check(renderer.Streams.Count==6&&renderer.Streams.Contains(ParticleSystemVertexStream.UV2)&&renderer.Streams.Contains(ParticleSystemVertexStream.AnimBlend),"Renderer supplies both UV frames and interpolation weight");
            check(assets.Texture.width==512&&assets.Texture.height==512&&assets.Texture.MipChain&&assets.Texture.AppliedMips,"Animated billow atlas uses filtered mip levels");
            check(assets.Material.Keywords.Contains("_FADING_ON")&&assets.Material.Keywords.Contains("SOFTPARTICLES_ON")&&assets.Material.Keywords.Contains("_REQUIRE_UV2"),"Compiled material requests soft depth fades and interpolated flipbooks");
            check(assets.Material.GetVector("_SoftParticleFadeParams").y==1f/32&&assets.Material.GetFloat("_ZWrite")==0,"Soft wall fades over 32 metres without writing opaque depth");
            check(!assets.Material.Passes["Always"]&&!assets.Material.Passes["ShadowCaster"],"Billows add no distortion capture or shadow map pass");
            bool neutral=true,border=true;int n=assets.Texture.width;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){Color32 c=assets.Texture.Pixels[y*n+x];neutral&=c.r==c.g&&c.g==c.b;if(x%128==0||x%128==127||y%128==0||y%128==127)border&=c.a==0;}
            check(neutral&&border,"All atlas frames retain neutral shading and empty cell borders");
            Vector3 eye=new Vector3(0,10,0);wall.Tick(eye,.1f,.05f,StormRunner.Model,world);wall.PreCull(view);
            check(renderer.enabled&&world.Calls==294&&ps.ParticleCount>0&&ps.ParticleCount<2058,"Visible front populates its volume and performs 294 heightmap footprint samples");
            check((view.depthTextureMode&DepthTextureMode.Depth)!=0,"Approaching front requests depth even before full storm fog");
            ParticleSystem.Particle[] storage=Field<ParticleSystem.Particle[]>(wall,"particles");Color[] colors=Field<Color[]>(wall,"colours");
            float min=10000,max=-10000;bool live=true,finite=true;for(int j=0;j<ps.ParticleCount;j++){var p=ps.Particles[j];min=Math.Min(min,p.position.x);max=Math.Max(max,p.position.x);live&=p.remainingLifetime>0&&p.remainingLifetime<=p.startLifetime;finite&=!float.IsNaN(p.position.y)&&!float.IsInfinity(p.position.z);}
            check(max-min>300&&live&&finite,"Front has genuine depth, live animated phases and finite positions");
            float alpha=colors[1000].a;Plugin.Dust=0f;wall.Tick(eye,.01f,.05f,StormRunner.Model,world);
            check(Math.Abs(colors[1000].a-alpha)<.000001,"Wall volume remains independent of the local dust slider");Plugin.Dust=null;
            Plugin.WallStrength=.25f;wall.Tick(eye,.01f,.05f,StormRunner.Model,world);check(Math.Abs(colors[1000].a-alpha*.25f)<.000001,"Wall strength scales volume opacity");Plugin.WallStrength=null;
            float[] heights=Field<float[]>(wall,"heights"),targets=Field<float[]>(wall,"targets");float[] before=(float[])heights.Clone();world.Height=400;
            wall.Tick(eye,.1f,.05f,StormRunner.Model,world);float change=0;for(int i=0;i<heights.Length;i++)change=Math.Max(change,Math.Abs(heights[i]-before[i]));
            check(change<=6.5001f,"New terrain samples cannot snap the rendered contour to a large height change");
            Vector3 old=owner.transform.position;int calls=world.Calls;StormRunner.Model.Tick(.01);wall.Tick(eye+Vector3.up*5,.01f,.05f,StormRunner.Model,world);
            check(Math.Abs(owner.transform.position.x-old.x-StormRunner.Model.Speed*.01)<.001&&owner.transform.position.y==old.y&&calls==world.Calls,"Front advances every frame without camera bob or extra ground samples");
            int objects=UnityEngine.Object.All.Count,queries=UnityEngine.Object.SceneQueries,start=world.Calls;
            for(int i=0;i<100;i++)wall.Tick(eye,.01f,.05f,StormRunner.Model,world);
            check(world.Calls-start<=2940&&UnityEngine.Object.All.Count==objects&&UnityEngine.Object.SceneQueries==queries,"Fast frames reuse fixed volume storage without scene searches or extra objects");
            check(ReferenceEquals(storage,Field<ParticleSystem.Particle[]>(wall,"particles"))&&ReferenceEquals(colors,Field<Color[]>(wall,"colours")),"Front reuses its particle and colour buffers");
            start=world.Calls;wall.Tick(eye,100,.05f,StormRunner.Model,world);check(world.Calls==start+294,"Long frames do not release catch-up terrain batches");
            Camera other=new GameObject("Other view").Add<Camera>();wall.PreCull(other);check(!renderer.enabled&&other.depthTextureMode==DepthTextureMode.None,"Other cameras cannot draw the front or acquire its depth");
            view.UseRenderedPose=true;view.RenderedPosition=owner.transform.position+storage[1000].position;StormView.Capture(view);wall.PreCull(view);
            bool absent=true;for(int j=0;j<ps.ParticleCount;j++)absent&=(ps.Particles[j].position-storage[1000].position).sqrMagnitude>.000001;
            check(absent,"Camera fades omit zero-alpha cards at the final rendered chase pose rather than its holder");view.UseRenderedPose=false;StormView.Clear();
            Camera replacement=new GameObject("New active view").Add<Camera>();StormRunner.View=replacement;wall.PreCull(replacement);
            check(view.depthTextureMode==DepthTextureMode.None&&replacement.depthTextureMode==DepthTextureMode.Depth,"Changing active cameras transfers the depth lease");
            old=owner.transform.position;float h=heights[0],target=targets[0];Vector3 delta=new Vector3(-1000,-5,1000);owner.transform.position+=delta;wall.ShiftOrigin(delta);
            check((owner.transform.position-old-delta).sqrMagnitude<.001&&heights[0]==h-5&&targets[0]==target-5,"Coordinate reset translates the volume once and preserves both terrain caches");
            start=world.Calls;Plugin.WallStrength=0f;wall.Tick(eye,1,.05f,StormRunner.Model,world);wall.PreCull(replacement);
            check(!renderer.enabled&&world.Calls==start&&ps.particleCount==0&&replacement.depthTextureMode==DepthTextureMode.None,"Zero wall strength stops drawing, terrain work and the owned depth request");Plugin.WallStrength=null;
            StormRunner.Model.Reset();wall.Tick(eye,1,0,StormRunner.Model,world);check(world.Calls==start&&!renderer.enabled,"Calm weather adds no wall terrain work");
            wall.Dispose();check(owner.Destroyed&&assets.Material.Destroyed&&assets.Texture.Destroyed,"Cleanup releases generated textures, material and volume owner");
            LocalBillows(check);VolumeMath(check);OriginChecks(check);
            StormRunner.Model.Reset();StormRunner.Strength=0;StormRunner.OriginAnchor=null;Plugin.Dust=null;Plugin.WallStrength=null;StormView.Clear();
        }
        private static void VolumeMath(Action<bool,string> check)
        {
            bool finite=true,loop=true,changing=false,clear=true;double shade,shade2;
            for(int y=0;y<=16;y++)for(int x=0;x<=16;x++)
            {
                double u=x/16.0,v=y/16.0,a=HaboobVolumeMath.Billow(u,v,0,out shade),b=HaboobVolumeMath.Billow(u,v,1,out shade2);
                finite&=a>=0&&a<=1&&shade>=0&&shade<=1;loop&=Math.Abs(a-b)<1e-10;
                changing|=Math.Abs(a-HaboobVolumeMath.Billow(u,v,.25,out shade2))>.01;
                if(x==0||y==0||x==16||y==16)clear&=a==0;
            }
            check(finite&&loop&&changing&&clear,"Billow animation is bounded, changes visibly and loops without a last-frame discontinuity");
            check(HaboobVolumeMath.Fade(0)==1&&HaboobVolumeMath.Fade(-620)==0&&HaboobVolumeMath.Fade(3000)==0,"Front covers impact before yielding to internal dust, with bounded far and rear envelopes");
            check(HaboobVolumeMath.CameraFade(0,200)==0&&HaboobVolumeMath.CameraFade(200,200)==1,"Individual billows fade before the camera can cut through their cards");
            double h=0;for(int i=0;i<120;i++)h=HaboobVolumeMath.TerrainStep(h,40,1.0/60);
            check(h>30&&h<40&&HaboobVolumeMath.TerrainStep(h,40,double.NaN)==h,"Terrain converges smoothly without overshoot or invalid-time contamination");
            bool slots=true;for(int i=-100;i<100;i++)slots&=HaboobVolumeMath.Slot(i*104)>=0&&HaboobVolumeMath.Slot(i*104)<49;
            check(slots,"Negative and positive crosswind cells map into the fixed terrain cache");
        }
        private static void LocalBillows(Action<bool,string> check)
        {
            Plugin.Active=true;Plugin.Dust=null;Plugin.MicroDust=null;PlayerStormHazards.Reset();StormView.Clear();StormRunner.OriginAnchor=null;
            Camera view=new GameObject("Local billow view").Add<Camera>();StormRunner.View=view;StormRunner.Strength=1;
            StormModel m=new StormModel();m.BeginFrom(0,0,WindFrom.North,600,90,75,3);m.Tick(90);
            StormBillows clouds=new StormBillows(new GameObject("Local billow root").transform);
            ParticleSystem ps=Field<ParticleSystem>(clouds,"system");ParticleSystemRenderer renderer=Field<ParticleSystemRenderer>(clouds,"renderer");
            clouds.Tick(Vector3.zero,.1f,1,1,m,true);clouds.PreCull(view);
            check(renderer.enabled&&ps.particleCount>=8&&ps.main.simulationSpace==ParticleSystemSimulationSpace.World&&!ps.collision.enabled,"Entry emits animated world-space billows without collision");
            bool direction=true;foreach(var p in ps.Live)direction&=p.velocity.z<0&&p.startSize>=9&&p.startSize<=22&&p.startLifetime>=4&&p.startLifetime<=7;
            check(direction,"Nearby clouds use downwind motion, metre-scale sizes and bounded lifetimes");
            for(int i=0;i<200;i++)clouds.Tick(Vector3.zero,.2f,1,1,m,false);check(ps.particleCount==144,"Local billows remain capped even at sustained maximum input");
            PlayerStormHazards.Sheltered=true;clouds.Tick(Vector3.zero,.1f,1,1,m,false);clouds.PreCull(view);
            check(ps.particleCount==0&&!renderer.enabled&&view.depthTextureMode==DepthTextureMode.None,"Shelter clears nearby clouds and releases their depth request");PlayerStormHazards.Sheltered=false;
            PlayerStormHazards.InVehicle=true;StormRunner.OriginAnchor=new GameObject("Cab player").transform;
            clouds.Tick(Vector3.zero,.1f,1,1,m,true);check(ps.particleCount==0,"First-person vehicle interior does not fill with nearby billows");PlayerStormHazards.InVehicle=false;
            Plugin.Dust=0f;clouds.Tick(Vector3.zero,.1f,1,1,m,true);check(ps.particleCount==0,"Zero local dust disables the added nearby clouds");Plugin.Dust=null;
            clouds.Dispose();StormRunner.OriginAnchor=null;
        }
        private static void OriginChecks(Action<bool,string> check)
        {
            StormRunner.OriginAnchor=new GameObject("Origin player").transform;
            StormRunner.OriginAnchor.position=new Vector3(1000,0,50);
            StormRunner.Model.Begin(1000,50,0,600,90,75,9);StormRunner.Model.Tick(20);
            StormOriginObserver.Snapshot state;StormOriginObserver.Before(out state);
            int count=StormRunner.OriginShifts;StormOriginObserver.After(state);
            check(StormRunner.OriginShifts==count,"No native translation means no origin update");
            double signed=StormRunner.Model.SignedDistance(1000,50);
            StormRunner.OriginAnchor.position+=new Vector3(-1000,0,1000);StormOriginObserver.After(state);
            Vector3 position=StormRunner.OriginAnchor.position;
            check(StormRunner.OriginShifts==count+1&&Math.Abs(StormRunner.Model.SignedDistance(position.x,position.z)-signed)<1e-7,"Observer preserves front when native world coordinates reset");
            StormOriginObserver.Before(out state);StormRunner.OriginAnchor=new GameObject("Replacement player").transform;
            StormOriginObserver.After(state);check(StormRunner.OriginShifts==count+1,"Stale player snapshots cannot shift a replacement session");
            Plugin.Active=false;StormOriginObserver.Before(out state);check(state.Anchor==null,"Disabled mod never observes player translations");Plugin.Active=true;
            StormOriginObserver.Before(out state);StormRunner.OriginAnchor.position=new Vector3(float.NaN,0,0);StormOriginObserver.After(state);
            check(StormRunner.OriginShifts==count+1,"Invalid native coordinates cannot contaminate the storm origin");
        }
    }
}
