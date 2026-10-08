using System;

namespace ApocaDustStorm
{
    internal struct DustFlow { internal double X, Y, Z; }
    // Velocity targets for small particles rising off the ground into downwind plumes.
    // Kept independent of Unity so lift, direction and smoothness can be checked.
    internal static class DustMotion
    {
        internal static DustFlow Flow(double age, double life, double seed, double windX, double windZ, double speed, double gust, bool low)
        {
            age = Math.Max(0, age); life = Math.Max(0.1, life);
            double entrain = StormModel.Smooth(age / (low ? 1.1 : 2.2));
            double fraction = low ? 0.24 + 0.40 * entrain : 0.12 + 0.76 * entrain;
            double curl = Math.Sin(age * 1.1 + seed) * (low ? 0.35 : 0.85)
                        + Math.Sin(age * 0.43 + seed * 0.7) * (low ? 0.12 : 0.35);
            double taper = 1 - StormModel.Smooth((age - 0.8) / Math.Min(4, life * 0.65));
            double lift = low ? 0.035 + 0.035 * StormModel.Unit(gust) : 0.18 + (1.15 + 0.8 * StormModel.Unit(gust)) * taper;
            return new DustFlow { X = windX * speed * fraction - windZ * curl,
                Y = lift, Z = windZ * speed * fraction + windX * curl };
        }
        internal static double Blend(double dt) { return 1 - Math.Exp(-Math.Max(0, Math.Min(0.2, dt)) * 2.5); }
        internal static double Pickup(double gust) { return 0.50 + 0.80 * StormModel.Unit(gust); }
    }
}
