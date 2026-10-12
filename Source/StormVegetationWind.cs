using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal sealed class StormVegetationWind : IDisposable
    {
        private readonly GameObject owner;
        private readonly WindZone zone;
        internal StormVegetationWind()
        {
            owner = new GameObject("ApocaDustStorm.GustFrontWind"); owner.hideFlags = HideFlags.HideAndDontSave;
            zone = owner.AddComponent<WindZone>(); zone.mode = WindZoneMode.Directional; zone.windMain = 0;
            zone.windTurbulence = 0; zone.windPulseMagnitude = 0; zone.windPulseFrequency = 0;
        }
        internal void Tick(StormModel model, float strength, float gust)
        {
            float gain = Mathf.Clamp01(strength) * Mathf.Clamp(Plugin.Value(Plugin.Gusts, 1), 0, 2);
            bool active = Plugin.Active && gain > 0.001f;
            if (owner.activeSelf != active) owner.SetActive(active);
            if (!active) { zone.windMain = zone.windTurbulence = zone.windPulseMagnitude = 0; return; }
            owner.transform.rotation = Quaternion.LookRotation(new Vector3((float)model.WindX, 0, (float)model.WindZ));
            zone.windMain = gain * Mathf.Lerp(0.6f, 1.6f, gust);
            zone.windTurbulence = gain * Mathf.Lerp(0.2f, 0.8f, gust);
            zone.windPulseMagnitude = 0.2f * gain; zone.windPulseFrequency = 0.35f;
        }
        internal void Silence() { zone.windMain = zone.windTurbulence = zone.windPulseMagnitude = 0; owner.SetActive(false); }
        public void Dispose() { Silence(); owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
    }
}
