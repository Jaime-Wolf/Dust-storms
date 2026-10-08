using System;
namespace ApocaDustStorm
{
    internal static class DischargeSound
    {
        internal const int SampleRate = 24000;
        internal static float[] Generate(int seed)
        {
            int dryLength = (int)(SampleRate * 4.2), length = (int)(SampleRate * 6.5);
            float[] dry = new float[length]; Random random = new Random(seed);
            double fast = 0, mid = 0, body = 0, bass = 0, swell = 0, squares = 0, peak = 0;
            double a = Filter(4800), b = Filter(900), c = Filter(260), d = Filter(85), e = Filter(3);
            double reflectionOne = 0.085 + random.NextDouble() * 0.035, reflectionTwo = 0.17 + random.NextDouble() * 0.06;
            float[] texture = ClapTexture(new Random(unchecked(seed * 397 ^ 0x53414e44)), dryLength);
            for (int i = 0; i < dryLength; i++)
            {
                double t = (double)i / SampleRate, noise = random.NextDouble() * 2 - 1;
                fast += a * (noise - fast); mid += b * (noise - mid);
                body += c * (noise - body); bass += d * (noise - bass); swell += e * (noise - swell);
                double attack = StormModel.Smooth(t / 0.004), tail = 1 - StormModel.Smooth((t - 3.1) / 1.1);
                // A broadband lightning snap gives way to irregular low noise,
                // rather than a repeating static buzz or a pitched impact tone.
                double crack = 1.10 * Math.Exp(-t * 23) + Echo(t, reflectionOne, 0.025) * 0.23 + Echo(t, reflectionTwo, 0.04) * 0.13;
                double roll = (1 - Math.Exp(-t * 24)) * Math.Exp(-t * 1.0);
                double motion = 0.75 + 0.25 * StormModel.Unit(0.5 + swell * 16);
                double sample = attack * tail * ((fast - mid) * (crack + texture[i] * 0.35)
                    + (mid - body) * (0.50 * Math.Exp(-t * 7) + texture[i] * 0.85)
                    + (body * 1.30 + bass * 1.50) * roll * motion);
                dry[i] = (float)sample;
            }
            float[] wet = new float[length];
            double[] delays = { 0.0713, 0.1137, 0.1733, 0.2291 };
            foreach (double seconds in delays)
            {
                int size = (int)(SampleRate * seconds); float[] buffer = new float[size];
                double feedback = Math.Exp(-3 * seconds / 2.6), filtered = 0;
                double damping = 1 - Math.Exp(-2 * Math.PI * 900 / SampleRate);
                for (int i = 0; i < length; i++)
                {
                    int index = i % size; double delayed = buffer[index];
                    filtered += damping * (delayed - filtered);
                    buffer[index] = (float)(dry[i] + filtered * feedback);
                    wet[i] += (float)(delayed * 0.25);
                }
            }
            Diffuse(wet, (int)(SampleRate * 0.0137));
            Diffuse(wet, (int)(SampleRate * 0.0191));
            Diffuse(wet, (int)(SampleRate * 0.0273));
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / SampleRate;
                double fade = 1 - StormModel.Smooth((t - 4.2) / 2.3);
                double sample = (dry[i] + wet[i] * 0.85) * fade;
                samples[i] = (float)sample; peak = Math.Max(peak, Math.Abs(sample));
                if (i < SampleRate) squares += sample * sample;
            }
            // Level the audible first second, with headroom for a louder source
            // and the full 0-2 slider. The last 2.3 seconds fade reflections.
            double scale = Math.Min(0.13 / Math.Sqrt(squares / SampleRate), 0.58 / peak);
            for (int i = 0; i < length; i++) samples[i] = (float)(samples[i] * scale);
            samples[0] = samples[length - 1] = 0;
            return samples;
        }
        private static double Filter(double hz) { return 1 - Math.Exp(-2 * Math.PI * hz / SampleRate); }
        private static double Echo(double t, double arrival, double width)
        { double x = (t - arrival) / width; return Math.Exp(-x * x); }
        private static float[] ClapTexture(Random random, int length)
        {
            // Blend a few subdued fragments into the initial thunderclap.
            // Broad, overlapping envelopes avoid the scattered static-like
            // pops of 0.1.14; later sound is the accepted rumble and reverb.
            float[] envelope = new float[length]; int fragments = 3 + random.Next(3);
            double arrival = 0.008;
            for (int fragment = 0; fragment < fragments; fragment++)
            {
                arrival += 0.015 + random.NextDouble() * 0.022;
                AddTexture(envelope, arrival, 0.011 + random.NextDouble() * 0.014,
                    (0.16 + random.NextDouble() * 0.12) * Math.Exp(-arrival * 4));
            }
            if (random.NextDouble() < 0.30) AddTexture(envelope, 0.14 + random.NextDouble() * 0.09,
                0.035 + random.NextDouble() * 0.025, 0.06 + random.NextDouble() * 0.06);
            return envelope;
        }
        private static void AddTexture(float[] envelope, double arrival, double width, double gain)
        {
            int first = Math.Max(0, (int)((arrival - width * 4) * SampleRate));
            int last = Math.Min(envelope.Length, (int)((arrival + width * 4) * SampleRate) + 1);
            for (int i = first; i < last; i++) envelope[i] += (float)(gain * Echo((double)i / SampleRate, arrival, width));
        }
        private static void Diffuse(float[] samples, int delay)
        {
            float[] buffer = new float[delay]; const float feedback = 0.5f;
            for (int i = 0; i < samples.Length; i++)
            {
                int index = i % delay; float input = samples[i], delayed = buffer[index];
                float output = delayed - input * feedback;
                buffer[index] = input + output * feedback; samples[i] = output;
            }
        }
    }
}
