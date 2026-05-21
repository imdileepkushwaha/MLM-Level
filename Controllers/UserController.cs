using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Controllers
{
    [Authorize(AuthenticationSchemes = "UserAuth", Roles = "User")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public UserController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new InvalidOperationException("User identity not found.");
            return int.Parse(claim.Value);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                return Json(new { success = false, message = "All password fields are required." });
            }

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "New password and confirmation password do not match." });
            }

            try
            {
                int userId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
                if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
                {
                    return Json(new { success = false, message = "Current password is incorrect." });
                }

                user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Password updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while updating the password: " + ex.Message });
            }
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

        // GET: User/Packages
        [HttpGet]
        public async Task<IActionResult> Packages()
        {
            int userId = GetCurrentUserId();
            var model = await BuildPackagesViewModelAsync(userId);
            if (model == null) return NotFound();
            return View(model);
        }

        // GET: User/Activate (legacy route)
        [HttpGet]
        public IActionResult Activate() => RedirectToAction(nameof(Packages));

        // POST: User/RequestPackage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPackage(string transactionReference, int packageId, IFormFile? paymentSlip)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var hasPending = await _context.ActivationRequests.AnyAsync(r => r.UserId == userId && r.Status == "Pending");
            if (hasPending)
            {
                TempData["ErrorMessage"] = "You already have a pending package request. Please wait for admin approval.";
                return RedirectToAction(nameof(Packages));
            }

            if (string.IsNullOrWhiteSpace(transactionReference))
            {
                TempData["ErrorMessage"] = "Transaction reference / UTR is required.";
                return RedirectToAction(nameof(Packages));
            }

            var normalizedUtr = transactionReference.Trim();
            var duplicateUtr = await _context.ActivationRequests.AnyAsync(r =>
                r.Status != "Rejected" &&
                r.TransactionReference.ToUpper() == normalizedUtr.ToUpper());
            if (duplicateUtr)
            {
                TempData["ErrorMessage"] = "This UTR / transaction reference has already been used. Please enter a different reference.";
                return RedirectToAction(nameof(Packages));
            }

            var package = await _context.Packages.FindAsync(packageId);
            if (package == null || !package.IsActive)
            {
                TempData["ErrorMessage"] = "Invalid package selection.";
                return RedirectToAction(nameof(Packages));
            }

            string? slipUrl = null;
            if (paymentSlip != null && paymentSlip.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "slips");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(paymentSlip.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await paymentSlip.CopyToAsync(stream);
                }
                slipUrl = "/uploads/slips/" + uniqueFileName;
            }

            var request = new ActivationRequest
            {
                UserId = userId,
                Amount = package.Price,
                PackageId = packageId,
                TransactionReference = normalizedUtr,
                PaymentSlipUrl = slipUrl,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _context.ActivationRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = user.IsActive
                ? $"Package request for '{package.Name}' submitted! Admin will verify your payment shortly."
                : $"Activation request for '{package.Name}' submitted! Admin will verify and activate your account shortly.";
            return RedirectToAction(nameof(Packages));
        }

        // POST: User/Activate (legacy route)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Activate(string transactionReference, int packageId, IFormFile? paymentSlip)
            => RequestPackage(transactionReference, packageId, paymentSlip);

        private async Task<UserPackagesViewModel?> BuildPackagesViewModelAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            var pending = await _context.ActivationRequests
                .Include(r => r.Package)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

            return new UserPackagesViewModel
            {
                UserInfo = user,
                Settings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting(),
                AvailablePackages = await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync(),
                ActivePackages = await _context.UserPackages
                    .Include(up => up.Package)
                    .Where(up => up.UserId == userId)
                    .OrderByDescending(up => up.ActivationDate)
                    .ToListAsync(),
                RecentRequests = await _context.ActivationRequests
                    .Include(r => r.Package)
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedDate)
                    .Take(10)
                    .ToListAsync(),
                HasPendingRequest = pending != null,
                PendingRequest = pending
            };
        }

        // GET: User/ActivationReport
        [HttpGet]
        public async Task<IActionResult> ActivationReport()
        {
            int userId = GetCurrentUserId();
            var requests = await _context.ActivationRequests
                .Include(r => r.Package)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(requests);
        }

        // GET: User/MyDirect
        [HttpGet]
        public async Task<IActionResult> MyDirect()
        {
            int userId = GetCurrentUserId();
            var directs = await _context.Users
                .Where(u => u.SponsorId == userId)
                .OrderByDescending(u => u.JoinedDate)
                .ToListAsync();

            return View(directs);
        }

        // GET: User/MyDownline
        [HttpGet]
        public async Task<IActionResult> MyDownline()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var downlineList = new List<DownlineNodeViewModel>();

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

                            downlineList.Add(node);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Active fallbacks
            }

            return View(downlineList);
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

        // GET: User/Wallet
        [HttpGet]
        public async Task<IActionResult> Wallet()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var withdrawals = await _context.WithdrawalRequests
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedDate)
                .ToListAsync();

            var ledgerQuery = _context.CommissionTrans.Where(t => t.UserId == userId);
            var totalIncome = await ledgerQuery.SumAsync(t => t.Amount);
            var ledgerCount = await ledgerQuery.CountAsync();

            ViewBag.MlmSettings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();

            var viewModel = new UserWalletViewModel
            {
                WalletBalance = user.WalletBalance,
                IncomeWallet = user.IncomeWallet,
                TotalIncome = totalIncome,
                LedgerEntryCount = ledgerCount,
                Withdrawals = withdrawals
            };

            return View(viewModel);
        }

        // GET: User/IncomeTransactions
        [HttpGet]
        public async Task<IActionResult> IncomeTransactions()
        {
            int userId = GetCurrentUserId();
            var ledger = await _context.CommissionTrans
                .AsNoTracking()
                .Include(t => t.FromUser)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            ViewBag.TotalIncome = ledger.Sum(l => l.Amount);
            ViewBag.CreditTotal = ledger.Where(l => l.Amount > 0).Sum(l => l.Amount);
            ViewBag.DebitTotal = ledger.Where(l => l.Amount < 0).Sum(l => Math.Abs(l.Amount));
            return View("IncomeTransactions", ledger);
        }

        // POST: User/RequestWithdrawal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestWithdrawal(decimal amount)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var kyc = await _context.KycDetails.FirstOrDefaultAsync(k => k.UserId == userId);
            if (kyc == null || kyc.Status != "Approved")
            {
                TempData["ErrorMessage"] = "You must complete and get your KYC approved before requesting withdrawals.";
                return RedirectToAction("Kyc");
            }

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

        // GET: User/DailyIncome
        [HttpGet]
        public async Task<IActionResult> DailyIncome()
        {
            int userId = GetCurrentUserId();
            var incomes = await _context.CommissionTrans
                .Include(c => c.FromUser)
                .Where(c => c.UserId == userId && c.Amount > 0 && c.Level == 0 && c.Description.Contains("Daily ROI"))
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync();

            ViewBag.TotalIncome = incomes.Sum(i => i.Amount);
            return View(incomes);
        }

        // GET: User/DirectIncome
        [HttpGet]
        public async Task<IActionResult> DirectIncome()
        {
            int userId = GetCurrentUserId();
            var incomes = await _context.CommissionTrans
                .Include(c => c.FromUser)
                .Where(c => c.UserId == userId && c.Amount > 0 && c.Level == 1 && c.Description.Contains("Commission"))
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync();

            ViewBag.TotalIncome = incomes.Sum(i => i.Amount);
            return View(incomes);
        }

        // GET: User/LevelIncome
        [HttpGet]
        public async Task<IActionResult> LevelIncome()
        {
            int userId = GetCurrentUserId();
            var incomes = await _context.CommissionTrans
                .Include(c => c.FromUser)
                .Where(c => c.UserId == userId && c.Amount > 0 && c.Level > 1 && c.Description.Contains("Commission"))
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync();

            ViewBag.TotalIncome = incomes.Sum(i => i.Amount);
            return View(incomes);
        }

        // GET: User/Rewards
        [HttpGet]
        public async Task<IActionResult> Rewards()
        {
            int userId = GetCurrentUserId();
            var rewards = await _context.MemberRewards
                .AsNoTracking()
                .Where(r => r.IsActive && (r.UserId == null || r.UserId == userId))
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            ViewBag.TotalRewardValue = rewards.Sum(r => r.Amount);
            return View(rewards);
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
        // KYC Actions
        [HttpGet]
        public async Task<IActionResult> Kyc()
        {
            var userId = GetCurrentUserId();
            var kyc = await _context.KycDetails.FirstOrDefaultAsync(k => k.UserId == userId);
            return View(kyc ?? new KycDetail { UserId = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadKyc(IFormFile panCard, IFormFile bankPassbook)
        {
            var userId = GetCurrentUserId();
            var kyc = await _context.KycDetails.FirstOrDefaultAsync(k => k.UserId == userId);

            if (kyc != null && kyc.Status == "Approved")
            {
                TempData["Error"] = "Your KYC is already approved and cannot be changed.";
                return RedirectToAction("Kyc");
            }

            if (kyc == null)
            {
                kyc = new KycDetail { UserId = userId };
                _context.KycDetails.Add(kyc);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "kyc");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (panCard != null && panCard.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + panCard.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await panCard.CopyToAsync(fileStream);
                }
                kyc.PanCardUrl = "/uploads/kyc/" + uniqueFileName;
            }

            if (bankPassbook != null && bankPassbook.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + bankPassbook.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await bankPassbook.CopyToAsync(fileStream);
                }
                kyc.BankPassbookUrl = "/uploads/kyc/" + uniqueFileName;
            }

            kyc.Status = "Pending";
            kyc.UpdatedDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "KYC Documents submitted successfully for review.";
            return RedirectToAction("Kyc");
        }
    }
}
