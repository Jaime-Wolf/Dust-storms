using UnityEngine;
namespace ApocaDustStorm
{
    // Cosmetic wind motion only. No collider queries or forces on game objects.
    internal static class DebrisMotion
    {
        internal const float MaxSpeed = 22;
        internal static void Step(ref Vector3 position, ref Vector3 velocity, float dt, Vector3 wind, float gust)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0 || dt > 0.1f) return;
            Vector3 target = wind * (7 + gust * 8);
            float blend = 1 - Mathf.Exp(-dt * 0.35f);
            velocity.x = Mathf.Lerp(velocity.x, target.x, blend); velocity.z = Mathf.Lerp(velocity.z, target.z, blend);
            velocity.y -= 9.81f * dt; velocity = Vector3.ClampMagnitude(velocity, MaxSpeed);
            position += velocity * dt;
        }
    }
}
