using System;
namespace ApocaDustStorm
{
    internal static class DustEmissionBudget
    {
        internal static float Add(float budget, float rate, float dt, int alive, int capacity, int batchLimit)
        {
            int available = Math.Max(0, capacity - Math.Max(0, alive));
            if (available == 0) return 0;
            return Math.Min(Math.Min(available, batchLimit), budget + rate * dt);
        }
    }
}
