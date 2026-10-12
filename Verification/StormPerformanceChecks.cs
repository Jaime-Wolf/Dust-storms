using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class StormPerformanceChecks
    {
        internal static void AudioWarmup(Action<bool,string> check)
        {
            DischargeSamples.Start(); DischargeSamples.Start();
            float[][] samples; Stopwatch clock = Stopwatch.StartNew();
            while (!DischargeSamples.TryGet(out samples) && clock.ElapsedMilliseconds < 10000) Thread.Sleep(1);
            check(samples != null && samples.Length == 3, "Background lightning sample preparation completes without a main-thread wait in production");
            float[][] again; DischargeSamples.TryGet(out again);
            check(System.Object.ReferenceEquals(samples, again), "Repeated requests reuse one bounded sample cache");
            for (int i = 0; i < 3; i++)
            {
                float[] original = DischargeSound.Generate(3118 + i * 97); bool same = original.Length == samples[i].Length;
                for (int n = 0; same && n < original.Length; n++) same = original[n] == samples[i][n];
                check(same, "Prepared lightning clip " + i + " exactly preserves the accepted sample data");
            }
        }
        internal static void Run(Action<bool,string> check)
        {
            DeferredAudio(check);
            check(DustEmissionBudget.Add(.8f,180,.1f,800,800,24) == 0, "Full dust pools request zero pickup queries and discard saved budget");
            check(DustEmissionBudget.Add(0,180,.1f,798,800,24) == 2, "Dust requests are limited to available particle slots");
            check(Math.Abs(DustEmissionBudget.Add(.3f,92,.1f,0,800,24) - 9.5f) < .0001, "Unsaturated dust retains its previous emission rate and fractional carry");
            check(DustEmissionBudget.Add(0,500,.3f,0,800,24) == 24, "Emission remains bounded after a slow frame");
            check(DustEmissionBudget.Add(0,180,.1f,0,800,24) == 18, "Freed capacity resumes ordinary emission without accumulated overflow");
            DamageGraceModel grace = new DamageGraceModel();
            check(grace.DamageSeconds(.1,true,false,true,0) == .1, "Zero grace preserves immediate damage");
            grace.Reset(); double total = 0;
            for (int i = 0; i < 75; i++) total += grace.DamageSeconds(.2,true,false,true,10);
            check(Math.Abs(total - 5) < 1e-8, "Ten-second grace charges exactly five seconds of a fifteen-second exposure");
            grace.Reset(); grace.DamageSeconds(.15,true,false,true,.2);
            check(Math.Abs(grace.DamageSeconds(.1,true,false,true,.2) - .05) < 1e-8, "Grace expiry charges only the exposed part of its crossing frame");
            grace.Reset(); grace.DamageSeconds(.2,true,false,true,1);
            grace.DamageSeconds(100,false,false,true,1);
            grace.DamageSeconds(Double.NaN,true,false,true,1);
            check(grace.DamageSeconds(.2,true,false,true,1) == 0, "Nonhazardous intervals and invalid time cannot exhaust grace");
            grace.DamageSeconds(.2,true,true,true,1);
            check(grace.DamageSeconds(.2,true,false,true,1) == 0, "Proper shelter resets grace");
            grace.Reset(); for (int i = 0; i < 6; i++) grace.DamageSeconds(.2,true,false,true,1);
            grace.DamageSeconds(.1,false,false,false,1);
            check(grace.DamageSeconds(.2,true,false,true,1) == 0, "A new storm receives a fresh grace period");
            grace.Reset(); check(grace.DamageSeconds(.1,true,false,true,Double.NaN) == .1, "Invalid grace falls back to immediate damage safely");
            grace.Reset(); for (int i = 0; i < 1500; i++) grace.DamageSeconds(.2,true,false,true,300);
            check(Math.Abs(grace.DamageSeconds(.2,true,false,true,300) - .2) < 1e-8, "Maximum grace expires and never makes the player permanently immune");
            GraceRuntime(check);
        }
        private static void DeferredAudio(Action<bool,string> check)
        {
            System.Reflection.FieldInfo field = typeof(DischargeSamples).GetField("job",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            object cachedJob = field.GetValue(null); float[][] samples; DischargeSamples.TryGet(out samples);
            System.Threading.Tasks.TaskCompletionSource<float[][]> gate = new System.Threading.Tasks.TaskCompletionSource<float[][]>();
            Camera view = new GameObject("Audio preparation test view").Add<Camera>();
            Plugin.Active = true; Plugin.Lightning = true; Plugin.DischargeVolume = null;
            StormRunner.View = view; StormRunner.Strength = 1; DustLightning.Clear();
            field.SetValue(null,gate.Task);
            try
            {
                DustLightning.Tick(view,.016f,1,true,true,false); DustLightning.PreCull(view);
                AudioSource source = null;
                foreach (AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    if (item.name == "ApocaDustStorm.StaticDischarge" && item.gameObject.activeInHierarchy) source = item;
                for (int i = 0; i < 10; i++) DustLightning.Tick(view,.016f,1,true,false,false);
                check(source != null && source.Shots == 0 && !gate.Task.IsCompleted, "An immediate test strike cannot block on unfinished sample generation or play a null clip");
                gate.SetResult(samples);
                DustLightning.Tick(view,.016f,1,true,false,false);
                check(source.Shots == 0, "The first ready audio tick uploads only one clip");
                DustLightning.Tick(view,.016f,1,true,false,false);
                check(source.Shots == 0, "The second ready audio tick still waits for the complete clip set");
                DustLightning.Tick(view,.016f,1,true,false,false);
                check(source.Shots == 1, "The pending strike plays exactly once after all three prepared clips are uploaded");
                DustLightning.Tick(view,.016f,1,true,false,false); check(source.Shots == 1, "Deferred sound is not replayed on later ticks");
                DustLightning.Clear(); gate = new System.Threading.Tasks.TaskCompletionSource<float[][]>(); field.SetValue(null,gate.Task);
                DustLightning.Tick(view,.016f,1,true,true,false); DustLightning.PreCull(view);
                for (int i = 0; i < 10; i++) DustLightning.Tick(view,.016f,1,true,false,false);
                DustLightning.Suspend(); gate.SetResult(samples);
                for (int i = 0; i < 4; i++) DustLightning.Tick(view,.016f,1,true,false,false);
                int shots = 0; foreach (AudioSource item in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    if (item.name == "ApocaDustStorm.StaticDischarge" && item.gameObject.activeInHierarchy) shots += item.Shots;
                check(shots == 0, "Pause suspension cancels an unfinished strike instead of playing stale thunder when preparation finishes");
            }
            finally { DustLightning.Clear(); field.SetValue(null,cachedJob); StormRunner.View = null; StormRunner.Strength = 0; }
        }
        private static void Step(GameObject player,float dt,float strength,bool skipped)
        { Time.unscaledTime += dt; Time.frameCount++; PlayerStormHazards.Tick(player,dt,strength,skipped); }
        private static void GraceRuntime(Action<bool,string> check)
        {
            Plugin.Active = true; Plugin.DamageGrace = 1f; Plugin.ExposureDamage = 1f;
            Apocasetter.GameMenu.Paused = false; Time.timeScale = 1; Time.unscaledTime = 2000;
            Physics.RaycastFixture = null; StormShelter.Reset(); PlayerStormHazards.Reset();
            GameObject player = new GameObject("Player");
            PlayMakerFSM health = player.Add<PlayMakerFSM>(); health.FsmName = "Health"; health.ActiveStateName = "playerHealth"; health.FsmVariables.Health.Value = 100;
            PlayMakerFSM cab = player.Add<PlayMakerFSM>(); cab.FsmName = "InCar"; cab.ActiveStateName = "OnFoot";
            PlayMakerFSM sleep = player.Add<PlayMakerFSM>(); sleep.FsmName = "Sleep"; sleep.ActiveStateName = "Awake";
            for (int i = 0; i < 4; i++) Step(player,.2f,1,false);
            check(health.FsmVariables.Health.Value == 100 && PlayerStormHazards.State(1) == StormCover.Exposed, "Grace delays health damage without hiding the exposed state");
            Apocasetter.GameMenu.Paused = true; Step(player,.2f,1,false); Apocasetter.GameMenu.Paused = false;
            Step(player,.2f,1,true);
            cab.ActiveStateName = "InCar"; Step(player,.1f,1,false);
            check(health.FsmVariables.Health.Value == 100 && PlayerStormHazards.VehicleProtection < .9f, "Cab protection still wears during grace while pause and clock skips preserve grace");
            cab.ActiveStateName = "OnFoot"; Step(player,.2f,1,false);
            check(Math.Abs(health.FsmVariables.Health.Value - 99.97f) < .0001, "Cab entry and exit cannot restart the exposure grace timer");
            GameObject house = new GameObject("building_grace_test"); Collider roof = house.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 position,Vector3 direction,float distance)
            { return direction.y > .5f ? new[] { new RaycastHit { collider = roof, distance = 3 } } : new RaycastHit[0]; };
            Step(player,.3f,1,false); check(PlayerStormHazards.Sheltered, "Grace reset uses recognized structural shelter");
            float before = health.FsmVariables.Health.Value; Physics.RaycastFixture = null;
            Step(player,.3f,1,false); Step(player,.2f,1,false);
            check(health.FsmVariables.Health.Value == before, "Leaving proper shelter grants fresh grace without resetting worn cab protection");
            Plugin.DamageGrace = 0f; Step(player,.1f,1,false);
            check(health.FsmVariables.Health.Value < before, "Changing grace to zero takes effect immediately through the live setting");
            Plugin.DamageGrace = null; Plugin.ExposureDamage = null; Physics.RaycastFixture = null;
            PlayerStormHazards.Reset(); StormShelter.Reset();
        }
    }
}
