using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace NovaWallet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a valid JWT token for testing and evaluation (Mock Identity Provider).
    /// </summary>
    /// <param name="customerId">Customer ID to embed as the subject (sub) claim</param>
    /// <returns>Signed JWT token</returns>
    [HttpPost("token")]
    public IActionResult GenerateToken([FromQuery] string customerId = "CUST-FIRSTBANK-001")
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "FirstBankNovaPaySuperSecretKeyForDevelopmentAndEvaluationOnly_Minimum32Chars!";
        var issuer = jwtSettings["Issuer"] ?? "NovaPay.AuthService";
        var audience = jwtSettings["Audience"] ?? "NovaWallet.Api";
        var expiryMinutes = int.TryParse(jwtSettings["ExpiryMinutes"], out var mins) ? mins : 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customerId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("customerId", customerId),
            new(ClaimTypes.Role, "Customer")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer",
            expiresInSeconds = expiryMinutes * 60,
            customerId
        });
    }
}

