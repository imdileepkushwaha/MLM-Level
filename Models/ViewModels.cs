using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MLM_Level.Models
{
    // --- AUTHENTICATION VIEW MODELS ---
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or Email is required")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression("^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid Phone Number")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Password and Confirmation Password do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Sponsor Referral Code (Optional)")]
        public string? SponsorCode { get; set; }
    }

    // --- USER DASHBOARD VIEW MODELS ---
    public class UserDashboardViewModel
    {
        public User UserInfo { get; set; } = null!;
        public int DirectReferralCount { get; set; }
        public int TotalTeamCount { get; set; }
        public int ActiveTeamCount { get; set; }
        public decimal TotalEarnings { get; set; }
        public string ReferralLink { get; set; } = string.Empty;
        public List<CommissionTran> RecentTransactions { get; set; } = new();
        public List<ActivationRequest> ActivationRequests { get; set; } = new();
        public List<WithdrawalRequest> WithdrawalRequests { get; set; } = new();
    }

    public class UserDownlineViewModel
    {
        public User UserInfo { get; set; } = null!;
        public List<DownlineNodeViewModel> Level1 { get; set; } = new();
        public List<DownlineNodeViewModel> Level2 { get; set; } = new();
        public List<DownlineNodeViewModel> Level3 { get; set; } = new();
        public List<DownlineNodeViewModel> Level4 { get; set; } = new();
        public List<DownlineNodeViewModel> Level5 { get; set; } = new();
        public List<DownlineNodeViewModel> FullDownlineList { get; set; } = new();
    }

    public class DownlineNodeViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string SponsorName { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public int SponsorId { get; set; }
        public int? ParentId { get; set; }
        public int Level { get; set; } // Relative depth level (1 to 5)
        public bool IsActive { get; set; }
        public DateTime JoinedDate { get; set; }
    }

    public class UserWalletViewModel
    {
        public decimal WalletBalance { get; set; }
        public decimal TotalIncome { get; set; }
        public List<CommissionTran> IncomeLedger { get; set; } = new();
        public List<WithdrawalRequest> Withdrawals { get; set; } = new();
    }

    // --- ADMIN DASHBOARD VIEW MODELS ---
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCommissionsDistributed { get; set; }
        public int PendingActivationsCount { get; set; }
        public int PendingWithdrawalsCount { get; set; }
        public List<User> RecentUsers { get; set; } = new();
        public List<ActivationRequest> PendingActivations { get; set; } = new();
        public List<WithdrawalRequest> PendingWithdrawals { get; set; } = new();
    }

    public class AdminApprovalsViewModel
    {
        public List<ActivationRequest> ActivationRequests { get; set; } = new();
        public List<WithdrawalRequest> WithdrawalRequests { get; set; } = new();
    }

    public class AdminUserEditViewModel
    {
        public int Id { get; set; }
        
        [Required]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Phone { get; set; } = string.Empty;
        
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        
        [Range(0, 10000000)]
        public decimal WalletBalance { get; set; }
    }

    public class LeaderboardItemViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public decimal TotalEarned { get; set; }
    }
}
