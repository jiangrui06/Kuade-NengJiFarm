namespace WebAPI.Configuration
{
    /// <summary>
    /// JWT 配置类
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// 密钥（至少32个字符）
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// 签发者
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// 受众
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Token 过期时间（分钟）
        /// </summary>
        public int ExpirationMinutes { get; set; } = 1440;
    }
}