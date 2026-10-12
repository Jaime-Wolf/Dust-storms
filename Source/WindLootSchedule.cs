using System;
namespace ApocaDustStorm
{
    internal sealed class WindLootSchedule
    {
        private double wait;
        private bool armed;
        internal int Count { get; private set; }
        internal static double RollWait(double roll) { return 45 + 45 * StormModel.Unit(roll); }
        internal bool Tick(double dt, bool eligible, Func<double> random)
        {
            if (!eligible || Count >= 2 || double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return false;
            if (!armed) { wait = Count == 0 ? 20 + 20 * StormModel.Unit(random()) : RollWait(random()); armed = true; }
            wait -= Math.Min(dt, 0.2);
            if (wait > 0) return false;
            armed = false; return true;
        }
        internal void Spawned() { Count++; }
        internal void Reset() { wait = 0; armed = false; Count = 0; }
    }
}
