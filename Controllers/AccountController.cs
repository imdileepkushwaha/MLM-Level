using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;
using MLM_Level.Models;

namespace MLM_Level.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // GET: Account/Login
        [HttpGet]
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

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

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
        public IActionResult Register(string? sponsorCode = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Index", "Admin");
                else
                    return RedirectToAction("Index", "User");
            }

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

            TempData["SuccessMessage"] = "Registration successful! Please login and activate your package.";
            return RedirectToAction("Login");
        }

        // GET: Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
    }
}
