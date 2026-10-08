using System;
namespace ApocaDustStorm
{
    internal struct ExposureLayout { internal double X, Y, Size; }
    internal static class ExposureHudMath
    {
        internal static double EdgeMask(double x, double y)
        {
            // Clear center; the dust only occupies the outer rim, with no animation or flashing.
            double rim = Math.Max(Math.Abs(x * 2 - 1), Math.Abs(y * 2 - 1));
            double fade = StormModel.Smooth((rim - 0.78) / 0.22);
            double grain = 0.78 + 0.11 * Math.Sin(x * 71 + y * 37) + 0.08 * Math.Sin(x * 173 - y * 93);
            return fade * Math.Max(0.5, grain) * 0.22;
        }
        internal static double EdgeTarget(StormCover state, double strength, double protection)
        {
            if (state == StormCover.None || state == StormCover.Shelter) return 0;
            return StormHazardModel.Severity(strength) * (state == StormCover.Vehicle ? 1 - protection : 1);
        }
        internal static ExposureLayout Place(double width, double height, double compassCentre, double compassTop)
        {
            double scale = Math.Max(0.75, Math.Min(1.35, height / 1080)), size = 48 * scale;
            return new ExposureLayout {
                X = Math.Max(8, Math.Min(width - size - 8, compassCentre - size*0.5)),
                Y = Math.Max(8, Math.Min(height - size - 8, compassTop - size - 8 * scale)), Size = size
            };
        }
    }
}
