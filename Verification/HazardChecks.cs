using System;
namespace ApocaDustStorm
{
    internal static class HazardChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            double damage = 0, protectedDamage = 0;
            check(Math.Abs(StormHazardModel.Damage(0.1, 1, false, false, 1) - 0.03) < 0.000001, "Dangerous outdoor dust damages on the very first frame without a grace timer");
            for (int i = 0; i < 600; i++)
            { damage += StormHazardModel.Damage(0.1, 1, false, false, 1); protectedDamage += StormHazardModel.Damage(0.1, 1, false, true, 1); }
            check(Math.Abs(damage - 18) < 0.00001, "The first full minute of peak outdoor dust deals 18 native health");
            check(Math.Abs(protectedDamage - 1.8) < 0.00001, "Vehicle reduces exposure damage by exactly ninety percent");
            check(StormHazardModel.Damage(0.1, 1, true, false, 1) == 0 && StormHazardModel.Damage(0.1, 1, true, true, 1) == 0, "Full shelter immediately stops both outdoor and vehicle damage");
            check(Math.Abs(StormHazardModel.Damage(0.1, 1, false, false, 1) - 0.03) < 0.000001, "Leaving shelter immediately resumes outdoor damage");
            check(StormHazardModel.Damage(0, 1, false, false, 1) == 0 && StormHazardModel.Damage(-1, 1, false, false, 1) == 0, "Paused or reversed time cannot damage health");
            check(StormHazardModel.Damage(double.NaN, 1, false, false, 1) == 0 && StormHazardModel.Damage(double.PositiveInfinity, 1, false, false, 1) == 0, "Invalid elapsed time cannot inflict damage");
            check(StormHazardModel.Damage(0.1, double.NaN, false, false, 1) == 0 && StormHazardModel.Damage(0.1, 1, false, false, double.PositiveInfinity) == 0, "Invalid strength or damage settings cannot corrupt health");
            check(Math.Abs(StormHazardModel.Damage(900, 1, false, false, 1) - 0.06) < 0.000001, "Large frame steps are bounded instead of applying accumulated sleep damage");
            check(StormHazardModel.Damage(0.2, 0.3, false, false, 1) == 0, "Light dust causes no damage");
            check(Math.Abs(StormHazardModel.Damage(0.1, 0.6, false, false, 1) - 0.015) < 0.000001, "Partial storm strength immediately applies a gentler damage rate");
            check(StormHazardModel.Damage(0.1, 1, false, false, 0) == 0, "Damage slider zero disables exposure damage");
            check(Math.Abs(StormHazardModel.Damage(0.1, 1, false, false, 1) / StormHazardModel.Damage(0.1, 1, false, true, 1) - 10) < 0.000001, "Leaving a vehicle immediately restores the full outdoor damage rate");
            VehicleProtectionModel protection = new VehicleProtectionModel();
            check(Math.Abs(protection.Protection - 0.9) < 0.000001, "Fresh vehicle protection starts at ninety percent");
            for (int i = 0; i < 1500; i++) protection.Step(0.1, true, false, true);
            check(Math.Abs(protection.Protection - 0.575) < 0.000001, "Half of five minutes gives fifty-seven-and-a-half percent cab protection");
            for (int i = 0; i < 1500; i++) protection.Step(0.1, true, false, true);
            check(Math.Abs(protection.Protection - 0.25) < 0.000001, "Five hazardous cab minutes reaches twenty-five percent protection");
            for (int i = 0; i < 100; i++) protection.Step(0.2, true, false, true);
            check(Math.Abs(protection.Protection - 0.25) < 0.000001, "Longer storms cannot reduce vehicle protection below twenty-five percent");
            check(Math.Abs(StormHazardModel.Damage(0.1, 1, false, true, 1, protection.Protection) * 600 - 13.5) < 0.000001, "Worn vehicle peak damage is thirteen-and-a-half health per minute");
            for (int i = 0; i < 1000; i++) { protection.Step(0.1, true, false, false); protection.Step(0.1, false, false, true); }
            check(Math.Abs(protection.Protection - 0.25) < 0.000001, "On-foot time and clear weather hold wear without restoring cab protection");
            for (int i = 0; i < 600; i++) protection.Step(0.1, true, true, false);
            check(Math.Abs(protection.Protection - 0.575) < 0.000001, "One minute in shelter gradually restores half the protection range");
            for (int i = 0; i < 600; i++) protection.Step(0.1, true, true, true);
            check(Math.Abs(protection.Protection - 0.9) < 0.000001, "Two sheltered minutes fully restore protection even in a parked cab");
            protection.Step(0, true, false, true); protection.Step(double.NaN, true, false, true);
            check(Math.Abs(protection.Protection - 0.9) < 0.000001, "Pause and invalid time do not wear protection");
            protection.Step(900, true, false, true);
            check(protection.Protection > 0.899, "Unexpected huge ordinary frame does not fast-forward cab wear");
            check(ExposureHudMath.EdgeMask(.5,.5) == 0 && ExposureHudMath.EdgeMask(.85,.5) == 0, "Exposure rim leaves the central view transparent");
            check(ExposureHudMath.EdgeMask(0,.5) > 0 && ExposureHudMath.EdgeMask(0,.5) < .22, "Screen-edge dust has a bounded faint mask");
            check(ExposureHudMath.EdgeTarget(StormCover.Shelter,1,.25)==0 && ExposureHudMath.EdgeTarget(StormCover.Vehicle,1,.9)<ExposureHudMath.EdgeTarget(StormCover.Vehicle,1,.25), "Shelter clears the rim and worn cab protection makes it stronger");
            ExposureLayout layout = ExposureHudMath.Place(1920,1080,1840,800);
            check(Math.Abs(layout.X+layout.Size*.5-1840)<.0001&&layout.Y+layout.Size<800,"Icon is centered directly above compass without covering it");
            layout = ExposureHudMath.Place(800,600,900,-30);
            check(layout.X >= 8 && layout.X + layout.Size <= 792 && layout.Y >= 8 && layout.Y + layout.Size <= 592, "Indicator stays within small or unusual screen bounds");
            check(Math.Abs(StormHazardModel.MovementGain(1, 1, 1, 1) - 0.7) < 0.000001, "Player and AI share thirty-percent peak movement resistance");
            check(StormHazardModel.MovementGain(0.3, 1, 1, 1) == 1 && StormHazardModel.MovementGain(1, 1, 1, 0) == 1, "Calm weather and zero movement slider preserve native speed");
            check(Math.Abs(StormHazardModel.MovementGain(1, 1, 2, 2) - 0.4) < 0.000001, "Maximum movement setting never immobilizes actors");
            check(StormHazardModel.MovementGain(1, 0, 1, 1) > 0.86 && StormHazardModel.MovementGain(1, 0, 1, 1) < 0.87, "Lulls ease movement resistance smoothly");
            double previous = 1, maximumStep = 0;
            for (int i = 0; i <= 1000; i++)
            { double gain = StormHazardModel.MovementGain(i / 1000.0, 1, 1, 1); maximumStep = Math.Max(maximumStep, Math.Abs(previous - gain)); previous = gain; }
            check(maximumStep < 0.001, "Movement resistance builds smoothly with the existing storm transition");
            WindLootSchedule loot = new WindLootSchedule();
            check(WindLootSchedule.RollWait(0) == 90 && WindLootSchedule.RollWait(1) == 180, "Occasional edible lizards are spaced ninety to one hundred eighty seconds apart");
            bool spawn = false;
            for (int i = 0; i < 2000; i++) spawn |= loot.Tick(0.1, false, delegate { return 0; });
            check(!spawn && loot.Count == 0, "Calm/sheltered periods cannot produce lizards");
            for (int i = 0; i < 899; i++) spawn |= loot.Tick(0.1, true, delegate { return 0; });
            check(!spawn, "A new storm cannot immediately dispense food");
            for (int i = 0; i < 5; i++) spawn |= loot.Tick(0.1, true, delegate { return 0; });
            check(spawn, "Heavy outdoor weather eventually permits an occasional lizard"); loot.Spawned();
            spawn = false; for (int i = 0; i < 905; i++) spawn |= loot.Tick(0.1, true, delegate { return 0; });
            check(spawn, "Second lizard requires another full heavy-weather wait"); loot.Spawned();
            spawn = false; for (int i = 0; i < 5000; i++) spawn |= loot.Tick(0.2, true, delegate { return 0; });
            check(!spawn && loot.Count == 2, "Per-storm food limit prevents repeated food farming"); loot.Reset();
            check(loot.Count == 0 && !loot.Tick(900, true, delegate { return 0; }), "Storm reset and sleep-sized time step do not cause a spawn burst");
        }
    }
}
