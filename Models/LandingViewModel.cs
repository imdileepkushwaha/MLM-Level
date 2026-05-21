namespace MLM_Level.Models;

public class LandingViewModel
{
    public MlmSetting Settings { get; set; } = new();
    public decimal StarterPackagePrice { get; set; } = 1000m;
    public List<Package> ActivePackages { get; set; } = new();
}
