using System;
namespace ApocaDustStorm
{
    internal static class DistanceFogMath
    {
        // Start only where the normal distance fog is already 95% opaque.
        // This closes far material/depth gaps without re-fogging the nearby cab.
        internal static double Start(double density)
        { return double.IsNaN(density)||double.IsInfinity(density)||density<=0 ? double.PositiveInfinity : -Math.Log(0.05)/density; }
        internal static double Opacity(double distance,double density)
        { return density<=0 ? 0 : 1-Math.Exp(-density*Math.Max(0,distance-Start(density))); }
        internal static double FarDistance(double farClip,double verticalFov,double aspect)
        {
            double half=Math.Tan(Math.Max(1,Math.Min(179,verticalFov))*Math.PI/360);
            return farClip*Math.Sqrt(1+half*half*(1+aspect*aspect));
        }
    }
}
