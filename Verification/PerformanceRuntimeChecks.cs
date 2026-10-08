using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class PerformanceRuntimeChecks
    {
        private static GameObject Player(out PlayMakerFSM health, out PlayMakerFSM cab)
        {
            foreach (GameObject old in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (old.name == "Player") old.SetActive(false);
            GameObject player = new GameObject("Player");
            health = player.Add<PlayMakerFSM>(); health.FsmName = "Health"; health.ActiveStateName = "playerHealth"; health.FsmVariables.Health.Value = 100;
            cab = player.Add<PlayMakerFSM>(); cab.FsmName = "InCar"; cab.ActiveStateName = "OnFoot";
            PlayMakerFSM sleep = player.Add<PlayMakerFSM>(); sleep.FsmName = "Sleep"; sleep.ActiveStateName = "Awake";
            return player;
        }
        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value); }
        private static object PooledSnapshot()
        {
            IEnumerable pool = (IEnumerable)typeof(StormFog).GetField("pool", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (object snapshot in pool) return snapshot;
            return null;
        }
        internal static void Run(Action<bool,string> check)
        {
            Plugin.Active = true; Plugin.ExposureDamage = Plugin.MovementResistance = Plugin.Darkness = Plugin.Headlights = null;
            Apocasetter.GameMenu.InGame = true; Apocasetter.GameMenu.Paused = false; Time.timeScale = 1;
            StormRunner.Model.Reset(); StormRunner.Strength = 0; Physics.RaycastFixture = null;
            PlayerStormHazards.Reset(); StormShelter.Reset(); StormAIMovement.Reset();
            PlayMakerFSM health, cab; GameObject player = Player(out health, out cab);
            int rays = Physics.Raycasts;
            for (int frame = 0; frame < 600; frame++)
            { Time.frameCount++; Time.unscaledTime = frame / 60f; player.transform.position += Vector3.forward; PlayerStormHazards.Tick(player, 1f/60, 0, false); }
            check(Physics.Raycasts == rays, "Ten seconds of moving in calm weather makes zero player shelter raycasts with unworn protection");
            Time.unscaledTime = 100; PlayerStormHazards.Reset();
            rays = Physics.Raycasts; cab.ActiveStateName = "InCar";
            for (int frame = 0; frame < 60; frame++)
            { Time.frameCount++; Time.unscaledTime = 100 + frame / 60f; player.transform.position += Vector3.forward; PlayerStormHazards.Tick(player, 1f/60, 1, false); }
            check(Physics.Raycasts - rays == 20, "Driving at 60 m/s and 60 FPS performs four five-ray shelter checks per second, not sixty");
            rays = Physics.Raycasts; cab.ActiveStateName = "OnFoot"; PlayerStormHazards.Tick(player, 1f/60, 1, false);
            check(Physics.Raycasts - rays == 5 && PlayerStormHazards.State(1) == StormCover.Exposed, "Leaving a cab invalidates cover immediately even inside the rate-limit interval");
            rays = Physics.Raycasts; player.transform.position += Vector3.forward * 200; PlayerStormHazards.Tick(player, 1f/60, 1, false);
            check(Physics.Raycasts - rays == 5, "A real teleport invalidates shelter immediately");
            GameObject house = new GameObject("building_performance"); Collider roof = house.Add<SphereCollider>();
            Physics.RaycastFixture = delegate(Vector3 p, Vector3 d, float distance) { return d.y > .5f ? new[] {new RaycastHit {collider=roof,distance=3}} : new RaycastHit[0]; };
            Time.unscaledTime += 1; float coverBefore = PlayerStormHazards.VehicleProtection;
            PlayerStormHazards.Tick(player, .2f, 0, false);
            check(PlayerStormHazards.Sheltered && PlayerStormHazards.VehicleProtection > coverBefore, "Worn vehicle protection still recovers only in proper shelter after clearing");
            rays = Physics.Raycasts;
            for (int i=0;i<50;i++) PlayerStormHazards.Tick(player,.01f,0,false);
            check(Physics.Raycasts == rays, "Calm recovery reuses shelter between its one-second checks");

            StormShelter.Reset(); Time.unscaledTime = 200; check(StormShelter.ContainsActor(player), "Named building shelter classification warms successfully");
            int names = GameObject.NameReads;
            for (int i=0;i<200;i++) StormShelter.ContainsActor(player);
            check(GameObject.NameReads == names, "Repeated collider hits reuse classification without name/string hierarchy work");
            roof.isTrigger = true; check(!StormShelter.ContainsActor(player), "A collider becoming a trigger is rejected despite cached classification"); roof.isTrigger = false;
            roof.attachedRigidbody = house.Add<Rigidbody>(); check(!StormShelter.ContainsActor(player), "A cached structure becoming a loose physics object cannot shelter the player"); roof.attachedRigidbody = null;
            GameObject tree = new GameObject("tree_performance"); house.transform.parent = tree.transform;
            check(!StormShelter.ContainsActor(player), "Reparenting invalidates cached collider classification immediately");
            house.transform.parent = null; check(StormShelter.ContainsActor(player), "Reparenting back to structural geometry refreshes classification");
            house.name = "wreck_performance"; Time.unscaledTime += 5;
            check(!StormShelter.ContainsActor(player), "Classification expiry detects changed modded hierarchy labels");

            Physics.RaycastFixture = null; StormRunner.View = new GameObject("Performance view").Add<Camera>();
            GameObject[] npcs = new GameObject[60];
            for (int i=0;i<npcs.Length;i++)
            {
                npcs[i] = new GameObject("Performance npc " + i);
                foreach (string marker in new[] {"Health","Detection","Attack"}) npcs[i].Add<PlayMakerFSM>().FsmName = marker;
            }
            StormRunner.Strength = 0; names = GameObject.NameReads; rays = Physics.Raycasts;
            for (int i=0;i<1000;i++) StormAIMovement.Gain(npcs[i%npcs.Length]);
            check(GameObject.NameReads == names && Physics.Raycasts == rays, "A thousand calm NPC velocity updates do no owner-name reads or shelter raycasts");
            StormRunner.Strength = 1; StormAIMovement.Reset(); Time.unscaledTime = 300;
            bool[] checkedActors = new bool[npcs.Length]; bool budget = true;
            for (int frame=0;frame<32;frame++)
            {
                Time.frameCount++; Time.unscaledTime = 300 + frame/60f; rays = Physics.Raycasts;
                for (int i=0;i<npcs.Length;i++) if (StormAIMovement.Gain(npcs[i]) < 1) checkedActors[i] = true;
                if (Physics.Raycasts - rays > 10) budget = false;
            }
            check(budget, "Sixty simultaneous NPCs never exceed two five-ray shelter checks in one frame");
            bool all = true; foreach (bool value in checkedActors) all &= value;
            check(all, "FIFO shelter work eventually checks every NPC without starvation");
            names = GameObject.NameReads;
            for (int i=0;i<1000;i++) StormAIMovement.Gain(npcs[i%npcs.Length]);
            check(GameObject.NameReads == names, "Known active NPCs also reuse eligibility without repeated name reads");

            StormCameraCache.Clear(); GameObject holder = new GameObject("PlayerCameraHolder"); GameObject eye = new GameObject("PlayerCamera");
            eye.transform.parent = holder.transform; Camera first = eye.Add<Camera>(); player.transform.parent = null;
            check(StormCameraCache.Select(player) == first, "Native first-person camera is discovered");
            int finds = GameObject.Finds, transformFinds = Transform.Finds;
            for (int i=0;i<1000;i++) StormCameraCache.Select(player);
            check(GameObject.Finds == finds && Transform.Finds == transformFinds, "A thousand on-foot camera selections do no additional Find calls");
            GameObject car = new GameObject("Performance vehicle"); car.Add<NWH.VehiclePhysics2.VehicleController>();
            GameObject drive = new GameObject("DriveTrigger"); drive.transform.parent = car.transform;
            GameObject thirdOwner = new GameObject("3rdCamera"); thirdOwner.transform.parent = drive.transform; Camera third = thirdOwner.Add<Camera>();
            player.transform.parent = car.transform;
            check(StormCameraCache.Select(player) == third && StormCameraCache.VehicleRoot == car.transform, "Entering a vehicle discovers its chase camera and vehicle root");
            finds = GameObject.Finds; transformFinds = Transform.Finds;
            for (int i=0;i<1000;i++) StormCameraCache.Select(player);
            check(GameObject.Finds == finds && Transform.Finds == transformFinds, "A thousand driving camera selections do no hierarchy searches");
            third.enabled = false; check(StormCameraCache.Select(player) == first, "Switching to first person responds immediately without rediscovery");
            third.enabled = true; check(StormCameraCache.Select(player) == third, "Switching back to chase view responds immediately");
            player.transform.parent = null; check(StormCameraCache.Select(player) == first && StormCameraCache.VehicleRoot == null, "Exiting the vehicle invalidates the vehicle binding");
            player.transform.parent = car.transform; StormCameraCache.Select(player);
            check(StormCameraCache.Select(null) == null && StormCameraCache.VehicleRoot == null, "Losing the player clears cached camera and vehicle bindings");
            check(StormCameraCache.Select(player) == third, "Camera discovery can resume after player loss without a stale binding");
            StormCameraCache.Clear();
            check(StormCameraCache.Select(player) == third, "Scene cleanup permits fresh camera discovery");

            StormDiscovery.Reset(); StormDiscovery.EnsureScene(); int queries = UnityEngine.Object.SceneQueries;
            for (int i=0;i<1000;i++) { Time.frameCount++; Time.unscaledTime += .1f; StormDiscovery.EnsureScene(); }
            check(UnityEngine.Object.SceneQueries == queries, "A hundred seconds of calm discovery ticks performs no repeated scene searches");
            StormDiscovery.StormStarted(); check(UnityEngine.Object.SceneQueries > queries, "Storm start explicitly refreshes late-created weather assets");
            queries = UnityEngine.Object.SceneQueries; StormDiscovery.BindVehicle(car.transform); StormDiscovery.BindVehicle(car.transform);
            check(UnityEngine.Object.SceneQueries == queries, "Vehicle entry refreshes local headlights without scene-wide discovery");
            PlayMakerFSM disable = new GameObject("__GameManager__").Add<PlayMakerFSM>(); disable.FsmName = "DisableSandstorm";
            NativeStormGuard.Observe(disable); check(NativeStormGuard.StormsDisabled(), "The enable hook discovers a late-created native storm switch");

            StormFog.Clear(); StormLighting.Clear(); AzureAtmosphere.Clear(); StormRunner.View = first; StormRunner.Strength = 1;
            StormLighting.Scan(); AzureAtmosphere.Scan(); RenderSettings.fogColor = new Color(.4f,.3f,.2f,1); RenderSettings.fogDensity = .001f;
            check(StormFog.Push(first), "Fog snapshot pool warms on its first real pass"); StormFog.FinishPass(true);
            object snapshot = PooledSnapshot(), lighting = Field(snapshot,"Lighting"), azure = Field(snapshot,"Azure");
            object intensityBuffer = Field(lighting,"intensities"), azureFloats = Field(azure,"floats"), materialList = Field(azure,"materials");
            bool restored = true, reused = true;
            for (int i=0;i<100;i++)
            {
                RenderSettings.fogDensity = .001f + i*.00001f; float native = RenderSettings.fogDensity;
                StormFog.Push(first); StormFog.FinishPass(true);
                restored &= RenderSettings.fogDensity == native && !StormFog.HasOverrides;
                reused &= System.Object.ReferenceEquals(snapshot,PooledSnapshot());
            }
            check(restored, "Reused fog scopes recapture and restore changing native values on every pass");
            check(reused && System.Object.ReferenceEquals(intensityBuffer,Field(lighting,"intensities")) && System.Object.ReferenceEquals(azureFloats,Field(azure,"floats")) && System.Object.ReferenceEquals(materialList,Field(azure,"materials")), "Fog, lighting and Azure snapshots/buffers are reused across a hundred draws");
            StormFog.Push(first); StormFog.Push(first); StormFog.FinishPass(true); StormFog.FinishPass(true);
            check(!StormFog.HasOverrides && System.Object.ReferenceEquals(snapshot,PooledSnapshot()), "Nested image passes retain outer ownership and return to the snapshot pool");
            Plugin.Darkness = Plugin.Headlights = 0f; RenderSettings.ambientIntensity = .73f;
            StormFog.Push(first); StormFog.FinishPass(true);
            check(RenderSettings.ambientIntensity == .73f, "A reused snapshot with lighting disabled cannot restore stale lighting from an earlier draw");
            Plugin.Darkness = Plugin.Headlights = null;

            ExposureHud.Clear(); PlayerStormHazards.Reset(); cab.ActiveStateName = "OnFoot"; PlayerStormHazards.Tick(player,.1f,1,false);
            foreach (GameObject old in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (old.name == "Compass") old.SetActive(false);
            GameObject canvasOwner = new GameObject("Performance canvas"); canvasOwner.Add<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject compassOwner = new GameObject("Compass"); RectTransform compass = compassOwner.Add<RectTransform>(); compassOwner.transform = compass; compass.parent = canvasOwner.transform;
            ExposureHud.Tick(.1f,1); int parentLookups = Component.ParentLookups;
            for (int i=0;i<100;i++) ExposureHud.Draw();
            check(Component.ParentLookups == parentLookups, "A hundred HUD repaint events perform no Canvas parent lookups");
            GameObject otherCanvas = new GameObject("Other canvas"); otherCanvas.Add<Canvas>(); compass.parent = otherCanvas.transform; ExposureHud.Tick(.1f,1);
            check(Component.ParentLookups == parentLookups+1, "Moving the native compass to another Canvas invalidates the cached anchor");
            ExposureHud.Clear(); StormFog.Clear(); StormDiscovery.Reset(); StormCameraCache.Clear(); StormLighting.Clear(); AzureAtmosphere.Clear();
            StormShelter.Reset(); StormAIMovement.Reset(); PlayerStormHazards.Reset(); NativeStormGuard.ResetScene();
            Physics.RaycastFixture = null; StormRunner.Strength = 0; StormRunner.View = null; Camera.main = null;
        }
    }
}
