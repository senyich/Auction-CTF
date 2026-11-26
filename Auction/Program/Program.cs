using Application;
using Application.Common;
using Application.Interfaces;
using Application.Logic.User;
using Application.Services;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Parbad.Builder;
using Parbad.Gateway.ParbadVirtual;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

namespace Auction
{
    public class Program
    {
        public static void Main(string[] args)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            var builder = WebApplication.CreateBuilder(args);
            var jwtSection = builder.Configuration.GetSection("JwtSettings");


            builder.Services.AddControllersWithViews();
            builder.Services.AddInfastructureLayer(builder.Configuration);

            builder.Services.AddApplicationLayer();

            builder.Services.AddTransient<LoginService>();
            builder.Services.AddTransient<DefaultDataHelper>();
            builder.Services.AddTransient<LotsManagementAndPaymentService>();
            builder.Services.AddScoped<CmdService>();
            builder.Services.AddHostedService<CmdListenerService>();
            builder.Services.AddSingleton<BanService>();
            var dataServerUrl = builder.Configuration["DataServer:BaseUrl"] ?? StaticAttributes.DataServerHostName;
            builder.Services.AddTransient(_ => new DataServerApiService(dataServerUrl));
            builder.Services.AddSwaggerGen();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxConcurrentConnections = 100;
                options.Limits.MaxConcurrentUpgradedConnections = 100;
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => RateLimitPartition.GetSlidingWindowLimiter(
                        context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 120,
                            Window = TimeSpan.FromSeconds(60),
                            SegmentsPerWindow = 6
                        }));
                options.AddFixedWindowLimiter(StaticAttributes.AuthRateLimitName, options =>
                {
                    options.PermitLimit = 3;
                    options.Window = TimeSpan.FromSeconds(15);
                });
            });
            builder.Services.AddAuthentication(options =>{
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"]!.ToString(),
                    ValidAudience = jwtSection["Audience"]!.ToString(),
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!.ToString())),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents()
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies[StaticAttributes.JwtAccessTokenName];
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        if (context.Handled) return;
                        context.HandleResponse();
                        await Console.Out.WriteLineAsync(context.HttpContext.Request.Path + context.HttpContext.Request.QueryString);
                        context.Response.Redirect(QueryHelpers.AddQueryString("/authorization/refresh", 
                            "returnUrl", context.HttpContext.Request.Path+context.HttpContext.Request.QueryString));
                    }
                };
            });
            builder.Services.AddAuthorization(opts =>
            {
                opts.AddPolicy(StaticAttributes.HasLinkedAccountPolicyName, p =>
                {
                    p.RequireClaim(StaticAttributes.HasLinkedAccountPolicyClaim);
                });
            }); 
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddParbad()
                .ConfigureGateways(gateways =>
                {
                    gateways.AddParbadVirtual().WithOptions(options => options.GatewayPath = "/virtual-payment");
                })
                .ConfigureStorage(storage =>
                {
                    storage.UseMemoryCache();
                });

            builder.Services.AddMemoryCache();

            var app = builder.Build();

            //app.UseMiddleware<DdosProtectionMiddleware>();

            app.UseParbadVirtualGateway();

            app.Use(async (c, next) =>
            {
                Console.WriteLine($"{c.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown"} >> {c.Request.Path}");
                if (c.Request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    //await c.Response.WriteAsync("Please, do not use X-Forwarded-For header");
                }
                await next.Invoke();
            });

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseRouting();
            app.UseStatusCodePages(async context =>
            {
                var response = context.HttpContext.Response;
                if (response.StatusCode == StatusCodes.Status403Forbidden)
                {
                    response.Redirect("/unauthorized");
                }
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseRateLimiter();

            app.MapControllers();

            app.UseStaticFiles();

            app.Run();
        }
    }
}
public class DefaultDataHelper
{
    private readonly AppDbContext _context;
    private readonly LoginService _ls;
    public DefaultDataHelper(AppDbContext context, LoginService ls)
    {
        _context = context;
        _ls = ls;
    }
    public void ResetDatabase()
    {
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
    }
    public void AddDefaultData()
    {
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
        _context.Users.First(x => x.Id == _ls.TryRegisterAsync("1", "a", "1").Result.Data)
            .Currencies.AddRange(new List<WalletCurrency>(){
                new WalletCurrency(1000, CurrencyType.RUB),
                new WalletCurrency(100000, CurrencyType.BTC)
            });
        _context.Users.First(x => x.Id == _ls.TryRegisterAsync("2", "dani", "1").Result.Data)
            .Currencies.AddRange(new List<WalletCurrency>(){
                new WalletCurrency(1000, CurrencyType.RUB)
            });
        _context.Users.First(x => x.Id == _ls.TryRegisterAsync("3", "sb", "1").Result.Data)
            .Currencies.AddRange(new List<WalletCurrency>(){
                new WalletCurrency(1000, CurrencyType.RUB)
            });
        _context.Users.First(x => x.Id == _ls.TryRegisterAsync("4", "s", "1").Result.Data)
            .Currencies.AddRange(new List<WalletCurrency>(){
                new WalletCurrency(1000, CurrencyType.RUB)
            });
        var user1 = new User("u1", DateTime.Now, "u1 n", "erg", new List<WalletCurrency>() { new WalletCurrency(1111, CurrencyType.RUB)});
        var user2 = new User("u2", DateTime.Now, "u2 n", "egr", new List<WalletCurrency>() { new WalletCurrency(10, CurrencyType.RUB) });
        var item1 = new Item("1", "Item 1", "Item 1 desc", ItemType.Usual, user2, "https://fb.ru/misc/i/gallery/10682/1225582.jpg");
        var item2 = new Item("2", "Item 2", "Item 2 desc", ItemType.GameSkin, user2, "/images/items/csgo.png");
        var item3 = new Item("3", "Item 3", "Item 3 desc", ItemType.Usual, user1);
        var lot1 = new Lot(item1, DateTime.Now, TimeSpan.FromSeconds(50), 
            new Money(10, CurrencyType.RUB), item1.Owner,
            new Money(10000, CurrencyType.BTC));
        var lot2 = new Lot(item2, DateTime.Now, TimeSpan.FromSeconds(150),
            new Money(100, CurrencyType.RUB), item2.Owner);
        _context.Users.Add(user1);
        _context.Users.Add(user2);
        _context.Items.Add(item1);
        _context.Items.Add(item2);
        _context.Items.Add(item3);
        _context.Lots.Add(lot1);
        _context.Lots.Add(lot2);
        _context.SaveChanges();
    }
}
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId)
            ? userId : Guid.Empty;
    }
    public static string? GetLinkedAccountId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("linked_account_id");
    }
}
