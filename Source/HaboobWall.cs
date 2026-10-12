using System;
using UnityEngine;

namespace ApocaDustStorm
{
    // Particle-built volume, not ray marching or intersecting sheets. Six depth
    // bands contain independently animated, staggered billows. Native particle
    // sorting and depth fades blend the dust against opaque terrain and roads.
    internal sealed class HaboobWall : IDisposable
    {
        private readonly GameObject owner;
        private readonly ParticleSystem system;
        private readonly ParticleSystemRenderer renderer;
        private readonly DustBillowAssets assets;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[HaboobVolumeMath.Count];
        private readonly ParticleSystem.Particle[] drawing = new ParticleSystem.Particle[HaboobVolumeMath.Count];
        private readonly Color[] colours = new Color[HaboobVolumeMath.Count];
        private readonly float[] heights = new float[HaboobVolumeMath.Columns*HaboobVolumeMath.Depths], targets = new float[HaboobVolumeMath.Columns*HaboobVolumeMath.Depths];
        private readonly double[] cells = new double[HaboobVolumeMath.Columns];
        private readonly bool[] valid = new bool[HaboobVolumeMath.Columns*HaboobVolumeMath.Depths], sampled = new bool[HaboobVolumeMath.Columns*HaboobVolumeMath.Depths];
        private float probeClock;
        private int probe;
        private bool visible, hasPosition;
        private Vector3 position;
        internal HaboobWall(Transform parent)
        {
            try
            {
                owner = new GameObject("ApocaDustStorm.HaboobVolume"); owner.hideFlags = HideFlags.HideAndDontSave; owner.transform.SetParent(parent,false);
                assets = new DustBillowAssets(owner,32,8,35);
                system = owner.AddComponent<ParticleSystem>(); renderer = system.GetComponent<ParticleSystemRenderer>();
                DustBillowAssets.Configure(system,renderer,particles.Length); renderer.sharedMaterial = assets.Material;
                ParticleSystem.MainModule main = system.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                system.Play(); system.Pause(); // Motion and particle lifetime are supplied below.
            }
            catch { Dispose(); throw; }
        }
        internal void Tick(Vector3 eye, float dt, float strength, StormModel model, IDustPickupWorld world)
        {
            float amount = Mathf.Clamp(Plugin.Value(Plugin.WallStrength,1),0,2);
            double signed = model.SignedDistance(eye.x,eye.z);
            double alpha = model.FrontOpacity(eye.x,eye.z)*HaboobVolumeMath.Fade(signed)*amount;
            visible = Plugin.Active && model.Active && alpha>0.002 && signed<3000 && signed>-620;
            if (!visible) { Hide(); assets.Camera(null,false); system.Clear(); return; }
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt<0) dt=0;
            Vector3 heading = new Vector3((float)model.PrevailingX,0,(float)model.PrevailingZ), cross = new Vector3(-heading.z,0,heading.x);
            float anchorY = hasPosition ? position.y : eye.y;
            Vector3 edge = new Vector3((float)model.OriginX,anchorY,(float)model.OriginZ)+heading*(float)model.Front;
            double center = Math.Floor(Vector3.Dot(eye-edge,cross)/HaboobVolumeMath.Spacing)*HaboobVolumeMath.Spacing;
            position=edge+cross*(float)center; hasPosition=true; owner.transform.position=position;
            bool fresh=false;
            for (int col=0; col<HaboobVolumeMath.Columns; col++)
            {
                double side=center+(col-HaboobVolumeMath.Columns/2)*HaboobVolumeMath.Spacing; int slot=HaboobVolumeMath.Slot(side);
                if (!valid[slot*HaboobVolumeMath.Depths] || cells[slot]!=side)
                {
                    cells[slot]=side; fresh=true;
                    for (int depth=0;depth<HaboobVolumeMath.Depths;depth++)
                    { int index=slot*HaboobVolumeMath.Depths+depth; valid[index]=true; sampled[index]=false; }
                }
            }
            probeClock+=Mathf.Min(dt,0.25f);
            if (probeClock>=0.1f || fresh)
            {
                probeClock=0;
                StormTerrainSampler terrain=world as StormTerrainSampler;
                if (terrain!=null) terrain.Prepare(eye);
                // Query each depth band's footprint, not just the leading plane.
                // Rotate the starting column so limited fallback rays cannot starve
                // unloaded tiles. Heightmap reads need no collider or scene search.
                for (int i=0;i<HaboobVolumeMath.Columns;i++)
                {
                    int col=(probe+i)%HaboobVolumeMath.Columns;
                    double side=center+(col-HaboobVolumeMath.Columns/2)*HaboobVolumeMath.Spacing;
                    int slot=HaboobVolumeMath.Slot(side);
                    for (int depth=0;depth<HaboobVolumeMath.Depths;depth++)
                    {
                        int index=slot*HaboobVolumeMath.Depths+depth; Vector3 ground;
                        Vector3 footprint=edge+cross*(float)side+heading*(float)(HaboobMath.Edge(side,model.Age)-depth*67);
                        if (world.Ground(footprint,out ground) && !float.IsNaN(ground.y) && !float.IsInfinity(ground.y))
                        {
                            targets[index]=ground.y;
                            // First reliable sample initializes an invisible cell.
                            // Later samples ease continuously as the front advances.
                            if (!sampled[index]) heights[index]=ground.y;
                            sampled[index]=true;
                        }
                    }
                }
                probe=(probe+4)%HaboobVolumeMath.Columns;
            }
            for (int i=0;i<heights.Length;i++)
                if (sampled[i]) heights[i]=(float)HaboobVolumeMath.TerrainStep(heights[i],targets[i],dt);
            Color sand=StormFog.DustColour(RenderSettings.fogColor,StormRunner.LightingStrength,NativeStormClock.NightStrength);
            int count=0;
            for (int col=0; col<HaboobVolumeMath.Columns; col++)
            {
                double side=center+(col-HaboobVolumeMath.Columns/2)*HaboobVolumeMath.Spacing; int slot=HaboobVolumeMath.Slot(side);
                double top=HaboobMath.Height(side,model.Age), edgeDepth=HaboobMath.Edge(side,model.Age);
                double sideFade=StormModel.Smooth(Math.Min(col,HaboobVolumeMath.Columns-1-col)/4.0);
                for (int row=0; row<HaboobVolumeMath.Rows; row++)
                {
                    double h=row/(double)(HaboobVolumeMath.Rows-1), radius=14+24*Math.Sin(h*Math.PI);
                    double roll=HaboobMath.Roll(h,side,model.Age);
                    for (int depth=0; depth<HaboobVolumeMath.Depths; depth++)
                    {
                    double seed=HaboobVolumeMath.Seed(side,row,depth,1);
                    double angle=model.Age*(0.045+seed*0.025)+seed*Math.PI*2;
                    double dx=(HaboobVolumeMath.Seed(side,row,depth,2)-0.5)*82;
                    int terrainIndex=slot*HaboobVolumeMath.Depths+depth;
                    double y=heights[terrainIndex]-position.y-55+h*top+radius*Math.Sin(angle);
                    double z=edgeDepth-depth*67+roll-radius*Math.Cos(angle);
                    float size=(float)(165+65*seed+32*(1-h));
                    ParticleSystem.Particle p=particles[count];
                    p.position=cross*(float)(side-center+dx)+heading*(float)z+Vector3.up*(float)y;
                    p.velocity=Vector3.zero; p.startSize=size; p.rotation=(float)(seed*360+model.Age*(seed-.5)*1.8);
                    p.startLifetime=16; p.remainingLifetime=16-(float)HaboobVolumeMath.Phase(model.Age,seed)*15.999f;
                    p.randomSeed=(uint)(seed*4000000000.0)+1;
                    Color c=sand*(float)(0.78+0.18*h+0.06*seed);
                    c.a=(float)StormModel.Unit(alpha*(sampled[terrainIndex]?1:0)*sideFade*(0.36+0.10*seed)*(row==HaboobVolumeMath.Rows-1 ? 0.62 : 1));
                    colours[count]=c; p.startColor=c; particles[count++]=p;
                    }
                }
            }
            // The final rendered camera pose is available in PreCull. Upload
            // once there, rather than doing a second full upload during Update.
        }
        private void Upload(Vector3 eye)
        {
            int count=0;
            for (int i=0; i<particles.Length; i++)
            {
                Color c=colours[i]; if (c.a<=0) continue;
                ParticleSystem.Particle p=particles[i];
                c.a*=(float)HaboobVolumeMath.CameraFade(Vector3.Distance(position+p.position,eye),p.startSize);
                p.startColor=c;
                // Native particle colour is byte-quantized. Skip only particles
                // with exactly zero final alpha, preserving every visible billow.
                if (p.startColor.a==0) continue;
                drawing[count++]=p;
            }
            system.SetParticles(drawing,count);
        }
        internal void PreCull(Camera camera)
        {
            if (renderer==null) return;
            renderer.enabled=visible && StormRunner.CanRender && camera==StormRunner.View;
            if (renderer.enabled) { assets.Camera(camera,true); Upload(StormView.Eye(camera)); }
            else if (!StormRunner.CanRender || camera==StormRunner.View) assets.Camera(null,false);
        }
        internal void Hide() { if (renderer!=null) renderer.enabled=false; }
        internal void ShiftOrigin(Vector3 delta)
        {
            if (hasPosition) { position+=delta; owner.transform.position=position; }
            for (int i=0; i<heights.Length; i++) { heights[i]+=delta.y; targets[i]+=delta.y; }
        }
        public void Dispose()
        {
            Hide(); if (assets!=null) assets.Dispose();
            if (owner!=null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
        }
    }
}
