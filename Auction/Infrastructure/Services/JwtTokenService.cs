using Application.Common;
using Application.Interfaces;
using Domain.Models;
using Microsoft.IdentityModel.Tokens;
using SQLitePCL;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Services;

public class JwtTokenService : ITokenService 
{
    private readonly IAppDbContext _context;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _refreshExpirationDateSeconds = 60*60;
    private readonly int _accessExpirationDateSeconds = 60;
    public JwtTokenService(string secretKey, string audience, string issuer, IAppDbContext context)
    {
        _secretKey = secretKey;
        _audience = audience;
        _issuer = issuer;
        _context = context;
    }
    public string GetAccessToken(Guid userId)
    { 
        var user= _context.Users.First(x => x.Id == userId);
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };
        if (user.OriginalId is not null)
            claims.Add(new Claim(StaticAttributes.HasLinkedAccountPolicyClaim, user.OriginalId.ToString()));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:  _issuer,
            audience:  _audience,
            signingCredentials: signingCredentials,
            expires: DateTime.UtcNow.AddSeconds(_accessExpirationDateSeconds),
            claims: claims
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return tokenString;
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string? token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
            ValidateLifetime = false
        };

        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken _);

        return principal;
    }
    public string GetRefreshToken(Guid userId)
    {
        var user = _context.Users.First(x => x.Id == userId);
        var prevToken = _context.RefreshTokens.FirstOrDefault(x => !x.IsExpired && x.User.Id == user.Id);
        if(prevToken is not null)
        {
            prevToken.Invalidate();
        }
        var newToken = new RefreshToken(Guid.NewGuid().ToString(), DateTime.UtcNow.Add(
            TimeSpan.FromSeconds(_refreshExpirationDateSeconds)), user);
        _context.RefreshTokens.Add(newToken);
        _context.SaveChanges();
        return newToken.Token;
    }

    public bool IsRefreshTokenEmptyOrExpired(string refreshToken, Guid userId)
    {
        var token = _context.RefreshTokens.FirstOrDefault(x => x.Token == refreshToken && x.User.Id == userId);
        if (token is null)
            return true;
        if (token.ExpiresAt < DateTime.UtcNow)
        {
            token.Invalidate();
            _context.SaveChanges();
            return true;
        }
        return false;
    }
}
