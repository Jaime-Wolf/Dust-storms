using System;
namespace ApocaDustStorm
{
    // Cosmetic pacing, not an electrostatic simulation. Each eligible heavy
    // storm interval rolls a fresh wait between 30 and 50 seconds.
    internal sealed class DustDischargeModel
    {
        private double wait, age = 100, delay = 0.1, power;
        private bool armed, sound;
        internal double Pulse { get { return age >= 1.6 ? 0 : power * StormModel.Smooth(age / 0.045) * (1 - StormModel.Smooth((age - 0.16) / 1.44)); } }
        internal double WindGain { get { return 1 - 0.25 * StormModel.Smooth(age / 0.14) * (1 - StormModel.Smooth((age - 1.35) / 3.4)); } }
        internal static double RollWait(double roll)
        { return 30 + StormModel.Unit(roll) * 20; }
        internal bool Step(double dt, bool eligible, Func<double> random)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return false;
            dt = Math.Min(dt, 0.2); age += dt;
            if (!eligible) { armed = false; return false; }
            if (!armed) { wait = RollWait(random()); armed = true; }
            wait -= dt;
            if (wait > 0 || age < 6.85) return false;
            Trigger(random()); wait = RollWait(random()); return true;
        }
        private void Trigger(double roll)
        { age = 0; power = 0.65 + 0.25 * StormModel.Unit(roll); delay = 0.1; sound = true; }
        internal bool Preview(double roll)
        { if (age < 6.85) return false; Trigger(roll); return true; }
        internal void SetDistance(double metres)
        { delay = Math.Max(0.04, Math.Min(0.25, metres / 343)); }
        internal bool TakeSound()
        { if (!sound || age < delay) return false; sound = false; return true; }
        internal void CancelPulse() { age = 100; sound = false; }
        internal void Reset() { CancelPulse(); armed = false; wait = 0; }
    }
}
