using System;

namespace ApocaDustStorm
{
    // Names specify where wind comes FROM, rather than where it travels.
    internal enum WindFrom { North, South, East, West }
    // One weather lifecycle covers the entire world. The fixed visual front is
    // only an arrival cue; position never gates fog, lighting, wind or duration.
    internal sealed class StormModel
    {
        internal bool Active { get; private set; }
        internal double Age, Duration, BuildTime, ClearTime, Speed, OriginX, OriginZ, WindX, WindZ;
        internal double PrevailingX, PrevailingZ;
        internal WindFrom Source { get; private set; }
        private double phase, baseAngle, stopping = -1, stopStrength;
        internal const double ManualClearTime = 30;
        internal double Front { get { return -1800 + Speed * Age; } }
        internal double Remaining { get { return Active ? Math.Max(0, Duration - Age) : 0; } }
        internal double AvailableSeconds { get { return !Active ? 0 : (stopping >= 0 ? Math.Max(0, ManualClearTime - stopping) : Remaining); } }
        internal void BeginFrom(double x, double z, WindFrom source, double duration, double buildTime, double clearTime, double seed)
        {
            // World basis: +Z north, +X east. A north wind travels toward -Z.
            double angle = source == WindFrom.North ? -Math.PI / 2 : source == WindFrom.South ? Math.PI / 2 : source == WindFrom.East ? Math.PI : 0;
            Begin(x, z, angle, duration, buildTime, clearTime, seed); Source = source;
        }
        internal void Begin(double x, double z, double angle, double duration, double buildTime, double clearTime, double seed)
        {
            OriginX = x; OriginZ = z; baseAngle = angle;
            PrevailingX = Math.Cos(angle); PrevailingZ = Math.Sin(angle);
            Duration = Math.Max(180, duration);
            BuildTime = Math.Max(30, Math.Min(Duration * 0.4, buildTime));
            ClearTime = Math.Max(30, Math.Min(Duration * 0.4, clearTime));
            Speed = 2000 / BuildTime; phase = seed;
            Age = 0; stopping = -1; Active = true;
            UpdateWind();
        }
        internal void Tick(double seconds)
        {
            if (!Active || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            Age += seconds;
            UpdateWind();
            if (stopping >= 0) stopping += seconds;
            if (stopping >= 0 ? stopping >= ManualClearTime : Age >= Duration) Reset();
        }
        internal void Stop()
        {
            if (!Active || stopping >= 0) return;
            stopStrength = Intensity(0, 0); stopping = 0;
        }
        internal void Reset() { Active = false; Age = 0; stopping = -1; }
        private void UpdateWind()
        {
            // Slow veering stays within 16 degrees of the storm's prevailing heading.
            // Front geometry keeps its original heading, avoiding a rotating dust wall.
            double veer = Smooth(Age / 10) * (0.20 * Math.Sin(Age * 0.027 + phase) + 0.08 * Math.Sin(Age * 0.061 + phase * 0.41));
            WindX = Math.Cos(baseAngle + veer); WindZ = Math.Sin(baseAngle + veer);
        }
        private double Lifecycle()
        {
            if (stopping >= 0) return stopStrength * (1 - Smooth(stopping / ManualClearTime));
            return Smooth(Age / BuildTime) * Smooth((Duration - Age) / ClearTime);
        }
        internal double Intensity(double x, double z)
        { return IntensityAfter(0); }
        internal double IntensityAfter(double ahead)
        {
            if (!Active || ahead < 0 || double.IsNaN(ahead) || double.IsInfinity(ahead) || ahead >= AvailableSeconds) return 0;
            if (stopping >= 0) return stopStrength * (1 - Smooth((stopping + ahead) / ManualClearTime));
            double age = Age + ahead;
            // Heavy weather persists everywhere; subtle slow breathing cannot
            // create clear holes or jump when driving/teleporting across the map.
            double pulse = 0.97 + 0.02 * Math.Sin(age * 0.03 + phase)
                                + 0.01 * Math.Sin(age * 0.07 - phase * 0.41);
            return Unit(pulse * Smooth(age / BuildTime) * Smooth((Duration - age) / ClearTime));
        }
        internal double FrontOpacity(double x, double z)
        {
            if (!Active) return 0;
            return Smooth(Age / 12) * (1 - Smooth((Age - BuildTime * 0.8) / (BuildTime * 0.4)))
                * (stopping >= 0 ? 1 - Smooth(stopping / ManualClearTime) : 1);
        }
        internal double Gust(double x, double z)
        {
            if (!Active) return 0;
            double along = (x - OriginX) * PrevailingX + (z - OriginZ) * PrevailingZ;
            return Smooth(0.5 + 0.34 * Math.Sin(Age * 0.39 + phase + along * 0.002)
                              + 0.16 * Math.Sin(Age * 0.17 - phase * 0.7));
        }
        internal static double Unit(double x) { return double.IsNaN(x) ? 0 : Math.Max(0, Math.Min(1, x)); }
        internal static double Smooth(double x) { x = Unit(x); return x * x * (3 - 2 * x); }
        internal static double FogDensity(double strength, double visibility)
        { return 2.3 / Math.Max(20, visibility) * Unit(strength); }
        internal static double RollDuration(double minimumMinutes, double maximumMinutes, double roll)
        {
            double low = Math.Max(3, Math.Min(minimumMinutes, maximumMinutes));
            double high = Math.Max(low, Math.Max(minimumMinutes, maximumMinutes));
            return (low + (high - low) * Unit(roll)) * 60;
        }
    }
    internal sealed class StormSchedule
    {
        private double calm, rollClock;
        internal void Reset() { calm = 0; rollClock = 0; }
        // Each full eligible minute gets one independent roll. No forced deadline.
        internal bool Tick(double seconds, double minimumCalmMinutes, double chancePercent, Func<double> roll)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            double before = calm; calm += seconds;
            double threshold = Math.Max(0, minimumCalmMinutes) * 60;
            double eligible = Math.Max(0, calm - threshold) - Math.Max(0, before - threshold);
            rollClock += eligible;
            while (rollClock >= 60)
            {
                rollClock -= 60;
                if (roll() < StormModel.Unit(chancePercent / 100)) { Reset(); return true; }
            }
            return false;
        }
    }
}
