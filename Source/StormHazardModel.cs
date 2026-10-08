using System;
namespace ApocaDustStorm
{
    internal enum StormCover { None, Shelter, Vehicle, Exposed }
    internal static class StormHazardModel
    {
        internal const double HealthPerSecond = 0.30, VehicleDamageMultiplier = 0.10;
        internal static double Severity(double strength)
        { return StormModel.Smooth((strength - 0.35) / 0.50); }
        internal static double MovementGain(double strength, double gust, double gustControl, double setting)
        {
            double pressure = 0.45 + 0.55 * StormModel.Unit(WindMath.Gain(gust, gustControl));
            return 1 - 0.30 * Severity(strength) * pressure * Math.Max(0, Math.Min(2, setting));
        }
        internal static double Damage(double dt, double strength, bool sheltered, bool inVehicle, double setting, double vehicleProtection = 0.90)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0 ||
                double.IsNaN(strength) || double.IsInfinity(strength) ||
                double.IsNaN(setting) || double.IsInfinity(setting) ||
                double.IsNaN(vehicleProtection) || double.IsInfinity(vehicleProtection)) return 0;
            dt = Math.Min(dt, 0.2);
            double severity = Severity(strength);
            if (sheltered || severity <= 0.001 || setting <= 0) return 0;
            double multiplier = inVehicle ? 1 - Math.Max(0.25, Math.Min(0.90, vehicleProtection)) : 1;
            return dt * severity * HealthPerSecond * Math.Max(0, Math.Min(2, setting)) * multiplier;
        }
    }
}
