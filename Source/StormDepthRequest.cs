using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace ApocaDustStorm
{
    // Cooperate between the two storm-owned effects; never clear native depth.
    internal static class StormDepthRequest
    {
        private sealed class Lease
        { internal DepthTextureMode Original; internal readonly HashSet<Behaviour> Users = new HashSet<Behaviour>(); }
        private static readonly Dictionary<Camera, Lease> leases = new Dictionary<Camera, Lease>();
        private static readonly HashSet<Behaviour> owned = new HashSet<Behaviour>();
        private static readonly Dictionary<Type, bool> imageEffects = new Dictionary<Type, bool>();
        internal static void Register(Behaviour effect) { owned.Add(effect); }
        internal static void Acquire(Camera camera, Behaviour effect)
        {
            if (camera == null || effect == null) return;
            Lease lease;
            if (!leases.TryGetValue(camera, out lease))
            { lease = new Lease { Original = camera.depthTextureMode }; leases.Add(camera, lease); }
            lease.Users.Add(effect); camera.depthTextureMode |= DepthTextureMode.Depth;
        }
        internal static void Release(Camera camera, Behaviour effect)
        {
            if (ReferenceEquals(camera, null)) return;
            Lease lease;
            if (!leases.TryGetValue(camera, out lease) || !lease.Users.Remove(effect) || lease.Users.Count != 0) return;
            leases.Remove(camera);
            if (camera == null || (lease.Original & DepthTextureMode.Depth) != 0) return;
            // A new feature or external image effect makes ownership ambiguous.
            // Keep depth in that case instead of breaking another renderer.
            if ((camera.depthTextureMode & ~DepthTextureMode.Depth) != (lease.Original & ~DepthTextureMode.Depth)) return;
            foreach (MonoBehaviour component in camera.gameObject.GetComponents<MonoBehaviour>())
            {
                if (component == null || !component.isActiveAndEnabled || owned.Contains(component)) continue;
                Type type = component.GetType(); bool image;
                if (!imageEffects.TryGetValue(type, out image))
                {
                    image = type.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new Type[] { typeof(RenderTexture), typeof(RenderTexture) }, null) != null;
                    imageEffects.Add(type, image);
                }
                if (image) return;
            }
            camera.depthTextureMode &= ~DepthTextureMode.Depth;
        }
        internal static void Forget(Behaviour effect) { owned.Remove(effect); }
    }
}
