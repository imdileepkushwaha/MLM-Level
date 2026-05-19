using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        public int? SponsorId { get; set; }

        [ForeignKey("SponsorId")]
        public virtual User? Sponsor { get; set; }

        public int? ParentId { get; set; }

        [ForeignKey("ParentId")]
        public virtual User? Parent { get; set; }

        [Required]
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ActivationDate { get; set; }

        [Required]
        public bool IsActive { get; set; } = false;

        [Required]
        public bool IsAdmin { get; set; } = false;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal WalletBalance { get; set; } = 0.00m;

        [Required]
        [MaxLength(50)]
        public string ReferralCode { get; set; } = string.Empty;

        public int? DefaultPlacementId { get; set; }

        [ForeignKey("DefaultPlacementId")]
        public virtual User? DefaultPlacement { get; set; }

        // Navigation Collections
        [InverseProperty("Sponsor")]
        public virtual ICollection<User> Referrals { get; set; } = new List<User>();

        [InverseProperty("Parent")]
        public virtual ICollection<User> PlacementDownlines { get; set; } = new List<User>();

        [InverseProperty("User")]
        public virtual ICollection<CommissionTran> CommissionsReceived { get; set; } = new List<CommissionTran>();

        [InverseProperty("FromUser")]
        public virtual ICollection<CommissionTran> CommissionsTriggered { get; set; } = new List<CommissionTran>();

        public virtual ICollection<ActivationRequest> ActivationRequests { get; set; } = new List<ActivationRequest>();

        public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
    }
}
