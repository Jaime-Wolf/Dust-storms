using UnityEngine;
namespace ApocaDustStorm
{
    internal static class NightFogColours
    {
        // A clear night must not reintroduce a daytime-white fog backdrop.
        // Correct colour only; density, weather alpha and lights remain native.
        internal static Color Colour(Color native,float night)
        {
            if (night<=0) return native;
            float peak=Mathf.Max(native.r,Mathf.Max(native.g,native.b));
            float gain=peak>0.08f ? 0.08f/peak : 1;
            Color dimmed=native*gain; dimmed.a=native.a;
            return Color.Lerp(native,dimmed,Mathf.Clamp01(night));
        }
        internal static Vector4 Weather(Vector4 native,float night)
        {
            Color colour=Colour(new Color(native.x,native.y,native.z,native.w),night);
            return new Vector4(colour.r,colour.g,colour.b,native.w);
        }
    }
}
