using System;
namespace ApocaDustStorm
{
    internal static class WindMath
    {
        internal static double Gain(double gust, double control) { return StormModel.Unit(gust) * Math.Max(0, Math.Min(2, control)); }
        internal static double Haze(double gust, double control) { return 2.5 + 0.12 * Gain(gust, control); }
        internal static double Speed(double gust, double control) { return 12 + 14 * Gain(gust, control); }
        internal static double Acceleration(double strength, double gust, double control, double buffet, double mass)
        {
            if (double.IsNaN(mass) || double.IsInfinity(mass) || mass < 100 || buffet <= 0) return 0;
            double weight = Math.Max(0.4, Math.Min(1.4, Math.Sqrt(1600 / mass)));
            return Math.Min(0.45, StormModel.Unit(strength) * Math.Max(0, Math.Min(2, buffet)) * 0.65 * weight * (0.08 + 0.48 * Gain(gust, control)));
        }
        internal static double WindVolume(double strength, double gust, double slider)
        { return 0.85 * Math.Min(1, StormModel.Unit(strength) * Math.Max(0, Math.Min(2, slider)) * 0.55 * (0.35 + 0.65 * StormModel.Unit(gust))); }
    }
}
