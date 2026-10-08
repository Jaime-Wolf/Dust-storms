using System;
namespace ApocaDustStorm
{
    internal static class HorizonMath
    {
        internal static double Radius(double farClip)
        {return Double.IsNaN(farClip)||Double.IsInfinity(farClip)||farClip<=1?1:farClip*0.95;}
        internal static double Opacity(double strength)
        { return 0.995 * StormModel.Smooth(strength * 1.08); }
        // Native fog colours can stay bright white after sunset. Hand their
        // colour/scattering over before the growing dust makes them conspicuous.
        // Sun elevation blends this smoothly through twilight; day/peak stay intact.
        internal static double NightBlend(double strength, double night)
        { return Math.Max(0, Math.Min(1, night)) * StormModel.Smooth(strength * 5); }
        internal static double ColourBlend(double strength, double night)
        {
            double day = StormModel.Smooth(strength);
            return day + Math.Max(0, Math.Min(1, night)) * (StormModel.Smooth(strength * 5) - day);
        }
        internal static double NativeFog(double native, double strength, double night = 0)
        {
            double day = StormModel.Smooth(strength * 1.08);
            double blend = day + Math.Max(0, Math.Min(1, night)) * (StormModel.Smooth(strength * 5) - day);
            return native * (1 - blend);
        }
    }
}
