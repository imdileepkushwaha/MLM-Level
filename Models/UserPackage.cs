using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class UserPackage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [Required]
        public int PackageId { get; set; }

        [ForeignKey("PackageId")]
        public virtual Package Package { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal RoiPercentage { get; set; }

        [Required]
        public int RoiDurationDays { get; set; }

        [Required]
        public int DaysPaid { get; set; } = 0;

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime ActivationDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastPaidDate { get; set; }
    }
}
