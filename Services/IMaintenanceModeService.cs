using System.Threading.Tasks;

namespace MLM_Level.Services
{
    public interface IMaintenanceModeService
    {
        Task<bool> IsSiteOnlineAsync();
        Task<string> GetSupportEmailAsync();
        Task<string> GetSiteNameAsync();
        void InvalidateCache();
    }
}
