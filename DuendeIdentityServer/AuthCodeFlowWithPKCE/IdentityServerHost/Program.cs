using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb");

builder.Services.AddIdentityServer()
    // 1. Replaces AddInMemoryClients / AddInMemoryApiScopes
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));
    })
    // 2. Replaces in-memory grant stores for Refresh Tokens and Auth Codes
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));
        
        // Automatically cleans expired refresh tokens out of SQL Server every 60 seconds
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 60; 
    })
    .AddDeveloperSigningCredential() // (We will replace this with Automatic Key Management on Day 9!)
    // .AddInMemoryIdentityResources(Config.IdentityResources)
    // .AddInMemoryApiScopes(Config.ApiScopes)
    // .AddInMemoryClients(Config.Clients);
    .AddExtensionGrantValidator<TokenExchangeGrantValidator>();

// Inside IdentityServerHost/Program.cs
builder.Services.AddAuthentication()
    .AddOpenIdConnect("AzureAD", "Microsoft Entra ID", options =>
    {
        // Microsoft's OpenID Connect Discovery URL
        options.Authority = "https://login.microsoftonline.com/tenant-id/v2.0";
        options.ClientId = "duende-app-client-id-in-azure";
        options.ClientSecret = "azure-secret-value";
        options.ResponseType = "code";
        
        // CRITICAL: We tell the Microsoft middleware to drop the user's Microsoft identity
        // into a TEMPORARY holding cookie inside Duende called "idsrv.external".
        // This gives Duende a chance to inspect the Microsoft claims before logging the user in.
        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
    });


var app = builder.Build();