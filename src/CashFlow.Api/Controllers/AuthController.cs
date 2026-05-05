using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CashFlow.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public AuthController(IConfiguration configuration) => _configuration = configuration;

    [HttpPost("token")]
    public IActionResult Token([FromBody] LoginRequest request)
    {
        if (request.Username != "admin" || request.Password != "admin123") return Unauthorized();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: new[] { new Claim(ClaimTypes.Name, request.Username), new Claim("scope", "cashflow.write") },
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}
