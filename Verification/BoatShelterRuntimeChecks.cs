using System;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class BoatShelterRuntimeChecks
    {
        private static GameObject Hierarchy(string path)
        {
            GameObject result = null;
            foreach (string name in path.Split('/'))
            {
                GameObject child = new GameObject(name);
                if (result != null) child.transform.parent = result.transform;
                result = child;
            }
            return result;
        }
        private static void Roof(Collider collider)
        {
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance)
            { return direction.y > .5f ? new[] { new RaycastHit { collider = collider, distance = 3 } } : new RaycastHit[0]; };
        }
        internal static void Run(Action<bool, string> check)
        {
            StormShelter.Reset(); PlayerStormHazards.Reset();
            Time.unscaledTime = 2000; Time.timeScale = 1; Plugin.Active = true;
            Plugin.ExposureDamage = null; Plugin.MovementResistance = null;
            Apocasetter.GameMenu.Paused = false;
            GameObject player = new GameObject("Player");
            PlayMakerFSM health = player.Add<PlayMakerFSM>(); health.FsmName = "Health"; health.ActiveStateName = "playerHealth";
            PlayMakerFSM cab = player.Add<PlayMakerFSM>(); cab.FsmName = "InCar"; cab.ActiveStateName = "OnFoot";
            foreach (string path in BoatShelterFixtures.Paths)
            {
                StormShelter.Reset(); Collider fixture = Hierarchy(path).Add<SphereCollider>(); Roof(fixture);
                check(StormShelter.ContainsActor(player), "Stock boat hull provides overhead shelter: " + path);
            }
            StormShelter.Reset();
            Collider hull = Hierarchy("Wreck_5(Clone)/shipwreck_2/collider").Add<SphereCollider>(); Roof(hull);
            check(StormShelter.ContainsActor(player), "Spawned boat POI clone provides full shelter");
            int reads = GameObject.NameReads, finds = Transform.Finds;
            for (int i = 0; i < 200; i++) StormShelter.ContainsActor(player);
            check(GameObject.NameReads == reads && Transform.Finds == finds, "Boat hull classification reuses the existing collider cache");
            PlayerStormHazards.Tick(player, .1f, 1, false);
            check(health.FsmVariables.Health.Value == 100 && PlayerStormHazards.State(1) == StormCover.Shelter && PlayerStormHazards.MovementGain(1, 1) == 1,
                "Boat shelter prevents player damage, reports shelter and removes movement resistance");
            cab.ActiveStateName = "InCar"; PlayerStormHazards.Tick(player, .1f, 1, false);
            check(health.FsmVariables.Health.Value == 100 && PlayerStormHazards.State(1) == StormCover.Shelter,
                "A cab under boat cover receives structural shelter");
            cab.ActiveStateName = "OnFoot"; Physics.RaycastFixture = null; Time.unscaledTime += .25f;
            PlayerStormHazards.Tick(player, .1f, 1, false);
            check(PlayerStormHazards.State(1) == StormCover.Exposed && health.FsmVariables.Health.Value < 100,
                "Leaving boat cover resumes exposure without a boat-wide safe zone");
            check(!StormShelter.ContainsActor(player), "Open deck without geometry overhead is exposed");
            Physics.RaycastFixture = delegate(Vector3 origin, Vector3 direction, float distance)
            { return direction.x > .5f ? new[] { new RaycastHit { collider = hull, distance = 3 } } : new RaycastHit[0]; };
            check(!StormShelter.ContainsActor(player), "Standing beside a single hull wall is exposed");

            foreach (string path in new[] {
                "Wreck_2/decor/car wreck 3_trunk/collider", "Wreck_5/decor/shelf_2/collider",
                "Wreck_2/camp_base_rock", "Wreck_6/decor/car_wreck_1_roof/collider",
                "shipwreck_2/car_wreck_roof/collider", "shipwreck_2/tree_big/collider", "shipwreck_2/grass/collider" })
            {
                StormShelter.Reset(); Roof(Hierarchy(path).Add<SphereCollider>());
                check(!StormShelter.ContainsActor(player), "Boat exception does not promote unrelated props: " + path);
            }
            StormShelter.Reset(); Roof(hull);
            hull.isTrigger = true; check(!StormShelter.ContainsActor(player), "Boat trigger volumes cannot shelter actors"); hull.isTrigger = false;
            hull.enabled = false; check(!StormShelter.ContainsActor(player), "Disabled boat collision cannot shelter actors"); hull.enabled = true;
            hull.attachedRigidbody = hull.gameObject.Add<Rigidbody>();
            check(!StormShelter.ContainsActor(player), "Dynamic boat parts cannot shelter actors"); hull.attachedRigidbody = null;
            check(!StormShelter.ContainsActor(hull.gameObject), "Boat hull does not shelter its own actor hierarchy");
            StormShelter.Reset(); Collider vehicle = Hierarchy("Driveable boat/shipwreck_2/collider").Add<SphereCollider>();
            vehicle.transform.parent.parent.gameObject.Add<NWH.VehiclePhysics2.VehicleController>(); Roof(vehicle);
            check(!StormShelter.ContainsActor(player), "Driveable vehicle using a boat mesh is excluded from full shelter");
            StormShelter.Reset(); GameObject driveHull = Hierarchy("boat/shipwreck_2");
            new GameObject("DriveTrigger").transform.parent = driveHull.transform.parent;
            Roof(driveHull.Add<SphereCollider>());
            check(!StormShelter.ContainsActor(player), "DriveTrigger exclusion survives boat recognition");
            StormShelter.Reset(); Roof(Hierarchy("Wreck_3/SHIPWRECK_1/collider").Add<SphereCollider>());
            check(StormShelter.ContainsActor(player), "Boat naming remains case insensitive");
            Physics.RaycastFixture = null; PlayerStormHazards.Reset(); StormShelter.Reset();
        }
    }
}
