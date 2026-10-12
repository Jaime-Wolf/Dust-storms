using System.Threading;
using System.Threading.Tasks;
namespace ApocaDustStorm
{
    // Bounded process-lifetime cache. Only pure sample math runs on the worker;
    // AudioClip creation/upload remains on Unity's main thread.
    internal static class DischargeSamples
    {
        private static Task<float[][]> job;
        internal static void Start()
        {
            if (job != null) return;
            job = Task.Factory.StartNew(delegate
            {
                float[][] result = new float[3][];
                for (int i = 0; i < result.Length; i++) result[i] = DischargeSound.Generate(3118 + i * 97);
                return result;
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }
        internal static System.Exception Failure;
        internal static bool TryGet(out float[][] samples)
        {
            Start(); samples = null;
            if (!job.IsCompleted) return false;
            if (job.IsFaulted || job.IsCanceled) { Failure = job.Exception; return false; }
            samples = job.Result; return true;
        }
    }
}
