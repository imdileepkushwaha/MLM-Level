using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;
using Microsoft.AspNetCore.Authentication;

namespace MLM_Level.Controllers
{
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MLM_Level.Services.IEmailService _emailService;
        private readonly MLM_Level.Services.IMaintenanceModeService _maintenance;

        public AdminController(
            ApplicationDbContext context,
            MLM_Level.Services.IEmailService emailService,
            MLM_Level.Services.IMaintenanceModeService maintenance)
        {
            _context = context;
            _emailService = emailService;
            _maintenance = maintenance;
        }

        // GET: Admin/Login
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            var adminAuth = await HttpContext.AuthenticateAsync("AdminAuth");
            if (adminAuth.Succeeded) return RedirectToAction("Index", "Admin");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Admin/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();

            // Look up user by username or email
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid Admin credentials.");
                return View(model);
            }

            // Verify Password
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid Admin credentials.");
                return View(model);
            }

            // Restrict login to Administrators only
            if (!user.IsAdmin)
            {
                ModelState.AddModelError(string.Empty, "Access Denied: You do not have administrator privileges.");
                return View(model);
            }

            // Create claims for cookie
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Username),
                new System.Security.Claims.Claim("FullName", user.FullName),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            };

            var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, "AdminAuth");

            var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("AdminAuth", new System.Security.Claims.ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Admin");
        }

        // GET: Admin/Index (Dashboard)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int totalUsers = await _context.Users.CountAsync(u => !u.IsAdmin);
            int activeUsers = await _context.Users.CountAsync(u => !u.IsAdmin && u.IsActive);
            decimal totalSales = activeUsers * 1000.00m; // Active users count * package price (₹1000)
            decimal totalCommissions = await _context.CommissionTrans.SumAsync(t => t.Amount);

            int pendingActivations = await _context.ActivationRequests.CountAsync(r => r.Status == "Pending");
            int pendingWithdrawals = await _context.WithdrawalRequests.CountAsync(w => w.Status == "Pending");

            var recentUsers = await _context.Users
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.JoinedDate)
                .Take(5)
                .ToListAsync();

            var recentActivations = await _context.ActivationRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedDate)
                .Take(5)
                .ToListAsync();

            var recentWithdrawals = await _context.WithdrawalRequests
                .Include(w => w.User)
                .Where(w => w.Status == "Pending")
                .OrderByDescending(w => w.CreatedDate)
                .Take(5)
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalSales = totalSales,
                TotalCommissionsDistributed = totalCommissions,
                PendingActivationsCount = pendingActivations,
                PendingWithdrawalsCount = pendingWithdrawals,
                RecentUsers = recentUsers,
                PendingActivations = recentActivations,
                PendingWithdrawals = recentWithdrawals
            };

            return View(viewModel);
        }

        // GET: Admin/Members
        [HttpGet]
        public async Task<IActionResult> Members()
        {
            var users = await _context.Users
                .Include(u => u.Sponsor)
                .Include(u => u.Parent)
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.JoinedDate)
                .ToListAsync();

            return View(users);
        }

        // GET: Admin/EditMember/5
        [HttpGet]
        public async Task<IActionResult> EditMember(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.IsAdmin) return NotFound();

            var viewModel = new AdminUserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                WalletBalance = user.WalletBalance
            };

            return View(viewModel);
        }

        // POST: Admin/EditMember/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMember(AdminUserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FindAsync(model.Id);
            if (user == null || user.IsAdmin) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.IsActive = model.IsActive;
            user.WalletBalance = model.WalletBalance;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Member updated successfully.";
            return RedirectToAction("Members");
        }

        // GET: Admin/Withdrawals
        [HttpGet]
        public async Task<IActionResult> Withdrawals()
        {
            var withdrawals = await _context.WithdrawalRequests
                .Include(w => w.User)
                .OrderByDescending(w => w.CreatedDate)
                .ToListAsync();

            var viewModel = new AdminApprovalsViewModel
            {
                WithdrawalRequests = withdrawals
            };

            return View(viewModel);
        }

        // GET: Admin/PendingRequests (legacy route)
        [HttpGet]
        public IActionResult PendingRequests()
            => RedirectToAction(nameof(Withdrawals));

        // GET: Admin/PendingActivations
        [HttpGet]
        public async Task<IActionResult> PendingActivations()
        {
            var activations = await _context.ActivationRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();

            return View(activations);
        }

        // POST: Admin/ApproveActivation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveActivation(int id)
        {
            var request = await _context.ActivationRequests
                .Include(r => r.User)
                .Include(r => r.Package)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("Withdrawals");
            }

            // 1. Update request status
            request.Status = "Approved";
            request.ApprovedDate = DateTime.UtcNow;

            // 2. Activate user
            var user = request.User;
            user.IsActive = true;
            user.ActivationDate = DateTime.UtcNow;

            // 2.5 Add UserPackage for ROI if applicable
            if (request.PackageId.HasValue && request.Package != null && request.Package.RoiDurationDays > 0)
            {
                var userPackage = new UserPackage
                {
                    UserId = user.Id,
                    PackageId = request.PackageId.Value,
                    Amount = request.Amount,
                    RoiPercentage = request.Package.RoiPercentage,
                    RoiDurationDays = request.Package.RoiDurationDays,
                    DaysPaid = 0,
                    IsActive = true,
                    ActivationDate = DateTime.UtcNow
                };
                _context.UserPackages.Add(userPackage);
            }

            _context.Entry(request).State = EntityState.Modified;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // 3. Distribute level commissions via Stored Procedure
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_DistributeCommission @ActivatedUserId = {0}, @PackageAmount = {1}", 
                    user.Id, request.Amount
                );
            }
            catch (Exception ex)
            {
                // In a production app, we would log this ex
                TempData["ErrorMessage"] = "Commissions distribution failed: " + ex.Message;
            }

            // Fire and forget Activation Email
            _ = Task.Run(async () =>
            {
                try
                {
                    string emailBody = $@"
                        <h2>Congratulations {user.FullName}!</h2>
                        <p>Your Elite MLM account (<strong>{user.Username}</strong>) has been successfully activated.</p>
                        <p>Your package <strong>{request.Package?.Name}</strong> is now active, and you are eligible to earn commissions from your downline.</p>
                        <br/>
                        <p>Login to your dashboard to view your network and earnings!</p>
                    ";
                    await _emailService.SendEmailAsync(user.Email, "Account Activated - Elite MLM", emailBody);
                }
                catch { /* ignore SMTP errors */ }
            });

            TempData["SuccessMessage"] = $"Account '{user.Username}' activated, and commissions have been distributed up to 5 levels!";
            return RedirectToAction("Withdrawals");
        }

        // POST: Admin/RejectActivation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectActivation(int id, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction("PendingActivations");
            }

            var request = await _context.ActivationRequests
                .Include(r => r.User)
                .Include(r => r.Package)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("PendingActivations");
            }

            request.Status = "Rejected";
            request.ApprovedDate = DateTime.UtcNow;
            request.RejectionReason = rejectionReason.Trim();

            _context.Entry(request).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var user = request.User;
            var packageName = request.Package?.Name ?? "Package";
            _ = Task.Run(async () =>
            {
                try
                {
                    string emailBody = $@"
                        <h2>Activation Request Declined</h2>
                        <p>Hi {user.FullName},</p>
                        <p>Your activation request for <strong>{packageName}</strong> (UTR: <strong>{request.TransactionReference}</strong>) was declined.</p>
                        <p><strong>Reason:</strong> {request.RejectionReason}</p>
                        <p>Please review the reason, submit a corrected payment proof if needed, and try again from your Packages page.</p>
                    ";
                    await _emailService.SendEmailAsync(user.Email, "Activation Declined - Elite MLM", emailBody);
                }
                catch { /* ignore SMTP errors */ }
            });

            TempData["SuccessMessage"] = "Activation request rejected. Member has been notified.";
            return RedirectToAction("PendingActivations");
        }

        // POST: Admin/ApproveWithdrawal/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveWithdrawal(int id)
        {
            var request = await _context.WithdrawalRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("Withdrawals");
            }

            request.Status = "Approved";
            request.ProcessedDate = DateTime.UtcNow;

            _context.Entry(request).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Fetch user for email
            var user = await _context.Users.FindAsync(request.UserId);
            if (user != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string emailBody = $@"
                            <h2>Withdrawal Request Approved!</h2>
                            <p>Hi {user.FullName},</p>
                            <p>Your withdrawal request of <strong>₹{request.Amount:N2}</strong> has been approved and processed.</p>
                            <p>The amount (after applicable deductions) should reflect in your bank account shortly.</p>
                            <br/>
                            <p>Thank you for being a part of Elite MLM!</p>
                        ";
                        await _emailService.SendEmailAsync(user.Email, "Withdrawal Approved - Elite MLM", emailBody);
                    }
                    catch { /* ignore SMTP errors */ }
                });
            }

            TempData["SuccessMessage"] = "Withdrawal request approved and processed.";
            return RedirectToAction("Withdrawals");
        }

        // POST: Admin/RejectWithdrawal/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectWithdrawal(int id, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction("Withdrawals");
            }

            var request = await _context.WithdrawalRequests
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("Withdrawals");
            }

            request.User.WalletBalance += request.Amount;
            request.Status = "Rejected";
            request.ProcessedDate = DateTime.UtcNow;
            request.RejectionReason = rejectionReason.Trim();

            _context.Entry(request).State = EntityState.Modified;
            _context.Entry(request.User).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var user = request.User;
            _ = Task.Run(async () =>
            {
                try
                {
                    string emailBody = $@"
                        <h2>Withdrawal Request Declined</h2>
                        <p>Hi {user.FullName},</p>
                        <p>Your withdrawal request of <strong>₹{request.Amount:N2}</strong> was declined and the amount has been refunded to your wallet.</p>
                        <p><strong>Reason:</strong> {request.RejectionReason}</p>
                        <p>You can review your wallet and submit a new request if needed.</p>
                    ";
                    await _emailService.SendEmailAsync(user.Email, "Withdrawal Declined - Elite MLM", emailBody);
                }
                catch { /* ignore SMTP errors */ }
            });

            TempData["SuccessMessage"] = "Withdrawal request rejected. Funds refunded and member notified.";
            return RedirectToAction("Withdrawals");
        }

        // GET: Admin/Commissions
        [HttpGet]
        public async Task<IActionResult> Commissions()
        {
            var commissions = await _context.CommissionTrans
                .Include(t => t.User)
                .Include(t => t.FromUser)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            return View(commissions);
        }

        // GET: Admin/TreeExplorer
        [HttpGet]
        public async Task<IActionResult> TreeExplorer(string? searchUsername = null)
        {
            User? targetUser = null;
            if (!string.IsNullOrEmpty(searchUsername))
            {
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == searchUsername && !u.IsAdmin);
                if (targetUser == null)
                {
                    ViewBag.SearchError = "User not found or is an administrator.";
                }
            }
            else
            {
                // Default tree root: first member after admin (typically ML000002)
                targetUser = await _context.Users
                    .Where(u => !u.IsAdmin)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync();
            }

            var treeNodes = new List<DownlineNodeViewModel>();
            if (targetUser != null)
            {
                ViewBag.TargetUser = targetUser;
                try
                {
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "sp_GetDownlineTree";
                        command.CommandType = CommandType.StoredProcedure;

                        var paramStartUserId = command.CreateParameter();
                        paramStartUserId.ParameterName = "@StartUserId";
                        paramStartUserId.Value = targetUser.Id;
                        command.Parameters.Add(paramStartUserId);

                        _context.Database.OpenConnection();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                treeNodes.Add(new DownlineNodeViewModel
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
                                });
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fallback
                }
            }

            return View(treeNodes);
        }

        // GET: Admin/Tickets
        [HttpGet]
        public async Task<IActionResult> Tickets()
        {
            var tickets = await _context.SupportTickets
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        // POST: Admin/ReplyTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyTicket(int id, string adminResponse)
        {
            if (string.IsNullOrWhiteSpace(adminResponse))
            {
                TempData["ErrorMessage"] = "Response content cannot be empty.";
                return RedirectToAction("Tickets");
            }

            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.AdminResponse = adminResponse.Trim();
            ticket.Status = "Resolved";

            _context.Entry(ticket).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Reply sent successfully and ticket closed!";
            return RedirectToAction("Tickets");
        }

        // GET: Admin/Ledger
        [HttpGet]
        public async Task<IActionResult> Ledger(string? searchUsername, string? filterType)
        {
            var query = _context.CommissionTrans
                .Include(t => t.User)
                .Include(t => t.FromUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchUsername))
            {
                query = query.Where(t => t.User.Username.Contains(searchUsername.Trim()));
            }

            if (!string.IsNullOrEmpty(filterType))
            {
                if (filterType == "Commission")
                {
                    query = query.Where(t => t.Level > 0 && t.Level <= 5);
                }
                else if (filterType == "P2P")
                {
                    query = query.Where(t => t.Level == 0);
                }
                else if (filterType == "Manual")
                {
                    query = query.Where(t => t.Level == 99);
                }
            }

            var transactions = await query
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            ViewBag.SearchUsername = searchUsername;
            ViewBag.FilterType = filterType;

            return View(transactions);
        }

        // POST: Admin/AdjustWallet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustWallet(string username, decimal amount, string description)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["ErrorMessage"] = "Username is required.";
                return RedirectToAction("Ledger");
            }

            if (amount == 0)
            {
                TempData["ErrorMessage"] = "Amount cannot be zero.";
                return RedirectToAction("Ledger");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username.Trim() && !u.IsAdmin);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Active member username not found.";
                return RedirectToAction("Ledger");
            }

            if (user.WalletBalance + amount < 0)
            {
                TempData["ErrorMessage"] = "Operation cancelled. Member's wallet balance cannot be negative.";
                return RedirectToAction("Ledger");
            }

            using (var dbTx = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    user.WalletBalance += amount;
                    _context.Entry(user).State = EntityState.Modified;

                    var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
                    int adminId = adminUser?.Id ?? user.Id;

                    var transaction = new CommissionTran
                    {
                        UserId = user.Id,
                        FromUserId = adminId,
                        Amount = amount,
                        Level = 99, // Special level representing admin adjustment
                        Timestamp = DateTime.UtcNow,
                        Description = string.IsNullOrWhiteSpace(description) 
                            ? "Manual Adjustment by Admin" 
                            : $"Manual Adjustment: {description.Trim()}"
                    };

                    _context.CommissionTrans.Add(transaction);
                    await _context.SaveChangesAsync();
                    await dbTx.CommitAsync();

                    TempData["SuccessMessage"] = $"Wallet adjusted successfully. New balance for @{user.Username}: ₹{user.WalletBalance}";
                }
                catch (Exception ex)
                {
                    await dbTx.RollbackAsync();
                    TempData["ErrorMessage"] = "Wallet adjustment failed: " + ex.Message;
                }
            }

            return RedirectToAction("Ledger");
        }

        // GET: Admin/Settings
        [HttpGet]
        public async Task<IActionResult> Settings(string activeTab = "general")
        {
            var settings = await _context.MlmSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new MlmSetting();
                _context.MlmSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
            
            var viewModel = new AdminSettingsViewModel
            {
                Settings = settings,
                AdminFullName = adminUser?.FullName ?? "",
                AdminEmail = adminUser?.Email ?? "",
                AdminUsername = adminUser?.Username ?? "",
                ActiveTab = activeTab
            };

            return View(viewModel);
        }

        // POST: Admin/Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AdminSettingsViewModel model)
        {
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
            
            var settings = await _context.MlmSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new MlmSetting();
                _context.MlmSettings.Add(settings);
            }

            // General Settings
            settings.SiteName = model.Settings.SiteName ?? "Elite MLM Engine";
            settings.SupportEmail = model.Settings.SupportEmail ?? "support@mlm.com";
            settings.MaintenanceMode = model.Settings.MaintenanceMode ?? "Online";

            // MLM & Payouts
            settings.Level1Commission = model.Settings.Level1Commission;
            settings.Level2Commission = model.Settings.Level2Commission;
            settings.Level3Commission = model.Settings.Level3Commission;
            settings.Level4Commission = model.Settings.Level4Commission;
            settings.Level5Commission = model.Settings.Level5Commission;
            
            // Store Charges
            settings.MinWithdrawalLimit = model.Settings.MinWithdrawalLimit;
            settings.WithdrawalFeePercent = model.Settings.WithdrawalFeePercent;
            settings.TdsPercent = model.Settings.TdsPercent;
            settings.AdminChargePercent = model.Settings.AdminChargePercent;
            
            // Integrations
            settings.CompanyQrCodeUrl = model.Settings.CompanyQrCodeUrl ?? string.Empty;
            settings.BankDetails = model.Settings.BankDetails ?? string.Empty;

            settings.SmtpHost = model.Settings.SmtpHost ?? string.Empty;
            settings.SmtpPort = model.Settings.SmtpPort;
            settings.SmtpUsername = model.Settings.SmtpUsername ?? string.Empty;
            settings.SmtpPassword = model.Settings.SmtpPassword ?? string.Empty;
            settings.SmtpEnableSsl = model.Settings.SmtpEnableSsl;

            settings.SmsApiUrl = model.Settings.SmsApiUrl ?? string.Empty;
            settings.SmsApiKey = model.Settings.SmsApiKey ?? string.Empty;
            settings.SmsSenderId = model.Settings.SmsSenderId ?? string.Empty;

            settings.WhatsAppAccessToken = model.Settings.WhatsAppAccessToken ?? string.Empty;
            settings.WhatsAppInstanceId = model.Settings.WhatsAppInstanceId ?? string.Empty;
            settings.WhatsAppProvider = model.Settings.WhatsAppProvider ?? string.Empty;

            _context.Entry(settings).State = EntityState.Modified;

            // Admin Profile Update
            if (model.ActiveTab == "adminProfile" && adminUser != null)
            {
                adminUser.FullName = model.AdminFullName ?? adminUser.FullName;
                adminUser.Email = model.AdminEmail ?? adminUser.Email;
                _context.Entry(adminUser).State = EntityState.Modified;
            }

            // Password Update
            if (model.ActiveTab == "password" && adminUser != null)
            {
                if (!string.IsNullOrEmpty(model.NewPassword) && model.NewPassword == model.ConfirmPassword)
                {
                    // Check current password logic if necessary. Here we just update.
                    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                    adminUser.PasswordHash = hasher.HashPassword(adminUser, model.NewPassword);
                    _context.Entry(adminUser).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            _maintenance.InvalidateCache();

            TempData["SuccessMessage"] = "System settings updated successfully.";
            return RedirectToAction("Settings", new { activeTab = model.ActiveTab });
        }

        // POST: Admin/SaveHomePopup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveHomePopup(IFormFile? popupImage, bool removePopup = false)
        {
            popupImage ??= Request.Form.Files.FirstOrDefault(f => f.Name == "popupImage");
            var enabledValues = Request.Form["homePopupEnabled"];
            var homePopupEnabled = enabledValues.Count > 0 &&
                string.Equals(enabledValues[enabledValues.Count - 1], "true", StringComparison.OrdinalIgnoreCase);

            var settings = await _context.MlmSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new MlmSetting();
                _context.MlmSettings.Add(settings);
            }

            if (removePopup)
            {
                DeletePopupImageFile(settings.HomePopupImageUrl);
                settings.HomePopupImageUrl = string.Empty;
                settings.HomePopupEnabled = false;
                await _context.SaveChangesAsync();
                _maintenance.InvalidateCache();
                TempData["SuccessMessage"] = "Homepage popup removed.";
                return RedirectToAction("Settings", new { activeTab = "homePopup" });
            }

            settings.HomePopupEnabled = homePopupEnabled;

            if (popupImage != null && popupImage.Length > 0)
            {
                try
                {
                    DeletePopupImageFile(settings.HomePopupImageUrl);
                    settings.HomePopupImageUrl = await SaveHomePopupImageAsync(popupImage);
                    settings.HomePopupEnabled = true;
                }
                catch (InvalidOperationException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction("Settings", new { activeTab = "homePopup" });
                }
            }
            else if (homePopupEnabled && string.IsNullOrWhiteSpace(settings.HomePopupImageUrl))
            {
                TempData["ErrorMessage"] = "Please upload a popup image first, then save again.";
                return RedirectToAction("Settings", new { activeTab = "homePopup" });
            }

            await _context.SaveChangesAsync();
            _maintenance.InvalidateCache();

            if (settings.HomePopupEnabled && !string.IsNullOrWhiteSpace(settings.HomePopupImageUrl))
                TempData["SuccessMessage"] = "Homepage popup is now LIVE. Open the homepage in a new tab to preview it.";
            else if (!string.IsNullOrWhiteSpace(settings.HomePopupImageUrl))
                TempData["SuccessMessage"] = "Popup image saved. Turn on the switch and save again to show it on the homepage.";
            else
                TempData["SuccessMessage"] = "Homepage popup settings saved.";

            return RedirectToAction("Settings", new { activeTab = "homePopup" });
        }

        private static async Task<string> SaveHomePopupImageAsync(IFormFile file)
        {
            const long maxBytes = 3 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException("Popup image must be 3MB or smaller.");

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                throw new InvalidOperationException("Only JPG, PNG, WEBP, or GIF images are allowed.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "popups");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/uploads/popups/" + uniqueFileName;
        }

        private static void DeletePopupImageFile(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/popups/", StringComparison.OrdinalIgnoreCase))
                return;

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // GET: Admin/Packages
        [HttpGet]
        public async Task<IActionResult> Packages()
        {
            var packages = await _context.Packages.OrderBy(p => p.Price).ToListAsync();
            return View(packages);
        }

        // POST: Admin/CreatePackage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePackage(string name, decimal price, string description, decimal roiPercentage, int roiDurationDays)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Package name is required.";
                return RedirectToAction("Packages");
            }

            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Price must be greater than zero.";
                return RedirectToAction("Packages");
            }

            var package = new Package
            {
                Name = name.Trim(),
                Price = price,
                Description = description?.Trim() ?? string.Empty,
                RoiPercentage = roiPercentage,
                RoiDurationDays = roiDurationDays,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.Packages.Add(package);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Package '{package.Name}' created successfully.";
            return RedirectToAction("Packages");
        }

        // POST: Admin/EditPackage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPackage(int id, string name, decimal price, string description, decimal roiPercentage, int roiDurationDays)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Package name is required.";
                return RedirectToAction("Packages");
            }

            if (price <= 0)
            {
                TempData["ErrorMessage"] = "Price must be greater than zero.";
                return RedirectToAction("Packages");
            }

            package.Name = name.Trim();
            package.Price = price;
            package.Description = description?.Trim() ?? string.Empty;
            package.RoiPercentage = roiPercentage;
            package.RoiDurationDays = roiDurationDays;

            _context.Entry(package).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Package '{package.Name}' updated successfully.";
            return RedirectToAction("Packages");
        }

        // POST: Admin/TogglePackage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePackage(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null) return NotFound();

            package.IsActive = !package.IsActive;
            _context.Entry(package).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Package '{package.Name}' is now {(package.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction("Packages");
        }

        // GET: Admin/Announcements
        [HttpGet]
        public async Task<IActionResult> Announcements()
        {
            var announcements = await _context.Announcements.OrderByDescending(a => a.CreatedDate).ToListAsync();
            return View(announcements);
        }

        // POST: Admin/CreateAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Title and Content are required.";
                return RedirectToAction("Announcements");
            }

            var announcement = new Announcement
            {
                Title = title.Trim(),
                Content = content.Trim(),
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Announcement published successfully.";
            return RedirectToAction("Announcements");
        }

        // POST: Admin/ToggleAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null) return NotFound();

            announcement.IsActive = !announcement.IsActive;
            _context.Entry(announcement).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Announcement is now {(announcement.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction("Announcements");
        }

        // POST: Admin/DeleteAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null) return NotFound();

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Announcement deleted successfully.";
            return RedirectToAction("Announcements");
        }

        // GET: Admin/Rewards
        [HttpGet]
        public async Task<IActionResult> Rewards()
        {
            var rewards = await _context.MemberRewards
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return View(rewards);
        }

        // POST: Admin/CreateReward
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReward(
            string title,
            string description,
            decimal amount,
            string rewardType,
            string? username)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Title and description are required.";
                return RedirectToAction(nameof(Rewards));
            }

            int? userId = null;
            if (!string.IsNullOrWhiteSpace(username))
            {
                var member = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Username == username.Trim() && !u.IsAdmin);
                if (member == null)
                {
                    TempData["ErrorMessage"] = $"Member username '{username.Trim()}' not found.";
                    return RedirectToAction(nameof(Rewards));
                }
                userId = member.Id;
            }

            var reward = new MemberReward
            {
                UserId = userId,
                Title = title.Trim(),
                Description = description.Trim(),
                Amount = amount < 0 ? 0 : amount,
                RewardType = string.IsNullOrWhiteSpace(rewardType) ? "Bonus" : rewardType.Trim(),
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.MemberRewards.Add(reward);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = userId.HasValue
                ? $"Reward published for @{username!.Trim()}."
                : "Reward published for all members.";
            return RedirectToAction(nameof(Rewards));
        }

        // POST: Admin/ToggleReward
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReward(int id)
        {
            var reward = await _context.MemberRewards.FindAsync(id);
            if (reward == null) return NotFound();

            reward.IsActive = !reward.IsActive;
            _context.Entry(reward).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Reward is now {(reward.IsActive ? "visible" : "hidden")} to members.";
            return RedirectToAction(nameof(Rewards));
        }

        // POST: Admin/DeleteReward
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReward(int id)
        {
            var reward = await _context.MemberRewards.FindAsync(id);
            if (reward == null) return NotFound();

            _context.MemberRewards.Remove(reward);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Reward deleted.";
            return RedirectToAction(nameof(Rewards));
        }

        // POST: Admin/TestEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestEmail(string testEmail)
        {
            if (string.IsNullOrWhiteSpace(testEmail))
            {
                TempData["ErrorMessage"] = "Recipient email is required.";
                return RedirectToAction("Settings", new { activeTab = "smtp" });
            }

            try
            {
                await _emailService.SendEmailAsync(testEmail, "Test Email from Elite MLM System", 
                    "<h3>Success!</h3><p>Your SMTP settings are configured correctly and the email system is working.</p>");
                TempData["SuccessMessage"] = $"Test email sent successfully to {testEmail}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to send test email: {ex.Message}";
            }

            return RedirectToAction("Settings", new { activeTab = "smtp" });
        }
        // GET: Admin/RoiManagement
        [HttpGet]
        public async Task<IActionResult> RoiManagement()
        {
            var nowIst = DateTime.UtcNow.AddHours(5).AddMinutes(30);
            var startOfTodayIst = nowIst.Date;
            var startOfTodayUtc = startOfTodayIst.AddHours(-5).AddMinutes(-30);

            var activeUserPackages = await _context.UserPackages
                .Include(up => up.User)
                .Include(up => up.Package)
                .Where(up => up.IsActive && up.DaysPaid < up.RoiDurationDays && (up.LastPaidDate == null || up.LastPaidDate < startOfTodayUtc))
                .OrderBy(up => up.ActivationDate)
                .ToListAsync();

            return View(activeUserPackages);
        }

        // POST: Admin/DistributeRoi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DistributeRoi()
        {
            int processedCount = 0;
            decimal totalDistributed = 0;

            try
            {
                using (var command = _context.Database.GetDbConnection().CreateCommand())
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
                    TempData["SuccessMessage"] = $"Successfully distributed ROI to {processedCount} users. Total Amount: ₹{totalDistributed:N2}.";
                }
                else
                {
                    TempData["InfoMessage"] = "No pending ROI payouts found for today.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to distribute ROI: " + ex.Message;
            }

            return RedirectToAction("RoiManagement");
        }
        // KYC Actions
        [HttpGet]
        public async Task<IActionResult> KycApprovals()
        {
            var kycList = await _context.KycDetails
                .Include(k => k.User)
                .OrderByDescending(k => k.CreatedDate)
                .ToListAsync();
            return View(kycList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateKycStatus(int id, string status, string rejectionReason)
        {
            var kyc = await _context.KycDetails.FindAsync(id);
            if (kyc == null) return NotFound();

            kyc.Status = status;
            if (status == "Rejected")
            {
                kyc.RejectionReason = rejectionReason;
            }
            kyc.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"KYC status updated to {status} successfully.";
            return RedirectToAction("KycApprovals");
        }
        [HttpGet]
        public async Task<IActionResult> ExportPendingWithdrawalsCsv()
        {
            var pendingWithdrawals = await _context.WithdrawalRequests
                .Include(w => w.User)
                .Where(w => w.Status == "Pending")
                .OrderBy(w => w.CreatedDate)
                .ToListAsync();

            var settings = await _context.MlmSettings.FirstOrDefaultAsync() ?? new MlmSetting();
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,User Name,Username,Requested Amount,Processing Fee (%),Net Amount,Bank Account Details,Request Date");

            foreach (var req in pendingWithdrawals)
            {
                var feeAmount = req.Amount * (settings.WithdrawalFeePercent / 100);
                var netAmount = req.Amount - feeAmount;
                
                // Fetch bank details string (which we don't have distinct cols for, but they should be in KycDetails or Profile). 
                // Let's pull from KYC if available
                var kyc = await _context.KycDetails.FirstOrDefaultAsync(k => k.UserId == req.UserId && k.Status == "Approved");
                string bankInfo = kyc != null && !string.IsNullOrEmpty(kyc.BankAccountNumber)
                    ? $"{kyc.BankAccountHolderName} | {kyc.BankName} | A/c {kyc.BankAccountNumber} | IFSC {kyc.BankIfsc}"
                    : (kyc != null ? $"Passbook: {kyc.BankPassbookUrl}" : "No Verified Bank Info");

                // Escape commas in strings
                string userName = req.User.FullName.Replace(",", " ");
                string username = req.User.Username.Replace(",", " ");
                
                csv.AppendLine($"{req.Id},{userName},{username},{req.Amount:F2},{settings.WithdrawalFeePercent:F2},{netAmount:F2},{bankInfo},{req.CreatedDate:yyyy-MM-dd HH:mm:ss}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"PendingWithdrawals_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }
    }
}

