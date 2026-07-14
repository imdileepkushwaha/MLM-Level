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

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Username or email is required")]
        [Display(Name = "Username or Email")]
        public string UsernameOrEmail { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;



        [MaxLength(500)]
        public string CompanyQrCodeUrl { get; set; } = string.Empty;

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
        public decimal IncomeWallet { get; set; }
        public decimal TotalIncome { get; set; }
        public int LedgerEntryCount { get; set; }
        public List<WithdrawalRequest> Withdrawals { get; set; } = new();
    }

    public class UserPackagesViewModel
    {
        public User UserInfo { get; set; } = null!;
        public MlmSetting Settings { get; set; } = new();
        public List<Package> AvailablePackages { get; set; } = new();
        public List<UserPackage> ActivePackages { get; set; } = new();
        public List<ActivationRequest> RecentRequests { get; set; } = new();
        public bool HasPendingRequest { get; set; }
        public ActivationRequest? PendingRequest { get; set; }
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

    public class AdminSettingsViewModel
    {
        public MlmSetting Settings { get; set; } = new MlmSetting();
        
        // Admin Profile
        public string AdminFullName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminUsername { get; set; } = string.Empty;

        // Password Update
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;

        public string ActiveTab { get; set; } = "general";
    }

    public class AdminNotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-bell";
        public string IconClass { get; set; } = "topbar-notif-icon-default";
        public string Action { get; set; } = string.Empty;
        public string Controller { get; set; } = "Admin";
        public DateTime CreatedAt { get; set; }
    }

    public class AdminNotificationsViewModel
    {
        public int TotalCount { get; set; }
        public List<AdminNotificationItem> Items { get; set; } = new();
    }

    public class MemberNotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-bell";
        public string IconClass { get; set; } = "topbar-notif-icon-default";
        public string Action { get; set; } = string.Empty;
        public string Controller { get; set; } = "User";
        public DateTime CreatedAt { get; set; }
    }

    public class MemberNotificationsViewModel
    {
        public int TotalCount { get; set; }
        public List<MemberNotificationItem> Items { get; set; } = new();
    }

    public class AdminRejectModalViewModel
    {
        public string ModalId { get; set; } = string.Empty;
        public string FormAction { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public string Title { get; set; } = "Reject request";
        public string? Subtitle { get; set; }
    }
}
