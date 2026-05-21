using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class ActivationRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public int? PackageId { get; set; }

        [ForeignKey("PackageId")]
        public virtual Package? Package { get; set; }

        [Required]
        [MaxLength(100)]
        public string TransactionReference { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedDate { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [MaxLength(255)]
        public string? PaymentSlipUrl { get; set; }
    }
}
