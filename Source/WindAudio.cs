using System;
using UnityEngine;

namespace ApocaDustStorm
{
    internal sealed class WindAudio : IDisposable
    {
        private readonly GameObject owner;
        private readonly AudioSource medium, heavy;
        private float nextFind, mediumLevel, heavyLevel; private bool reported, missing;
        internal WindAudio()
        {
            owner = new GameObject("ApocaDustStorm.Wind"); owner.hideFlags = HideFlags.HideAndDontSave;
            medium = Source("Wind bed"); heavy = Source("Wind gusts");
        }
        private AudioSource Source(string name)
        {
            GameObject go = new GameObject(name); go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetParent(owner.transform, false);
            AudioSource source = go.AddComponent<AudioSource>(); source.loop = true; source.playOnAwake = false;
            source.volume = 0; source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = 100;
            source.ignoreListenerVolume = false; source.ignoreListenerPause = false; source.outputAudioMixerGroup = null;
            return source;
        }
        private void FindClips()
        {
            foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (clip == null || clip.length < 2) continue;
                if (clip.name.Equals("Medium Wind Sound", StringComparison.OrdinalIgnoreCase)) medium.clip = clip;
                if (clip.name.Equals("Heavy Wind Sound", StringComparison.OrdinalIgnoreCase)) heavy.clip = clip;
            }
            if (medium.clip == null || heavy.clip == null)
                foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                    if (clip != null && clip.name.Equals("Sandstorm_SoundLoop", StringComparison.OrdinalIgnoreCase))
                    { if (medium.clip == null) medium.clip = clip; if (heavy.clip == null) heavy.clip = clip; break; }
            if (medium.clip != null) medium.clip.LoadAudioData();
            if (heavy.clip != null) heavy.clip.LoadAudioData();
            if (!reported && (medium.clip != null || heavy.clip != null))
            {
                reported = true; Plugin.Log.LogInfo("Storm wind uses native clips: " + (medium.clip == null ? "none" : medium.clip.name) + " / " + (heavy.clip == null ? "none" : heavy.clip.name) + ".");
            }
            if (!reported && !missing) { missing = true; Plugin.Log.LogWarning("Native wind clips are not loaded yet; retrying while the preview runs."); }
        }
        internal void Tick(float strength, float gust, float dt)
        {
            if ((medium.clip == null || heavy.clip == null) && Time.unscaledTime >= nextFind)
            { nextFind = Time.unscaledTime + 2; FindClips(); }
            float slider = Mathf.Clamp(Plugin.Value(Plugin.WindVolume, 1), 0, 2);
            if (slider <= 0 || strength <= 0.001f) { Silence(); return; }
            float mix = Mathf.Clamp01((float)WindMath.Gain(gust, Plugin.Value(Plugin.Gusts, 1)));
            float volume = (float)WindMath.WindVolume(strength, mix, slider);
            Play(medium, volume * (1 - mix * 0.55f), dt, ref mediumLevel);
            Play(heavy, volume * (0.25f + mix * 0.65f), dt, ref heavyLevel);
        }
        private static void Play(AudioSource source, float volume, float dt, ref float level)
        {
            if (source.clip == null || source.clip.loadState != AudioDataLoadState.Loaded) return;
            // Keep the normal gust envelope separate from the brief strike dip,
            // so wind returns to its current mix without a second slow fade.
            level = Mathf.Lerp(level, Mathf.Clamp01(volume), 1 - Mathf.Exp(-dt / 0.7f));
            source.volume = level * DustLightning.WindGain;
            if (!source.isPlaying && source.volume > 0.001f) source.Play();
        }
        internal void Silence()
        {
            mediumLevel = 0; heavyLevel = 0;
            if (medium != null) { medium.Stop(); medium.volume = 0; }
            if (heavy != null) { heavy.Stop(); heavy.volume = 0; }
        }
        public void Dispose() { Silence(); if (owner != null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); } }
    }
}
