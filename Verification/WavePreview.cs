using System;
using System.IO;
namespace ApocaDustStorm
{
    internal static class WavePreview
    {
        private static void Main(string[] args)
        {
            float[] samples = args.Length > 1 && args[1] == "static" ? DischargeSound.Generate(3118) : SandSound.Generate(37016);
            using (BinaryWriter writer = new BinaryWriter(File.Create(args[0])))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(SandSound.SampleRate); writer.Write(SandSound.SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float sample in samples) writer.Write((short)Math.Round(sample * 32767));
            }
            Console.WriteLine("Saved unmixed generated sound preview: " + args[0]);
        }
    }
}
