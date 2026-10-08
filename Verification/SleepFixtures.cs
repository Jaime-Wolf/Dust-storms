using HutongGames.PlayMaker;
namespace ApocaDustStorm
{
    // Shape extracted from the installed level1 Player/Sleep FSM. Unity executes the
    // actual EnableFSM/NextFrameEvent actions; this harness checks routing only.
    internal static class SleepFixtures
    {
        private static FsmState State(string name,string eventName=null,string destination=null)
        {return new FsmState{Name=name,Transitions=eventName==null?new FsmTransition[0]:new[]{new FsmTransition{FsmEvent=new FsmEvent{Name=eventName},ToState=destination}}};}
        internal static void Configure(PlayMakerFSM sleeper)
        {
            sleeper.FsmName="Sleep";sleeper.ActiveStateName="Awake";
            sleeper.Fsm.States=new[]{State("Awake"),State("checkInCar"),State("SleepOnFoot","back","Enable"),State("SleepInCar","back","Awake"),State("reset"),State("reset 2"),State("Enable","FINISHED","Awake")};
        }
    }
}
