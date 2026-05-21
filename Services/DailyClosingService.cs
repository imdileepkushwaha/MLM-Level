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
    public class DailyClosingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyClosingService> _logger;

        public DailyClosingService(IServiceProvider serviceProvider, ILogger<DailyClosingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily Closing Service started.");

            // Let startup DB initialization finish before the first sweep.
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

            await ProcessClosing();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessClosing();
            }
        }

        private async Task ProcessClosing()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Fetch settings for TDS and Admin Charge
                var settings = await context.MlmSettings.FirstOrDefaultAsync();
                if (settings == null) return;

                decimal tdsPercent = settings.TdsPercent;
                decimal adminChargePercent = settings.AdminChargePercent;

                var usersWithIncome = await context.Users
                    .Where(u => u.IncomeWallet > 0)
                    .ToListAsync();

                if (!usersWithIncome.Any())
                {
                    _logger.LogInformation("Daily Closing: No pending income balances found.");
                    return;
                }

                _logger.LogInformation("Daily Closing: Found {Count} users to process.", usersWithIncome.Count);

                int processedCount = 0;
                decimal totalProcessed = 0;

                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var user in usersWithIncome)
                    {
                        decimal grossIncome = user.IncomeWallet;
                        decimal tdsAmount = grossIncome * (tdsPercent / 100m);
                        decimal adminChargeAmount = grossIncome * (adminChargePercent / 100m);
                        decimal netPayable = grossIncome - tdsAmount - adminChargeAmount;

                        // Transfer balance
                        user.WalletBalance += netPayable;
                        user.IncomeWallet = 0; // Reset pending wallet

                        // Create transaction record for the ledger
                        string desc = $"Closing Payout: Gross ₹{grossIncome:F2} | TDS ({tdsPercent}%): ₹{tdsAmount:F2} | Admin Chg ({adminChargePercent}%): ₹{adminChargeAmount:F2} | Net: ₹{netPayable:F2}";
                        
                        var commTran = new CommissionTran
                        {
                            UserId = user.Id,
                            FromUserId = user.Id, // System/Self
                            Amount = netPayable,
                            Level = 100, // Special level representing Net Closing Payout
                            Description = desc,
                            Timestamp = DateTime.UtcNow
                        };
                        context.CommissionTrans.Add(commTran);

                        totalProcessed += netPayable;
                        processedCount++;
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Daily Closing: Successfully processed closing for {Count} users. Total Net Payouts: ₹{Amount}", processedCount, totalProcessed);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Daily Closing: Error occurred during transaction.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily Closing: Critical error in background service.");
            }
        }
    }
}
