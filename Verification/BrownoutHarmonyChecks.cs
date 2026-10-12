using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
namespace ApocaDustStorm
{
    internal static class BrownoutHarmonyChecks
    {
        internal static void Run(Harmony harmony,Action<bool,string> check)
        {
            StormBrownout.Clear();Plugin.Active=true;PlayerStormHazards.Sheltered=false;
            Camera view=new GameObject("Harmony grading gameplay").Add<Camera>();StormRunner.View=view;
            PostProcessLayer layer=view.gameObject.Add<PostProcessLayer>();layer.volumeLayer=new LayerMask{value=1<<8};
            Camera menu=new GameObject("Harmony grading menu").Add<Camera>();PostProcessLayer other=menu.gameObject.Add<PostProcessLayer>();other.volumeLayer=layer.volumeLayer;
            PostProcessProfile native=ScriptableObject.CreateInstance<PostProcessProfile>();ColorGrading nativeGrade=native.AddSettings<ColorGrading>();nativeGrade.saturation.Override(17);
            PostProcessVolume nativeVolume=new GameObject("Native fixture volume").Add<PostProcessVolume>();nativeVolume.sharedProfile=native;nativeVolume.weight=.4f;
            StormBrownout.Tick(view,.75f);
            PostProcessVolume owned=(PostProcessVolume)typeof(StormBrownout).GetField("volume",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            harmony.Patch(AccessTools.Method(typeof(PostProcessLayer),"UpdateVolumeSystem"),prefix:new HarmonyMethod(typeof(StormBrownoutPatch),"Prefix"),finalizer:new HarmonyMethod(typeof(StormBrownoutPatch),"Finalizer"));
            layer.UpdateVolumeSystem(view,null);check(Math.Abs(layer.Observed-.75f)<.00001&&owned.weight==0,"Real Harmony scopes PPv2 grading to active view blending and restores weight");
            other.UpdateVolumeSystem(menu,null);check(other.Observed==0&&owned.weight==0,"Real Harmony excludes another camera sharing the same post-processing layer mask");
            layer.ThrowOnce=true;bool thrown=false;try{layer.UpdateVolumeSystem(view,null);}catch(Exception){thrown=true;}
            check(thrown&&owned.weight==0,"Real Harmony finalizer restores grading after a native blending exception without swallowing it");
            Plugin.Active=false;layer.UpdateVolumeSystem(view,null);check(layer.Observed==0,"Disabled mod preserves native PPv2 processing");Plugin.Active=true;
            check(nativeVolume.weight==.4f&&nativeGrade.saturation.value==17&&nativeVolume.sharedProfile==native,"Camera grading hooks never modify the native/shared profile or volume");
            StormBrownout.Clear();check(!native.Destroyed&&!nativeGrade.Destroyed,"Owned-profile cleanup leaves native settings alive");
        }
    }
}
