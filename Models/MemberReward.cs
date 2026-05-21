using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class MemberReward
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Null = visible to all active members.</summary>
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string RewardType { get; set; } = "Bonus";

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
