// Small simulation harness, not a replacement for Unity or an in-game test.
using System;
using System.Collections.Generic;
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static Type TypeByName(string name){return Type.GetType(name);}
        public static System.Reflection.MethodInfo Method(Type type,string name,Type[] args){return type.GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,args,null);}
        public static System.Reflection.MethodInfo Method(Type type,string name){return type.GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);}
    }
    public class CodeInstruction { public System.Reflection.Emit.OpCode opcode;public object operand;public List<System.Reflection.Emit.Label> labels=new List<System.Reflection.Emit.Label>();public List<object> blocks=new List<object>();public CodeInstruction(System.Reflection.Emit.OpCode code,object value=null){opcode=code;operand=value;}public bool Calls(System.Reflection.MethodInfo method){return Equals(operand,method)&&(opcode==System.Reflection.Emit.OpCodes.Call||opcode==System.Reflection.Emit.OpCodes.Callvirt);} }
    public enum HarmonyPatchType { Transpiler }
    public class HarmonyMethod { public HarmonyMethod(Type type,string name){} }
    public class Harmony { public int Patches;public void Patch(System.Reflection.MethodInfo method,HarmonyMethod transpiler=null){Patches++;}public void Unpatch(System.Reflection.MethodInfo method,HarmonyPatchType type,string owner){} }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) {} }
}
namespace UnityEngine
{
    public class Object
    {
        public static readonly List<Object> All = new List<Object>();
        public Object() { All.Add(this); }
        public static T Instantiate<T>(T prefab,Vector3 position,Quaternion rotation) where T:Object
        {GameObject original=prefab as GameObject;GameObject clone=new GameObject(original.name);clone.transform.position=position;clone.transform.rotation=rotation;clone.Add<Rigidbody>();foreach(PlayMakerFSM fsm in original.GetComponents<PlayMakerFSM>()){PlayMakerFSM copy=clone.Add<PlayMakerFSM>();copy.FsmName=fsm.FsmName;copy.ActiveStateName=fsm.ActiveStateName;}return clone as T;}
        public bool Destroyed;public static void Destroy(Object value){value.Destroyed=true;All.Remove(value);}
        public static T[] FindObjectsOfType<T>() where T : Object { return All.FindAll(x => x is T).ConvertAll(x => (T)x).ToArray(); }
        public static T FindObjectOfType<T>() where T : Object { T[] all=FindObjectsOfType<T>();return all.Length>0?all[0]:null; }
    }
    public class Component : Object
    {
        public GameObject gameObject; public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : Component { return gameObject.GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component { return gameObject.GetComponentsInChildren<T>(inactive); }
        public T GetComponentInChildren<T>(bool inactive) where T : Component { T[] all=GetComponentsInChildren<T>(inactive);return all.Length>0?all[0]:null; }
        public T GetComponentInParent<T>() where T : Component { for(Transform t=transform;t!=null;t=t.parent){T found=t.GetComponent<T>();if(found!=null)return found;}return null; }
        public string name {get{return gameObject.name;}}
    }
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled {get{return enabled&&gameObject.activeInHierarchy;}} }
    public class MonoBehaviour : Behaviour {}
    [AttributeUsage(AttributeTargets.Method)] public sealed class ImageEffectOpaque : Attribute {}
    [Flags] public enum DepthTextureMode {None=0,Depth=1,DepthNormals=2}
    public class Camera : Behaviour
    {
        public DepthTextureMode depthTextureMode;public float farClipPlane=3000,fieldOfView=60,aspect=1.6f;public int cullingMask=-1;
        public bool UseRenderedPose;public Vector3 RenderedPosition,RenderedForward=Vector3.forward;
        private Vector3 Forward{get{return (UseRenderedPose?RenderedForward:transform.forward).normalized;}}
        private Vector3 Position{get{return UseRenderedPose?RenderedPosition:transform.position;}}
        private Vector3 Right{get{return Vector3.Cross(Vector3.up,Forward).normalized;}}
        private Vector3 Up{get{return Vector3.Cross(Forward,Right).normalized;}}
        public Matrix4x4 worldToCameraMatrix{get{return Matrix4x4.View(Position,Forward);}}
        public Matrix4x4 projectionMatrix{get{float half=(float)Math.Tan(fieldOfView*Math.PI/360);return new Matrix4x4{m00=1/(half*aspect),m11=1/half};}}
        public Ray ViewportPointToRay(Vector3 p)
        {float half=(float)Math.Tan(fieldOfView*Math.PI/360);return new Ray(Position,(Forward+Right*((p.x-0.5f)*2*half*aspect)+Up*((p.y-0.5f)*2*half)).normalized);}
        public Vector3 WorldToViewportPoint(Vector3 p)
        {Vector3 d=p-Position;float z=Vector3.Dot(d,Forward),half=(float)Math.Tan(fieldOfView*Math.PI/360);return new Vector3(0.5f+Vector3.Dot(d,Right)/(2*z*half*aspect),0.5f+Vector3.Dot(d,Up)/(2*z*half),z);}
    }
    public struct Ray {public Vector3 origin,direction;public Ray(Vector3 o,Vector3 d){origin=o;direction=d;}public Vector3 GetPoint(float distance){return origin+direction*distance;}}
    public class Matrix4x4
    {
        public float m00,m02,m11,m12;private Vector3 position,forward,right,up;private bool cameraSpace;
        public static Matrix4x4 View(Vector3 p,Vector3 f){Vector3 r=Vector3.Cross(Vector3.up,f).normalized;return new Matrix4x4{position=p,forward=f,right=r,up=Vector3.Cross(f,r).normalized,cameraSpace=true};}
        public Matrix4x4 inverse{get{return new Matrix4x4{position=position,forward=forward,right=right,up=up,cameraSpace=!cameraSpace,m00=m00==0?0:1/m00,m11=m11==0?0:1/m11,m02=m02,m12=m12};}}
        public float this[int row,int column]{get{return row==1&&column==1?m11:m00;}set{if(row==1&&column==1)m11=value;else m00=value;}}
        public Vector3 MultiplyVector(Vector3 v){return cameraSpace?new Vector3(Vector3.Dot(v,right),Vector3.Dot(v,up),-Vector3.Dot(v,forward)):right*v.x+up*v.y-forward*v.z;}
        public Vector3 MultiplyPoint3x4(Vector3 v){return cameraSpace?MultiplyVector(v-position):position+MultiplyVector(v);}
    }
    public enum RenderTextureMemoryless {None=0}
    public struct RenderTextureDescriptor
    {
        public int width,height,depthBufferBits,msaaSamples,colorFormat;
        public bool bindMS,useMipMap,autoGenerateMips;public RenderTextureMemoryless memoryless;
    }
    public class RenderTexture : Object
    {
        public static RenderTexture active;public static int Rents,Returns;public static bool FailNextRent;
        public RenderTextureDescriptor descriptor=new RenderTextureDescriptor{width=1920,height=1080,msaaSamples=1};
        public FilterMode filterMode;public bool Normalized,AlternateOrientation,Released;
        public string Top="sky",Bottom="ground",AtV0="ground",AtV1="sky";public float TexelSizeY=-1f/1080;
        public static RenderTexture GetTemporary(RenderTextureDescriptor descriptor){if(FailNextRent){FailNextRent=false;throw new Exception("Simulated texture allocation failure");}Rents++;return new RenderTexture{descriptor=descriptor};}
        public static void ReleaseTemporary(RenderTexture texture){texture.Released=true;Returns++;Object.All.Remove(texture);}
    }
    public static class Graphics
    {
        public static int PlainBlits,FogBlits,LastPass;public static Material LastMaterial;public static RenderTexture LastFogSource;public static bool ThrowOnce;public static RenderTexture ScreenTarget=new RenderTexture();
        // This fixture uses the actual decoded built-in vertex equation:
        // UV.y = _MainTex_TexelSize.y > 0 ? 1-inputUV.y : inputUV.y.
        // It models sampler rows, not GPU scheduling or the final Unity frame.
        public static void Blit(RenderTexture source,RenderTexture destination){PlainBlits++;RenderTexture.active=destination;if(destination==null)destination=ScreenTarget;destination.Top=source.Top;destination.Bottom=source.Bottom;destination.Normalized=true;destination.AlternateOrientation=false;destination.TexelSizeY=1f/destination.descriptor.height;destination.AtV0=source.Bottom;destination.AtV1=source.Top;}
        public static void Blit(RenderTexture source,RenderTexture destination,Material material,int pass){if(ThrowOnce){ThrowOnce=false;throw new Exception("Simulated depth pass failure");}FogBlits++;LastMaterial=material;LastFogSource=source;LastPass=pass;RenderTexture.active=destination;if(destination==null)destination=ScreenTarget;bool flip=source.TexelSizeY>0;destination.Top=flip?source.AtV0:source.AtV1;destination.Bottom=flip?source.AtV1:source.AtV0;}
    }
    public static class GL {public static Matrix4x4 GetGPUProjectionMatrix(Matrix4x4 matrix,bool renderTexture){return matrix;}}
    public static class SystemInfo {public static UnityEngine.Rendering.GraphicsDeviceType graphicsDeviceType=UnityEngine.Rendering.GraphicsDeviceType.Direct3D11;}
    public class AudioSource : Behaviour
    { public bool loop,playOnAwake,ignoreListenerVolume,ignoreListenerPause,isPlaying;public float volume,spatialBlend,dopplerLevel,panStereo;public int priority,Shots;public float LastShotGain;public object outputAudioMixerGroup;public AudioClip clip;public void Play(){isPlaying=true;}public void PlayOneShot(AudioClip value,float gain){Shots++;LastShotGain=gain;isPlaying=true;}public void Stop(){isPlaying=false;} }
    public class AudioClip : Object
    { public string name;public HideFlags hideFlags;public float[] Data;public int channels,frequency;public float length{get{return Data.Length/(float)(channels*frequency);}}public AudioDataLoadState loadState=AudioDataLoadState.Loaded;public bool LoadAudioData(){return true;}public static AudioClip Create(string name,int length,int channels,int rate,bool stream){return new AudioClip{name=name,Data=new float[length],channels=channels,frequency=rate};}public bool SetData(float[] samples,int offset){Array.Copy(samples,0,Data,offset,samples.Length);return true;} }
    public enum AudioDataLoadState {Unloaded,Loading,Loaded,Failed}
    public static class Resources {public static T[] FindObjectsOfTypeAll<T>() where T:Object{return Object.FindObjectsOfType<T>();}}
    public enum LightType { Directional,Point,Spot }
    public enum LightShadows {None,Hard,Soft}public enum LightRenderMode {Auto,ForcePixel,ForceVertex}
    public class Light : Behaviour { public LightType type; public float intensity,range=30,bounceIntensity,shadowStrength,shadowBias,shadowNormalBias;public Color color;public LightShadows shadows;public LightRenderMode renderMode;public UnityEngine.Rendering.LightShadowResolution shadowResolution;public int cullingMask; }
    public class Renderer : Component { public bool enabled = true; public Material sharedMaterial; }
    public class ParticleSystemRenderer : Renderer {}
    public class MeshRenderer : Renderer {public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public UnityEngine.Rendering.LightProbeUsage lightProbeUsage;public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage;}
    public class LineRenderer : Renderer {public bool useWorldSpace,receiveShadows;public int positionCount;public float startWidth,endWidth;public Color startColor,endColor;public Vector3[] Positions;public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;public UnityEngine.Rendering.LightProbeUsage lightProbeUsage;public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage;public void SetPositions(Vector3[] points){Positions=points;} }
    public class MeshFilter : Component {public Mesh sharedMesh;}
    public class Mesh : Object {public string name;public HideFlags hideFlags;public Vector3[] vertices;public Vector2[] uv;public Color[] colors;public int[] triangles;public void RecalculateBounds(){} }
    public enum TextureFormat { RGBA32 } public enum TextureWrapMode { Clamp } public enum FilterMode { Bilinear }
    public struct Color32 {public byte r,g,b,a;public Color32(byte red,byte green,byte blue,byte alpha){r=red;g=green;b=blue;a=alpha;}}
    public class Texture2D : Object {public static Texture2D whiteTexture=new Texture2D();public string name;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;public int width,height;public Color32[] Pixels;public Texture2D(){}public Texture2D(int w,int h,TextureFormat format,bool mip){width=w;height=h;}public void SetPixels32(Color32[] pixels){Pixels=pixels;}public void Apply(bool mip,bool unreadable){} }
    public struct Rect {public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
    public enum EventType { Layout,Repaint } public class Event {public static Event current=new Event{type=EventType.Repaint};public EventType type;}
    public static class Screen {public static int width=1920,height=1080;}
    public static class GUI {public static Color color=Color.white;public static int depth;public struct Draw {public Rect Area;public Texture2D Texture;public Color Tint;}public static List<Draw> Draws=new List<Draw>();public static void DrawTexture(Rect area,Texture2D texture){Draws.Add(new Draw{Area=area,Texture=texture,Tint=color});}}
    public class RectTransform : Transform {public Vector3[] Corners=new[]{new Vector3(700,40,0),new Vector3(700,140,0),new Vector3(1150,140,0),new Vector3(1150,40,0)};public void GetWorldCorners(Vector3[] result){Array.Copy(Corners,result,4);}}
    public static class RectTransformUtility {public static Vector2 WorldToScreenPoint(Camera camera,Vector3 point){return new Vector2(point.x,point.y);}}
    public enum RenderMode { ScreenSpaceOverlay,ScreenSpaceCamera,WorldSpace } public class Canvas : Behaviour {public RenderMode renderMode;public Camera worldCamera;}
    public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
    public class Collider : Component { public bool enabled = true; public bool isTrigger; public Rigidbody attachedRigidbody; }
    public class SphereCollider : Collider { public float radius; }
    public class Rigidbody : Component
    {
        public bool isKinematic;public float mass=1600; public Vector3 angularVelocity,worldCenterOfMass;public Vector3 velocity{get;set;}
        public int Forces;public Vector3 LastForce,LastPoint;
        public void AddForceAtPosition(Vector3 force,Vector3 point,ForceMode mode){Forces++;LastForce=force;LastPoint=point;}
        public Vector3 GetPointVelocity(Vector3 point){return Vector3.zero;}
    }
    public enum ForceMode {Force}
    public enum HideFlags {HideAndDontSave}
    public enum QueryTriggerInteraction {Ignore}
    public struct Quaternion {public static Quaternion identity {get{return new Quaternion();}}public static Quaternion Euler(float x,float y,float z){return new Quaternion();}}
    public struct RaycastHit {public Collider collider;public float distance;public Vector3 normal,point;}
    public static class Physics
    {
        public static Func<Vector3,Vector3,float,RaycastHit[]> RaycastFixture;
        public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] hits,float distance,int layer,QueryTriggerInteraction query){RaycastHit[] found=RaycastFixture==null?new RaycastHit[0]:RaycastFixture(origin,direction,distance);Array.Copy(found,hits,Math.Min(found.Length,hits.Length));return Math.Min(found.Length,hits.Length);}
        public static int SphereCastNonAlloc(Vector3 p,float r,Vector3 direction,RaycastHit[] hits,float distance,int layer,QueryTriggerInteraction query){return 0;}
        public static int OverlapSphereNonAlloc(Vector3 p,float r,Collider[] results,int layer,QueryTriggerInteraction query){return 0;}
        public static bool ComputePenetration(Collider a,Vector3 ap,Quaternion ar,Collider b,Vector3 bp,Quaternion br,out Vector3 normal,out float depth){normal=Vector3.zero;depth=0;return false;}
    }
    public static class Time {public static float fixedDeltaTime=0.02f,unscaledTime,timeScale=1;public static int frameCount;}
    public class Material : Object
    {
        public Material(Shader shader){}public HideFlags hideFlags;public Texture2D mainTexture;public int renderQueue;
        public Material(){}public Material(Material other){foreach(var pair in other.colors)colors[pair.Key]=pair.Value;foreach(var pair in other.floats)floats[pair.Key]=pair.Value;}
        public bool ThrowOnceOnWrite;
        private readonly Dictionary<string,Color> colors=new Dictionary<string,Color>();
        private readonly Dictionary<string,float> floats=new Dictionary<string,float>();
        private readonly Dictionary<string,Vector4> vectors=new Dictionary<string,Vector4>();
        private readonly Dictionary<string,Matrix4x4> matrices=new Dictionary<string,Matrix4x4>();
        public readonly HashSet<string> Keywords=new HashSet<string>();
        public void EnableKeyword(string key){Keywords.Add(key);}
        public void SetVector(string key,Vector4 value){vectors[key]=value;}public Vector4 GetVector(string key){return vectors[key];}
        public object BoundTexture;public void SetTexture(string key,object value){BoundTexture=value;}
        public void SetMatrix(string key,Matrix4x4 value){matrices[key]=value;}public Matrix4x4 GetMatrix(string key){return matrices[key];}
        public bool HasProperty(string name){return colors.ContainsKey(name)||floats.ContainsKey(name);}
        public Color GetColor(string name){return colors[name];}public void SetColor(string name,Color color){if(ThrowOnceOnWrite){ThrowOnceOnWrite=false;throw new Exception("Simulated material write failure");}colors[name]=color;}
        public float GetFloat(string name){return floats[name];}public void SetFloat(string name,float value){floats[name]=value;}
        public void SetInt(string name,int value){floats[name]=value;}
    }
    public class Transform : Component
    {
        public Transform parent; public Vector3 position,up=Vector3.up,localScale=Vector3.one,forward=Vector3.forward;public Quaternion rotation;
        public void SetParent(Transform t,bool stay){parent=t;}
        public Vector3 TransformVector(Vector3 value){Vector3 right=Vector3.Cross(up,forward).normalized;return right*value.x+up*value.y+forward*value.z;}
        public Transform Find(string name){foreach(Object obj in Object.All){Transform t=obj as Transform;if(t!=null&&t.parent==this&&t.name==name)return t;}return null;}
        public T[] GetComponents<T>() where T : Component { return gameObject.GetComponents<T>(); }
    }
    public class GameObject : Object
    {
        public bool activeInHierarchy=true;public void SetActive(bool value){activeInHierarchy=value;}
        public string name;public HideFlags hideFlags; public Transform transform; public List<Component> Components = new List<Component>();
        public bool IsPrefab;public UnityEngine.SceneManagement.Scene scene{get{return new UnityEngine.SceneManagement.Scene{Valid=!IsPrefab};}}
        public GameObject(string n) { name=n; transform=new Transform {gameObject=this}; Components.Add(transform); }
        public static GameObject Find(string name){foreach(Object obj in Object.All){GameObject go=obj as GameObject;if(go!=null&&go.name==name&&!go.Destroyed&&go.activeInHierarchy)return go;}return null;}
        public T Add<T>() where T : Component,new() { T c=new T {gameObject=this}; Components.Add(c); return c; }
        public T AddComponent<T>() where T : Component,new() {return Add<T>();}
        public T GetComponent<T>() where T : Component { return Components.Find(x=>x is T&&!x.Destroyed) as T; }
        public T[] GetComponents<T>() where T : Component { return Components.FindAll(x=>x is T).ConvertAll(x=>(T)x).ToArray(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component
        {
            List<T> result=new List<T>();
            foreach(Object obj in Object.All) { T component=obj as T; if(component==null || component.gameObject==null)continue;
                for(Transform t=component.transform;t!=null;t=t.parent) if(t==transform){result.Add(component);break;}}
            return result.ToArray();
        }
    }
    public struct Vector4
    { public float x,y,z,w; public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;} }
    public struct Color
    {
        public static Color white {get{return new Color(1,1,1,1);}}
        public float r,g,b,a; public Color(float red,float green,float blue,float alpha){r=red;g=green;b=blue;a=alpha;}
        public float maxColorComponent {get{return Math.Max(Math.Max(r,g),Math.Max(b,a));}}
        public static Color operator*(Color c,float f){return new Color(c.r*f,c.g*f,c.b*f,c.a*f);}
        public static Color Lerp(Color x,Color y,float t){return new Color(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);}
    }
    public enum FogMode { Linear=1,Exponential=2,ExponentialSquared=3 }
    public static class RenderSettings
    {
        public static bool fog; public static FogMode fogMode; public static float fogDensity,fogStartDistance,fogEndDistance; public static Color fogColor;
        public static Light sun; public static Material skybox;
        public static float ambientIntensity; public static Color ambientLight,ambientSkyColor,ambientEquatorColor,ambientGroundColor;
        public static UnityEngine.Rendering.AmbientMode ambientMode;
        private static UnityEngine.Rendering.SphericalHarmonicsL2 probe;
        public static int ProbeWrites;
        public static UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe{get{return probe;}set{probe=value;ProbeWrites++;}}
    }
    public static class DynamicGI
    {
        public static int Requests;public static bool ThrowOnce;
        public static float AmbientAtRequest;public static Color SkyAtRequest;
        public static UnityEngine.Rendering.SphericalHarmonicsL2 ProbeAtRequest;
        public static void UpdateEnvironment(){Requests++;if(ThrowOnce){ThrowOnce=false;throw new Exception("Simulated GI request failure");}AmbientAtRequest=RenderSettings.ambientIntensity;SkyAtRequest=RenderSettings.ambientSkyColor;ProbeAtRequest=RenderSettings.ambientProbe;}
    }
    public class Shader
    {
        public static bool Available=true;public bool isSupported=true;public static Shader Find(string name){return Available?new Shader():null;}
        private static readonly Dictionary<string,Vector4> vectors=new Dictionary<string,Vector4>();
        private static readonly Dictionary<string,float> floats=new Dictionary<string,float>();
        public static Vector4 GetGlobalVector(string n){return vectors.ContainsKey(n)?vectors[n]:new Vector4();}
        public static void SetGlobalVector(string n,Vector4 v){vectors[n]=v;}
        public static float GetGlobalFloat(string n){return floats.ContainsKey(n)?floats[n]:0;}
        public static void SetGlobalFloat(string n,float v){floats[n]=v;}
    }
    public static class Mathf
    { public static float Max(float a,float b){return Math.Max(a,b);} public static float Min(float a,float b){return Math.Min(a,b);}
      public static float Clamp(float a,float b,float c){return Math.Max(b,Math.Min(c,a));} public static float Clamp01(float x){return Clamp(x,0,1);} public static float Log(float x){return (float)Math.Log(x);}
      public static float Exp(float x){return (float)Math.Exp(x);}public static float Lerp(float a,float b,float x){return a+(b-a)*Clamp01(x);}
      public static float MoveTowards(float a,float b,float delta){return Math.Abs(b-a)<=delta?b:a+Math.Sign(b-a)*delta;}
      public const float PI=(float)Math.PI;public static float Sin(float x){return (float)Math.Sin(x);}public static float Cos(float x){return (float)Math.Cos(x);}
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
        public static Vector3 zero{get{return new Vector3();}}public static Vector3 up{get{return new Vector3(0,1,0);}}
        public static Vector3 one{get{return new Vector3(1,1,1);}}
        public static Vector3 forward{get{return new Vector3(0,0,1);}}public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;}
        public float sqrMagnitude{get{return x*x+y*y+z*z;}}public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}}
        public Vector3 normalized{get{return magnitude<0.000001f?zero:this*(1/magnitude);}}
        public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator-(Vector3 a){return a*(-1);}
        public static Vector3 operator*(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}
        public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
        public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
        public static Vector3 ClampMagnitude(Vector3 v,float max){return v.magnitude>max?v.normalized*max:v;}
    }
    public struct Vector3Int { public int x,y,z;public Vector3Int(int a,int b,int c){x=a;y=b;z=c;} }
}
namespace UnityEngine.AzureSky
{
    public class AzureFogScattering : UnityEngine.Behaviour
    {
        public UnityEngine.Material fogScatteringMaterial;public int InitCalls;private void Start(){InitCalls++;}
        public UnityEngine.Vector3[] FrustumResults=new UnityEngine.Vector3[4];
        internal void OnRenderImage()
        {
            FrustumResults[0]=transform.TransformVector(new UnityEngine.Vector3(0,0,10));
            FrustumResults[1]=transform.TransformVector(new UnityEngine.Vector3(1,0,10));
            FrustumResults[2]=transform.TransformVector(new UnityEngine.Vector3(0,1,10));
            FrustumResults[3]=transform.TransformVector(new UnityEngine.Vector3(1,1,10));
        }
    }
    public class AzureSkyRenderController : UnityEngine.Behaviour { public UnityEngine.Material m_skyMaterial,m_fogMaterial; }
    public class AzureTimeController : UnityEngine.Behaviour
    {
        private float m_dayLength=60;public float Timeline,SunElevation=1;public UnityEngine.Vector3Int Date=new UnityEngine.Vector3Int(2026,10,7);
        public UnityEngine.Vector3Int GetDate(){return Date;}public float GetTimeline(){return Timeline;}
        public float GetSunElevation(){return SunElevation;}
        public void SetLength(float value){m_dayLength=value;}public float ReadLength(){return m_dayLength;}
    }
}
namespace HutongGames.PlayMaker
{
    public class Fsm
    {
        public string Name; public bool Initialized=true; public PlayMakerFSM FsmComponent;
        public FsmState[] States=new FsmState[0]; public FsmState ActiveState;
        public FsmState GetState(string name){foreach(FsmState state in States)if(state.Name==name)return state;return null;}
        internal void SwitchState(FsmState state){if(state==null)return;ActiveState=state;FsmComponent.ActiveStateName=state.Name;state.Entries++;}
    }
    public class FsmState {public string Name;public FsmTransition[] Transitions=new FsmTransition[0];public int Entries;}
    public class FsmTransition {public FsmEvent FsmEvent;public string ToState;}
    public class FsmEvent {public string Name;}
    public class FsmString {public string Value;}
    public class FsmFloat {public bool IsNone;public float Value;}
    public class FsmVector3 {public bool IsNone;public UnityEngine.Vector3 Value;}
    public class FsmVariables {public FsmFloat Health=new FsmFloat{Value=100};public FsmFloat FindFsmFloat(string name){return name=="Health"?Health:null;}}
    public class FsmStateAction
    { public UnityEngine.GameObject Owner; public Fsm Fsm; public bool Finished; public void Finish(){Finished=true;} }
}
namespace HutongGames.PlayMaker.Actions
{ public class Explosion : HutongGames.PlayMaker.FsmStateAction {} public class SendEvent : HutongGames.PlayMaker.FsmStateAction {} public class CallMethod:HutongGames.PlayMaker.FsmStateAction{public HutongGames.PlayMaker.FsmString methodName;public int NativeCalls;internal void DoMethodCall(){NativeCalls++;}} public class GetAxisKeyAxis:HutongGames.PlayMaker.FsmStateAction{public HutongGames.PlayMaker.FsmFloat store;public float NativeInput=5;internal void DoGetAxis(){store.Value=NativeInput;}}public class SetVelocity:HutongGames.PlayMaker.FsmStateAction{public UnityEngine.Vector3 NativeCommand;internal void DoSetVelocity(){Owner.GetComponent<UnityEngine.Rigidbody>().velocity=NativeCommand;}} }
public class PlayMakerFSM : UnityEngine.Behaviour
{
    private string fsmName;public string FsmName{get{return fsmName;}set{fsmName=value;Fsm.Name=value;Fsm.FsmComponent=this;}}
    public string ActiveStateName;public HutongGames.PlayMaker.Fsm Fsm=new HutongGames.PlayMaker.Fsm();public HutongGames.PlayMaker.FsmVariables FsmVariables=new HutongGames.PlayMaker.FsmVariables();
    public int BackEvents;public void SendEvent(string value){if(value=="back")BackEvents++;HutongGames.PlayMaker.FsmState state=Fsm.GetState(ActiveStateName);if(state==null)return;foreach(HutongGames.PlayMaker.FsmTransition transition in state.Transitions)if(transition.FsmEvent.Name==value){SetState(transition.ToState);return;}}
    public void SetState(string name){Fsm.SwitchState(Fsm.GetState(name));}
}
public class Tornado : UnityEngine.Behaviour {}
public class EnviroSkyRenderingLW : UnityEngine.Behaviour {}
public class EnviroFogSettings { public float startDistance; }
public class EnviroSkyLite { public static EnviroSkyLite instance = new EnviroSkyLite(); public EnviroFogSettings fogSettings = new EnviroFogSettings(); }
namespace ApocaDustStorm
{
    internal class Logger { internal void LogInfo(string text) {} internal void LogWarning(string text){} }
    internal static class Plugin
    { internal class BoolSetting{public bool Value=true;}internal const string GUID="local.apocalypter.duststorm";internal static BoolSetting WindblownLizards=new BoolSetting();internal static bool Active=true,Lightning=true;internal static bool DustLightningEnabled{get{return Active&&Lightning;}} internal static Logger Log=new Logger(); internal static object Visibility=null,Gusts=null,Buffeting=null,Darkness=null,Headlights=null,SandVolume=null,FlashBrightness=null,DischargeVolume=null,WindVolume=null,ExposureDamage=null,ExposureIndicators=null,MovementResistance=null; internal static float StormVisibility {get{return Value(Visibility,45)*1.02f;}} internal static float Value(object entry,float fallback){return entry==null?fallback:(float)entry;} }
    internal static class StormRunner
    { internal static readonly StormModel Model=new StormModel();internal static bool CanRender {get{return Plugin.Active && View!=null;}} internal static UnityEngine.Camera View; internal static float Strength,Gust=0; }
}
namespace Apocasetter { public static class GameMenu { public static bool Paused;public static bool InGame=true; } }
namespace UnityEngine.SceneManagement {public struct Scene {public bool Valid;public bool IsValid(){return Valid;}}}
namespace NPCAI
{
    internal static class Brain { internal static bool Enabled=true;internal static UnityEngine.Vector3 Command=new UnityEngine.Vector3(5,-4,10);internal static bool BeforeSetVelocity(HutongGames.PlayMaker.Actions.SetVelocity __instance){if(!Enabled||__instance.Fsm.Name!="Movement"||__instance.Owner.name=="Player")return true;__instance.Owner.GetComponent<UnityEngine.Rigidbody>().velocity=Command;return false;} }
    internal static class Senses { internal sealed class Agent {public UnityEngine.Transform T;} }
    internal static class Idle {internal sealed class Ctl {public Senses.Agent A;}internal static void Drive(Ctl ctl,float dt){ctl.A.T.GetComponent<UnityEngine.Rigidbody>().velocity=Brain.Command;} }
}
namespace NWH.VehiclePhysics2
{public class VehicleController : UnityEngine.Behaviour {public bool Grounded=true;public bool IsGrounded(){return Grounded;}}}
namespace ApocaChaseCamera
{
    internal static class ChaseView
    {
        internal static bool Override;internal static int Calls;
        internal static bool Apply(UnityEngine.Camera camera)
        {Calls++;if(!Override)return false;camera.UseRenderedPose=true;camera.RenderedPosition=new UnityEngine.Vector3(100,8,50);camera.RenderedForward=new UnityEngine.Vector3(0,0,-1);return true;}
    }
}
namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType {Direct3D11=2,OpenGLES2=8,OpenGLES3=11,OpenGLCore=17}
    public enum AmbientMode {Skybox,Trilight,Flat}
    public enum ShadowCastingMode {Off,On}public enum LightProbeUsage {Off}public enum ReflectionProbeUsage {Off}public enum LightShadowResolution {FromQualitySettings,Low,Medium,High,VeryHigh}
    public enum CompareFunction {LessEqual=4}
    public struct SphericalHarmonicsL2
    {
        private float[] values;
        public float this[int channel,int coefficient]
        {get{return values==null?0:values[channel*9+coefficient];}set{values=values==null?new float[27]:(float[])values.Clone();values[channel*9+coefficient]=value;}}
    }
}
