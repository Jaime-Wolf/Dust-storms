using System;

namespace ApocaDustStorm
{
    internal static class HaboobMath
    {
        internal const int Columns = 33, Rows = 6, Layers = 2;
        internal const double Spacing = 160;
        internal static int HeightSlot(double side)
        { return (int)((Math.Round(side / Spacing) % Columns + Columns) % Columns); }
        internal static double Edge(double side, double age)
        { return (26 * Math.Sin(side * 0.002 + age * 0.035) + 12 * Math.Sin(side * 0.006 - age * 0.024)) * StormModel.Smooth(age / 8); }
        internal static double Arrival(double signedDistance)
        { return StormModel.Smooth((320 - signedDistance) / 520); }
        internal static double WallFade(double signedDistance)
        { return StormModel.Smooth((signedDistance + 220) / 400); }
        internal static double Height(double side, double age)
        { return 325 + 190 * (0.72 * Noise((side - age * 3) / 2300, age * 0.0025, 8) + 0.28 * Noise((side + 173) / 2300, age * 0.004, 16)); }
        internal static double Roll(double height, double side, double age)
        { return (45 + 30 * (2 * Noise((side - age * 4) / 1800, age * 0.005, 8) - 1)) * Math.Sin(height * Math.PI) - 110 * height; }
        internal static double SideFade(int column)
        { return StormModel.Smooth(Math.Min(column, Columns - 1 - column) / 4.0); }
        internal static double TopFade(double height)
        { return 1 - StormModel.Smooth((height - 0.72) / 0.28); }
        private static double Grain(int x, int y, int period)
        {
            x = (x % period + period) % period;
            uint hash = unchecked((uint)(x * 374761393 + y * 668265263 + 144269));
            hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
            return (hash ^ (hash >> 16)) / (double)uint.MaxValue;
        }
        private static double Noise(double u, double v, int period)
        {
            u = (u - Math.Floor(u)) * period;
            double y = v * period; int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(y);
            double tx = StormModel.Smooth(u - x0), ty = StormModel.Smooth(y - y0);
            double a = Grain(x0, y0, period) * (1 - tx) + Grain(x0 + 1, y0, period) * tx;
            double b = Grain(x0, y0 + 1, period) * (1 - tx) + Grain(x0 + 1, y0 + 1, period) * tx;
            return a * (1 - ty) + b * ty;
        }
        // Overlapping, jittered elliptical billows give the upper edge rounded
        // lobes instead of a straight strip. This runs only at texture creation.
        private static double Billows(double u, double v)
        {
            const int period = 6;
            double x = (u - Math.Floor(u)) * period;
            int cell = (int)Math.Floor(x);
            double result = 1 - StormModel.Smooth((v - 0.32) / 0.30);
            for (int i = cell - 2; i <= cell + 2; i++)
            {
                double cx = i + 0.5 + 0.45 * (Grain(i, 23, period) - 0.5);
                double cy = 0.45 + 0.08 * Grain(i, 24, period);
                double rx = 1.05 + 0.25 * Grain(i, 25, period), ry = 0.28 + 0.16 * Grain(i, 26, period);
                double dx = (x - cx) / rx, dy = (v - cy) / ry;
                result = Math.Max(result, 1 - StormModel.Smooth((Math.Sqrt(dx * dx + dy * dy) - 0.62) / 0.38));
            }
            return result;
        }
        // Generated once: domain warping breaks up the repeated cell pattern,
        // and neutral shading keeps the existing day/night sand palette.
        internal static double TextureAlpha(double u, double v)
        {
            double warpedU = u + 0.065 * (Noise(u, v, 4) - 0.5);
            double warpedV = v + 0.14 * (Noise(u + 0.37, v, 8) - 0.5) * Math.Sin(v * Math.PI);
            double grain = 0.72 + 0.28 * (0.6 * Noise(warpedU, warpedV, 4) + 0.28 * Noise(warpedU, warpedV, 8) + 0.12 * Noise(warpedU, warpedV, 16));
            double erosion = (0.10 + 0.30 * v) * (Noise(warpedU, warpedV, 32) - 0.5);
            double body = StormModel.Smooth((Billows(warpedU, warpedV) + erosion) / 0.94);
            return StormModel.Unit(grain * body * TopFade(v));
        }
        internal static double TextureShade(double u, double v)
        { return 0.68 + 0.32 * StormModel.Smooth((0.65 * Noise(u, v, 8) + 0.35 * Noise(u + 0.19, v, 16) - 0.2) / 0.6); }
        internal static double TextureU(double side, double height, double age, int layer)
        { return side / 1280 + age * (layer == 0 ? 0.003 : -0.0017) + layer * 0.41 + 0.035 * Math.Sin(age * 0.045 + height * 5 + layer * 2.1) * Math.Sin(height * Math.PI); }
        internal static double TextureV(double side, double height, double age, int layer)
        { return height + 0.055 * Math.Sin(side * 0.0021 + age * (layer == 0 ? 0.055 : -0.04) + layer * 2.4 + height * 4) * Math.Sin(height * Math.PI); }
    }
}
