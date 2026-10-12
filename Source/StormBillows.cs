using System;
using UnityEngine;

namespace ApocaDustStorm
{
    // Animated wisps fill the last few metres as the distant front fades out.
    // World simulation lets the player move through them rather than carrying
    // a translucent screen overlay around. Existing fine streaks remain separate.
    internal sealed class StormBillows : IDisposable
    {
        private readonly GameObject owner;
        private readonly ParticleSystem system;
        private readonly ParticleSystemRenderer renderer;
        private readonly DustBillowAssets assets;
        private readonly System.Random random = new System.Random();
        private float budget;
        private Vector3 lastEye;
        private bool visible, hasEye;
        internal StormBillows(Transform parent)
        {
            try
            {
                owner=new GameObject("ApocaDustStorm.LocalBillows"); owner.hideFlags=HideFlags.HideAndDontSave; owner.transform.SetParent(parent,false);
                assets=new DustBillowAssets(owner,3,1.5f,7);
                system=owner.AddComponent<ParticleSystem>(); renderer=system.GetComponent<ParticleSystemRenderer>();
                DustBillowAssets.Configure(system,renderer,144); renderer.sharedMaterial=assets.Material;
                ParticleSystem.MainModule main=system.main; main.simulationSpace=ParticleSystemSimulationSpace.World;
                ParticleSystem.ColorOverLifetimeModule fade=system.colorOverLifetime; fade.enabled=true;
                Gradient gradient=new Gradient(); gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(1,.18f),new GradientAlphaKey(.8f,.65f),new GradientAlphaKey(0,1)});
                fade.color=new ParticleSystem.MinMaxGradient(gradient);
                system.Play();
            }
            catch { Dispose(); throw; }
        }
        private float Range(float low,float high) { return low+(float)random.NextDouble()*(high-low); }
        private static bool Interior(Vector3 eye)
        { return PlayerStormHazards.Sheltered || (PlayerStormHazards.InVehicle && StormRunner.OriginAnchor!=null && (eye-StormRunner.OriginAnchor.position).sqrMagnitude<4); }
        internal void Tick(Vector3 eye,float dt,float strength,float gust,StormModel model,bool entered)
        {
            if (StormRunner.View!=null) eye=StormView.Eye(StormRunner.View);
            float amount=Mathf.Clamp(Plugin.Value(Plugin.Dust,1),0,2)*Mathf.Clamp(Plugin.Value(Plugin.MicroDust,1),0,2);
            float arrival=(float)StormModel.Smooth((200-model.SignedDistance(eye.x,eye.z))/360);
            visible=Plugin.Active && amount>0 && strength>.01f && arrival>0 && !Interior(eye);
            if (!visible) { Clear(); return; }
            if (hasEye && (lastEye-eye).sqrMagnitude>62500) system.Clear();
            lastEye=eye; hasEye=true;
            float impact=Mathf.Clamp01(StormRunner.ImpactStrength);
            int occupied=system.particleCount;
            budget=DustEmissionBudget.Add(budget,14*amount*arrival*Mathf.Clamp01(strength*2)*(1+impact),dt,occupied,144,10);
            int count=Mathf.Min((int)budget,Mathf.Max(0,144-occupied)); budget-=count;
            if (entered) count=Mathf.Min(144-occupied,count+8);
            Vector3 wind=new Vector3((float)model.WindX,0,(float)model.WindZ), cross=new Vector3(-wind.z,0,wind.x);
            Color color=StormFog.DustColour(RenderSettings.fogColor,StormRunner.LightingStrength,NativeStormClock.NightStrength); color.a=.24f+.10f*impact;
            for (int i=0; i<count; i++)
            {
                ParticleSystem.EmitParams p=new ParticleSystem.EmitParams();
                p.position=eye-wind*Range(18,48)+cross*Range(-32,32)+Vector3.up*Range(-2,12);
                p.velocity=wind*Mathf.Lerp(7,15,Mathf.Clamp01(gust))+Vector3.up*Range(.1f,1.1f);
                p.startSize=Range(9,22); p.startLifetime=Range(4,7); p.startColor=color; p.rotation=Range(0,360);
                system.Emit(p,1);
            }
        }
        internal void PreCull(Camera camera)
        {
            if (renderer==null) return;
            renderer.enabled=visible && StormRunner.CanRender && camera==StormRunner.View && !Interior(StormView.Eye(camera));
            if (renderer.enabled) assets.Camera(camera,true);
            else if (!StormRunner.CanRender || camera==StormRunner.View) assets.Camera(null,false);
        }
        internal void Hide() { if (renderer!=null) renderer.enabled=false; }
        internal void Clear()
        { visible=false; budget=0; hasEye=false; Hide(); if (system!=null) system.Clear(); if (assets!=null) assets.Camera(null,false); }
        public void Dispose()
        { Clear(); if (assets!=null) assets.Dispose(); if (owner!=null) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); } }
    }
}
