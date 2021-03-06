﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySSO.EF.Context;
using MySSO.Entity.Entities;
using MySSO.Services.Services;
using System;
using IdentityServer4;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.Interfaces;
using MySSO.EF.Context.Persisted;
using IdentityServer4.EntityFramework.Options;
using IdentityServer4.EntityFramework.DbContexts;
using MySSO.EF.Context.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MySSO.Configuration
{
    public static class ConfigureDbContext
    {
        public static void ConfigureOgunIdsServices(this IServiceCollection services, IConfiguration configuration)
        {
            //Configure Providers
            var dataProtectionProviderType = typeof(DataProtectorTokenProvider<ApplicationUser>);
            var phoneNumberProviderType = typeof(PhoneNumberTokenProvider<ApplicationUser>);
            var emailTokenProviderType = typeof(EmailTokenProvider<ApplicationUser>);


            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddSingleton(new ConfigurationStoreOptions());
            services.AddScoped<IConfigurationDbContext, IdentityConfigurationDbContext>();
            services.AddDbContext<IdentityConfigurationDbContext>(
                options => options.UseSqlServer(connectionString,
                                                op => op.EnableRetryOnFailure(maxRetryCount: 5,
                                                                              maxRetryDelay: TimeSpan.FromSeconds(30),
                                                                              errorNumbersToAdd: null)));
            services.AddDbContext<ConfigurationDbContext>(options => options.UseSqlServer(connectionString));

            services.AddSingleton(new OperationalStoreOptions { EnableTokenCleanup = false });
            services.AddScoped<IPersistedGrantDbContext, IdentityPersistedGrantDbContext>();
            services.AddDbContext<IdentityPersistedGrantDbContext>(
                options => options.UseSqlServer(connectionString,
                                                op => op.EnableRetryOnFailure(maxRetryCount: 5,
                                                                              maxRetryDelay: TimeSpan.FromSeconds(30),
                                                                              errorNumbersToAdd: null))
            );
            services.AddDbContext<PersistedGrantDbContext>(options => options.UseSqlServer(connectionString));
            services.AddDbContext<IdentityServerDbContext>(options => options.UseSqlServer(connectionString));
            services.AddIdentity<ApplicationUser, IdentityRole>(x =>
            {
                x.Lockout.MaxFailedAccessAttempts = 5;
                x.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                x.Lockout.AllowedForNewUsers = true;
                x.User.RequireUniqueEmail = true;
                x.Password.RequiredLength = 6;
                x.Password.RequireDigit = false;
                x.Password.RequireLowercase = false;
                x.Password.RequireNonAlphanumeric = false;
                x.Password.RequireUppercase = false;
            })
                .AddEntityFrameworkStores<IdentityServerDbContext>()
                .AddTokenProvider(TokenOptions.DefaultProvider, dataProtectionProviderType)
                .AddTokenProvider<EmailTokenProvider<ApplicationUser>>("Email");

            ////Configure Identity server SQL connections and persist
            services.AddIdentityServer(opt =>
            {
                opt.Csp.Level = IdentityServer4.Models.CspLevel.One;
                opt.Csp.AddDeprecatedHeader = true;
                opt.Caching.ClientStoreExpiration = TimeSpan.MaxValue;
                opt.Caching.ResourceStoreExpiration = TimeSpan.MaxValue;
                opt.Caching.CorsExpiration = TimeSpan.MaxValue;
                opt.EmitLegacyResourceAudienceClaim = true;
                opt.AccessTokenJwtType = "JWT";
            })
                //.AddSigningCredential("CN=sts")
                .AddDeveloperSigningCredential()
                 .AddConfigurationStore(options =>
                 {
                     options.ConfigureDbContext = dbBuilder => dbBuilder.UseSqlServer(connectionString, SetSqlContextOptions());
                 })
                .AddAspNetIdentity<ApplicationUser>()
                .AddProfileService<SSOUserProfileService>();
        }

        public static Action<SqlServerDbContextOptionsBuilder> SetSqlContextOptions()
        {
            return (SqlServerDbContextOptionsBuilder builder) =>
            {
                builder.EnableRetryOnFailure(maxRetryCount: 5,
                                             maxRetryDelay: TimeSpan.FromSeconds(30),
                                             errorNumbersToAdd: null);
                builder.UseRelationalNulls(true);
            };
        }
    }
}
