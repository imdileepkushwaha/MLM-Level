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

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TdsPercent { get; set; } = 5.00m;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AdminChargePercent { get; set; } = 5.00m;

        [MaxLength(500)]
        public string CompanyQrCodeUrl { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string BankDetails { get; set; } = string.Empty;
        // --- General Settings ---
        [MaxLength(200)]
        public string SiteName { get; set; } = "Elite MLM Engine";

        [MaxLength(200)]
        public string SupportEmail { get; set; } = "support@mlm.com";

        [MaxLength(50)]
        public string MaintenanceMode { get; set; } = "Online";

        // --- SMTP Settings ---
        [MaxLength(200)]
        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        [MaxLength(200)]
        public string SmtpUsername { get; set; } = string.Empty;

        [MaxLength(200)]
        public string SmtpPassword { get; set; } = string.Empty;

        public bool SmtpEnableSsl { get; set; } = true;

        // --- SMS Gateway Settings ---
        [MaxLength(500)]
        public string SmsApiUrl { get; set; } = string.Empty;

        [MaxLength(500)]
        public string SmsApiKey { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SmsSenderId { get; set; } = string.Empty;

        // --- WhatsApp API Settings ---
        [MaxLength(500)]
        public string WhatsAppAccessToken { get; set; } = string.Empty;

        [MaxLength(200)]
        public string WhatsAppInstanceId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string WhatsAppProvider { get; set; } = string.Empty;

        // --- Homepage promo popup ---
        public bool HomePopupEnabled { get; set; } = false;

        [MaxLength(500)]
        public string HomePopupImageUrl { get; set; } = string.Empty;
    }
}

