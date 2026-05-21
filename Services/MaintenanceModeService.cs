using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLM_Level.Data;

namespace MLM_Level.Services
{
    public class MaintenanceModeService : IMaintenanceModeService
    {
        private const string CacheKey = "mlm:maintenance-status";
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public MaintenanceModeService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<bool> IsSiteOnlineAsync()
        {
            var snapshot = await GetSnapshotAsync();
            return string.Equals(snapshot.MaintenanceMode, "Online", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> GetSupportEmailAsync()
        {
            var snapshot = await GetSnapshotAsync();
            return snapshot.SupportEmail;
        }

        public async Task<string> GetSiteNameAsync()
        {
            var snapshot = await GetSnapshotAsync();
            return snapshot.SiteName;
        }

        public void InvalidateCache() => _cache.Remove(CacheKey);

        private async Task<MaintenanceSnapshot> GetSnapshotAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var settings = await _context.MlmSettings.AsNoTracking().FirstOrDefaultAsync();
                return new MaintenanceSnapshot
                {
                    MaintenanceMode = settings?.MaintenanceMode ?? "Online",
                    SupportEmail = settings?.SupportEmail ?? "support@mlm.com",
                    SiteName = settings?.SiteName ?? "Elite MLM"
                };
            }) ?? new MaintenanceSnapshot();
        }

        private sealed class MaintenanceSnapshot
        {
            public string MaintenanceMode { get; set; } = "Online";
            public string SupportEmail { get; set; } = "support@mlm.com";
            public string SiteName { get; set; } = "Elite MLM";
        }
    }
}
