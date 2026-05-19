using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;
using Microsoft.AspNetCore.Authentication;

namespace MLM_Level.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Index", "Admin");
                else
                    return RedirectToAction("Index", "User");
            }

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

            var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, new System.Security.Claims.ClaimsPrincipal(claimsIdentity), authProperties);

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

        // GET: Admin/PendingRequests
        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            var activations = await _context.ActivationRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();

            var withdrawals = await _context.WithdrawalRequests
                .Include(w => w.User)
                .Where(w => w.Status == "Pending")
                .OrderBy(w => w.CreatedDate)
                .ToListAsync();

            var viewModel = new AdminApprovalsViewModel
            {
                ActivationRequests = activations,
                WithdrawalRequests = withdrawals
            };

            return View(viewModel);
        }

        // POST: Admin/ApproveActivation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveActivation(int id)
        {
            var request = await _context.ActivationRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("PendingRequests");
            }

            // 1. Update request status
            request.Status = "Approved";
            request.ApprovedDate = DateTime.UtcNow;

            // 2. Activate user
            var user = request.User;
            user.IsActive = true;
            user.ActivationDate = DateTime.UtcNow;

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

            TempData["SuccessMessage"] = $"Account '{user.Username}' activated, and commissions have been distributed up to 5 levels!";
            return RedirectToAction("PendingRequests");
        }

        // POST: Admin/RejectActivation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectActivation(int id)
        {
            var request = await _context.ActivationRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("PendingRequests");
            }

            request.Status = "Rejected";
            request.ApprovedDate = DateTime.UtcNow;

            _context.Entry(request).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Activation request rejected.";
            return RedirectToAction("PendingRequests");
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
                return RedirectToAction("PendingRequests");
            }

            request.Status = "Approved";
            request.ProcessedDate = DateTime.UtcNow;

            _context.Entry(request).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Withdrawal request approved and processed.";
            return RedirectToAction("PendingRequests");
        }

        // POST: Admin/RejectWithdrawal/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectWithdrawal(int id)
        {
            var request = await _context.WithdrawalRequests
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (request == null) return NotFound();
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Request is already processed.";
                return RedirectToAction("PendingRequests");
            }

            // Refund user wallet
            request.User.WalletBalance += request.Amount;
            request.Status = "Rejected";
            request.ProcessedDate = DateTime.UtcNow;

            _context.Entry(request).State = EntityState.Modified;
            _context.Entry(request.User).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Withdrawal request rejected. Funds have been refunded to member's wallet.";
            return RedirectToAction("PendingRequests");
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
                // Default to top seeded root user
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "root");
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
    }
}
