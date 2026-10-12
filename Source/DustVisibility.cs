using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class DustVisibility
    {
        internal static float RetireDistance(float strength, float visibility, float gust, float gustSetting)
        {
            double density = StormModel.FogDensity(strength * strength, visibility) * WindMath.Haze(gust, gustSetting);
            return density > 0 ? (float)(-Math.Log(0.001) / density) : float.PositiveInfinity;
        }
        internal static bool Hidden(Vector3 position, Vector3 eye, float size, float distance)
        {
            // Bound the growing/stretched billboard, including a small velocity
            // allowance. The former 2x size + 20 m padding retained hidden dust.
            float padded = distance + Math.Max(0, size) * 1.2f + 4;
            return !float.IsInfinity(padded) && (position - eye).sqrMagnitude > padded * padded;
        }
    }
}
