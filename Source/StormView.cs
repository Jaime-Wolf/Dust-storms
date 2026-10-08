using UnityEngine;
using UnityEngine.Rendering;
namespace ApocaDustStorm
{
    // Capture the geometry camera once before drawing. Image effects can run after
    // a chase camera restores its matrices; their rays must still match that draw.
    internal static class StormView
    {
        private static Camera captured;
        private static Matrix4x4 toWorld;
        private static Matrix4x4 fromScreen;
        private static int frame=-1;
        internal static void Capture(Camera camera)
        {
            if (!StormRunner.CanRender || camera!=StormRunner.View || StormRunner.Strength<0.001f) return;
            DustLightning.AlignView(camera);
            captured=camera;toWorld=camera.worldToCameraMatrix.inverse;fromScreen=Projection(camera);frame=Time.frameCount;
        }
        internal static Vector3 Eye(Camera camera)
        {return captured==camera&&frame==Time.frameCount?toWorld.MultiplyPoint3x4(Vector3.zero):camera.transform.position;}
        private static Matrix4x4 Projection(Camera camera)
        {
            Matrix4x4 inverse=GL.GetGPUProjectionMatrix(camera.projectionMatrix,true).inverse;
            GraphicsDeviceType type=SystemInfo.graphicsDeviceType;
            if(type!=GraphicsDeviceType.OpenGLCore&&type!=GraphicsDeviceType.OpenGLES3&&type!=GraphicsDeviceType.OpenGLES2)
                inverse[1,1]*=-1;
            return inverse;
        }
        internal static Matrix4x4 World(Camera camera)
        {return captured==camera&&frame==Time.frameCount?toWorld:camera.worldToCameraMatrix.inverse;}
        internal static Matrix4x4 Screen(Camera camera)
        {return captured==camera&&frame==Time.frameCount?fromScreen:Projection(camera);}
        internal static Vector3 FogVector(Transform native,Vector3 corner)
        {
            if (Plugin.Active && StormRunner.CanRender && StormRunner.Strength>=0.001f && captured!=null &&
                captured==StormRunner.View && native==captured.transform && frame==Time.frameCount)
                return toWorld.MultiplyVector(new Vector3(corner.x,corner.y,-corner.z));
            return native.TransformVector(corner);
        }
        internal static void Clear(){captured=null;frame=-1;}
    }
}
