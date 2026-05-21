using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [Required]
        [MaxLength(128)]
        public string Token { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public bool IsUsed { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
