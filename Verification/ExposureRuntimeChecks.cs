using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.UI;
namespace ApocaDustStorm
{
    internal static class ExposureRuntimeChecks
    {
        internal static void Run(Action<bool,string> check)
        {
            PlayerStormHazards.Reset(); StormSleep.Reset(); ExposureHud.Clear(); Physics.RaycastFixture=null;
            foreach(GameObject old in UnityEngine.Object.FindObjectsOfType<GameObject>())if(old.name=="Player")old.SetActive(false);
            Plugin.Active=true; Plugin.ExposureDamage=null; Plugin.ExposureIndicators=null;
            Time.timeScale=1; Time.unscaledTime=500; Apocasetter.GameMenu.Paused=false;
            GameObject player=new GameObject("Player");
            PlayMakerFSM health=player.Add<PlayMakerFSM>();health.FsmName="Health";health.ActiveStateName="playerHealth";
            PlayMakerFSM cab=player.Add<PlayMakerFSM>();cab.FsmName="InCar";cab.ActiveStateName="InCar";
            PlayMakerFSM sleep=player.Add<PlayMakerFSM>();SleepFixtures.Configure(sleep);
            for(int i=0;i<1500;i++)PlayerStormHazards.Tick(player,.2f,1,false);
            check(Math.Abs(PlayerStormHazards.VehicleProtection-.25f)<.00001 && health.FsmVariables.Health.Value>61 && health.FsmVariables.Health.Value<62,"Five real cab minutes progressively lose protection without an abrupt damage jump");
            float hp=health.FsmVariables.Health.Value;cab.ActiveStateName="OnFoot";PlayerStormHazards.Tick(player,.1f,1,false);
            check(Math.Abs(hp-health.FsmVariables.Health.Value-.03f)<.0001&&Math.Abs(PlayerStormHazards.VehicleProtection-.25f)<.00001,"Hopping out retains worn cover and immediately gives full outdoor damage");
            cab.ActiveStateName="InCar";hp=health.FsmVariables.Health.Value;PlayerStormHazards.Tick(player,.1f,1,false);
            check(Math.Abs(hp-health.FsmVariables.Health.Value-.0225f)<.0001,"Re-entering the cab retains twenty-five percent protection");
            hp=health.FsmVariables.Health.Value;PlayerStormHazards.Tick(player,.1f,0,true);
            check(hp==health.FsmVariables.Health.Value&&Math.Abs(PlayerStormHazards.VehicleProtection-.25f)<.00001,"An unrelated clock jump cannot damage or reset protection");

            GameObject building=new GameObject("Building_test");Collider roof=building.Add<SphereCollider>();
            Physics.RaycastFixture=delegate(Vector3 p,Vector3 d,float distance){return d.y>.5f?new[]{new RaycastHit{collider=roof,distance=4}}:new RaycastHit[0];};
            Time.unscaledTime+=1;for(int i=0;i<600;i++)PlayerStormHazards.Tick(player,.2f,1,false);
            check(hp==health.FsmVariables.Health.Value&&Math.Abs(PlayerStormHazards.VehicleProtection-.9f)<.00001,"Proper shelter restores cab protection over two minutes and completely stops damage");

            GameObject inventory=new GameObject("WeaponSlots_UI");inventory.Add<RectTransform>();inventory.Add<Graphic>();
            GameObject compass=new GameObject("Compass");RectTransform compassRect=compass.Add<RectTransform>();
            compassRect.Corners=new[]{new Vector3(1780,120,0),new Vector3(1780,280,0),new Vector3(1900,280,0),new Vector3(1900,120,0)};
            GUI.Draws.Clear();GUI.color=new Color(.1f,.2f,.3f,.4f);GUI.depth=7;
            ExposureHud.Tick(.2f,1);ExposureHud.Draw();
            check(GUI.Draws.Count==3&&GUI.Draws[0].Texture.name=="ApocaDustStorm.ExposureShelter","Shelter draws a house badge with protection fill and no text banner");
            check(GUI.color.a==.4f&&GUI.depth==7,"Indicator drawing restores GUI tint and depth for other mods");
            check(Math.Abs(GUI.Draws[0].Area.x+GUI.Draws[0].Area.width*.5f-1840)<.001&&GUI.Draws[0].Area.y+GUI.Draws[0].Area.height<800,"Actual GUI badge centers above the native compass bounds");
            compassRect.Corners=new[]{new Vector3(1500,160,0),new Vector3(1500,320,0),new Vector3(1620,320,0),new Vector3(1620,160,0)};
            GUI.Draws.Clear();ExposureHud.Draw();
            check(Math.Abs(GUI.Draws[0].Area.x+GUI.Draws[0].Area.width*.5f-1560)<.001&&GUI.Draws[0].Area.y+GUI.Draws[0].Area.height<760,"Moving or scaling the compass updates badge position without scanning inventory graphics");
            Texture2D badge=GUI.Draws[0].Texture;
            cab.ActiveStateName="OnFoot";Physics.RaycastFixture=null;player.transform.position+=Vector3.forward*.02f;
            PlayerStormHazards.Tick(player,.1f,1,false);for(int i=0;i<30;i++)ExposureHud.Tick(.2f,1);
            GUI.Draws.Clear();ExposureHud.Draw();
            check(GUI.Draws.Count==3&&GUI.Draws[1].Texture.name=="ApocaDustStorm.ExposureExposed","Leaving cover switches the badge to the exposed wind glyph immediately");
            Texture2D rim=GUI.Draws[0].Texture;
            check(rim.Pixels[(rim.height/2)*rim.width+rim.width/2].a==0&&GUI.Draws[0].Tint.a<.501f,"The rim is transparent in the center and faint at the normal setting");
            int maximumAlpha=0;foreach(Color32 pixel in rim.Pixels)maximumAlpha=Math.Max(maximumAlpha,pixel.a);
            check(maximumAlpha<=57,"Generated edge dust remains within its opacity bound");
            cab.ActiveStateName="InCar";PlayerStormHazards.Tick(player,.1f,1,false);ExposureHud.Tick(.2f,1);GUI.Draws.Clear();ExposureHud.Draw();
            check(GUI.Draws[1].Texture.name=="ApocaDustStorm.ExposureVehicle"&&GUI.Draws[3].Area.width<GUI.Draws[2].Area.width,"Cab badge uses a separate car glyph and current protection fill");
            Plugin.ExposureIndicators=0f;hp=health.FsmVariables.Health.Value;GUI.Draws.Clear();ExposureHud.Draw();PlayerStormHazards.Tick(player,.1f,1,false);
            check(GUI.Draws.Count==0&&health.FsmVariables.Health.Value<hp,"Hiding indicators changes neither protection nor damage");Plugin.ExposureIndicators=null;
            Event.current.type=EventType.Layout;GUI.Draws.Clear();ExposureHud.Draw();check(GUI.Draws.Count==0,"GUI layout passes do not redraw or recreate textures");Event.current.type=EventType.Repaint;

            health.FsmVariables.Health.Value=100;sleep.ActiveStateName="SleepInCar";StormRunner.Strength=1;
            PlayerStormHazards.Tick(player,.1f,1,false);
            check(sleep.ActiveStateName=="Awake"&&sleep.BackEvents==1&&health.FsmVariables.Health.Value==100,"Strong dust wakes cab sleepers through their native back event without health loss");
            PlayerStormHazards.Tick(player,.1f,1,false);
            check(health.FsmVariables.Health.Value==100,"The first awake update adds no sleep or wake damage");
            PlayerStormHazards.Tick(player,.1f,1,false);
            check(health.FsmVariables.Health.Value<100&&health.FsmVariables.Health.Value>99.9,"Later awake updates resume ordinary exposure damage");
            health.FsmVariables.Health.Value=5;sleep.ActiveStateName="SleepOnFoot";
            PlayerStormHazards.Tick(player,.1f,1,false);
            check(sleep.ActiveStateName=="Enable"&&health.FsmVariables.Health.Value==5,"On-foot cancellation reaches native control restoration and does not harm or heal low HP");
            sleep.SetState("Awake");PlayerStormHazards.Tick(player,.1f,1,false);
            check(health.FsmVariables.Health.Value==5,"Low-health waking has neither a ten-HP floor nor storm penalty");

            FsmState attempt=sleep.Fsm.GetState("checkInCar"),redirect=attempt;
            StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect.Name=="Awake"&&attempt.Entries==0,"An exposed sleep attempt redirects before native checkInCar healing/rewards or time skip");
            PlayerStormHazards.Tick(player,.1f,1,false);hp=health.FsmVariables.Health.Value;
            redirect=attempt;StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect.Name=="Awake"&&health.FsmVariables.Health.Value==hp,"Repeated sleep attempts cannot run native sleep rewards or change health");
            Plugin.ExposureDamage=0f;redirect=attempt;StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect.Name=="Awake","Turning off health damage still prevents exposed sleep through hazardous dust");Plugin.ExposureDamage=null;

            sleep.ActiveStateName="SleepOnFoot";
            CallMethod timeline=new CallMethod{Owner=player,Fsm=sleep.Fsm,methodName=new FsmString{Value="SetTimeline"}};
            check(!StormSleepClockPatch.Prefix(timeline)&&timeline.Finished&&sleep.ActiveStateName=="Enable"&&timeline.NativeCalls==0,"Mid-sleep hazardous dust blocks native timeline acceleration before it can skip the storm");
            sleep.ActiveStateName="SleepInCar";sleep.Fsm.GetState("SleepInCar").Transitions[0].ToState="ChangedNativeState";
            check(!StormSleep.Interrupt(sleep,1)&&StormSleepClockPatch.Prefix(timeline),"Changed native wake layout is left untouched rather than trapping player controls");
            sleep.Fsm.GetState("SleepInCar").Transitions[0].ToState="Awake";
            timeline.methodName.Value="GetTimeline";check(StormSleepClockPatch.Prefix(timeline),"Read-only native timeline queries always run");
            timeline.methodName.Value="SetTimeline";timeline.Fsm=new Fsm{Name="Other"};check(StormSleepClockPatch.Prefix(timeline),"Non-sleep timeline calls are untouched");timeline.Fsm=sleep.Fsm;
            timeline.Owner=building;check(StormSleepClockPatch.Prefix(timeline),"A method call from another owner cannot cancel player sleep");timeline.Owner=player;
            check(StormSleepClockPatch.Prefix(null),"Absent method actions safely retain native behavior");
            GameObject npc=new GameObject("Sleeping AI");PlayMakerFSM npcSleep=npc.Add<PlayMakerFSM>();SleepFixtures.Configure(npcSleep);
            check(!StormSleep.Dangerous(npcSleep,1),"AI sleep and health are excluded from sleep interruption");
            Plugin.Active=false;redirect=attempt;StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect==attempt&&!StormSleep.Interrupt(sleep,1),"Disabled storms preserve native sleep entry and active sleep");Plugin.Active=true;
            StormRunner.Strength=.3f;redirect=attempt;StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect==attempt&&!StormSleep.Interrupt(sleep,.3f),"Calm weather and harmless residual dust permit sleep");
            StormRunner.Model.Begin(0,0,0,900,90,75,1);StormRunner.Strength=.01f;redirect=attempt;
            StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);
            check(redirect!=attempt,"An active storm's gradual approach cannot be bypassed by starting exposed sleep");StormRunner.Model.Reset();StormRunner.Strength=1;
            Apocasetter.GameMenu.Paused=true;check(!StormSleep.Interrupt(sleep,1),"Paused menus cannot interrupt sleepers");Apocasetter.GameMenu.Paused=false;
            Time.timeScale=0;check(!StormSleep.Interrupt(sleep,1),"Frozen game time does not wake sleepers");Time.timeScale=1;
            Apocasetter.GameMenu.InGame=false;check(!StormSleep.Interrupt(sleep,1),"Menu scenes cannot intercept sleep");Apocasetter.GameMenu.InGame=true;
            check(!StormSleep.Interrupt(sleep,float.NaN)&&!StormSleep.Interrupt(sleep,float.PositiveInfinity),"Invalid storm strength cannot wake players");
            health.ActiveStateName="playerDeath";check(!StormSleep.Interrupt(sleep,1),"Dead player states are untouched");health.ActiveStateName="playerHealth";
            health.FsmVariables.Health.Value=float.NaN;check(!StormSleep.Interrupt(sleep,1),"Invalid health is never written or used to force wake");health.FsmVariables.Health.Value=80;
            sleep.enabled=false;check(!StormSleep.Interrupt(sleep,1),"Disabled native Sleep components are untouched");sleep.enabled=true;
            Physics.RaycastFixture=delegate(Vector3 p,Vector3 d,float distance){return d.y>.5f?new[]{new RaycastHit{collider=roof,distance=4}}:new RaycastHit[0];};
            Time.unscaledTime+=1;health.FsmVariables.Health.Value=80;sleep.ActiveStateName="SleepInCar";PlayerStormHazards.Tick(player,.1f,1,false);
            check(sleep.ActiveStateName=="SleepInCar"&&PlayerStormHazards.State(1)==StormCover.None,"Proper shelter allows uninterrupted sleep and hides exposure indicators");
            redirect=attempt;StormSleepEntryPatch.Prefix(sleep.Fsm,ref redirect);timeline.Finished=false;
            check(redirect==attempt&&StormSleepClockPatch.Prefix(timeline)&&!timeline.Finished,"Sheltered native sleep entry and time acceleration remain allowed");
            PlayerStormHazards.AdvanceShelteredSleep(1200);
            health.FsmVariables.Health.Value=95;sleep.ActiveStateName="Awake";PlayerStormHazards.Tick(player,.1f,0,true);
            check(health.FsmVariables.Health.Value==95&&Math.Abs(PlayerStormHazards.VehicleProtection-.9f)<.00001,"Sheltered sleeping preserves native healing and gradually restores protection");
            Physics.RaycastFixture=null;Time.unscaledTime+=1;sleep.ActiveStateName="SleepOnFoot";PlayerStormHazards.Tick(player,.1f,.3f,false);
            PlayerStormHazards.AdvanceShelteredSleep(1200);sleep.ActiveStateName="Awake";PlayerStormHazards.Tick(player,.1f,0,true);
            check(health.FsmVariables.Health.Value==95,"Ordinary clock jumps and harmless sleep never apply accumulated health loss");
            StormSleep.Reset();check(!StormSleep.ConsumeWake(player),"Scene cleanup clears pending wake state");
            ExposureHud.Clear();GUI.Draws.Clear();ExposureHud.Draw();check(badge.Destroyed&&rim.Destroyed&&GUI.Draws.Count==0,"HUD cleanup releases its own textures and stops drawing");
            PlayerStormHazards.Reset();Physics.RaycastFixture=null;UnityEngine.Object.Destroy(inventory);UnityEngine.Object.Destroy(compass);GUI.color=Color.white;GUI.depth=0;
        }
    }
}
