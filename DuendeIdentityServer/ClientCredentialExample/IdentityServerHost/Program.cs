using IdentityServerHost;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityServer(options =>
    {
        // Emits standard OIDC events to ILogger for auditing and SIEM ingestion
        options.EmitStaticAudienceClaim = true;
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;
    })
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryClients(Config.Clients);
    // Note: In Development, Duende automatically registers a temporary signing key 
    // (tempkey.jwk) so you can inspect JWTs immediately.

var app = builder.Build();

app.UseHttpsRedirection();

// Registers the Discovery endpoint, JWKS endpoint, Token endpoint, etc.
app.UseIdentityServer();

app.Run();