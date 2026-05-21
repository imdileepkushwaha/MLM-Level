using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Services
{
    public class MemberNotificationService : IMemberNotificationService
    {
        private readonly ApplicationDbContext _context;

        public MemberNotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MemberNotificationsViewModel> GetNotificationsAsync(int userId)
        {
            var items = new List<MemberNotificationItem>();

            var announcements = await _context.Announcements
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedDate)
                .Take(3)
                .ToListAsync();

            foreach (var a in announcements)
            {
                items.Add(new MemberNotificationItem
                {
                    Title = a.Title,
                    Description = a.Content.Length > 60 ? a.Content[..60] + "…" : a.Content,
                    Icon = "bi-megaphone",
                    IconClass = "topbar-notif-icon-blue",
                    Action = "Index",
                    CreatedAt = a.CreatedDate
                });
            }

            var pendingActivation = await _context.ActivationRequests
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedDate)
                .FirstOrDefaultAsync();

            if (pendingActivation != null)
            {
                items.Add(new MemberNotificationItem
                {
                    Title = "Activation pending",
                    Description = $"UTR {pendingActivation.TransactionReference} awaiting approval",
                    Icon = "bi-shield-lock",
                    IconClass = "topbar-notif-icon-amber",
                    Action = "ActivationReport",
                    CreatedAt = pendingActivation.CreatedDate
                });
            }

            var pendingWithdrawal = await _context.WithdrawalRequests
                .AsNoTracking()
                .Where(w => w.UserId == userId && w.Status == "Pending")
                .OrderByDescending(w => w.CreatedDate)
                .FirstOrDefaultAsync();

            if (pendingWithdrawal != null)
            {
                items.Add(new MemberNotificationItem
                {
                    Title = "Withdrawal pending",
                    Description = $"₹{pendingWithdrawal.Amount:N0} is being processed",
                    Icon = "bi-wallet2",
                    IconClass = "topbar-notif-icon-warning",
                    Action = "Wallet",
                    CreatedAt = pendingWithdrawal.CreatedDate
                });
            }

            var kyc = await _context.KycDetails.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId);
            if (kyc != null && kyc.Status == "Pending")
            {
                items.Add(new MemberNotificationItem
                {
                    Title = "KYC under review",
                    Description = "Your documents are being verified",
                    Icon = "bi-shield-check",
                    IconClass = "topbar-notif-icon-teal",
                    Action = "Kyc",
                    CreatedAt = kyc.CreatedDate
                });
            }
            else if (kyc != null && kyc.Status == "Rejected")
            {
                var kycReason = string.IsNullOrWhiteSpace(kyc.RejectionReason)
                    ? "Please re-upload your documents"
                    : kyc.RejectionReason.Length > 60 ? kyc.RejectionReason[..60] + "…" : kyc.RejectionReason;
                items.Add(new MemberNotificationItem
                {
                    Title = "KYC rejected",
                    Description = kycReason,
                    Icon = "bi-shield-x",
                    IconClass = "topbar-notif-icon-warning",
                    Action = "Kyc",
                    CreatedAt = kyc.UpdatedDate ?? kyc.CreatedDate
                });
            }

            var cutoff = DateTime.UtcNow.AddDays(-30);
            var rejectedActivation = await _context.ActivationRequests
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.Status == "Rejected" && r.ApprovedDate >= cutoff)
                .OrderByDescending(r => r.ApprovedDate)
                .FirstOrDefaultAsync();

            if (rejectedActivation != null)
            {
                var reason = string.IsNullOrWhiteSpace(rejectedActivation.RejectionReason)
                    ? "Please review and submit again"
                    : rejectedActivation.RejectionReason.Length > 60 ? rejectedActivation.RejectionReason[..60] + "…" : rejectedActivation.RejectionReason;
                items.Add(new MemberNotificationItem
                {
                    Title = "Activation declined",
                    Description = reason,
                    Icon = "bi-shield-x",
                    IconClass = "topbar-notif-icon-warning",
                    Action = "ActivationReport",
                    CreatedAt = rejectedActivation.ApprovedDate ?? rejectedActivation.CreatedDate
                });
            }

            var rejectedWithdrawal = await _context.WithdrawalRequests
                .AsNoTracking()
                .Where(w => w.UserId == userId && w.Status == "Rejected" && w.ProcessedDate >= cutoff)
                .OrderByDescending(w => w.ProcessedDate)
                .FirstOrDefaultAsync();

            if (rejectedWithdrawal != null)
            {
                var reason = string.IsNullOrWhiteSpace(rejectedWithdrawal.RejectionReason)
                    ? "Amount refunded to your wallet"
                    : rejectedWithdrawal.RejectionReason.Length > 60 ? rejectedWithdrawal.RejectionReason[..60] + "…" : rejectedWithdrawal.RejectionReason;
                items.Add(new MemberNotificationItem
                {
                    Title = "Withdrawal declined",
                    Description = reason,
                    Icon = "bi-wallet2",
                    IconClass = "topbar-notif-icon-warning",
                    Action = "Wallet",
                    CreatedAt = rejectedWithdrawal.ProcessedDate ?? rejectedWithdrawal.CreatedDate
                });
            }

            var openTicket = await _context.SupportTickets
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.Status == "Pending")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (openTicket != null)
            {
                items.Add(new MemberNotificationItem
                {
                    Title = "Support ticket open",
                    Description = openTicket.Subject,
                    Icon = "bi-headset",
                    IconClass = "topbar-notif-icon-blue",
                    Action = "Support",
                    CreatedAt = openTicket.CreatedAt
                });
            }

            var ordered = items.OrderByDescending(i => i.CreatedAt).Take(8).ToList();

            return new MemberNotificationsViewModel
            {
                TotalCount = ordered.Count,
                Items = ordered
            };
        }
    }
}
