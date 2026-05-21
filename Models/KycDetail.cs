using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class KycDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [MaxLength(12)]
        public string AadharNumber { get; set; } = string.Empty;

        [MaxLength(255)]
        public string AadharFrontUrl { get; set; } = string.Empty;

        [MaxLength(255)]
        public string AadharBackUrl { get; set; } = string.Empty;

        [MaxLength(10)]
        public string PanNumber { get; set; } = string.Empty;

        [MaxLength(255)]
        public string PanCardUrl { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankAccountHolderName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [MaxLength(11)]
        public string BankIfsc { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string BankPassbookUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [MaxLength(500)]
        public string RejectionReason { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }
}
