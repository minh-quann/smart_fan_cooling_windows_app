namespace SmartFanCooling.Services.Interfaces
{
    public interface IAutoFanCurveService
    {
        int CalculatePwm(float maxTemp, string mode);
    }
}
