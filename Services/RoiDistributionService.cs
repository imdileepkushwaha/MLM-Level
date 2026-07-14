using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Services
{
    public class RoiDistributionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RoiDistributionService> _logger;

        public RoiDistributionService(IServiceProvider serviceProvider, ILogger<RoiDistributionService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Auto ROI Distribution Service started.");

            // Let startup DB initialization finish before the first sweep.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

            await ProcessRoiPayouts();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessRoiPayouts();
            }
        }

        private async Task ProcessRoiPayouts()
        {
            try
            {
                // Create a new scope to resolve scoped DbContext
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Calculate the start of "Today" in IST, then convert that to UTC
                var nowIst = DateTime.UtcNow.AddHours(5).AddMinutes(30);
                var startOfTodayIst = nowIst.Date;
                var startOfTodayUtc = startOfTodayIst.AddHours(-5).AddMinutes(-30);

                var activeUserPackages = await context.UserPackages
                    .Include(up => up.User)
                    .Include(up => up.Package)
                    .Where(up => up.IsActive && up.DaysPaid < up.RoiDurationDays && (up.LastPaidDate == null || up.LastPaidDate < startOfTodayUtc))
                    .ToListAsync();

                int processedCount = 0;
                decimal totalDistributed = 0;

                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "EXEC sp_DistributeDailyROI";
                    command.CommandType = System.Data.CommandType.Text;
                    
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                        await command.Connection.OpenAsync();

                    using (var result = await command.ExecuteReaderAsync())
                    {
                        if (await result.ReadAsync())
                        {
                            processedCount = result.GetInt32(0);
                            totalDistributed = result.GetDecimal(1);
                        }
                    }
                }

                if (processedCount > 0)
                {
                    _logger.LogInformation("Auto ROI: Successfully distributed ₹{Amount} to {Count} packages.", totalDistributed, processedCount);
                }
                else
                {
                    _logger.LogInformation("Auto ROI: No pending packages found during processing.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto ROI: Critical error in background service.");
            }
        }
    }
}
