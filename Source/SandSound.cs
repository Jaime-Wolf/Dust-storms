using System;
namespace ApocaDustStorm
{
    internal static class SandSound
    {
        internal const int SampleRate = 24000, Seconds = 24;
        private static double Unit(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? 0 : Math.Max(0, Math.Min(1, x)); }
        internal static double Gain(double strength, double gust, double setting, bool driving)
        { return driving ? 0.32 * Unit(strength) * (0.62 + 0.38 * Unit(gust)) * (2 * Unit(setting / 2)) : 0; }
        internal static float[] Generate(int seed)
        {
            int length = SampleRate * Seconds;
            float[] white = new float[length], grain = new float[length], result = new float[length];
            Random random = new Random(seed);
            for (int i = 0; i < length; i++) white[i] = (float)(random.NextDouble() * 2 - 1);
            // Irregular, soft grains: no repetitive mechanical squeak or tonal loop.
            for (int g = 0; g < 480; g++)
            {
                int start = random.Next(length), width = random.Next(120, 430);
                double level = 0.05 + random.NextDouble() * 0.25;
                for (int i = 0; i < width; i++)
                {
                    double envelope = Math.Sin(Math.PI * i / width); envelope *= envelope;
                    grain[(start + i) % length] += (float)(level * envelope);
                }
            }
            double low = 0, high = 0, sum = 0, squares = 0, peak = 0;
            double fast = 1 - Math.Exp(-2 * Math.PI * 4200 / SampleRate), slow = 1 - Math.Exp(-2 * Math.PI * 350 / SampleRate);
            // A warm-up lap makes the filter states continuous at the loop seam.
            for (int lap = 0; lap < 2; lap++) for (int i = 0; i < length; i++)
            {
                high += fast * (white[i] - high); low += slow * (white[i] - low);
                double phase = 2 * Math.PI * i / length;
                double breath = 0.68 + 0.16 * Math.Sin(phase * 3 + 0.2) + 0.09 * Math.Sin(phase * 7 + 1.1);
                double sample = (high - low) * (breath + grain[i]);
                if (lap == 1) { result[i] = (float)sample; sum += sample; }
            }
            double mean = sum / length;
            for (int i = 0; i < length; i++) { double s = result[i] - mean; squares += s * s; peak = Math.Max(peak, Math.Abs(s)); }
            double scale = Math.Min(0.16 / Math.Sqrt(squares / length), 0.65 / peak);
            for (int i = 0; i < length; i++) result[i] = (float)((result[i] - mean) * scale);
            return result;
        }
    }
}
