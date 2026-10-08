using UnityEngine;
namespace ApocaDustStorm
{
    internal static class StormDiscovery
    {
        private static bool scanned;
        private static int sceneFrame = -1;
        private static Transform vehicle;
        internal static void EnsureScene()
        {
            if (scanned) return;
            NativeStormGuard.Scan(); StormLighting.Scan(); AzureAtmosphere.Scan(); NativeStormClock.Scan();
            scanned = true; sceneFrame = Time.frameCount;
        }
        internal static void StormStarted()
        {
            EnsureScene();
            // Pick up late-created weather assets without scanning throughout calm weather.
            if (sceneFrame != Time.frameCount) { StormLighting.Scan(); AzureAtmosphere.Scan(); NativeStormClock.Scan(); }
        }
        internal static void BindVehicle(Transform current)
        {
            if (vehicle == current) return;
            vehicle = current;
            if (vehicle != null) StormLighting.RefreshHeadlights(vehicle);
        }
        internal static void Reset() { scanned = false; sceneFrame = -1; vehicle = null; }
    }
}
