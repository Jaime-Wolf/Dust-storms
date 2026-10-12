using UnityEngine;
namespace ApocaDustStorm
{
    internal struct HaboobFrame { internal float Fog, Wind, Light, Palette, Grade, Impact; internal bool Entered; }
    internal sealed class HaboobTransition
    {
        // One storm clock moves the front. Distance to that front provides local
        // progress, so walking/driving into it meets the same phase sequence.
        private readonly AnimationCurve wind = Ramp(0, 0.45f);
        private readonly AnimationCurve dust = Ramp(580f / 1100f, 1);
        private readonly AnimationCurve light = Ramp(0.15f, 1);
        private readonly AnimationCurve palette = Ramp(0.08f, 0.86f);
        private readonly AnimationCurve grade = Ramp(0.48f, 1);
        private readonly AnimationCurve impact = new AnimationCurve(new Keyframe(0, 1, 0, 0), new Keyframe(0.25f, 0.7f, -2, -2), new Keyframe(1, 0, 0, 0));
        private readonly HaboobEntryModel entry = new HaboobEntryModel();
        private float impactAge = 2;
        private static AnimationCurve Ramp(float from, float to)
        { return new AnimationCurve(new Keyframe(from, 0, 0, 0), new Keyframe(to, 1, 0, 0)); }
        internal HaboobFrame Step(StormModel model, Vector3 player, float dt, bool skipped)
        {
            HaboobFrame result = new HaboobFrame();
            double signed = model.SignedDistance(player.x, player.z);
            bool entered = entry.Step(model.Active, model.Age, signed, dt, skipped);
            if (double.IsNaN(signed) || double.IsInfinity(signed)) { impactAge = 2; return result; }
            if (skipped || !model.Active) impactAge = 2;
            else if (entered) impactAge = 0;
            else impactAge += Mathf.Max(0, dt);
            if (!model.Active) return result;
            float progress = Mathf.Clamp01((900 - (float)signed) / 1100);
            float life = (float)model.FrontOpacity(player.x, player.z);
            result.Fog = (float)model.Envelope * (0.025f + 0.975f * dust.Evaluate(progress));
            result.Wind = Mathf.Max(result.Fog, life * wind.Evaluate(progress));
            result.Light = Mathf.Max(result.Fog, life * light.Evaluate(progress));
            result.Palette = Mathf.Max(result.Fog, life * palette.Evaluate(progress));
            result.Grade = life * grade.Evaluate(progress);
            result.Impact = impactAge < 1 ? Mathf.Clamp01(impact.Evaluate(impactAge)) : 0;
            result.Entered = entered; return result;
        }
        internal void Reset() { entry.Reset(); impactAge = 2; }
    }
}
