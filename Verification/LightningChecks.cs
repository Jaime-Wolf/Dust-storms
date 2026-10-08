using System;
namespace ApocaDustStorm
{
    internal static class LightningChecks
    {
        internal static void Run(Action<bool,string> check)
        {
            check(DustDischargeModel.RollWait(0)==30,"Discharges have a thirty-second minimum heavy-weather wait");
            check(DustDischargeModel.RollWait(1)==50,"Natural discharge waits stay within fifty seconds of eligible heavy weather");
            Random random=new Random(27);double total=0,minimum=1e9;
            double maximum=0;
            for(int i=0;i<10000;i++){double wait=DustDischargeModel.RollWait(random.NextDouble());total+=wait;minimum=Math.Min(minimum,wait);maximum=Math.Max(maximum,wait);}
            check(minimum>=30&&maximum<=50&&total/10000>39.5&&total/10000<40.5,"Random thirty-to-fifty-second intervals average forty seconds");
            DustDischargeModel model=new DustDischargeModel();
            check(!model.Step(0,true,delegate{throw new Exception("Paused RNG");})&&!model.Step(double.NaN,true,delegate{return 0;}),"Pause and invalid delta cannot trigger or roll lightning");
            bool eventOccurred=false;for(int i=0;i<1000;i++)eventOccurred|=model.Step(0.2,false,delegate{return 0;});
            check(!eventOccurred&&model.Pulse==0,"Weak or absent storms never create automatic discharges");
            int eventFrame=-1;for(int i=0;i<452;i++){if(model.Step(0.2,true,delegate{return 0;})){eventFrame=i;break;}}
            check(eventFrame>=149&&eventFrame<=151,"First discharge obeys minimum eligible time");
            model.Reset();check(model.Preview(0.5)&&model.Pulse==0,"Preview begins with a smooth dark onset");model.SetDistance(34.3);
            check(!model.TakeSound(),"Crackle does not precede the flash");
            double max=0,previous=0,step=0;
            for(int i=0;i<1700;i++){model.Step(0.001,false,delegate{return 0;});double p=model.Pulse;max=Math.Max(max,p);step=Math.Max(step,Math.Abs(p-previous));previous=p;if(i==110)check(model.TakeSound()&&!model.TakeSound(),"Delayed crackle is consumed exactly once");if(i==650)check(p>0.2,"Bolt retains a smooth visible afterglow beyond the former flash duration");if(i==1200)check(p>0.1,"Improved strike light retains a soft fade beyond one second");}
            check(max>0.7&&max<=0.9&&step<0.03&&model.Pulse==0,"Flash is a bounded continuous brief pulse without repeated strobing");
            check(!model.Preview(0.5),"Preview cannot retrigger while the reverb finishes");for(int i=0;i<30;i++)model.Step(0.2,false,delegate{return 0;});
            check(model.Preview(0.5),"Preview can be used again after the anti-repeat interval");model.CancelPulse();
            check(model.Pulse==0&&!model.TakeSound()&&model.WindGain==1,"Suspend discards glow and pending sound and restores wind gain");
            model.Reset();for(int i=0;i<220;i++)model.Step(0.2,true,delegate{return 0;});model.Step(0.2,false,delegate{return 0;});
            check(!model.Step(0.2,true,delegate{return 0;}),"Returning heavy weather starts a fresh wait instead of a queued strike");
            WindEnvelopeChecks(check);
            for(int seed=3118;seed<=3312;seed+=97)
            {
                float[] samples=DischargeSound.Generate(seed);double energy=0,peak=0;
                foreach(float sample in samples){energy+=sample*sample;peak=Math.Max(peak,Math.Abs(sample));}
                check(samples.Length==156000&&Math.Abs(samples[0])<0.000001&&Math.Abs(samples[samples.Length-1])<0.000001,"Lightning clip "+seed+" has a spacious longer tail and soft sample endpoints");
                check(peak<=0.581&&Math.Sqrt(energy/samples.Length)>0.02&&Math.Sqrt(energy/samples.Length)<=0.13,"Lightning clip "+seed+" retains headroom at the maximum volume slider");
                double tail=Energy(samples,4.2,5.5), ending=Energy(samples,6.15,6.5);
                check(tail>0.00000001&&ending<tail*0.2,"Lightning clip "+seed+" retains reflections after the dry sound and decays gently");
                check(Energy(samples,0.3,1.2)>0.000225&&LowFraction(samples,0.3,1.2)>0.35,"Lightning clip "+seed+" has a sustained low rumble rather than only a bright snap");
                check(Energy(samples,2.4,3.0)>0.000001,"Lightning clip "+seed+" still carries a rumbling tail beyond the former clip duration");
                check(Energy(samples,4.4,5.2)>0.0000001,"Lightning clip "+seed+" has measurable reverb after the previous entire clip ended");
            }
        }
        private static void WindEnvelopeChecks(Action<bool,string> check)
        {
            DustDischargeModel model=new DustDischargeModel();
            check(model.WindGain==1&&model.Preview(0.5)&&model.WindGain==1,"Wind stays at its normal mix before and at a strike's smooth onset");
            double minimum=1,previous=1,jump=0;
            for(int i=0;i<500;i++)
            {
                model.Step(0.01,false,delegate{return 0;});double gain=model.WindGain;
                minimum=Math.Min(minimum,gain);jump=Math.Max(jump,Math.Abs(gain-previous));previous=gain;
                if(i==99)check(gain==0.75,"The lightning crack has a modest held wind dip rather than muting the storm");
                if(i==199)check(gain>0.75&&gain<1,"Wind begins returning smoothly during the thunder tail");
            }
            check(minimum>=0.75&&jump<0.03&&model.WindGain==1,"Wind dip is bounded, continuous and fully recovers without another event");
            model.Reset();check(model.WindGain==1,"Reset cannot leave wind attenuated");
        }
        private static double Energy(float[] samples,double start,double end)
        {
            int first=(int)(start*DischargeSound.SampleRate),last=(int)(end*DischargeSound.SampleRate);double energy=0;
            for(int i=first;i<last;i++)energy+=samples[i]*samples[i];
            return energy/(last-first);
        }
        private static double LowFraction(float[] samples,double start,double end)
        {
            int first=(int)(start*DischargeSound.SampleRate),last=(int)(end*DischargeSound.SampleRate);double low=0,lowEnergy=0,allEnergy=0;
            double a=1-Math.Exp(-2*Math.PI*300/DischargeSound.SampleRate);
            for(int i=0;i<last;i++){low+=a*(samples[i]-low);if(i>=first){lowEnergy+=low*low;allEnergy+=samples[i]*samples[i];}}
            return lowEnergy/allEnergy;
        }
    }
}
