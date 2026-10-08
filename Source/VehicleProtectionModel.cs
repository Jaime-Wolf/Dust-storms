using System;
namespace ApocaDustStorm
{
    internal sealed class VehicleProtectionModel
    {
        internal const double WearSeconds = 300, RecoverySeconds = 120;
        internal const double InitialProtection = 0.90, MinimumProtection = 0.25;
        private double wear;
        internal double Protection { get { return InitialProtection - (InitialProtection - MinimumProtection) * wear / WearSeconds; } }
        internal void Step(double dt, bool hazardous, bool sheltered, bool inVehicle)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return;
            dt = Math.Min(dt, 0.2);
            if (sheltered) wear = Math.Max(0, wear - dt * WearSeconds / RecoverySeconds);
            else if (hazardous && inVehicle) wear = Math.Min(WearSeconds, wear + dt);
            // On foot, calm weather and pause hold wear. Only structural shelter recovers it.
        }
        internal void Reset() { wear = 0; }
    }
}
