using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class HarmonyCompatibilityChecks
    {
        private static int count;
        private static void Check(bool ok, string detail) { count++; if (!ok) throw new Exception(detail); }
        private static void OriginChecks(Harmony harmony)
        {
            Plugin.Active=true;StormRunner.View=new GameObject("Origin fixture camera").Add<Camera>();
            StormRunner.OriginAnchor=new GameObject("Origin fixture player").transform;
            StormRunner.OriginAnchor.position=new Vector3(1050,0,200);
            StormRunner.Model.Begin(1000,200,0,420,90,75,9);StormRunner.Model.Tick(40);
            double before=StormRunner.Model.SignedDistance(1050,200);
            Type[] patches={typeof(StormOriginPatch),typeof(StormAdvancedOriginPatch),typeof(StormNewOriginPatch)};
            FloatingOrigin[] fixtures={new GameObject("Origin").Add<FloatingOrigin>(),new GameObject("Advanced").Add<FloatingOriginAdvanced>(),new GameObject("New").Add<FloatingOriginNew>()};
            for(int i=0;i<fixtures.Length;i++)
            {
                harmony.Patch(AccessTools.Method(fixtures[i].GetType(),"LateUpdate"),prefix:new HarmonyMethod(patches[i],"Prefix"),postfix:new HarmonyMethod(patches[i],"Postfix"));
                fixtures[i].Anchor=StormRunner.OriginAnchor;fixtures[i].Delta=new Vector3(-1000,0,1000);
                int shifts=StormRunner.OriginShifts;
                AccessTools.Method(fixtures[i].GetType(),"LateUpdate").Invoke(fixtures[i],null);
                Check(StormRunner.OriginShifts==shifts+1,"Real Harmony origin prefix/postfix shares state exactly once: "+i);
                Vector3 position=StormRunner.OriginAnchor.position;
                Check(Math.Abs(StormRunner.Model.SignedDistance(position.x,position.z)-before)<0.00001,"Native translation preserves sampled front distance: "+i);
            }
            StormRunner.Model.Reset();StormRunner.OriginAnchor=null;
        }
        private static void Main()
        {
            Harmony storm = new Harmony(Plugin.GUID), npc = new Harmony("checked.npcai.brain");
            try
            {
                OriginChecks(storm);
                BrownoutHarmonyChecks.Run(storm,Check);
                GameObject player = new GameObject("Player"); player.Add<Rigidbody>();
                PlayMakerFSM hp = player.Add<PlayMakerFSM>(); hp.FsmName = "Health"; hp.ActiveStateName = "playerHealth";
                player.Add<PlayMakerFSM>().FsmName = "InCar";
                GameObject pursuer = new GameObject("Checked NPCAI pursuer"); pursuer.Add<Rigidbody>();
                foreach (string name in new string[] { "Health", "Detection", "Attack" }) pursuer.Add<PlayMakerFSM>().FsmName = name;
                PlayerStormHazards.Tick(player, 0.1f, 1, false); StormRunner.Strength = 1; StormRunner.Gust = 1;
                StormRunner.View = new GameObject("Checked gameplay camera").Add<Camera>();
                storm.Patch(AccessTools.Method(typeof(GetAxisKeyAxis), "DoGetAxis"), postfix: new HarmonyMethod(typeof(PlayerStormMovementPatch), "Postfix"));
                storm.Patch(AccessTools.Method(typeof(SetVelocity), "DoSetVelocity"), transpiler: new HarmonyMethod(typeof(NativeAIStormMovementPatch), "Transpiler"));
                npc.Patch(AccessTools.Method(typeof(SetVelocity), "DoSetVelocity"), prefix: new HarmonyMethod(typeof(NPCAI.Brain), "BeforeSetVelocity"));
                StormAIMovement.InstallNpcBridge(storm);
                Check(StormAIMovement.NpcBridgeInstalled, "Real installed Harmony accepts both optional NPCAI transpilers");
                GetAxisKeyAxis input = new GetAxisKeyAxis { Owner = player, Fsm = new Fsm { Name = "Movement" }, store = new FsmFloat(), NativeInput = 5 };
                input.DoGetAxis(); Check(Math.Abs(input.store.Value - 3.5f) < 0.00001, "Real Harmony scales freshly-read player movement once");
                input.DoGetAxis(); Check(Math.Abs(input.store.Value - 3.5f) < 0.00001, "Repeated frames do not compound player slowdown");
                SetVelocity action = new SetVelocity { Owner = pursuer, Fsm = new Fsm { Name = "Movement" }, NativeCommand = new Vector3(20, -2, 20) };
                action.DoSetVelocity(); Vector3 result = pursuer.GetComponent<Rigidbody>().velocity;
                Check(Math.Abs(result.x - 3.5f) < 0.00001 && result.z == 7 && result.y == -4, "NPCAI prefix can skip original native movement while its own command is slowed exactly once");
                NPCAI.Brain.Enabled = false; action.DoSetVelocity(); result = pursuer.GetComponent<Rigidbody>().velocity;
                Check(result.x == 14 && result.z == 14 && result.y == -2, "NPCAI fallback to native movement receives one resistance multiplier");
                NPCAI.Idle.Ctl ctl = new NPCAI.Idle.Ctl { A = new NPCAI.Senses.Agent { T = pursuer.transform } };
                NPCAI.Idle.Drive(ctl, 0.1f); result = pursuer.GetComponent<Rigidbody>().velocity;
                Check(result.z == 7 && result.y == -4, "Real Harmony also adapts NPCAI wandering independently of native movement");
                NPCAI.Brain.Enabled = true; action.Owner = player; action.NativeCommand = new Vector3(3.5f, -2, 3.5f); action.DoSetVelocity();
                Check(player.GetComponent<Rigidbody>().velocity.z == 3.5f, "Player velocity is not slowed twice through the AI adapter");
                action.Owner = pursuer; Plugin.MovementResistance = 0f; action.DoSetVelocity();
                Check(pursuer.GetComponent<Rigidbody>().velocity.z == 10, "Movement slider zero immediately removes modded AI resistance"); Plugin.MovementResistance = null;
                Plugin.Active = false; NPCAI.Idle.Drive(ctl, 0.1f);
                Check(pursuer.GetComponent<Rigidbody>().velocity.z == 10, "Disabled storms preserve modded wandering speed"); Plugin.Active = true;
                Check(pursuer.GetComponent<PlayMakerFSM>().FsmVariables.Health.Value == 100, "Composed native/NPCAI movement hooks never change NPC health");
                PlayMakerFSM sleeper=player.Add<PlayMakerFSM>();SleepFixtures.Configure(sleeper);
                storm.Patch(AccessTools.Method(typeof(Fsm), "SwitchState"), prefix:new HarmonyMethod(typeof(StormSleepEntryPatch), "Prefix"));
                storm.Patch(AccessTools.Method(typeof(CallMethod), "DoMethodCall"), prefix:new HarmonyMethod(typeof(StormSleepClockPatch), "Prefix"));
                float before=hp.FsmVariables.Health.Value;
                sleeper.SetState("checkInCar");
                Check(sleeper.ActiveStateName=="Awake"&&sleeper.Fsm.GetState("checkInCar").Entries==0,"Real Harmony redirects exposed entry before native sleep rewards execute");
                sleeper.ActiveStateName="SleepOnFoot";
                CallMethod timeline=new CallMethod{Owner=player,Fsm=sleeper.Fsm,methodName=new FsmString{Value="SetTimeline"}};
                timeline.DoMethodCall();
                Check(timeline.NativeCalls==0&&timeline.Finished&&sleeper.ActiveStateName=="Enable","Real Harmony blocks mid-sleep timeline advancement and routes on-foot control restoration");
                sleeper.ActiveStateName="SleepInCar";timeline.DoMethodCall();
                Check(timeline.NativeCalls==0&&sleeper.ActiveStateName=="Awake","Real Harmony wakes cab sleepers using their native cancel destination");
                Check(before==hp.FsmVariables.Health.Value,"Entry and clock hooks never change player health");
                timeline.methodName.Value="GetTimeline";timeline.DoMethodCall();
                Check(timeline.NativeCalls==1,"Sleep timeline reads remain available under real Harmony");
                Plugin.Active=false;sleeper.SetState("checkInCar");timeline.methodName.Value="SetTimeline";timeline.DoMethodCall();
                Check(sleeper.ActiveStateName=="checkInCar"&&timeline.NativeCalls==2,"Disabled storms restore original native entry and timeline calls");Plugin.Active=true;
                GameObject building=new GameObject("Building_test");Collider roof=building.Add<SphereCollider>();
                Physics.RaycastFixture=delegate(Vector3 p,Vector3 d,float distance){return d.y>.5f?new[]{new RaycastHit{collider=roof,distance=4}}:new RaycastHit[0];};
                sleeper.SetState("SleepOnFoot");timeline.DoMethodCall();
                Check(sleeper.ActiveStateName=="SleepOnFoot"&&timeline.NativeCalls==3,"Real Harmony allows normal native sheltered sleep and time acceleration");Physics.RaycastFixture=null;
                Camera view=StormRunner.View;view.UseRenderedPose=true;view.RenderedPosition=new Vector3(100,8,50);view.RenderedForward=new Vector3(0,0,-1);
                UnityEngine.AzureSky.AzureFogScattering fog=view.gameObject.Add<UnityEngine.AzureSky.AzureFogScattering>();
                storm.Patch(AccessTools.Method(typeof(UnityEngine.AzureSky.AzureFogScattering),"OnRenderImage"),transpiler:new HarmonyMethod(typeof(StormAzureFogPatch),"Transpiler"));
                StormView.Capture(view);fog.OnRenderImage();
                Check(fog.FrustumResults[0].z==-10&&fog.FrustumResults[1].x==-1&&fog.FrustumResults[2].y==1&&fog.FrustumResults[3].z==-10,"Real Harmony corrects all four fog frustum corners to the rendered chase pose");
                view.UseRenderedPose=false;fog.OnRenderImage();
                Check(fog.FrustumResults[0].z==-10,"Real Harmony image fog keeps captured geometry pose after live matrix reset");
                Camera other=new GameObject("Other fog camera").Add<Camera>();UnityEngine.AzureSky.AzureFogScattering otherFog=other.gameObject.Add<UnityEngine.AzureSky.AzureFogScattering>();otherFog.OnRenderImage();
                Check(otherFog.FrustumResults[0].z==10,"Real Harmony preserves original fog rays on other cameras");
                Time.frameCount++;fog.OnRenderImage();Check(fog.FrustumResults[0].z==10,"Real Harmony cannot reuse a stale fog pose on a later frame");
                view.UseRenderedPose=true;StormView.Capture(view);Plugin.Active=false;fog.OnRenderImage();
                Check(fog.FrustumResults[0].z==10,"Disabled storms restore native transform-based fog rays");Plugin.Active=true;view.UseRenderedPose=false;
                GameObject lizard=new GameObject("Lizard_Dead");lizard.IsPrefab=true;lizard.Add<Rigidbody>();
                foreach(string marker in new[]{"Food","ID","ItemName"})lizard.Add<PlayMakerFSM>().FsmName=marker;
                WindblownLizards.Reset();
                storm.Patch(AccessTools.Method(typeof(CreateObject),"OnEnter"),prefix:new HarmonyMethod(typeof(WindblownLizardPrefabPatch),"Prefix"));
                CreateObject create=new CreateObject{gameObject=new FsmGameObject{Value=lizard}};create.OnEnter();
                Check(create.NativeCalls==1,"Real Harmony prefab observation leaves native object creation running");
                Check(ReferenceEquals(typeof(WindblownLizards).GetField("prefab",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null),lizard),"Real Harmony captures the native edible lizard asset reference");
                WindblownLizards.Reset();
                storm.UnpatchSelf(); action.DoSetVelocity();
                Check(pursuer.GetComponent<Rigidbody>().velocity.z == 10, "Removing storm patches restores original NPCAI movement");
                sleeper.SetState("SleepInCar");timeline.DoMethodCall();
                Check(sleeper.ActiveStateName=="SleepInCar"&&timeline.NativeCalls==4,"Removing storm patches also restores unmodified native sleep behavior");
                fog.OnRenderImage();Check(fog.FrustumResults[0].z==10,"Removing storm patches restores original Azure frustum transforms");StormView.Clear();
                Console.WriteLine(count + " real-Harmony fog/sleep/movement compatibility checks passed against the installed loader library and controlled native/NPCAI-shaped fixtures.");
            }
            finally { storm.UnpatchSelf(); npc.UnpatchSelf(); }
        }
    }
}
