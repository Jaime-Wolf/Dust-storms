using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApocaDustStorm
{
#if APOCA_DEV
    [BepInPlugin(GUID, "ApocaDustStorm (dev)", VERSION)]
#else
    [BepInPlugin(GUID, "ApocaDustStorm", VERSION)]
#endif
    [BepInDependency(Apocasetter.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.denis.apocalypter.npcai", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "local.apocalypter.duststorm";
        public const string VERSION = "0.1.29";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled, Automatic, Status, FollowNativeSwitch, Lightning, WindblownLizards;
        internal static ConfigEntry<float> Visibility, Dust, Darkness, Debris, MinimumDuration, MaximumDuration, BuildTime, ClearTime, Chance, MinimumCalm, Gusts, WindVolume, SandVolume, Headlights, FlashBrightness, DischargeVolume, Buffeting, MovementResistance, ExposureDamage, ExposureIndicators;
#if APOCA_DEV
        internal static ConfigEntry<KeyCode> PreviewKey, DischargeKey;
#endif
        internal static bool Active { get { return Enabled != null && Enabled.Value; } }
        internal static float StormVisibility { get { return Value(Visibility, 45) * 1.02f; } }
        internal static bool DustLightningEnabled { get { return Active && (Lightning == null || Lightning.Value); } }
        private static GameObject runner;
        private static Harmony patches;
        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show storm settings in MODS.");
            Enabled = Config.Bind("General", "Enabled", true, "Worldwide dust storms with movement resistance, modest vehicle gusts and player-only exposure damage. Vehicle protection starts at 90% and falls to 25% after five minutes of hazardous driving; proper shelter gradually restores it and stops damage. AI never receive storm damage.");
            Automatic = Config.Bind("Storms", "Automatic storms", true, "Chance-based worldwide storms, controlled independently of vanilla storms by default.");
            FollowNativeSwitch = Config.Bind("Storms", "Follow vanilla storm switch", false, "Enable to let the game's Dust Storm Off option also stop automatic storms from this mod. Disabled by default, so you can leave vanilla storms off and use these instead.");
            Lightning = Config.Bind("Storms", "Dust lightning", true, "Cosmetic lightning in heavy dust, with a branching arc and a brief local light that casts shadows where the game's shaders/settings support them. Brief textured thunderclap followed by low rumble and reverb. Each strike waits a random 30-50 seconds of heavy dust, averaging about 40 seconds. No damage or physical lightning strikes.");
            WindblownLizards = Config.Bind("Storms", "Windblown lizards", true, "Occasionally a native raw-lizard food item blows past during heavy dust. It lands as real loot you can pick up, eat or cook. At most two per storm, spaced 90-180 seconds of heavy outdoor dust apart. Existing lizards and AI are never removed or damaged; no spawned AI. Requires the game's food prefab to be loaded.");
            Visibility = Slider("Appearance", "Peak visibility", 45, 20, 180, "Visibility reference in metres, before the dense-storm haze multiplier. Higher values make the storm easier to see through; your saved choice is preserved.");
            Dust = Slider("Effects", "Dust amount", 1, 0, 2, "Amount of visible dust lifted nearby and blown downwind. 1 is the tuned plume strength; zero stops new local dust particles. Distance haze uses Peak visibility.");
            Darkness = Slider("Effects", "Storm darkening", 1, 0, 2, "Sunlight and ambient dimming, with an earthy overcast sky. 1 is the dark-daylight baseline. Fades with the worldwide storm and has a brightness floor. Zero keeps native lighting. Headlights retain their brightness.");
            Debris = Slider("Effects", "Twigs and sticks", 1, 0, 2, "Small windblown branches. 1 is the normal tuned amount. They bounce against world and vehicle surfaces without sending damage or pushing game objects. Zero clears them. Tumbleweeds have been removed.");
            Gusts = Slider("Effects", "Gust strength", 1.0f, 0, 2, "Strength of changing wind gusts in dust movement, wind sound and vehicle buffeting. Zero leaves a gentler steady breeze.");
            WindVolume = Slider("Effects", "Wind volume", 1, 0, 2, "Volume of native wind recordings, rising and falling with gusts. The normal mix is 15% quieter than 0.1.13 at the same setting. Briefly fades by up to 25% during audible lightning, then returns smoothly as thunder fades. 1 is the normal tuned mix, not maximum volume. Respects game master volume. Zero mutes the added wind layer.");
            SandVolume = Slider("Effects", "Sand on metal volume", 1, 0, 2, "Soft, irregular sand hiss and grain patter against vehicle bodywork while driving in a storm. 1 is the normal mix beneath wind. Respects master volume. Zero mutes only this layer.");
            Headlights = Slider("Effects", "Headlight assist", 1, 0, 2, "Modest storm-only boost to vehicle spotlights that are already on. 1 adds up to 35% intensity and 20% range. Does not clear distant dust or turn lights on. Zero retains native headlights.");
            FlashBrightness = Slider("Effects", "Dust flash brightness", 1, 0, 2, "Brightness of lightning arcs and their longer local shadow-casting light. Automatically boosts the light and bolt at night, without changing the slider. Fog and ambient sky retain their storm baseline. Zero hides bolt/light while leaving sound independently adjustable.");
            DischargeVolume = Slider("Effects", "Dust discharge volume", 1, 0, 2, "Brief thunderclap with softer tearing texture, followed by a 6.5-second low thunder roll and spacious reverb tail. 1 is the tuned mix; zero mutes it. Respects listener master volume and pause.");
            Buffeting = Slider("Effects", "Vehicle buffeting", 1, 0, 2, "Gentle horizontal wind pressure on the vehicle you are driving, while grounded. 1 is the normal tuned pressure. No lift, suction or direct part damage. Zero turns it off.");
            MovementResistance = Slider("Effects", "Movement resistance", 1, 0, 2, "Wind slows movement during strong storms, reaching 30% in peak gusts at 1. Zero keeps native speeds. Does not affect gravity, jumping, ladder climbing or vehicle throttle.");
            ExposureDamage = Slider("Effects", "Player exposure damage", 1, 0, 2, "Player-only storm damage starts immediately outside shelter, with no grace timer. At 1, peak damage is 18 health per minute outside. Vehicle protection drops from 90% to 25% over five minutes of hazardous driving, so peak cab damage rises from 1.8 to 13.5 health per minute. On-foot time/calm weather do not reset protection; proper shelter restores it over up to two minutes and stops damage. Active storms interrupt exposed sleep, including in vehicles and during approach, without sleep or wake damage. Buildings, caves and conex allow normal sleep. AI remain immune. Zero disables health damage; exposed sleep is still interrupted.");
            ExposureIndicators = Slider("Effects", "Exposure indicators", 1, 0, 2, "Subtle, steady dusty screen edges and a small rusty icon centered just above the compass. House = sheltered, cab = vehicle cover, wind = exposed. The small fill shows current protection, shrinking as the cab loses protection. A short yellow pulse on the existing health label and number marks actual storm damage, at most once every two seconds. Center vision stays clear. Zero hides the indicators and health pulse without changing damage.");
            MinimumDuration = Slider("Storms", "Minimum storm duration", 5, 3, 60, "Minimum duration in minutes of normal play. Each storm randomly picks between minimum and maximum, including approach and clearing. Sleeping advances it using the native game clock.");
            MaximumDuration = Slider("Storms", "Maximum storm duration", 15, 3, 60, "Maximum duration in minutes of normal play. The default random range is 5 to 15 minutes. Storms can happen during day or night. Reversed endpoints are sorted automatically.");
            BuildTime = Slider("Storms", "Storm build-up", 90, 30, 240, "Seconds for dust, lighting and wind to gradually reach full strength. Capped at 40% of total duration.");
            ClearTime = Slider("Storms", "Storm clearing", 75, 30, 180, "Seconds for a natural storm to gradually clear. Capped at 40% of total duration.");
            Chance = Slider("Storms", "Storm chance per minute", 6, 0, 30, "Percent chance each eligible minute after the minimum calm time. Storms are never guaranteed; zero disables random arrivals.");
            MinimumCalm = Slider("Storms", "Minimum calm time", 5, 0, 30, "Minutes of clear weather before chance rolls begin, including after loading a game. This is a cooldown, not a fixed storm schedule.");
#if APOCA_DEV
            PreviewKey = Config.Bind("Controls", "Preview storm", KeyCode.F8, "Start a gradually approaching worldwide test storm. Press again to fade it out over 30 seconds. Works on foot or while driving. None disables the key.");
            DischargeKey = Config.Bind("Controls", "Preview dust lightning", KeyCode.N, "Preview one harmless static discharge during visible dust, without changing automatic frequency. Requires Dust lightning enabled. None disables the key.");
            // Move the former default to N; preserve other user-selected keys.
            if (DischargeKey.Value == KeyCode.F9) DischargeKey.Value = KeyCode.N;
#endif
            // Retain the previous notice preference while removing the test label
            // from the player-facing settings. Old preview-key values remain in
            // the configuration for switching back to a development build.
            ConfigEntry<bool> previousStatus = Config.Bind("Controls", "Show test status", true, "Legacy storm notice preference.");
            Status = Config.Bind("Controls", "Show storm notices", previousStatus.Value, "Brief approaching/passing notices. Exposure uses the separate Exposure indicators setting, without text banners.");
            Config.Remove(previousStatus.Definition);
            patches = new Harmony(GUID); patches.PatchAll(typeof(Plugin).Assembly); StormAIMovement.InstallNpcBridge(patches);
            Enabled.SettingChanged += SettingsChanged;
            SceneManager.sceneLoaded += Loaded;
            Camera.onPreCull += StormFog.PreCull;
            Camera.onPostRender += StormFog.PostRender;
            EnsureRunner();
#if APOCA_DEV
            Log.LogInfo("ApocaDustStorm " + VERSION + " dev ready; " + PreviewKey.Value + " starts/clears; " + DischargeKey.Value + " previews cosmetic dust lightning. Player exposure hazards enabled; AI and vehicle parts remain immune to storm damage.");
#else
            Log.LogInfo("ApocaDustStorm " + VERSION + " ready. Automatic storms enabled according to saved settings; AI and vehicle parts remain immune to storm damage.");
#endif
        }
        private ConfigEntry<float> Slider(string group, string key, float value, float min, float max, string text)
        { return Config.Bind(group, key, value, new ConfigDescription(text, new AcceptableValueRange<float>(min, max))); }
        internal static float Value(ConfigEntry<float> entry, float fallback)
        { float x = entry == null ? fallback : entry.Value; return float.IsNaN(x) || float.IsInfinity(x) ? fallback : x; }
        private static void SettingsChanged(object sender, EventArgs e)
        { if (!Active) { StormRunner.ClearAll(); NativeStormGuard.Restore(); } }
        private static void Loaded(Scene scene, LoadSceneMode mode) { StormRunner.ClearAll(); NativeStormGuard.ResetScene(); EnsureRunner(); }
        private static void EnsureRunner()
        {
            if (runner != null && runner.activeInHierarchy) return;
            runner = new GameObject("ApocaDustStorm.Runner"); runner.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runner); runner.AddComponent<StormRunner>();
        }
        private void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= Loaded; Enabled.SettingChanged -= SettingsChanged;
            Camera.onPreCull -= StormFog.PreCull; Camera.onPostRender -= StormFog.PostRender;
            StormRunner.ClearAll(); NativeStormGuard.Restore(); if (patches != null) patches.UnpatchSelf();
        }
    }
}
