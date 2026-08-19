namespace NHN.Power.API.Models
{
    public class AppSettings
    {
        public string SupabaseConnectionString { get; set; } = string.Empty;
        
        // JWT
        public string JwtSecret { get; set; } = string.Empty;
        public string JwtIssuer { get; set; } = string.Empty;
        public string JwtAudience { get; set; } = string.Empty;
        
        // R2 Storage
        public string R2AccountId { get; set; } = string.Empty;
        public string R2AccessKeyId { get; set; } = string.Empty;
        public string R2SecretAccessKey { get; set; } = string.Empty;
        public string R2BucketName { get; set; } = string.Empty;
        public string R2PublicBaseUrl { get; set; } = string.Empty;
        
        // SMTP
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string SmtpFromEmail { get; set; } = string.Empty;
        
        // SMS
        public string SmsProvider { get; set; } = "disabled";
        public string SmsApiKey { get; set; } = string.Empty;
    }
}
