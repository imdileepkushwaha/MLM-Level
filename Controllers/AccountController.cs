using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;
using MLM_Level.Services;

namespace MLM_Level.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _emailService = emailService;
        }

        // GET: Account/Login
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            var userAuth = await HttpContext.AuthenticateAsync("UserAuth");
            if (userAuth.Succeeded) return RedirectToAction("Index", "User");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Look up user by username or email
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid Username/Email or Password.");
                return View(model);
            }

            // Verify Password
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid Username/Email or Password.");
                return View(model);
            }

            // Create claims for cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
            };

            var scheme = user.IsAdmin ? "AdminAuth" : "UserAuth";
            var claimsIdentity = new ClaimsIdentity(claims, scheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(scheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user.IsAdmin)
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "User");
        }

        // GET: Account/Register
        [HttpGet]
        public async Task<IActionResult> Register(string? sponsorCode = null)
        {
            var userAuth = await HttpContext.AuthenticateAsync("UserAuth");
            if (userAuth.Succeeded) return RedirectToAction("Index", "User");

            var model = new RegisterViewModel();
            if (!string.IsNullOrEmpty(sponsorCode))
            {
                model.SponsorCode = sponsorCode;
            }
            return View(model);
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "Username is already taken.");
                return View(model);
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email address is already in use.");
                return View(model);
            }

            // Find Sponsor and Parent
            User? sponsor = null;
            if (!string.IsNullOrEmpty(model.SponsorCode))
            {
                sponsor = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == model.SponsorCode);
                if (sponsor == null)
                {
                    ModelState.AddModelError("SponsorCode", "Referral/Sponsor code is invalid.");
                    return View(model);
                }
            }
            else
            {
                // Fallback to seeded 'root' member
                sponsor = await _context.Users.FirstOrDefaultAsync(u => u.Username == "root");
                if (sponsor == null)
                {
                    // Fallback to admin if root is missing
                    sponsor = await _context.Users.FirstOrDefaultAsync(u => u.IsAdmin);
                }
            }

            if (sponsor == null)
            {
                ModelState.AddModelError(string.Empty, "System configuration error. No sponsor available.");
                return View(model);
            }

            // Determine placement parent (dynamic placement Placer interceptor)
            int parentId = sponsor.Id;
            if (sponsor.DefaultPlacementId != null)
            {
                parentId = sponsor.DefaultPlacementId.Value;
            }

            // Create New User
            var newUser = new User
            {
                Username = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                SponsorId = sponsor.Id,
                ParentId = parentId, // Set dynamic parent
                JoinedDate = DateTime.UtcNow,
                IsActive = false, // Must submit activation payment request
                IsAdmin = false,
                WalletBalance = 0.00m,
                ReferralCode = GenerateUniqueReferralCode()
            };

            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Fire and forget welcome email (wrap in try-catch so registration doesn't fail if SMTP is not set)
            _ = Task.Run(async () =>
            {
                try
                {
                    string emailBody = $@"
                        <h2>Welcome to Elite MLM, {newUser.FullName}!</h2>
                        <p>Your account has been created successfully.</p>
                        <p><strong>Username:</strong> {newUser.Username}</p>
                        <p><strong>Sponsor ID:</strong> {(sponsor != null ? sponsor.Username : "None")}</p>
                        <br/>
                        <p>Login to your dashboard to activate your account and start earning!</p>
                    ";
                    await _emailService.SendEmailAsync(newUser.Email, "Welcome to Elite MLM!", emailBody);
                }
                catch { /* Ignore SMTP errors to prevent blocking registration */ }
            });

            TempData["SuccessMessage"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        // GET: Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

            if (user != null)
            {
                var existingTokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.Id && !t.IsUsed)
                    .ToListAsync();
                foreach (var old in existingTokens)
                {
                    old.IsUsed = true;
                }

                var token = GenerateResetToken();
                _context.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsUsed = false,
                    CreatedDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var resetUrl = Url.Action("ResetPassword", "Account", new { token }, Request.Scheme);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string emailBody = $@"
                            <h2>Password Reset Request</h2>
                            <p>Hi {user.FullName},</p>
                            <p>We received a request to reset your Elite MLM account password.</p>
                            <p><a href=""{resetUrl}"">Click here to reset your password</a></p>
                            <p>This link expires in 1 hour. If you did not request this, you can ignore this email.</p>
                            <p style=""word-break:break-all;color:#666;"">{resetUrl}</p>
                        ";
                        await _emailService.SendEmailAsync(user.Email, "Reset Your Password - Elite MLM", emailBody);
                    }
                    catch { /* ignore SMTP errors */ }
                });
            }

            TempData["SuccessMessage"] = "If an account exists with that username or email, a password reset link has been sent.";
            return RedirectToAction(nameof(Login));
        }

        // GET: Account/ResetPassword
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction(nameof(Login));
            }

            var resetToken = await GetValidResetTokenAsync(token);
            if (resetToken == null)
            {
                TempData["ErrorMessage"] = "This password reset link is invalid or has expired.";
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var resetToken = await GetValidResetTokenAsync(model.Token);
            if (resetToken == null)
            {
                TempData["ErrorMessage"] = "This password reset link is invalid or has expired.";
                return RedirectToAction(nameof(Login));
            }

            var user = resetToken.User;
            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            resetToken.IsUsed = true;

            var otherTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.Id != resetToken.Id)
                .ToListAsync();
            foreach (var old in otherTokens)
            {
                old.IsUsed = true;
            }

            _context.Entry(user).State = EntityState.Modified;
            _context.Entry(resetToken).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your password has been reset. Please sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }

        // GET: Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminAuth");
            await HttpContext.SignOutAsync("UserAuth");
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private string GenerateUniqueReferralCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string code;

            do
            {
                var stringChars = new char[8];
                for (int i = 0; i < stringChars.Length; i++)
                {
                    stringChars[i] = chars[random.Next(chars.Length)];
                }
                code = "MLM" + new string(stringChars);
            }
            while (_context.Users.Any(u => u.ReferralCode == code));

            return code;
        }

        private static string GenerateResetToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private async Task<PasswordResetToken?> GetValidResetTokenAsync(string token)
        {
            return await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    !t.IsUsed &&
                    t.ExpiresAt > DateTime.UtcNow);
        }
    }
}
