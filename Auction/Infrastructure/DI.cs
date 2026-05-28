using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfastructureLayer(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("SqliteConnectionString");
            services.AddDbContext<AppDbContext>(x => x.UseSqlite(connectionString));
            services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
            services.AddScoped<DefaultDataHelper>();
            services.AddScoped<ITokenService>(provider =>
            {
                var jwtSection = configuration.GetSection("JwtSettings");
                var dbContext = provider.GetRequiredService<IAppDbContext>(); 
                return new JwtTokenService(jwtSection["SecretKey"]!.ToString(),
                    jwtSection["Audience"]!.ToString(),
                    jwtSection["Issuer"]!.ToString(), dbContext);
            });
            return services;
        }
    }
}
