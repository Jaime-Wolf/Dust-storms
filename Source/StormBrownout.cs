using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
namespace ApocaDustStorm
{
    // Private runtime volume/profile. Native/shared profiles and exposure settings
    // are never edited. Weight is enabled only during the active view's blending.
    internal static class StormBrownout
    {
        private static GameObject owner;
        private static PostProcessVolume volume;
        private static PostProcessProfile profile;
        private static PostProcessLayer layer;
        private static Camera view;
        private static float nextFind, weight;
        private static bool failed;
        internal static void Tick(Camera camera, float amount)
        {
            weight = Mathf.Clamp01(amount * Plugin.Value(Plugin.Brownout, 1)) * (PlayerStormHazards.Sheltered ? 0.3f : 1);
            if (view != camera) { view = camera; layer = null; nextFind = 0; }
            if (camera == null || weight < 0.001f || failed) { if (volume != null) volume.weight = 0; return; }
            if (layer == null && Time.unscaledTime >= nextFind)
            { nextFind = Time.unscaledTime + 2; layer = camera.GetComponent<PostProcessLayer>(); }
            if (layer == null || !layer.isActiveAndEnabled || layer.volumeLayer.value == 0) return;
            try
            {
                int mask = layer.volumeLayer.value, bit = 0;
                while (bit < 31 && (mask & (1 << bit)) == 0) bit++;
                if (owner == null)
                {
                    owner = new GameObject("ApocaDustStorm.Brownout"); owner.hideFlags = HideFlags.HideAndDontSave;
                    owner.layer = bit;
                    profile = ScriptableObject.CreateInstance<PostProcessProfile>(); profile.hideFlags = HideFlags.HideAndDontSave;
                    ColorGrading grading = profile.AddSettings<ColorGrading>(); grading.enabled.Override(true);
                    grading.temperature.Override(8); grading.saturation.Override(-24); grading.contrast.Override(-8);
                    grading.colorFilter.Override(new Color(1, 0.92f, 0.80f, 1));
                    grading.gamma.Override(new Vector4(1.01f, 1, 0.98f, 0));
                    grading.gain.Override(new Vector4(1, 0.99f, 0.97f, 0));
                    volume = owner.AddComponent<PostProcessVolume>(); volume.isGlobal = true;
                    volume.sharedProfile = profile; volume.priority = 100; volume.weight = 0;
                }
                else if (owner.layer != bit)
                {
                    // Re-register immediately, rather than waiting for PPv2's
                    // next update after switching to a different camera mask.
                    volume.enabled = false; owner.layer = bit; volume.enabled = true;
                }
            }
            catch (Exception e) { Clear(); failed = true; Plugin.Log.LogWarning("Brownout grading unavailable; retaining brown distance fog: " + e.Message); }
        }
        internal static float Before(PostProcessLayer target, Camera camera)
        {
            if (volume == null) return 0;
            float previous = volume.weight;
            volume.weight = Plugin.Active && StormRunner.CanRender && target == layer && camera == view && camera == StormRunner.View ? weight : 0;
            return previous;
        }
        internal static void After(float previous) { if (volume != null) volume.weight = previous; }
        internal static void Suspend() { weight = 0; if (volume != null) volume.weight = 0; }
        internal static void Clear()
        {
            Suspend(); if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
            if (profile != null)
            { foreach (PostProcessEffectSettings setting in profile.settings) if (setting != null) UnityEngine.Object.Destroy(setting); UnityEngine.Object.Destroy(profile); }
            owner = null; volume = null; profile = null; view = null; layer = null; nextFind = 0; failed = false;
        }
    }
    [HarmonyPatch(typeof(PostProcessLayer), "UpdateVolumeSystem")]
    internal static class StormBrownoutPatch
    {
        private static void Prefix(PostProcessLayer __instance, Camera __0, out float __state)
        { __state = StormBrownout.Before(__instance, __0); }
        private static Exception Finalizer(Exception __exception, float __state)
        { StormBrownout.After(__state); return __exception; }
    }
}
