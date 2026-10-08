using System;

namespace ApocaDustStorm
{
    // Normal play uses simulation seconds. Large forward clock changes (sleep)
    // consume their equivalent normal-play time without counting regular frames twice.
    internal sealed class StormClock
    {
        private double previous = double.NaN;
        internal void Reset() { previous = double.NaN; }
        internal double Step(double seconds, double worldHours, double hoursPerSecond)
        {
            seconds = double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : Math.Max(0, seconds);
            if (double.IsNaN(worldHours) || double.IsInfinity(worldHours)) { Reset(); return seconds; }
            if (double.IsNaN(previous)) { previous = worldHours; return seconds; }
            double hours = worldHours - previous; previous = worldHours;
            // A repeating calendar can wrap midnight without advancing its date.
            if (hours < -12 && hours >= -24.0001) hours += 24;
            if (hours <= 0 || hoursPerSecond <= 0 || double.IsNaN(hoursPerSecond) || double.IsInfinity(hoursPerSecond)) return seconds;
            double equivalent = hours / hoursPerSecond;
            return equivalent > Math.Max(2, seconds * 4) ? Math.Max(seconds, equivalent) : seconds;
        }
    }
}
