using System;
using HutongGames.PlayMaker;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class EntryShakeRuntimeChecks
    {
        private static bool Same(Matrix4x4 a, Matrix4x4 b)
        { for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(a[r,c]!=b[r,c])return false;return true; }
        private static void Start()
        { StormEntryShake.Clear(); StormEntryShake.Tick(.016f,true,false); StormEntryShake.Tick(.1f,false,false); }
        internal static void Run(Action<bool,string> check)
        {
            EntryWindShakeModel a=new EntryWindShakeModel(),b=new EntryWindShakeModel();
            a.Step(.1,false);check(a.X==0&&a.Y==0,"Shake stays idle without a boundary entry");
            a.Step(0,true);check(a.X==0&&a.Y==0,"Entry begins at zero offset rather than a camera snap");
            bool bounded=true,moved=false;double last=0;
            for(int i=0;i<1000;i++) { a.Step(.005,false);bounded&=Math.Abs(a.X)<=1&&Math.Abs(a.Y)<=1;moved|=Math.Abs(a.X)>.1;last=Math.Abs(a.X)+Math.Abs(a.Y); }
            check(bounded&&moved&&last<.00001,"Entry motion is bounded and fades smoothly to zero in five seconds");
            a.Step(0,true);a.Step(4,false);check(Math.Abs(a.X)+Math.Abs(a.Y)>.0001,"The requested longer shake remains active four seconds after entry");
            a.Step(.1,true);b.Step(.1,true);for(int i=0;i<30;i++)a.Step(.01,false);for(int i=0;i<10;i++)b.Step(.03,false);
            check(Math.Abs(a.X-b.X)<1e-12&&Math.Abs(a.Y-b.Y)<1e-12,"Shake follows elapsed time consistently across frame rates");
            a.Step(double.NaN,false);check(a.X==0&&a.Y==0,"Invalid timing cancels instead of leaving a stuck offset");

            StormEntryShake.Clear();StormView.Clear();PlayerStormHazards.Reset();StormSleep.Reset();
            Plugin.Active=true;Plugin.EntryShake=null;Application.isFocused=true;Apocasetter.InputBlocker.Active=false;
            Apocasetter.GameMenu.InGame=true;Apocasetter.GameMenu.Paused=false;Time.timeScale=1;
            Physics.RaycastFixture=null;Time.unscaledTime+=100;
            Camera view=new GameObject("Entry shake view").Add<Camera>(),menu=new GameObject("Entry menu").Add<Camera>();
            StormRunner.View=view;StormRunner.Strength=.5f;StormRunner.Model.BeginFrom(0,0,WindFrom.North,600,90,75,9);
            GameObject player=new GameObject("Player");
            PlayMakerFSM health=player.Add<PlayMakerFSM>();health.FsmName="Health";health.ActiveStateName="playerHealth";
            PlayMakerFSM cab=player.Add<PlayMakerFSM>();cab.FsmName="InCar";cab.ActiveStateName="OnFoot";
            PlayMakerFSM sleep=player.Add<PlayMakerFSM>();sleep.FsmName="Sleep";sleep.ActiveStateName="Awake";
            PlayerStormHazards.Tick(player,.1f,.5f,false);
            check(PlayerStormHazards.Ready&&!PlayerStormHazards.Sheltered,"Entry fixture uses a live exposed player");
            Matrix4x4 original=view.projectionMatrix,world=view.worldToCameraMatrix;Vector3 position=view.transform.position;
            int searches=UnityEngine.Object.SceneQueries,finds=GameObject.Finds;
            Start();StormEntryShake.PreCull(menu);check(!menu.ProjectionIsCustom,"Menu and secondary cameras receive no entry shake");
            StormEntryShake.PreCull(view);Matrix4x4 shifted=view.projectionMatrix;
            check(!Same(original,shifted)&&Math.Abs(shifted.m02-original.m02)<=.0041f&&Math.Abs(shifted.m12-original.m12)<=.0031f,"Default entry moves the screen by only a few pixels");
            check(Same(world,view.worldToCameraMatrix)&&view.transform.position.x==position.x&&view.transform.position.y==position.y,"Entry preserves the actual camera pose and look direction");
            StormEntryShake.PreCull(view);check(Same(shifted,view.projectionMatrix),"Repeated camera callbacks cannot accumulate hidden offsets");
            StormView.Capture(view);check(StormView.Screen(view).m02==shifted.m02&&StormView.Screen(view).m12==shifted.m12,"Storm fog captures the same shifted projection as the rendered view");
            StormEntryShake.PostRender(menu);check(Same(shifted,view.projectionMatrix),"Secondary-camera completion cannot restore the active camera early");
            StormEntryShake.PostRender(view);check(Same(original,view.projectionMatrix)&&!view.ProjectionIsCustom,"Camera completion restores automatic projection mode");
            view.fieldOfView=80;check(view.projectionMatrix.m11!=original.m11,"Projection cleanup does not freeze later native FOV changes");view.fieldOfView=60;

            Matrix4x4 custom=original;custom.m02=.12f;custom.m12=-.07f;view.projectionMatrix=custom;
            Start();StormEntryShake.PreCull(view);StormEntryShake.PostRender(view);
            check(view.ProjectionIsCustom&&Same(custom,view.projectionMatrix),"A pre-existing custom projection is restored exactly");
            Start();StormEntryShake.PreCull(view);Matrix4x4 foreign=original;foreign.m02=.4f;view.projectionMatrix=foreign;StormEntryShake.Restore();
            check(Same(foreign,view.projectionMatrix),"Cleanup preserves newer projection writes by another mod");view.ResetProjectionMatrix();
            Start();StormEntryShake.PreCull(view);StormEntryShake.Tick(.016f,false,false);
            check(!view.ProjectionIsCustom&&Same(original,view.projectionMatrix),"Next update recovers an interrupted render with no closing callback");
            Start();StormEntryShake.PreCull(view);StormEntryShake.Clear();check(!view.ProjectionIsCustom,"Disable/session cleanup removes an active screen offset immediately");
            Start();StormEntryShake.Tick(.016f,false,true);StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Sleep/clock skips cancel entry motion instead of replaying it");
            Start();Plugin.EntryShake=0f;StormEntryShake.PreCull(view);Plugin.EntryShake=null;
            StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Setting zero cancels the pulse and reenabling does not replay it");

            Start();StormEntryShake.PreCull(view);float outside=view.projectionMatrix.m02;StormEntryShake.Restore();
            cab.ActiveStateName="InCar";PlayerStormHazards.Tick(player,.1f,.5f,false);
            Start();StormEntryShake.PreCull(view);check(Math.Abs(view.projectionMatrix.m02-outside*.65f)<.000001,"Vehicle entry motion is reduced to 65 percent");StormEntryShake.Clear();
            cab.ActiveStateName="OnFoot";PlayerStormHazards.Tick(player,.1f,.5f,false);
            Start();PlayerStormHazards.Sheltered=true;StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Proper shelter cancels the wind impact");PlayerStormHazards.Sheltered=false;
            Start();sleep.ActiveStateName="SleepOnFoot";StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Sleeping players receive no entry shake");sleep.ActiveStateName="Awake";
            Start();Apocasetter.GameMenu.Paused=true;StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Pause cancels screen motion");Apocasetter.GameMenu.Paused=false;
            Start();Apocasetter.InputBlocker.Active=true;StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"An open input-blocking menu suppresses screen motion");Apocasetter.InputBlocker.Active=false;
            Start();Application.isFocused=false;StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Unfocused gameplay suppresses screen motion");Application.isFocused=true;
            Start();StormRunner.View=menu;StormEntryShake.Tick(.016f,false,false);StormEntryShake.PreCull(menu);
            check(!menu.ProjectionIsCustom,"First/third-person camera changes cancel rather than restart an existing pulse");StormRunner.View=view;
            Start();view.UseRenderedPose=true;view.RenderedPosition=new Vector3(20,3,10);view.RenderedForward=new Vector3(1,0,0);
            Vector3 eye=view.worldToCameraMatrix.inverse.MultiplyPoint3x4(Vector3.zero);StormEntryShake.PreCull(view);
            check(view.UseRenderedPose&&view.worldToCameraMatrix.inverse.MultiplyPoint3x4(Vector3.zero).x==eye.x,"Chase-camera custom rendered pose remains intact");StormEntryShake.Clear();view.UseRenderedPose=false;
            check(UnityEngine.Object.SceneQueries==searches&&GameObject.Finds==finds,"Entry effect adds no scene searches during ticks or camera callbacks");
            Start();StormEntryShake.Tick(6,false,false);StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"A completed pulse does not repeat while remaining inside a storm");
            Start();Plugin.Active=false;StormEntryShake.PreCull(view);check(!view.ProjectionIsCustom,"Disabled storm mod restores and suppresses entry motion");Plugin.Active=true;
            StormEntryShake.Clear();PlayerStormHazards.Reset();StormSleep.Reset();StormRunner.Model.Reset();StormRunner.Strength=0;StormView.Clear();
        }
    }
}
