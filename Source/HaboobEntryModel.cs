using System;
namespace ApocaDustStorm
{
    // Boundary crossings, not a fixed timer or a camera turn, trigger entry dust.
    internal sealed class HaboobEntryModel
    {
        private bool sampled, armed;
        private double previous, age, cooldown;
        internal void Reset() { sampled = armed = false; previous = age = cooldown = 0; }
        internal bool Step(bool active, double stormAge, double signed, double dt, bool skipped)
        {
            if (!active || double.IsNaN(signed) || double.IsInfinity(signed) || double.IsNaN(stormAge) || double.IsInfinity(stormAge)) { Reset(); return false; }
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0) dt = 0;
            if (stormAge < age) Reset();
            age = stormAge; cooldown = Math.Max(0, cooldown - Math.Max(0, dt));
            bool crossed = sampled && armed && previous > 0 && signed <= 0 &&
                Math.Abs(signed - previous) < 300 && !skipped && cooldown <= 0;
            if (crossed) { armed = false; cooldown = 8; }
            if (signed > 150 && cooldown <= 0) armed = true;
            if (skipped) armed = signed > 150;
            previous = signed; sampled = true; return crossed;
        }
    }
}
