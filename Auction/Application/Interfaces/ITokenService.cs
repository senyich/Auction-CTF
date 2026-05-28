using Application.Common;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ITokenService
    {
        string GetAccessToken(Guid userId);
        string GetRefreshToken(Guid userId);
        bool IsRefreshTokenEmptyOrExpired(string refreshToken, Guid userId);
        ClaimsPrincipal GetPrincipalFromExpiredToken(string? token);
    }
}
