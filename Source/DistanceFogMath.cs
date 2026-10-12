using System;
namespace ApocaDustStorm
{
    internal static class DistanceFogMath
    {
        // Start only where the normal distance fog is already 95% opaque.
        // This closes far material/depth gaps without re-fogging the nearby cab.
        internal static double Start(double density)
        { return double.IsNaN(density)||double.IsInfinity(density)||density<=0 ? double.PositiveInfinity : -Math.Log(0.05)/density; }
        internal static double CorrectionDensity(double density, double night)
        { return double.IsNaN(density)||double.IsInfinity(density)||density<=0 ? 0 : density * (1 - 0.25 * StormModel.Unit(night)); }
        internal static double CorrectionStart(double density, double night)
        {
            double start = Start(density);
            if (double.IsInfinity(start)) return start;
            // Spread the night correction over a wider distance, starting
            // gently where ordinary fog is already at least 86% opaque.
            // The 95%-correction endpoint stays exactly at 2*Start(density).
            double scale = 1 - 0.25 * StormModel.Unit(night);
            return start * (2 - 1 / scale);
        }
        internal static double CorrectionOpacity(double distance, double density, double night)
        {
            if (double.IsInfinity(Start(density))) return 0;
            return 1 - Math.Exp(-CorrectionDensity(density, night) * Math.Max(0, distance - CorrectionStart(density, night)));
        }
        internal static double Opacity(double distance,double density)
        { return density<=0 ? 0 : 1-Math.Exp(-density*Math.Max(0,distance-Start(density))); }
        internal static double FarDistance(double farClip,double verticalFov,double aspect)
        {
            double half=Math.Tan(Math.Max(1,Math.Min(179,verticalFov))*Math.PI/360);
            return farClip*Math.Sqrt(1+half*half*(1+aspect*aspect));
        }
    }
}
