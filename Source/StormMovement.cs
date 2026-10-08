using HarmonyLib;
using HutongGames.PlayMaker.Actions;
namespace ApocaDustStorm
{
    // Scale freshly-read native player input; do not retain changes to walk/run speeds,
    // jump/gravity values, rigidbody velocity, ladder movement or god-mode flight.
    [HarmonyPatch(typeof(GetAxisKeyAxis), "DoGetAxis")]
    internal static class PlayerStormMovementPatch
    {
        internal static void Postfix(GetAxisKeyAxis __instance)
        {
            if (__instance.Owner != PlayerStormHazards.Player || __instance.Fsm.Name != "Movement" || __instance.store == null || __instance.store.IsNone) return;
            __instance.store.Value *= PlayerStormHazards.MovementGain(StormRunner.Strength, StormRunner.Gust);
        }
    }
}
