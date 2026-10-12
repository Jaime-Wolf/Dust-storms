using UnityEngine;

namespace ApocaDustStorm
{
    // The distant front must not use the observer's altitude as its ground plane.
    // MapMagic's Unity terrains expose heights before their physics is ready.
    internal sealed class StormTerrainSampler : IDustPickupWorld
    {
        private Terrain[] terrains;
        private float nextScan;
        private int rayBudget;
        private Vector3 eye;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        internal void Prepare(Vector3 observer)
        {
            eye=observer; rayBudget=24;
            if (terrains==null || Time.unscaledTime>=nextScan)
            { terrains=Terrain.activeTerrains; nextScan=Time.unscaledTime+1; }
        }
        internal void Reset() { terrains=null; nextScan=0; }
        public bool Ground(Vector3 location, out Vector3 point)
        {
            point=location; bool found=false; float highest=float.MinValue;
            if (terrains!=null) foreach (Terrain terrain in terrains)
            {
                if (terrain==null || !terrain.isActiveAndEnabled || terrain.terrainData==null) continue;
                TerrainData data=terrain.terrainData; Vector3 origin=terrain.transform.position, size=data.size;
                float x=location.x-origin.x, z=location.z-origin.z;
                if (size.x<=0 || size.z<=0 || x<0 || z<0 || x>size.x || z>size.z) continue;
                float height=origin.y+data.GetInterpolatedHeight(x/size.x,z/size.z);
                if (float.IsNaN(height) || float.IsInfinity(height)) continue;
                if (!found || height>highest) { highest=height; found=true; }
            }
            if (found) { point.y=highest; return true; }
            // Bounded fallback for native terrain colliders not yet in the tile list.
            // Starting well above the observer covers both ridges and deep valleys.
            if (rayBudget--<=0) return false;
            Vector3 start=new Vector3(location.x,eye.y+2000,location.z);
            int n=Physics.RaycastNonAlloc(start,Vector3.down,hits,6000,~0,QueryTriggerInteraction.Ignore);
            float best=float.MaxValue;
            for (int i=0;i<n;i++)
            {
                Collider collider=hits[i].collider;
                if (!(collider is TerrainCollider) || collider.attachedRigidbody!=null || hits[i].normal.y<=0) continue;
                if (hits[i].distance<best) { point=hits[i].point; best=hits[i].distance; found=true; }
            }
            return found;
        }
        public bool OpenSky(Vector3 point) { return true; } // Not used by the front.
    }
}
