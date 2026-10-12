using System;

namespace ApocaDustStorm
{
    // Visual math only. The gameplay boundary and storm clock are unchanged.
    internal static class HaboobVolumeMath
    {
        internal const int Columns = 49, Rows = 7, Depths = 6;
        internal const int Count = Columns * Rows * Depths;
        internal const double Spacing = 104;
        internal static int Slot(double cell)
        { return (int)((Math.Round(cell / Spacing) % Columns + Columns) % Columns); }
        internal static double Seed(double cell, int row, int depth, int salt)
        {
            uint x = unchecked((uint)((long)Math.Round(cell / Spacing) * 374761393L + row * 668265263L + depth * 144269L + salt * 1274126177L));
            x = unchecked((x ^ (x >> 13)) * 1274126177u);
            return (x ^ (x >> 16)) / (double)uint.MaxValue;
        }
        internal static double Fade(double signed)
        { return StormModel.Smooth((signed + 620) / 460) * StormModel.Smooth((3000 - signed) / 600); }
        internal static double CameraFade(double distance, double diameter)
        { return StormModel.Smooth((distance - diameter * 0.24) / Math.Max(1, diameter * 0.40)); }
        internal static double TerrainStep(double height, double target, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return height;
            double dt = Math.Min(seconds, 0.25);
            double difference = (target - height) * (1 - Math.Exp(-dt / 0.9));
            return height + Math.Max(-65 * dt, Math.Min(65 * dt, difference));
        }
        internal static double Phase(double age, double seed)
        { double t = age / 13.0 + seed; return t - Math.Floor(t); }
        private static double Hash(int x, int y)
        {
            uint n = unchecked((uint)(x * 374761393 + y * 668265263 + 144269));
            n = unchecked((n ^ (n >> 13)) * 1274126177u);
            return (n ^ (n >> 16)) / (double)uint.MaxValue;
        }
        private static double Noise(double x, double y)
        {
            int a = (int)Math.Floor(x), b = (int)Math.Floor(y);
            double u = StormModel.Smooth(x - a), v = StormModel.Smooth(y - b);
            return (Hash(a,b)*(1-u)+Hash(a+1,b)*u)*(1-v)+(Hash(a,b+1)*(1-u)+Hash(a+1,b+1)*u)*v;
        }
        internal static double Billow(double u, double v, double phase, out double shade)
        {
            double angle = phase*Math.PI*2, a = Math.Cos(angle)*0.30, b = Math.Sin(angle)*0.30;
            double x = u*2-1, y = v*2-1;
            double wx = (Noise(u*3+a+7,v*3+b+13)-0.5)*0.22;
            double wy = (Noise(u*3-b+31,v*3+a+5)-0.5)*0.22;
            double coarse = Noise((u+wx)*4+a,(v+wy)*4+b);
            double fine = Noise((u+wx)*11-b+17,(v+wy)*11+a+29);
            double radius = Math.Sqrt((x+wx)*(x+wx)+(y+wy)*(y+wy));
            double edge = 1-StormModel.Smooth((radius-0.35-coarse*0.15)/0.43);
            // Empty cell borders protect animated atlas frames from mip bleeding.
            double border = StormModel.Smooth(Math.Min(Math.Min(u,1-u),Math.Min(v,1-v))/0.075);
            shade = 0.62+0.28*coarse+0.10*(1-v);
            return StormModel.Unit(edge*border*(0.48+0.42*coarse+0.10*fine));
        }
    }
}
