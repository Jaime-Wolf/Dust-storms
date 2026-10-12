// Controlled API fixtures. These do not simulate Unity's GPU or vegetation shaders.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class ScriptableObject : Object
    { public HideFlags hideFlags; public static T CreateInstance<T>() where T : ScriptableObject,new() { return new T(); } }
    public struct LayerMask { public int value; }
    public struct Keyframe
    {
        public float time,value,inTangent,outTangent;
        public Keyframe(float t,float v,float i,float o) { time=t;value=v;inTangent=i;outTangent=o; }
    }
    public class AnimationCurve
    {
        private readonly Keyframe[] keys;
        public AnimationCurve(params Keyframe[] values) { keys=values; }
        public static AnimationCurve Linear(float a,float b,float c,float d){float slope=(d-b)/(c-a);return new AnimationCurve(new Keyframe(a,b,slope,slope),new Keyframe(c,d,slope,slope));}
        public float Evaluate(float t)
        {
            if(t<=keys[0].time)return keys[0].value;
            for(int i=1;i<keys.Length;i++)if(t<=keys[i].time)
            {
                Keyframe a=keys[i-1],b=keys[i];float width=b.time-a.time,x=(t-a.time)/width,x2=x*x,x3=x2*x;
                return (2*x3-3*x2+1)*a.value+(x3-2*x2+x)*width*a.outTangent+(-2*x3+3*x2)*b.value+(x3-x2)*width*b.inTangent;
            }
            return keys[keys.Length-1].value;
        }
    }
    public enum WindZoneMode { Directional }
    public class WindZone : Component { public WindZoneMode mode;public float windMain,windTurbulence,windPulseMagnitude,windPulseFrequency; }
    public enum ParticleSystemSimulationSpace { Local,World }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public enum ParticleSystemCullingMode { AlwaysSimulate }
    public enum ParticleSystemRenderMode { Stretch,Billboard }
    public enum ParticleSystemSortMode { Distance }
    public enum ParticleSystemAnimationMode { Grid }
    public enum ParticleSystemAnimationType { WholeSheet }
    public enum ParticleSystemVertexStream { Position,Normal,Color,UV,UV2,AnimBlend }
    public struct GradientColorKey { public GradientColorKey(Color c,float t){} }
    public struct GradientAlphaKey { public GradientAlphaKey(float a,float t){} }
    public class Gradient { public void SetKeys(GradientColorKey[] c,GradientAlphaKey[] a){} }
    public class ParticleSystem : Component
    {
        public class MainModule { public ParticleSystemSimulationSpace simulationSpace;public int maxParticles;public float startSpeed;public bool loop,playOnAwake;public ParticleSystemCullingMode cullingMode; }
        public class EmissionModule { public bool enabled; }
        public class ShapeModule { public bool enabled; }
        public class CollisionModule { public bool enabled; }
        public class ColorOverLifetimeModule { public bool enabled;public MinMaxGradient color; }
        public class TextureSheetAnimationModule {public bool enabled;public ParticleSystemAnimationMode mode;public ParticleSystemAnimationType animation;public int numTilesX,numTilesY,cycleCount;public MinMaxCurve frameOverTime;}
        public struct MinMaxCurve {public float multiplier;public AnimationCurve curve;public MinMaxCurve(float value,AnimationCurve source){multiplier=value;curve=source;}}
        public struct Particle {public Vector3 position,velocity;public float startSize,rotation,startLifetime,remainingLifetime;public uint randomSeed;public Color startColor;}
        public struct MinMaxGradient { public MinMaxGradient(Gradient gradient){} }
        public struct EmitParams { public Vector3 position,velocity;public float startSize,startLifetime,rotation;public Color startColor; }
        public readonly MainModule main=new MainModule();public readonly EmissionModule emission=new EmissionModule();
        public readonly ShapeModule shape=new ShapeModule();public readonly CollisionModule collision=new CollisionModule();public readonly ColorOverLifetimeModule colorOverLifetime=new ColorOverLifetimeModule();
        public readonly TextureSheetAnimationModule textureSheetAnimation=new TextureSheetAnimationModule();
        public Particle[] Particles;public int ParticleCount,UploadCalls;public bool Paused;
        public void SetParticles(Particle[] source,int count){UploadCalls++;if(Particles==null||Particles.Length!=source.Length)Particles=new Particle[source.Length];Array.Copy(source,Particles,count);ParticleCount=count;}
        public readonly List<EmitParams> Live=new List<EmitParams>();public int particleCount { get { return Live.Count+ParticleCount; } }
        public void Stop(bool children,ParticleSystemStopBehavior behavior){Clear();}public void Play(){Paused=false;}public void Pause(){Paused=true;}public void Clear(){Live.Clear();ParticleCount=0;}
        public void Emit(EmitParams particle,int count){for(int i=0;i<count&&Live.Count<main.maxParticles;i++)Live.Add(particle);}
    }
}
namespace UnityEngine.Rendering.PostProcessing
{
    using UnityEngine;
    public class Parameter<T> { public T value;public bool overrideState;public void Override(T v){value=v;overrideState=true;} }
    public class PostProcessEffectSettings : ScriptableObject { public Parameter<bool> enabled=new Parameter<bool>(); }
    public class ColorGrading : PostProcessEffectSettings
    {
        public Parameter<float> temperature=new Parameter<float>(),saturation=new Parameter<float>(),contrast=new Parameter<float>();
        public Parameter<Color> colorFilter=new Parameter<Color>();public Parameter<Vector4> gamma=new Parameter<Vector4>(),gain=new Parameter<Vector4>();
    }
    public class PostProcessProfile : ScriptableObject
    {
        public readonly List<PostProcessEffectSettings> settings=new List<PostProcessEffectSettings>();
        public T AddSettings<T>() where T:PostProcessEffectSettings,new(){T setting=new T();settings.Add(setting);return setting;}
    }
    public class PostProcessVolume : Behaviour { public PostProcessProfile sharedProfile;public bool isGlobal;public float weight,priority; }
    public class PostProcessLayer : Behaviour
    {
        public LayerMask volumeLayer;public float Observed;public bool ThrowOnce;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal void UpdateVolumeSystem(Camera cam,object cmd)
        {
            Observed=0;
            foreach(UnityEngine.Object obj in UnityEngine.Object.All){PostProcessVolume v=obj as PostProcessVolume;if(v!=null&&v.gameObject.name=="ApocaDustStorm.Brownout")Observed+=v.weight;}
            if(ThrowOnce){ThrowOnce=false;throw new Exception("Controlled volume blending failure");}
        }
    }
}
