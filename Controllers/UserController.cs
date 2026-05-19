using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Controllers
{
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new InvalidOperationException("User identity not found.");
            return int.Parse(claim.Value);
        }

        // GET: User/Index (Dashboard)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users
                .Include(u => u.Sponsor)
                .Include(u => u.Parent)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return RedirectToAction("Logout", "Account");
            }

            // Fetch recent transactions
            var recentTransactions = await _context.CommissionTrans
                .Include(t => t.FromUser)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Timestamp)
                .Take(5)
                .ToListAsync();

            // Total Earnings
            var totalEarnings = await _context.CommissionTrans
                .Where(t => t.UserId == userId)
                .SumAsync(t => t.Amount);

            // Direct referrals count
            int directReferrals = await _context.Users.CountAsync(u => u.SponsorId == userId);

            // Execute sp_GetTeamStats Stored Procedure
            int totalTeam = 0;
            int activeTeam = 0;
            try
            {
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "sp_GetTeamStats";
                    command.CommandType = CommandType.StoredProcedure;

                    var paramUserId = command.CreateParameter();
                    paramUserId.ParameterName = "@UserId";
                    paramUserId.Value = userId;
                    command.Parameters.Add(paramUserId);

                    var paramTotalTeam = command.CreateParameter();
                    paramTotalTeam.ParameterName = "@TotalTeam";
                    paramTotalTeam.Direction = ParameterDirection.Output;
                    paramTotalTeam.DbType = DbType.Int32;
                    command.Parameters.Add(paramTotalTeam);

                    var paramActiveTeam = command.CreateParameter();
                    paramActiveTeam.ParameterName = "@ActiveTeam";
                    paramActiveTeam.Direction = ParameterDirection.Output;
                    paramActiveTeam.DbType = DbType.Int32;
                    command.Parameters.Add(paramActiveTeam);

                    _context.Database.OpenConnection();
                    command.ExecuteNonQuery();

                    totalTeam = paramTotalTeam.Value != DBNull.Value ? (int)paramTotalTeam.Value : 0;
                    activeTeam = paramActiveTeam.Value != DBNull.Value ? (int)paramActiveTeam.Value : 0;
                }
            }
            catch (Exception)
            {
                // Fallback in case of database connectivity issues during initialization
                totalTeam = 0;
                activeTeam = 0;
            }

            var activationRequests = await _context.ActivationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            var withdrawalRequests = await _context.WithdrawalRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            // Fetch live announcements for notice board
            var activeAnnouncements = await _context.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
            ViewBag.Announcements = activeAnnouncements;

            // Create referral link
            var referralLink = $"{Request.Scheme}://{Request.Host}/Account/Register?sponsorCode={user.ReferralCode}";

            // Leaderboard (Top 5 earners)
            var leaderboard = await _context.CommissionTrans
                .GroupBy(t => new { t.UserId, t.User.Username, t.User.FullName })
                .Select(g => new LeaderboardItemViewModel
                {
                    Username = g.Key.Username,
                    FullName = g.Key.FullName,
                    TotalEarned = g.Sum(t => t.Amount)
                })
                .Where(x => x.TotalEarned > 0)
                .OrderByDescending(x => x.TotalEarned)
                .Take(5)
                .ToListAsync();

            ViewBag.Leaderboard = leaderboard;

            var viewModel = new UserDashboardViewModel
            {
                UserInfo = user,
                DirectReferralCount = directReferrals,
                TotalTeamCount = totalTeam,
                ActiveTeamCount = activeTeam,
                TotalEarnings = totalEarnings,
                ReferralLink = referralLink,
                RecentTransactions = recentTransactions,
                ActivationRequests = activationRequests,
                WithdrawalRequests = withdrawalRequests
            };

            return View(viewModel);
        }

        // GET: User/Activate (Submit Activation Request)
        [HttpGet]
        public async Task<IActionResult> Activate()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.IsActive)
            {
                TempData["InfoMessage"] = "Your account is already active!";
                return RedirectToAction("Index");
            }

            // Check if there is a pending activation request
            var hasPending = await _context.ActivationRequests.AnyAsync(r => r.UserId == userId && r.Status == "Pending");
            ViewBag.HasPendingRequest = hasPending;

            // Fetch dynamic settings & packages
            var settings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();
            var activePackages = await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();

            ViewBag.MlmSettings = settings;
            ViewBag.Packages = activePackages;

            return View();
        }

        // POST: User/Activate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(string transactionReference, int packageId)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.IsActive)
            {
                TempData["InfoMessage"] = "Your account is already active!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(transactionReference))
            {
                ModelState.AddModelError(string.Empty, "Transaction Reference / UTR is required.");
                ViewBag.HasPendingRequest = false;
                ViewBag.MlmSettings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();
                ViewBag.Packages = await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();
                return View();
            }

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null || !package.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Invalid package selection.");
                ViewBag.HasPendingRequest = false;
                ViewBag.MlmSettings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();
                ViewBag.Packages = await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();
                return View();
            }

            var request = new ActivationRequest
            {
                UserId = userId,
                Amount = package.Price,
                TransactionReference = transactionReference.Trim(),
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _context.ActivationRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Activation request for '{package.Name}' submitted! Admin will verify and activate your account shortly.";
            return RedirectToAction("Index");
        }

        // GET: User/Tree (Downline Tree & Levels)
        [HttpGet]
        public async Task<IActionResult> Tree()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var viewModel = new UserDownlineViewModel
            {
                UserInfo = user
            };

            try
            {
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "sp_GetDownlineTree";
                    command.CommandType = CommandType.StoredProcedure;

                    var paramStartUserId = command.CreateParameter();
                    paramStartUserId.ParameterName = "@StartUserId";
                    paramStartUserId.Value = userId;
                    command.Parameters.Add(paramStartUserId);

                    _context.Database.OpenConnection();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var node = new DownlineNodeViewModel
                            {
                                Id = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                FullName = reader.GetString(2),
                                SponsorId = reader.GetInt32(3),
                                ParentId = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                                Level = reader.GetInt32(5),
                                IsActive = reader.GetBoolean(6),
                                JoinedDate = reader.GetDateTime(7),
                                SponsorName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                ParentName = reader.IsDBNull(9) ? "" : reader.GetString(9)
                            };

                            viewModel.FullDownlineList.Add(node);

                            // Categorize by level (relative to start user)
                            switch (node.Level)
                            {
                                case 1: viewModel.Level1.Add(node); break;
                                case 2: viewModel.Level2.Add(node); break;
                                case 3: viewModel.Level3.Add(node); break;
                                case 4: viewModel.Level4.Add(node); break;
                                case 5: viewModel.Level5.Add(node); break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Active fallbacks
            }

            return View(viewModel);
        }

        // GET: User/Wallet (Transaction Ledger & Withdraw)
        [HttpGet]
        public async Task<IActionResult> Wallet()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var ledger = await _context.CommissionTrans
                .Include(t => t.FromUser)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            var withdrawals = await _context.WithdrawalRequests
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedDate)
                .ToListAsync();

            var totalIncome = ledger.Sum(l => l.Amount);

            // Pass MLM settings to UI
            ViewBag.MlmSettings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();

            var viewModel = new UserWalletViewModel
            {
                WalletBalance = user.WalletBalance,
                TotalIncome = totalIncome,
                IncomeLedger = ledger,
                Withdrawals = withdrawals
            };

            return View(viewModel);
        }

        // POST: User/RequestWithdrawal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestWithdrawal(decimal amount)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Withdrawal amount must be greater than zero.";
                return RedirectToAction("Wallet");
            }

            var settings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();

            if (amount < settings.MinWithdrawalLimit)
            {
                TempData["ErrorMessage"] = $"Minimum withdrawal limit is ₹{settings.MinWithdrawalLimit:N2}.";
                return RedirectToAction("Wallet");
            }

            if (user.WalletBalance < amount)
            {
                TempData["ErrorMessage"] = "Insufficient wallet balance.";
                return RedirectToAction("Wallet");
            }

            // Create Request
            var request = new WithdrawalRequest
            {
                UserId = userId,
                Amount = amount,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            // Deduct immediately to prevent double spending
            user.WalletBalance -= amount;

            _context.WithdrawalRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Withdrawal request of ₹{amount:N2} submitted successfully! A fee of {settings.WithdrawalFeePercent}% will be applied upon payout.";
            return RedirectToAction("Wallet");
        }

        // GET: User/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.Include(u => u.Sponsor).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();
            return View(user);
        }

        // POST: User/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string fullName, string phone)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError(string.Empty, "Full Name is required.");
                return View(user);
            }

            user.FullName = fullName;
            user.Phone = phone ?? string.Empty;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        // GET: User/Support
        [HttpGet]
        public async Task<IActionResult> Support()
        {
            int userId = GetCurrentUserId();
            var tickets = await _context.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        // POST: User/CreateTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTicket(string subject, string message)
        {
            int userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Subject and Message are required.";
                return RedirectToAction("Support");
            }

            var ticket = new SupportTicket
            {
                UserId = userId,
                Subject = subject.Trim(),
                Message = message.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Support ticket submitted successfully! Admin will reply shortly.";
            return RedirectToAction("Support");
        }

        // POST: User/P2PTransfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> P2PTransfer(string receiverUsername, decimal amount)
        {
            int userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(receiverUsername))
            {
                TempData["ErrorMessage"] = "Receiver username is required.";
                return RedirectToAction("Wallet");
            }

            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Transfer amount must be greater than zero.";
                return RedirectToAction("Wallet");
            }

            // Execute stored procedure
            var senderIdParam = new Microsoft.Data.SqlClient.SqlParameter("@SenderId", userId);
            var receiverUsernameParam = new Microsoft.Data.SqlClient.SqlParameter("@ReceiverUsername", receiverUsername.Trim());
            var amountParam = new Microsoft.Data.SqlClient.SqlParameter("@Amount", amount);
            
            var successParam = new Microsoft.Data.SqlClient.SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output };
            var messageParam = new Microsoft.Data.SqlClient.SqlParameter("@Message", SqlDbType.NVarChar, 250) { Direction = ParameterDirection.Output };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_TransferWalletFunds @SenderId, @ReceiverUsername, @Amount, @Success OUTPUT, @Message OUTPUT",
                senderIdParam, receiverUsernameParam, amountParam, successParam, messageParam);

            bool success = (successParam.Value != DBNull.Value) && (bool)successParam.Value;
            string message = (messageParam.Value != DBNull.Value) ? (string)messageParam.Value : "An error occurred.";

            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            return RedirectToAction("Wallet");
        }

        // POST: User/SetPlacementNode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPlacementNode(string? placementUsername)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(placementUsername))
            {
                user.DefaultPlacementId = null;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Placement policy reset to direct parent placement.";
                return RedirectToAction("Index");
            }

            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == placementUsername.Trim());
            if (targetUser == null)
            {
                TempData["ErrorMessage"] = "Target username does not exist.";
                return RedirectToAction("Index");
            }

            int placementId = targetUser.Id;
            if (placementId == userId)
            {
                TempData["ErrorMessage"] = "You cannot place members under yourself recursively.";
                return RedirectToAction("Index");
            }

            // Verify ancestry recursively to ensure target is actually in user's downline tree
            bool isDownline = false;
            var current = await _context.Users.FindAsync(placementId);
            while (current != null && current.ParentId != null)
            {
                if (current.ParentId == userId)
                {
                    isDownline = true;
                    break;
                }
                current = await _context.Users.FindAsync(current.ParentId);
            }

            if (!isDownline)
            {
                TempData["ErrorMessage"] = "Target user is not part of your downline team tree.";
                return RedirectToAction("Index");
            }

            user.DefaultPlacementId = placementId;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"New signups will now be placed under: {targetUser.FullName} (@{targetUser.Username})!";
            return RedirectToAction("Index");
        }

        // GET: User/GetEarningsChartData
        [HttpGet]
        public async Task<IActionResult> GetEarningsChartData()
        {
            int userId = GetCurrentUserId();
            var last30Days = Enumerable.Range(0, 30)
                .Select(i => DateTime.UtcNow.Date.AddDays(-i))
                .OrderBy(d => d)
                .ToList();

            var commissions = await _context.CommissionTrans
                .Where(t => t.UserId == userId && t.Timestamp >= DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            var chartData = last30Days.Select(date => new
            {
                Date = date.ToString("dd MMM"),
                Amount = commissions
                    .Where(t => t.Timestamp.Date == date.Date && t.Amount > 0)
                    .Sum(t => t.Amount)
            }).ToList();

            return Json(chartData);
        }
    }
}
