using System;
namespace ApocaDustStorm
{
    internal static class HaboobChecks
    {
        internal static void Run(Action<bool,string> check)
        {
            StormModel storm=new StormModel();
            foreach(WindFrom source in Enum.GetValues(typeof(WindFrom)))
            {
                storm.BeginFrom(100,200,source,600,90,75,9);
                double x=storm.PrevailingX,z=storm.PrevailingZ;
                check(Math.Abs(x*x+z*z-1)<1e-10,"Cardinal direction is unit length: "+source);
                check(Math.Abs(storm.SignedDistance(100,200)-1800)<1e-8,"Front starts 1.8 km upwind: "+source);
                storm.Tick(90);
                check(storm.Intensity(100-x*5000,200-z*5000)>.94,"Passed front leaves heavy dust: "+source);
                check(storm.Intensity(100+x*5000,200+z*5000)<.03,"Ahead of wall remains mostly clear: "+source);
                bool coverage=true;
                for(int i=-100;i<=100;i++)
                    if(storm.Intensity(100-x*1000-z*i*1000,200-z*1000+x*i*1000)<.94)coverage=false;
                check(coverage,"No crosswind regional boundary across 200 km: "+source);
                double px=100+x*150-z*80,pz=200+z*150+x*80;
                double distance=storm.SignedDistance(px,pz),intensity=storm.Intensity(px,pz),gust=storm.Gust(px,pz);
                storm.ShiftOrigin(-1000,2000);
                check(Math.Abs(storm.SignedDistance(px-1000,pz+2000)-distance)<1e-8,"Origin translation preserves front distance: "+source);
                check(Math.Abs(storm.Intensity(px-1000,pz+2000)-intensity)<1e-8&&Math.Abs(storm.Gust(px-1000,pz+2000)-gust)<1e-8,"Origin translation preserves weather: "+source);
                double future=storm.IntensityAfter(px-1000,pz+2000,7);storm.Tick(7);
                check(Math.Abs(storm.Intensity(px-1000,pz+2000)-future)<1e-8,"Future intensity matches stepped moving front: "+source);
                storm.Stop();double front=storm.Front;storm.Tick(3);
                check(storm.Front==front,"Manual clear stops advance instead of sliding wall: "+source);
            }
            storm.BeginFrom(0,0,WindFrom.West,600,90,75,7);storm.Tick(90);
            double previous=-1,maxStep=0;bool monotonic=true;
            for(int i=1000;i>=-1000;i--)
            {
                double v=storm.Intensity(i,0);
                if(previous>=0){maxStep=Math.Max(maxStep,Math.Abs(v-previous));if(v<previous-1e-12)monotonic=false;}
                previous=v;
            }
            check(monotonic&&maxStep<.003,"Crossing the front smoothly increases haze without a hard fog boundary");
            check(storm.IntensityAfter(-1)==0&&storm.IntensityAfter(double.NaN)==0&&storm.IntensityAfter(double.PositiveInfinity)==0,"Invalid future times have no weather");
            check(storm.Intensity(double.NaN,0)==0&&storm.Intensity(0,double.PositiveInfinity)==0,"Invalid sample positions never contaminate intensity");
            storm.Reset();check(storm.Intensity(0,0)==0&&storm.FrontOpacity(0,0)==0,"Calm state has no haze or wall");
            double maxTextureStep=0;bool bounded=true,periodic=true;
            for(int i=0;i<=1000;i++)
            {
                double side=(i-500)*37.0,age=i*.13,h=(i%101)/100.0;
                double height=HaboobMath.Height(side,age),edge=HaboobMath.Edge(side,age),roll=HaboobMath.Roll(h,side,age),alpha=HaboobMath.TextureAlpha(i*.017,h);
                if(height<325||height>515||Math.Abs(edge)>38.00001||roll< -111||roll>65||alpha<0||alpha>1)bounded=false;
                if(Math.Abs(alpha-HaboobMath.TextureAlpha(i*.017+1,h))>1e-10)periodic=false;
                maxTextureStep=Math.Max(maxTextureStep,Math.Abs(HaboobMath.TextureAlpha(0,h)-HaboobMath.TextureAlpha(1,h)));
            }
            check(bounded,"Billow height, depth, and procedural opacity stay bounded");
            check(periodic&&maxTextureStep<1e-10,"Repeating wall texture has a continuous horizontal seam");
            bool coreDense=true,shadeBounded=true,motionBounded=true,seamSmooth=true;
            double minTop=1,maxTop=0;
            for(int x=0;x<64;x++)
            {
                double u=x/64.0,last=0;
                for(int y=0;y<=128;y++)
                {
                    double v=y/128.0,a=HaboobMath.TextureAlpha(u,v),s=HaboobMath.TextureShade(u,v);
                    if(a>.06)last=v;
                    if(v<.25&&a<.7)coreDense=false;
                    if(s<.68||s>1||Math.Abs(s-HaboobMath.TextureShade(u+1,v))>1e-10)shadeBounded=false;
                    if(Math.Abs(HaboobMath.TextureAlpha(-.00001,v)-HaboobMath.TextureAlpha(.00001,v))>.002)seamSmooth=false;
                    for(int layer=0;layer<2;layer++)
                    {
                        double tv=HaboobMath.TextureV(x*37,v,120,layer);
                        if(tv<0||tv>1||Math.Abs(HaboobMath.TextureU(x*37,v,120.01,layer)-HaboobMath.TextureU(x*37,v,120,layer))>.001)motionBounded=false;
                    }
                }
                minTop=Math.Min(minTop,last);maxTop=Math.Max(maxTop,last);
            }
            check(maxTop-minTop>.08,"Rounded billows vary the visible top across the wall instead of a straight alpha cutoff");
            check(coreDense,"Billow shaping retains a continuous dense lower dust bank");
            check(shadeBounded&&seamSmooth,"Billow shading and warped opacity stay seamless and never amplify light");
            check(motionBounded,"Independent layer flow stays smooth with UVs inside the vertical clamp");
            check(HaboobMath.TopFade(1)==0&&HaboobMath.TopFade(0)==1,"Cloud top fades to transparent while its base stays dense");
            check(HaboobMath.SideFade(0)==0&&HaboobMath.SideFade(32)==0&&HaboobMath.SideFade(16)==1,"Remote mesh edges fade without thinning its center");
            bool unique=true;
            for(int center=-100;center<=100;center++)
            {
                bool[] seen=new bool[HaboobMath.Columns];
                for(int i=0;i<HaboobMath.Columns;i++)
                {int slot=HaboobMath.HeightSlot((center+i-16)*HaboobMath.Spacing);if(slot<0||slot>=seen.Length||seen[slot])unique=false;else seen[slot]=true;}
            }
            check(unique,"Rolling ground-height cache maps every column uniquely including negative coordinates");
            check(HaboobMath.Arrival(320)==0&&HaboobMath.Arrival(-200)==1&&HaboobMath.WallFade(-220)==0,"Wall-to-haze handoff endpoints match visibility envelope");
        }
    }
}
