using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Services
{
    public class AdminNotificationService : IAdminNotificationService
    {
        private readonly ApplicationDbContext _context;

        public AdminNotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminNotificationsViewModel> GetNotificationsAsync()
        {
            var items = new List<AdminNotificationItem>();

            var activations = await _context.ActivationRequests
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.Status == "Pending")
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToListAsync();

            foreach (var a in activations)
            {
                items.Add(new AdminNotificationItem
                {
                    Title = "Activation request",
                    Description = $"{a.User?.Username ?? "Member"} · UTR {a.TransactionReference}",
                    Icon = "bi-shield-lock",
                    IconClass = "topbar-notif-icon-amber",
                    Action = "PendingActivations",
                    CreatedAt = a.CreatedDate
                });
            }

            var withdrawals = await _context.WithdrawalRequests
                .AsNoTracking()
                .Include(w => w.User)
                .Where(w => w.Status == "Pending")
                .OrderByDescending(w => w.CreatedDate)
                .Take(5)
                .ToListAsync();

            foreach (var w in withdrawals)
            {
                items.Add(new AdminNotificationItem
                {
                    Title = "Withdrawal request",
                    Description = $"{w.User?.Username ?? "Member"} · ₹{w.Amount:N0}",
                    Icon = "bi-wallet2",
                    IconClass = "topbar-notif-icon-warning",
                    Action = "PendingRequests",
                    CreatedAt = w.CreatedDate
                });
            }

            var kycList = await _context.KycDetails
                .AsNoTracking()
                .Include(k => k.User)
                .Where(k => k.Status == "Pending")
                .OrderByDescending(k => k.CreatedDate)
                .Take(5)
                .ToListAsync();

            foreach (var k in kycList)
            {
                items.Add(new AdminNotificationItem
                {
                    Title = "KYC submission",
                    Description = $"{k.User?.Username ?? "Member"} awaiting verification",
                    Icon = "bi-shield-check",
                    IconClass = "topbar-notif-icon-teal",
                    Action = "KycApprovals",
                    CreatedAt = k.CreatedDate
                });
            }

            var tickets = await _context.SupportTickets
                .AsNoTracking()
                .Include(t => t.User)
                .Where(t => t.Status == "Pending")
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var t in tickets)
            {
                items.Add(new AdminNotificationItem
                {
                    Title = "Support ticket",
                    Description = $"{t.User?.Username ?? "Member"} · {t.Subject}",
                    Icon = "bi-chat-left-text",
                    IconClass = "topbar-notif-icon-blue",
                    Action = "Tickets",
                    CreatedAt = t.CreatedAt
                });
            }

            var ordered = items
                .OrderByDescending(i => i.CreatedAt)
                .Take(8)
                .ToList();

            var totalCount = await _context.ActivationRequests.CountAsync(a => a.Status == "Pending")
                + await _context.WithdrawalRequests.CountAsync(w => w.Status == "Pending")
                + await _context.KycDetails.CountAsync(k => k.Status == "Pending")
                + await _context.SupportTickets.CountAsync(t => t.Status == "Pending");

            return new AdminNotificationsViewModel
            {
                TotalCount = totalCount,
                Items = ordered
            };
        }
    }
}
