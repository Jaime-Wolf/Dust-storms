using System;
using UnityEngine;
using UnityEngine.UI;
namespace ApocaDustStorm
{
    internal static class ExposureHud
    {
        private static Texture2D edges, shelter, vehicle, exposed;
        private static float edgeAmount;
        private static StormCover state;
        private static RectTransform compass;
        private static float nextLookup;
        private static readonly Vector3[] corners = new Vector3[4];
        internal static void Tick(float dt, float strength)
        {
            state = PlayerStormHazards.State(strength);
            float target = (float)ExposureHudMath.EdgeTarget(state, strength, PlayerStormHazards.VehicleProtection);
            if (dt > 0 && !float.IsNaN(dt) && !float.IsInfinity(dt))
                edgeAmount = Mathf.Lerp(edgeAmount, target, 1 - Mathf.Exp(-Mathf.Min(dt, 0.2f) / 0.8f));
        }
        internal static void Hide() { state = StormCover.None; edgeAmount = 0; }
        internal static void Clear()
        {
            Hide(); compass = null; nextLookup = 0;
            foreach (Texture2D texture in new Texture2D[] { edges, shelter, vehicle, exposed })
                if (texture != null) UnityEngine.Object.Destroy(texture);
            edges = shelter = vehicle = exposed = null;
        }
        private static Texture2D Texture(string name, int width, int height, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "ApocaDustStorm." + name; texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels); texture.Apply(false, true);
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        private static void EnsureTextures()
        {
            if (edges != null) return;
            const int width = 256, height = 144;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                pixels[y * width + x] = new Color32(169, 130, 82, (byte)(255 * ExposureHudMath.EdgeMask((x + 0.5) / width, (y + 0.5) / height)));
            edges = Texture("ExposureRim", width, height, pixels);
            shelter = MakeIcon(StormCover.Shelter); vehicle = MakeIcon(StormCover.Vehicle); exposed = MakeIcon(StormCover.Exposed);
        }
        private static double Segment(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay, t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
            return Math.Sqrt((x - ax - t * dx) * (x - ax - t * dx) + (y - ay - t * dy) * (y - ay - t * dy));
        }
        private static bool Line(double x, double y, double ax, double ay, double bx, double by)
        { return Segment(x, y, ax, ay, bx, by) < 1.6; }
        private static bool Glyph(StormCover mode, double x, double y)
        {
            if (mode == StormCover.Shelter)
                return Line(x,y,14,32,32,17) || Line(x,y,32,17,50,32) || Line(x,y,19,30,19,45) ||
                    Line(x,y,45,30,45,45) || Line(x,y,19,45,45,45) || Line(x,y,28,45,28,35) ||
                    Line(x,y,28,35,36,35) || Line(x,y,36,35,36,45);
            if (mode == StormCover.Vehicle)
                return Line(x,y,14,38,50,38) || Line(x,y,14,38,17,29) || Line(x,y,17,29,23,29) ||
                    Line(x,y,23,29,27,21) || Line(x,y,27,21,39,21) || Line(x,y,39,21,45,29) ||
                    Line(x,y,45,29,49,30) || Line(x,y,49,30,50,38) || Line(x,y,23,29,45,29) ||
                    Math.Abs(Math.Sqrt((x-22)*(x-22)+(y-39)*(y-39))-4)<1.5 ||
                    Math.Abs(Math.Sqrt((x-43)*(x-43)+(y-39)*(y-39))-4)<1.5;
            return Line(x,y,14,23,42,23) || Line(x,y,42,23,48,19) || Line(x,y,14,32,48,32) ||
                Line(x,y,48,32,51,28) || Line(x,y,14,41,36,41) || Line(x,y,36,41,44,45);
        }
        internal static Texture2D MakeIcon(StormCover mode)
        {
            const int n = 64; Color32[] pixels = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                // Coordinates face down like GUI; texture pixel storage faces up.
                double radius = Math.Max(Math.Abs(x - 31.5), Math.Abs(y - 31.5));
                if (radius > 30) continue;
                int grain = (x * 13 + y * 29 + x * y * 3) % 17;
                Color32 color = radius > 26 ? new Color32((byte)(97+grain),(byte)(62+grain/2),(byte)(37+grain/3),220) : new Color32(27,23,18,185);
                if (radius > 28.5) color = new Color32(39,30,22,190);
                if (((x<10 || x>53) && (y<10 || y>53)) && ((x-7)*(x-7)+(y-7)*(y-7)<5 || (x-56)*(x-56)+(y-7)*(y-7)<5 || (x-7)*(x-7)+(y-56)*(y-56)<5 || (x-56)*(x-56)+(y-56)*(y-56)<5)) color = new Color32(167,140,105,230);
                if (Glyph(mode, x, y)) color = mode == StormCover.Shelter ? new Color32(157,176,132,245) : (mode == StormCover.Vehicle ? new Color32(215,186,131,245) : new Color32(222,147,93,245));
                pixels[(n - 1 - y) * n + x] = color;
            }
            return Texture("Exposure" + mode, n, n, pixels);
        }
        private static ExposureLayout Layout()
        {
            if (compass == null && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 2;
                GameObject native = GameObject.Find("Canvas/Compass") ?? GameObject.Find("Compass");
                if (native != null) compass = native.GetComponent<RectTransform>();
            }
            float centre = Screen.width * 0.965f, top = Screen.height * 0.745f;
            if (compass != null && compass.gameObject.activeInHierarchy)
            {
                Canvas canvas = compass.GetComponentInParent<Canvas>();
                Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                float left = float.PositiveInfinity, right = float.NegativeInfinity, nativeTop = float.PositiveInfinity;
                compass.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    left=Mathf.Min(left,point.x);right=Mathf.Max(right,point.x);nativeTop=Mathf.Min(nativeTop,Screen.height-point.y);
                }
                if (!float.IsInfinity(left) && !float.IsInfinity(right) && !float.IsInfinity(nativeTop)) { centre=(left+right)*0.5f;top=nativeTop; }
            }
            return ExposureHudMath.Place(Screen.width, Screen.height, centre, top);
        }
        internal static void Draw()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || state == StormCover.None ||
                !Plugin.Active || !PlayerStormHazards.Ready || Apocasetter.GameMenu.Paused || Time.timeScale <= 0) return;
            float setting = Mathf.Clamp(Plugin.Value(Plugin.ExposureIndicators, 1), 0, 2); if (setting <= 0) return;
            EnsureTextures(); Color previous = GUI.color; int depth = GUI.depth;
            try
            {
                GUI.depth = 20; // Native inventory and other HUDs remain above the faint dust rim.
                GUI.color = new Color(1,1,1,Mathf.Clamp01(edgeAmount * setting * 0.5f));
                if (edgeAmount > 0.001f) GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height), edges);
                ExposureLayout layout = Layout(); float x=(float)layout.X,y=(float)layout.Y,size=(float)layout.Size;
                GUI.color = new Color(1,1,1,Mathf.Min(1,0.86f*setting));
                GUI.DrawTexture(new Rect(x,y,size,size), state == StormCover.Shelter ? shelter : (state == StormCover.Vehicle ? vehicle : exposed));
                float protection = state == StormCover.Shelter ? 1 : (state == StormCover.Vehicle ? PlayerStormHazards.VehicleProtection : 0);
                GUI.color = new Color(0.35f,0.27f,0.17f,0.85f); GUI.DrawTexture(new Rect(x+size*.2f,y+size*.80f,size*.6f,size*.035f),Texture2D.whiteTexture);
                GUI.color = state == StormCover.Shelter ? new Color(.62f,.70f,.52f,.9f) : new Color(.84f,.68f,.39f,.9f);
                if (protection > 0) GUI.DrawTexture(new Rect(x+size*.2f,y+size*.80f,size*.6f*protection,size*.035f),Texture2D.whiteTexture);
            }
            finally { GUI.color = previous; GUI.depth = depth; }
        }
    }
}
