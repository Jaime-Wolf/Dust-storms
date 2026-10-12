using System;
namespace ApocaDustStorm
{
    internal sealed class DamageGraceModel
    {
        private double elapsed;
        internal double DamageSeconds(double dt, bool hazardous, bool sheltered, bool storm, double seconds)
        {
            if (sheltered || !storm) { Reset(); return 0; }
            if (!hazardous || Double.IsNaN(dt) || Double.IsInfinity(dt) || dt <= 0) return 0;
            dt = Math.Min(dt, 0.2);
            seconds = Double.IsNaN(seconds) || Double.IsInfinity(seconds) ? 0 : Math.Max(0, Math.Min(300, seconds));
            double before = elapsed;
            elapsed = Math.Min(300, elapsed + dt);
            return Math.Max(0, dt - Math.Max(0, seconds - before));
        }
        internal void Reset() { elapsed = 0; }
    }
}
