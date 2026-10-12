using UnityEngine;
namespace ApocaDustStorm
{
    internal static class StormEntryShake
    {
        private static readonly EntryWindShakeModel model = new EntryWindShakeModel();
        private static Camera selected, rendered;
        private static Matrix4x4 saved, applied;
        private static bool automatic;
        private static bool Allowed
        {
            get
            {
                return StormRunner.CanRender && StormRunner.Model.Active && Apocasetter.GameMenu.InGame &&
                    !Apocasetter.GameMenu.Paused && Time.timeScale > 0 && Application.isFocused &&
                    !Apocasetter.InputBlocker.Active && PlayerStormHazards.Ready &&
                    !PlayerStormHazards.Sleeping && !PlayerStormHazards.Sheltered &&
                    Plugin.Value(Plugin.EntryShake, 1) > 0;
            }
        }
        internal static void Tick(float dt, bool entered, bool skipped)
        {
            Restore();
            if (skipped || !Allowed) { Clear(); return; }
            if (selected != StormRunner.View) { model.Reset(); selected = StormRunner.View; }
            model.Step(dt, entered);
        }
        private static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                if (a[row, column] != b[row, column]) return false;
            return true;
        }
        internal static void PreCull(Camera camera)
        {
            if (camera == null || camera != StormRunner.View) return;
            if (!Allowed) { Clear(); return; }
            if (selected != camera || (model.X == 0 && model.Y == 0)) return;
            // Repeated callbacks cannot compound the offset. Shift the lens only:
            // no camera/player transform, look input, roll or chase orbit changes.
            if (rendered != null) return;
            float gain = Mathf.Clamp(Plugin.Value(Plugin.EntryShake, 1), 0, 2) * (PlayerStormHazards.InVehicle ? .65f : 1);
            saved = camera.projectionMatrix;
            camera.ResetProjectionMatrix();
            automatic = Same(saved, camera.projectionMatrix);
            applied = saved;
            applied.m02 += (float)model.X * .004f * gain;
            applied.m12 += (float)model.Y * .003f * gain;
            camera.projectionMatrix = applied;
            rendered = camera;
        }
        internal static void PostRender(Camera camera) { if (camera == rendered) Restore(); }
        internal static void Restore()
        {
            Camera camera = rendered; rendered = null;
            // Never overwrite a newer projection supplied by another camera mod.
            if (camera == null || !Same(camera.projectionMatrix, applied)) return;
            if (automatic) camera.ResetProjectionMatrix(); else camera.projectionMatrix = saved;
        }
        internal static void Clear() { Restore(); selected = null; model.Reset(); }
    }
}
