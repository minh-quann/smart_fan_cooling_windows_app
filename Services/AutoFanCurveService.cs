using System;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Calculates fan PWM percentage based on target temperature and fan curve presets.
    /// </summary>
    public static class AutoFanCurveService
    {
        public static int CalculatePwm(float maxTemp, string mode)
        {
            return mode switch
            {
                "Quiet" => maxTemp switch
                {
                    < 40 => 20,
                    < 60 => 35,
                    < 75 => 55,
                    _ => 75
                },
                "Balanced" => maxTemp switch
                {
                    < 40 => 30,
                    < 60 => 50,
                    < 75 => 75,
                    _ => 90
                },
                "Turbo" => maxTemp switch
                {
                    < 40 => 50,
                    < 60 => 80,
                    _ => 100
                },
                _ => (int)Math.Clamp((maxTemp - 30) * 2, 20, 100) // Custom Dynamic Linear Curve
            };
        }
    }
}
