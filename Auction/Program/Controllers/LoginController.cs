using Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Application.Logic.User;
using Domain.Models;
using Application.Services;
using Program.ViewModels;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.RateLimiting;
using Auction;
using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Parbad.Internal;

namespace Program.Controllers
{
    [Route("authorization/[action]")]
    public class LoginController(LoginService loginService, IMediator mdtr,
        DataServerApiService apiService, ITokenService tokenService) : Controller
    {
        [HttpGet]
        [Route("/authorization")]
        public IActionResult Index([FromQuery] string? returnUrl)
        {
            return View("Index", returnUrl ?? "");
        }
        [HttpGet]
        public IActionResult Login([FromQuery] string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl ?? "/";
            return View(new LoginRequest(string.Empty, string.Empty));
        }
        [HttpPost]
        [EnableRateLimiting(StaticAttributes.AuthRateLimitName)]
        public async Task<IActionResult> Login([FromQuery]string? returnUrl, [FromForm]LoginRequest loginRequest)
        {
            if (!ModelState.IsValid)
                return View(loginRequest);
            var userIdResult = await loginService.TryLoginAsync(loginRequest.Username, loginRequest.Password);
            if (userIdResult.Failed)
            {
                ModelState.AddModelError("Validate", "Wrong password or login");
                return View(loginRequest);
            }
            var userId = userIdResult.Data!;

            var accessToken = tokenService.GetAccessToken(userId);
            var refreshToken = tokenService.GetRefreshToken(userId);

            HttpContext.Response.Cookies.Append(StaticAttributes.JwtAccessTokenName, accessToken);
            HttpContext.Response.Cookies.Append(StaticAttributes.JwtRefreshTokenName, refreshToken);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("User", "Index");
        }
        [HttpGet]
        public IActionResult Register([FromQuery] string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl ?? "/";
            return View(new RegisterRequest(string.Empty, string.Empty, string.Empty));
        }
        [HttpPost]
        [EnableRateLimiting(StaticAttributes.AuthRateLimitName)]
        public async Task<IActionResult> Register([FromQuery] string? returnUrl, [FromForm] RegisterRequest registerRequest)
        {
            if (!ModelState.IsValid)
                return View(registerRequest);
            var userIdResult = await loginService.TryRegisterAsync(registerRequest.Username, registerRequest.Password, registerRequest.Name);
            if (userIdResult.Failed)
            {
                ModelState.AddModelError("Validate", $"Username {registerRequest.Username} is already in use");
                return View(registerRequest);
            }
            var userId = userIdResult.Data!;

            var accessToken = tokenService.GetAccessToken(userId);
            var refreshToken = tokenService.GetRefreshToken(userId);

            HttpContext.Response.Cookies.Append(StaticAttributes.JwtAccessTokenName, accessToken);
            HttpContext.Response.Cookies.Append(StaticAttributes.JwtRefreshTokenName, refreshToken);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("User", "Index");
        }
        [Authorize]
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Response.Cookies.Delete(StaticAttributes.JwtAccessTokenName);
            HttpContext.Response.Cookies.Delete(StaticAttributes.JwtRefreshTokenName);
            return RedirectToAction("Index", "Home");
        }
        [Authorize]
        [HttpGet]
        public IActionResult LinkGameAccount()
        {
            ViewBag.SelectedPageName = "Account";
            return View();
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> LinkGameAccount(LoginGameRequest loginRequest)
        {
            Guid userId = HttpContext.User.GetUserId();
            if (!ModelState.IsValid)
                return View(loginRequest);
            var userGameIdResult = await apiService.RequestUserId(loginRequest.Username, loginRequest.Password);
            if (userGameIdResult.Failed)
            {
                ModelState.AddModelError("Validate", "Wrong password or login");
                return View(loginRequest);
            }
            var userGameId = userGameIdResult.Data!;
            await loginService.LinkUser(userId, userGameId);

            var accessToken = tokenService.GetAccessToken(userId);
            var refreshToken = tokenService.GetRefreshToken(userId);

            HttpContext.Response.Cookies.Append(StaticAttributes.JwtAccessTokenName, accessToken);
            HttpContext.Response.Cookies.Append(StaticAttributes.JwtRefreshTokenName, refreshToken);

            return RedirectToAction("Index", "currentUser");
        }
        [HttpGet]
        public IActionResult Refresh([FromQuery]string? returnUrl)
        {
            var refreshToken = HttpContext.Request.Cookies[StaticAttributes.JwtRefreshTokenName];
            var accessToken = HttpContext.Request.Cookies[StaticAttributes.JwtAccessTokenName];
            if (string.IsNullOrEmpty(refreshToken))
            {
                TempData["ErrorMessage"] = "Couldn't find the token";
                return Redirect("/authorization");
            }
            var userId = tokenService.GetPrincipalFromExpiredToken(accessToken).GetUserId();
            if (tokenService.IsRefreshTokenEmptyOrExpired(refreshToken, userId))
            {
                TempData["ErrorMessage"] = "Refresh token expired";
                return Redirect("/authorization");
            }

            var newAccessToken = tokenService.GetAccessToken(userId);
            var newRefreshToken = tokenService.GetRefreshToken(userId);

            HttpContext.Response.Cookies.Append(StaticAttributes.JwtAccessTokenName, newAccessToken);
            HttpContext.Response.Cookies.Append(StaticAttributes.JwtRefreshTokenName, newRefreshToken);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("User", "Index");
        }
    }
}
