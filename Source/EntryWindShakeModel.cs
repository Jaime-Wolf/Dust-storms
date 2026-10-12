using System;
namespace ApocaDustStorm
{
    internal sealed class EntryWindShakeModel
    {
        internal const double Duration = 5;
        private double age = Duration;
        internal double X { get; private set; }
        internal double Y { get; private set; }
        internal void Reset() { age = Duration; X = Y = 0; }
        internal void Step(double dt, bool entered)
        {
            if (Double.IsNaN(dt) || Double.IsInfinity(dt) || dt < 0) { Reset(); return; }
            age = entered ? 0 : Math.Min(Duration, age + dt);
            if (age >= Duration) { X = Y = 0; return; }
            // Gentle onset and a longer smooth tail; no abrupt final snap.
            double envelope = StormModel.Smooth(age / .08) * (1 - StormModel.Smooth(age / Duration));
            double t = age * Math.PI * 2;
            X = envelope * (.55 * Math.Sin(t * 6.7) + .30 * Math.Sin(t * 10.1) + .15 * Math.Sin(t * 3.2));
            Y = envelope * (.65 * Math.Sin(t * 8.3) + .35 * Math.Sin(t * 4.6));
        }
    }
}
