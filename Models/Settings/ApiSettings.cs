using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DulceAtardecer.Models.Settings;

public class ApiSettings
{
    public const string SectionName = "ApiSettings";
    public const int MinimumKeyBytes = 32;

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 10;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SecretKey));
}
