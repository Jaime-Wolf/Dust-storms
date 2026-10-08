using UnityEngine;
namespace ApocaDustStorm
{
    // Cache discovery, but inspect enabled state every frame for instant native view changes.
    internal static class StormCameraCache
    {
        private static GameObject owner;
        private static Transform parent;
        private static Camera first, third, fallback;
        private static float nextFirst, nextThird, nextFallback;
        internal static Transform VehicleRoot;
        internal static void Clear()
        { owner = null; parent = VehicleRoot = null; first = third = fallback = null; nextFirst = nextThird = nextFallback = 0; }
        internal static Camera Select(GameObject player)
        {
            if (player == null) { Clear(); return null; }
            Transform currentParent = player.transform.parent;
            if (owner != player) { Clear(); owner = player; }
            if (parent != currentParent)
            { parent = currentParent; third = null; VehicleRoot = null; nextThird = 0; }
            if (parent != null && third == null && Time.unscaledTime >= nextThird)
            {
                nextThird = Time.unscaledTime + 1;
                for (Transform t = parent; t != null; t = t.parent)
                {
                    Transform drive = t.Find("DriveTrigger");
                    if (drive == null) continue;
                    VehicleRoot = t;
                    Transform camera = drive.Find("3rdCamera");
                    if (camera != null) third = camera.GetComponentInChildren<Camera>(true);
                    break;
                }
            }
            if (third != null && third.isActiveAndEnabled) return third;
            if (first == null && Time.unscaledTime >= nextFirst)
            {
                nextFirst = Time.unscaledTime + 1;
                GameObject holder = GameObject.Find("PlayerCameraHolder");
                Transform eye = holder == null ? null : holder.transform.Find("PlayerCamera");
                if (eye != null) first = eye.GetComponent<Camera>();
            }
            if (first != null && first.isActiveAndEnabled) return first;
            if (fallback == null || !fallback.isActiveAndEnabled)
            {
                if (Time.unscaledTime >= nextFallback) { nextFallback = Time.unscaledTime + 1; fallback = Camera.main; }
            }
            return fallback != null && fallback.isActiveAndEnabled ? fallback : null;
        }
    }
}
