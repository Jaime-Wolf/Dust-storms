using System;
using System.Drawing;
using System.Drawing.Imaging;
using UnityEngine;
namespace ApocaDustStorm
{
    internal static class ExposurePreview
    {
        private static Bitmap Badge(StormCover state)
        {
            Texture2D texture=ExposureHud.MakeIcon(state);Bitmap bitmap=new Bitmap(64,64,PixelFormat.Format32bppArgb);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {Color32 p=texture.Pixels[(63-y)*64+x];bitmap.SetPixel(x,y,System.Drawing.Color.FromArgb((int)(p.a*.86),p.r,p.g,p.b));}
            return bitmap;
        }
        private static void Main(string[] args)
        {
            using(Bitmap result=new Bitmap(640,190,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<190;y++)for(int x=0;x<640;x++)
                {double alpha=ExposureHudMath.EdgeMask(x/639.0,y/189.0)*.5;result.SetPixel(x,y,System.Drawing.Color.FromArgb(255,(int)(46*(1-alpha)+169*alpha),(int)(41*(1-alpha)+130*alpha),(int)(33*(1-alpha)+82*alpha)));}
                using(Graphics g=Graphics.FromImage(result))using(Font font=new Font("Segoe UI",11))using(Brush text=new SolidBrush(System.Drawing.Color.FromArgb(219,208,187)))
                {
                    string[] labels={"Shelter","Cab: 90%","Cab: 25%","Exposed"};StormCover[] states={StormCover.Shelter,StormCover.Vehicle,StormCover.Vehicle,StormCover.Exposed};float[] protection={1,.9f,.25f,0};
                    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    for(int i=0;i<4;i++)
                    {
                        float x=40+i*160,y=35,size=80;using(Bitmap badge=Badge(states[i]))g.DrawImage(badge,x,y,size,size);
                        using(Brush track=new SolidBrush(System.Drawing.Color.FromArgb(217,89,69,43)))g.FillRectangle(track,x+size*.2f,y+size*.80f,size*.6f,size*.035f);
                        using(Brush fill=new SolidBrush(i==0?System.Drawing.Color.FromArgb(230,158,179,133):System.Drawing.Color.FromArgb(230,214,173,99)))g.FillRectangle(fill,x+size*.2f,y+size*.80f,size*.6f*protection[i],size*.035f);
                        SizeF measure=g.MeasureString(labels[i],font);g.DrawString(labels[i],font,text,x+(size-measure.Width)/2,y+size+15);
                    }
                }
                result.Save(args[0],ImageFormat.Png);
            }
        }
    }
}
