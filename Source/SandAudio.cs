using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal sealed class SandAudio : IDisposable
    {
        private readonly GameObject owner;
        private readonly AudioSource source;
        private readonly AudioClip clip;
        internal SandAudio()
        {
            owner = new GameObject("ApocaDustStorm.SandOnMetal"); owner.hideFlags = HideFlags.HideAndDontSave;
            source = owner.AddComponent<AudioSource>(); source.loop = true; source.playOnAwake = false;
            source.volume = 0; source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = 110;
            source.ignoreListenerVolume = false; source.ignoreListenerPause = false; source.outputAudioMixerGroup = null;
            float[] samples = SandSound.Generate(37016);
            clip = AudioClip.Create("ApocaDustStorm.SoftSandOnBodywork", samples.Length, 1, SandSound.SampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave; clip.SetData(samples, 0); source.clip = clip;
        }
        internal void Tick(GameObject player, float strength, float gust, float dt)
        {
            float setting = Mathf.Clamp(Plugin.Value(Plugin.SandVolume, 1), 0, 2);
            if (!Plugin.Active || setting <= 0 || strength <= 0.001f || !VehicleWind.IsDriving(player)) { Silence(); return; }
            float target = (float)SandSound.Gain(strength, (float)WindMath.Gain(gust, Plugin.Value(Plugin.Gusts, 1)), setting, true);
            source.volume = Mathf.Lerp(source.volume, target, 1 - Mathf.Exp(-Mathf.Max(0, dt) / 0.9f));
            if (!source.isPlaying && source.volume > 0.001f) source.Play();
        }
        internal void Silence() { source.Stop(); source.volume = 0; }
        public void Dispose()
        { Silence(); owner.SetActive(false); UnityEngine.Object.Destroy(owner); UnityEngine.Object.Destroy(clip); }
    }
}
