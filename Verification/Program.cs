using System;
namespace ApocaDustStorm
{
    internal static class Program
    {
        private static int count;
        private static void Check(bool condition, string description)
        { count++; if (!condition) throw new Exception("FAILED: " + description); }
        private static void Near(double actual, double expected, string description)
        { Check(Math.Abs(actual - expected) < 0.000001, description + ": " + actual); }
        private static void Main()
        {
            StormModel storm = new StormModel();
            storm.Begin(1000, -500, 0, 420, 90, 75, 9);
            Near(storm.Intensity(1000, -500), 0, "Worldwide storm initially clear");
            Check(storm.Front < -1500, "Visual arrival cue starts far upwind");
            double front = storm.Front;
            storm.Tick(20); Check(storm.Front > front && storm.Front - front < 500, "Visual front moves gradually downwind");
            Check(storm.Intensity(1000, -500) > 0 && storm.Intensity(1000, -500) < 0.15, "Early approach remains light before heavy dust");
            storm.Tick(70); Check(storm.Intensity(1000, -500) > 0.94, "Ninety-second build reaches heavy dust");
            Near(storm.OriginX, 1000, "Origin stays fixed"); Near(storm.OriginZ, -500, "Origin Z stays fixed");
            Near(storm.Intensity(10000, 10000), storm.Intensity(1000, -500), "Driving far away cannot escape worldwide storm");
            Near(storm.Intensity(-1000000, 1000000), storm.Intensity(1000, -500), "World coverage has no distant radial boundary");
            Near(storm.Intensity(1000, -500 + 1700), storm.Intensity(1000, -500), "Old regional edge has no visibility change");
            Near(storm.Intensity(1000, -500 - 10000), storm.Intensity(1000, -500), "Upwind travel has the same storm lifecycle");
            double paused = storm.Age;
            storm.Tick(0); storm.Tick(-5); storm.Tick(double.NaN); storm.Tick(double.PositiveInfinity);
            Near(storm.Age, paused, "Paused or invalid time does not advance weather");
            double a = storm.Intensity(1000, -500); storm.Tick(0.016);
            Check(Math.Abs(storm.Intensity(1000, -500) - a) < 0.005, "Gusts do not flash between frames");
            storm.Stop(); a = storm.Intensity(1000, -500); storm.Tick(0.016);
            Check(storm.Active && Math.Abs(storm.Intensity(1000, -500) - a) < 0.005, "Clearing begins smoothly");
            storm.Tick(15); Check(storm.Active && storm.Intensity(0, 0) > 0.4 && storm.Intensity(0, 0) < 0.55, "Manual clearing takes a gradual thirty seconds");
            storm.Tick(15); Check(!storm.Active, "Preview clearing finishes"); Near(storm.Intensity(1000, -500), 0, "Cleared world has zero intensity");
            storm.Begin(0, 0, Math.PI / 2, 420, 90, 75, 9);
            Check(storm.Age == 0 && storm.Intensity(0, 0) == 0, "F8 uses the same gentle approach as automatic weather");
            double origin = storm.OriginX; storm.Intensity(500, 300); Near(storm.OriginX, origin, "Sampling from moving car never changes storm origin");
            storm.Tick(420); Check(!storm.Active, "Full weather lifecycle completes");
            storm.Begin(0, 0, 0, 420, 90, 75, 7);
            double maximumStep = 0, previous = 0, max = 0, min = 1;
            for (int i = 0; i < 4200; i++)
            {
                storm.Tick(0.1); double strength = storm.Intensity(0, 0);
                if (strength < 0 || strength > 1 || double.IsNaN(strength)) throw new Exception("Invalid intensity in lifecycle sweep");
                maximumStep = Math.Max(maximumStep, Math.Abs(strength - previous)); previous = strength;
                if (storm.Age > 180 && storm.Age < 240) { min = Math.Min(min, strength); max = Math.Max(max, strength); }
            }
            Check(maximumStep < 0.01, "Entire lifecycle is continuous");
            Check(max - min > 0.005 && min > 0.94, "Peak dust breathes slowly without clear holes");
            storm.Begin(0, 0, 0, 420, 90, 75, 7); storm.Tick(345);
            Check(storm.Intensity(0, 0) > 0.94, "Natural clearing begins from sustained heavy weather");
            storm.Tick(37.5); Check(storm.Intensity(0, 0) > 0.45 && storm.Intensity(0, 0) < 0.51, "Natural clearing has a smooth seventy-five-second fade");
            storm.Tick(22.5); storm.Stop(); double stopping = storm.Intensity(0, 0); storm.Tick(20);
            Check(storm.Active && storm.Intensity(0, 0) < stopping, "Stopping near natural end retains its own fade");
            storm.Tick(10); Check(!storm.Active, "Manual clearing completes near natural end");
            storm.Begin(0, 0, 0, 180, 240, 180, 7);
            Check(storm.BuildTime + storm.ClearTime < storm.Duration, "Short storms retain a heavy middle phase");
            Near(StormModel.FogDensity(0, 65), 0, "No dust means no added fog");
            Check(StormModel.FogDensity(1, 25) > StormModel.FogDensity(1, 180), "Visibility control adjusts attenuation correctly");
            StormSchedule schedule = new StormSchedule(); int rolls = 0;
            Func<double> low = delegate { rolls++; return 0.01; };
            Check(!schedule.Tick(300, 5, 6, low) && rolls == 0, "Five calm minutes do not force a storm or roll");
            Check(!schedule.Tick(59, 5, 6, low) && rolls == 0, "Roll waits for full eligible minute");
            Check(schedule.Tick(1, 5, 6, low) && rolls == 1, "Passing chance roll can start storm");
            rolls = 0; schedule.Reset();
            Check(!schedule.Tick(7200, 5, 6, delegate { rolls++; return 0.9; }), "Failed chance rolls never force an arrival");
            Check(rolls == 115, "Chance rolls are minute based, not frame based");
            schedule.Reset(); Check(!schedule.Tick(7200, 0, 0, delegate { return 0.001; }), "Zero chance disables random storms");
            schedule.Reset(); Check(!schedule.Tick(59, 0, 100, low) && schedule.Tick(1, 0, 100, low), "100 percent chance still rolls at minute boundary");
            schedule.Reset(); rolls = 0;
            for (int i = 0; i < 600; i++) schedule.Tick(0.1, 0, 0, delegate { rolls++; return 0.5; });
            // Floating-point accumulation can place the boundary at the next frame.
            schedule.Tick(0.1, 0, 0, delegate { rolls++; return 0.5; });
            Check(rolls == 1, "Fine timesteps produce one chance roll");
            Check(!schedule.Tick(0, 0, 100, low), "Pause never rolls weather");
            Check(WindMath.Haze(0.8,1)>1.15*(1.85+0.35*0.8),"Haze is denser than 0.1.4 at identical settings");
            Check(Math.Exp(-StormModel.FogDensity(1,45)*WindMath.Haze(0.8,1)*50)<0.002,"Default peak dust strongly conceals terrain fifty metres away in attenuation model");
            Check(WindMath.Haze(1,1)/WindMath.Haze(0,1)<1.05,"Gusts no longer swing distance density rapidly");
            Check(WindMath.Speed(1,1)>WindMath.Speed(0,1),"Gusts increase dust speed");
            Check(WindMath.Acceleration(1,1,1,0,1600)==0,"Buffeting slider zero produces no acceleration");
            Check(WindMath.Acceleration(0,1,1,1,1600)==0,"No storm produces no wind force");
            Check(WindMath.Acceleration(1,1,2,1,800)<=0.45,"Strong gusts remain under hard acceleration cap");
            Check(WindMath.Acceleration(1,1,1,1,12000)<WindMath.Acceleration(1,1,1,1,1600),"Heavy truck reacts less than small car");
            Check(WindMath.Acceleration(1,1,1,1,double.NaN)==0,"Invalid mass cannot get wind force");
            Check(WindMath.WindVolume(1,1,0)==0,"Wind slider zero mutes added audio");
            Check(WindMath.WindVolume(1,1,1)>WindMath.WindVolume(1,0,1),"Gust peaks get louder");
            Near(WindMath.WindVolume(1,1,1),0.4675,"One is the quieter tuned wind-volume baseline");
            Near(WindMath.Acceleration(1,1,1,1,1600),0.364,"One is the stronger but capped buffeting baseline");
            Check(WindMath.WindVolume(1,1,2)>WindMath.WindVolume(1,1,1)&&WindMath.WindVolume(1,1,2)<=1,"Above-one wind control increases volume within audio limit");
            storm.Begin(0,0,0,420,90,75,7);double previousGust=storm.Gust(0,0),gustChange=0;
            for(int i=0;i<2000;i++){storm.Tick(0.016);double g=storm.Gust(0,0);gustChange=Math.Max(gustChange,Math.Abs(g-previousGust));previousGust=g;}
            Check(gustChange<0.02,"Stronger gusts remain smooth");
            DirectionAndDustChecks();
            ClockAndDurationChecks();
            HorizonAndSoundChecks();
            LightningChecks.Run(Check);
            HazardChecks.Run(Check);
            Console.WriteLine(count + " storm lifecycle, scheduling, directional wind and dust-flow checks passed.");
        }
        private static void HorizonAndSoundChecks()
        {
            double depthDensity=StormModel.FogDensity(1,45.9),depthStart=DistanceFogMath.Start(depthDensity);
            Check(depthStart>59&&depthStart<61,"Far gap correction starts after the ordinary 95 percent fog distance");
            Near(DistanceFogMath.Opacity(20,depthDensity),0,"Near scene and cab are not fogged twice");
            Near(DistanceFogMath.Opacity(depthStart,depthDensity),0,"Far correction starts at zero opacity without a visible wall");
            Near(DistanceFogMath.Opacity(depthStart+Math.Log(20)/depthDensity,depthDensity),0.95,"Unsupported distant surfaces receive the same opaque dust coverage");
            Check(double.IsInfinity(DistanceFogMath.Start(0))&&double.IsInfinity(DistanceFogMath.Start(double.NaN)),"Absent or invalid density cannot produce an artificial far fog wall");
            Check(DistanceFogMath.FarDistance(3000,60,1.6)>4000,"Radial far coverage includes screen corners beyond axial far clip");
            double radialError=0;
            for(int i=0;i<360;i++){double a=i*Math.PI/180,x=120*Math.Cos(a),z=120*Math.Sin(a);radialError=Math.Max(radialError,Math.Abs(DistanceFogMath.Opacity(Math.Sqrt(x*x+z*z),depthDensity)-DistanceFogMath.Opacity(120,depthDensity)));}
            Check(radialError<0.000001,"Distant fog coverage is invariant under camera/world yaw");
            Near(HorizonMath.Opacity(0),0,"Clear sky has no dust backdrop");
            Check(HorizonMath.Opacity(0.94)>0.99,"Heavy storm masks the contrasting sky behind fogged ridges");
            Near(HorizonMath.NativeFog(0.3,1),0,"Peak storm does not stack a differently coloured Azure fog layer");
            Near(HorizonMath.NativeFog(0.3,0),0.3,"Clear weather retains native sky fog");
            Near(HorizonMath.ColourBlend(0,1),0,"Night handoff starts at native clear-weather colour");
            Near(HorizonMath.ColourBlend(0.2,1),1,"Night dust colour is established before the storm becomes dense");
            Near(HorizonMath.ColourBlend(1,1),HorizonMath.ColourBlend(1,0),"Day and night retain the same full-strength colour endpoint");
            Near(HorizonMath.NativeFog(0.3,0.2,1),0,"Pale native scattering is gone before substantial night dust");
            Near(HorizonMath.NativeFog(0.3,0.1,0.5),(HorizonMath.NativeFog(0.3,0.1,0)+HorizonMath.NativeFog(0.3,0.1,1))/2,"Twilight handoff interpolates instead of switching at sunset");
            double nightStep=0,nightPrevious=0;
            for(int i=0;i<=10000;i++){double v=HorizonMath.ColourBlend(i/10000.0,1);nightStep=Math.Max(nightStep,Math.Abs(v-nightPrevious));nightPrevious=v;}
            Check(nightStep<0.0008,"Earlier night colour handoff remains continuous without jumps");
            Near(HorizonMath.Radius(3000),2850,"Background stays near far plane instead of intersecting nearby ground");
            Check(HorizonMath.Radius(200)>180&&HorizonMath.Radius(200)<200,"Short camera clip distances retain the backdrop inside the far plane");
            Check(HorizonMath.Radius(double.NaN)==1&&HorizonMath.Radius(double.PositiveInfinity)==1,"Invalid far distances cannot corrupt backdrop scale");
            double maximum=0,previous=0;
            for(int i=0;i<=10000;i++){double v=HorizonMath.Opacity(i/10000.0);maximum=Math.Max(maximum,Math.Abs(v-previous));previous=v;}
            Check(maximum<0.0002,"Horizon dust blends continuously during approach and clearing");
            Near(SandSound.Gain(1,1,0,true),0,"Sand slider zero mutes the grit layer");
            Near(SandSound.Gain(1,1,1,false),0,"On foot has no vehicle bodywork sound");
            Near(SandSound.Gain(0,1,1,true),0,"Clear weather has no sand noise");
            Check(SandSound.Gain(1,1,1,true)>SandSound.Gain(1,0,1,true),"Gusts increase bodywork patter");
            Check(SandSound.Gain(1,1,2,true)<=0.64,"Maximum sand gain is bounded");
            float[] samples=SandSound.Generate(37016),again=SandSound.Generate(37016);
            double sum=0,squares=0,peak=0,minBlock=1,maxBlock=0;
            bool deterministic=true;
            for(int i=0;i<samples.Length;i++){sum+=samples[i];squares+=samples[i]*samples[i];peak=Math.Max(peak,Math.Abs(samples[i]));if(samples[i]!=again[i])deterministic=false;}
            Check(samples.Length==SandSound.SampleRate*24,"Grit sound has a long twenty-four-second loop");
            Check(deterministic,"Sound generation is reproducible for the review preview");
            Check(Math.Abs(sum/samples.Length)<0.000001,"Generated sound has no DC offset");
            Check(peak<=0.651&&Math.Sqrt(squares/samples.Length)>0.14&&Math.Sqrt(squares/samples.Length)<0.17,"Sound mix has audible level and ample clipping headroom");
            Check(Math.Abs(samples[0]-samples[samples.Length-1])<0.3,"Loop seam is no larger than an ordinary noise transition");
            for(int block=0;block<24;block++){double energy=0;for(int i=block*SandSound.SampleRate;i<(block+1)*SandSound.SampleRate;i++)energy+=samples[i]*samples[i];double rms=Math.Sqrt(energy/SandSound.SampleRate);minBlock=Math.Min(minBlock,rms);maxBlock=Math.Max(maxBlock,rms);}
            Check(maxBlock/minBlock>1.3,"Sound varies slowly without a constant mechanical rattle");
            foreach(int frequency in new[]{600,900,1200,2000,3000})
            {double real=0,imag=0;for(int i=0;i<samples.Length;i++){double phase=2*Math.PI*frequency*i/SandSound.SampleRate;real+=samples[i]*Math.Cos(phase);imag+=samples[i]*Math.Sin(phase);}Check(2*Math.Sqrt(real*real+imag*imag)/samples.Length<0.004,"No prominent squeal tone at "+frequency+"Hz");}
        }
        private static void ClockAndDurationChecks()
        {
            Near(StormModel.RollDuration(5,15,0),300,"Shortest default duration is five normal-play minutes");
            Near(StormModel.RollDuration(5,15,1),900,"Longest default duration is fifteen normal-play minutes");
            Near(StormModel.RollDuration(15,5,0.5),600,"Reversed duration endpoints are sorted");
            Near(StormModel.RollDuration(5,5,0.8),300,"Equal duration endpoints give a fixed storm");
            System.Random random=new System.Random(8);double shortest=900,longest=300;
            for(int i=0;i<1000;i++){double duration=StormModel.RollDuration(5,15,random.NextDouble());if(duration<300||duration>900)throw new Exception("Invalid duration");shortest=Math.Min(shortest,duration);longest=Math.Max(longest,duration);}
            Check(shortest<305&&longest>895,"Random durations span the full requested range");
            StormClock clock=new StormClock();double rate=0.4/60,world=17e6,total=0;
            clock.Step(0,world,rate);
            for(int i=0;i<6000;i++){world+=rate*0.016;total+=clock.Step(0.016,world,rate);}
            Near(total,96,"Normal clock advancement is not counted twice");
            double skip=clock.Step(0.016,world+8,rate);
            Check(Math.Abs(skip-1200)<0.001,"Eight sleep hours use the installed native clock conversion");
            StormModel storm=new StormModel();storm.Begin(0,0,0,900,90,75,7);storm.Tick(skip);
            Check(!storm.Active&&storm.Intensity(0,0)==0,"Enough sleep expires even the longest default storm");
            clock.Reset();clock.Step(0,10,rate);
            Near(clock.Step(0.016,11,rate),150,"Short sleep consumes only part of a long storm");
            clock.Reset();clock.Step(0,23.9,rate);
            Check(Math.Abs(clock.Step(0.016,24.1,rate)-30)<0.001,"Calendar advancement through midnight consumes sleep time");
            clock.Reset();clock.Step(0,23.9,rate);
            Check(Math.Abs(clock.Step(0.016,0.1,rate)-30)<0.001,"Repeating native calendar midnight still advances storm time");
            clock.Reset();clock.Step(0,10,rate);Near(clock.Step(0.016,9,rate),0.016,"Small backward clock adjustment cannot rewind weather");
            Near(clock.Step(0.016,11,0),0.016,"Unavailable conversion falls back to normal play time");
            Near(clock.Step(0.016,double.NaN,rate),0.016,"Unavailable clock does not stop storm time");
            Near(clock.Step(0.016,1000,rate),0.016,"Returning clock starts a new baseline without an artificial skip");
            clock.Reset();clock.Step(0,10,rate);Near(clock.Step(100,10+rate*100,rate),100,"Accelerated simulation time is counted once");
        }
        private static void DirectionAndDustChecks()
        {
            WindFrom[] directions={WindFrom.North,WindFrom.South,WindFrom.East,WindFrom.West};
            double[] expectedX={0,0,-1,1},expectedZ={-1,1,0,0};
            for(int direction=0;direction<4;direction++)
            {
                StormModel storm=new StormModel();storm.BeginFrom(50,-100,directions[direction],420,90,75,9);
                Near(storm.WindX,expectedX[direction],directions[direction]+" wind blows along correct X axis");
                Near(storm.WindZ,expectedZ[direction],directions[direction]+" wind blows toward opposite compass direction");
                Check(storm.Source==directions[direction],"Source label retains originating compass direction");
                double beforeX=storm.WindX,beforeZ=storm.WindZ,maxChange=0,maxVeer=0;
                for(int i=0;i<20000;i++)
                {
                    storm.Tick(0.016);
                    maxChange=Math.Max(maxChange,Math.Sqrt(Math.Pow(storm.WindX-beforeX,2)+Math.Pow(storm.WindZ-beforeZ,2)));
                    beforeX=storm.WindX;beforeZ=storm.WindZ;
                    double dot=storm.WindX*expectedX[direction]+storm.WindZ*expectedZ[direction];
                    if(dot<0.96)throw new Exception("Prevailing wind reversed or veered too far");
                    maxVeer=Math.Max(maxVeer,1-dot);
                }
                Check(maxChange<0.001&&maxVeer>0.01,"Direction varies smoothly within prevailing compass sector");
                Near(storm.PrevailingX,expectedX[direction],"Wind veering never rotates front X heading");
                Near(storm.PrevailingZ,expectedZ[direction],"Wind veering never rotates front Z heading");
                Near(storm.OriginX,50,"Wind veering never moves the visual-front origin");
                DustFlow flow=DustMotion.Flow(3,8,1,storm.WindX,storm.WindZ,24,0.8,false);
                Check(flow.X*expectedX[direction]+flow.Z*expectedZ[direction]>0,"Lifted dust travels toward the opposite compass sector");
                double windX=storm.WindX,windZ=storm.WindZ;storm.Tick(0);Near(storm.WindX,windX,"Paused wind X stays fixed");Near(storm.WindZ,windZ,"Paused wind Z stays fixed");
            }
            DustFlow birth=DustMotion.Flow(0,8,0.3,1,0,24,0.8,false);
            DustFlow carried=DustMotion.Flow(3,8,0.3,1,0,24,0.8,false);
            DustFlow old=DustMotion.Flow(7,8,0.3,1,0,24,0.8,false);
            DustFlow ground=DustMotion.Flow(0,5,0.3,1,0,24,0.8,true);
            Check(birth.Y>1&&birth.X<carried.X*0.25,"Dust begins lifting gently before accelerating downwind");
            Check(old.Y<birth.Y*0.25&&old.X>=carried.X,"Lift tapers as mature dust continues downwind");
            Check(ground.Y<0.1&&ground.X>0,"Ground scud skims soil while moving downwind");
            Check(DustMotion.Flow(0,8,0.3,1,0,24,1,false).Y>DustMotion.Flow(0,8,0.3,1,0,24,0,false).Y,"Stronger gusts lift more dust");
            Check(DustMotion.Pickup(1)>DustMotion.Pickup(0)&&DustMotion.Pickup(9)<=1.3,"Gusts increase bounded pickup");
            Near(DustMotion.Blend(0),0,"Paused dust motion does not change velocity");
            Near(DustMotion.Blend(-1),0,"Negative time does not change velocity");
            double combined=1-Math.Pow(1-DustMotion.Blend(0.025),2);
            Near(combined,DustMotion.Blend(0.05),"Velocity response is stable across timestep splits");
            double previousX=0,previousY=0,previousZ=0,maxStep=0;
            for(int i=0;i<=800;i++)
            {
                DustFlow flow=DustMotion.Flow(i*0.01,8,1,0,-1,24,0.8,false);
                if(i>0)maxStep=Math.Max(maxStep,Math.Sqrt(Math.Pow(flow.X-previousX,2)+Math.Pow(flow.Y-previousY,2)+Math.Pow(flow.Z-previousZ,2)));
                if(flow.Y<0||flow.Z>0||double.IsNaN(flow.X))throw new Exception("Invalid dust flow");
                previousX=flow.X;previousY=flow.Y;previousZ=flow.Z;
            }
            Check(maxStep<0.2,"Plume lift and swirling vary without sudden velocity jumps");
        }
    }
}
