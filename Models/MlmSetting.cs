using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLM_Level.Models
{
    public class MlmSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Level1Commission { get; set; } = 10.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Level2Commission { get; set; } = 5.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Level3Commission { get; set; } = 3.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Level4Commission { get; set; } = 2.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Level5Commission { get; set; } = 1.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MinWithdrawalLimit { get; set; } = 500.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal WithdrawalFeePercent { get; set; } = 5.00m;

        [MaxLength(500)]
        public string CompanyQrCodeUrl { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string BankDetails { get; set; } = string.Empty;
    }
}
