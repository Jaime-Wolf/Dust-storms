using System;
using System.Reflection;
using UnityEngine;
namespace ApocaDustStorm
{
    // Focused terrain service checks; these do not render terrain.
    internal static class TerrainElevationChecks
    {
        internal static void Run(Action<bool,string> check)
        {
            TerrainGround(check); VolumeGround(check);
        }
        private static void TerrainGround(Action<bool,string> check)
        {
            Terrain tile=new GameObject("Elevation tile").Add<Terrain>();
            tile.transform.position=new Vector3(-1000,-400,-500);
            tile.terrainData=new TerrainData{size=new Vector3(1000,1000,1000),Height=(x,z)=>100+500*x+200*z};
            Terrain.Tiles=new[]{tile};Time.unscaledTime=2000;
            StormTerrainSampler sample=new StormTerrainSampler();int reads=Terrain.Reads,rays=Physics.Raycasts;
            sample.Prepare(new Vector3(0,900,0));Vector3 p;
            check(sample.Ground(new Vector3(-900,900,-400),out p)&&Math.Abs(p.y+230)<.001,"High observer samples deep valley from normalized tile height plus world base");
            check(sample.Ground(new Vector3(-100,0,400),out p)&&Math.Abs(p.y-330)<.001,"Low observer samples elevated ridge independently of observer Y");
            check(Physics.Raycasts==rays,"Heightmaps work without terrain colliders or raycasts");
            sample.Prepare(Vector3.zero);check(Terrain.Reads==reads+1,"Tile list is reused within its bounded refresh interval");
            Vector3 shift=new Vector3(1000,-20,1000);tile.transform.position+=shift;
            check(sample.Ground(new Vector3(100,0,600),out p)&&Math.Abs(p.y+250)<.001,"Cached terrain references follow coordinate translations without stale world bounds");
            Terrain.Tiles=new Terrain[0];sample.Reset();sample.Prepare(new Vector3(0,1000,0));
            TerrainCollider ground=new GameObject("Fallback terrain").Add<TerrainCollider>();
            Collider roof=new GameObject("Static roof").Add<Collider>();
            Vector3 start=Vector3.zero;float length=0;
            Physics.RaycastFixture=(o,d,n)=>{start=o;length=n;return new[]{new RaycastHit{collider=roof,distance=100,point=new Vector3(0,2900,0),normal=Vector3.up},new RaycastHit{collider=ground,distance=3600,point=new Vector3(0,-600,0),normal=Vector3.up}};};
            check(sample.Ground(new Vector3(0,1000,0),out p)&&p.y==-600&&start.y==3000&&length==6000,"Fallback spans mountain/valley elevations and ignores roofs");
            rays=Physics.Raycasts;for(int i=0;i<100;i++)sample.Ground(Vector3.zero,out p);
            check(Physics.Raycasts-rays==23,"Unloaded terrain fallback raycasts are capped per contour refresh");
            Physics.RaycastFixture=null;sample.Reset();sample.Prepare(Vector3.zero);
            check(!sample.Ground(Vector3.zero,out p),"Missing terrain returns unknown rather than the player's height as ground");
            UnityEngine.Object.Destroy(tile);UnityEngine.Object.Destroy(ground);UnityEngine.Object.Destroy(roof);
        }
        private sealed class Slope : IDustPickupWorld
        {
            public bool Ground(Vector3 location,out Vector3 point){point=new Vector3(location.x,-500+location.x*.2f,location.z);return true;}
            public bool OpenSky(Vector3 point){return true;}
        }
        private static void VolumeGround(Action<bool,string> check)
        {
            Plugin.Active=true;StormRunner.Model.BeginFrom(0,0,WindFrom.West,600,90,75,9);StormRunner.Model.Tick(20);
            HaboobWall wall=new HaboobWall(new GameObject("Mountain front fixture").transform);
            wall.Tick(new Vector3(0,1500,0),.1f,.1f,StormRunner.Model,new Slope());
            BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            float[] heights=(float[])typeof(HaboobWall).GetField("heights",flags).GetValue(wall);
            bool[] sampled=(bool[])typeof(HaboobWall).GetField("sampled",flags).GetValue(wall);
            bool correct=heights.Length==294;
            for(int i=0;i<heights.Length;i+=6) correct&=sampled[i]&&Math.Abs(heights[i]-heights[i+5]-67)<.001&&heights[i]<-300;
            check(correct,"All six depth bands sample their own sloping footprint and never initialize at mountain camera height");
            wall.Dispose();StormRunner.Model.Reset();
        }
    }
}
