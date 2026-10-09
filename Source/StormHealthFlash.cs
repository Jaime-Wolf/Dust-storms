using UnityEngine;
using UnityEngine.UI;
namespace ApocaDustStorm
{
    // One short pulse on the existing health readout, only after real storm loss.
    internal static class StormHealthFlash
    {
        private const float Duration = 0.6f, Interval = 2;
        private static float started = -Interval, nextPulse, nextLookup;
        private static readonly Tint label = new Tint(), number = new Tint();
        private sealed class Tint
        {
            internal Text Target;
            private Color native, written;
            private bool owns;
            internal void Bind(Text next) { Restore(); Target = next; }
            internal void Apply(float pulse)
            {
                if (Target == null || !Target.isActiveAndEnabled) { Restore(); return; }
                Color current = Target.color;
                if (!owns || !Same(current, written)) native = current;
                // Preserve native transparency and only blend the visible RGB.
                written = Color.Lerp(native, new Color(1, 0.85f, 0.12f, native.a), pulse);
                Target.color = written; owns = true;
            }
            internal void Restore()
            {
                if (Target != null && owns && Same(Target.color, written)) Target.color = native;
                owns = false;
            }
        }
        private static bool Same(Color a, Color b)
        { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }
        internal static void DamageTaken()
        {
            if (!Visible() || Time.unscaledTime < nextPulse) return;
            started = Time.unscaledTime; nextPulse = started + Interval;
        }
        private static bool Visible()
        {
            return Plugin.Active && PlayerStormHazards.Ready && !PlayerStormHazards.Sleeping &&
                !Apocasetter.GameMenu.Paused && Time.timeScale > 0 &&
                Plugin.Value(Plugin.ExposureIndicators, 1) > 0;
        }
        internal static void Tick()
        {
            float age = Time.unscaledTime - started;
            if (!Visible() || age < 0 || age >= Duration)
            { label.Restore(); number.Restore(); return; }
            if ((label.Target == null || number.Target == null) && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1;
                if (label.Target == null) label.Bind(Find("Canvas/SurvivalCanvas/HealthLable"));
                if (number.Target == null) number.Bind(Find("Canvas/SurvivalCanvas/HealthLable/HealthText"));
            }
            // Apply after native HUD updates; damage every frame cannot restart the fade.
            float pulse = Mathf.Clamp01((Duration - age) / (Duration - 0.1f)) *
                Mathf.Clamp01(Plugin.Value(Plugin.ExposureIndicators, 1));
            label.Apply(pulse); number.Apply(pulse);
        }
        private static Text Find(string path)
        {
            GameObject owner = GameObject.Find(path);
            return owner != null ? owner.GetComponent<Text>() : null;
        }
        internal static void Clear()
        {
            label.Restore(); number.Restore(); label.Target = number.Target = null;
            started = -Interval; nextPulse = nextLookup = 0;
        }
    }
}
