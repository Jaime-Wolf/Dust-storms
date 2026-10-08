using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ApocaDustStorm
{
    internal static class NativeStormClock
    {
        private static readonly FieldInfo dayLength = typeof(AzureTimeController).GetField("m_dayLength", BindingFlags.Instance | BindingFlags.NonPublic);
        private static AzureTimeController controller;
        private static readonly StormClock clock = new StormClock();
        internal static void Scan()
        { if (controller == null) controller = UnityEngine.Object.FindObjectOfType<AzureTimeController>(); }
        internal static float NightStrength
        {
            get
            {
                if (controller == null) return 0;
                float elevation = controller.GetSunElevation();
                if (float.IsNaN(elevation) || float.IsInfinity(elevation)) return 0;
                // Installed Azure reports dot(-sun.forward, up), in [-1,1].
                return (float)StormModel.Smooth(-elevation / 0.15);
            }
        }
        internal static double Step(double seconds)
        {
            if (controller == null || dayLength == null) return clock.Step(seconds, double.NaN, 0);
            Vector3Int date = controller.GetDate();
            if (date.x < 1 || date.x > 9999 || date.y < 1 || date.y > 12 || date.z < 1 || date.z > DateTime.DaysInMonth(date.x, date.y))
                return clock.Step(seconds, double.NaN, 0);
            double hours = new DateTime(date.x, date.y, date.z).Ticks / (double)TimeSpan.TicksPerHour + controller.GetTimeline();
            double length = (float)dayLength.GetValue(controller);
            // Verified in installed AzureTimeController.GetTimeProgressionStep.
            double rate = length > 0 ? 0.4 / length : 0;
            return clock.Step(seconds, hours, rate);
        }
        internal static void Reset() { controller = null; clock.Reset(); }
    }
}
