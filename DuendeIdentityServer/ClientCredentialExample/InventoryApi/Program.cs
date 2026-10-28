using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Base URL of our Duende IdentityServerHost
        // On startup, JwtBearerMiddleware fetches /.well-known/openid-configuration
        // and caches the public signing keys from the jwks_uri.
        options.Authority = "https://localhost:5001";

        // Matches the ApiResource name! Rejects tokens meant for other microservices.
        options.Audience = "https://inventory.company.internal";

        // Prevents Microsoft's legacy SOAP claim-type mapping from renaming 'sub' to a long XML schema URI
        options.MapInboundClaims = false;

        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanReadInventory", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "inventory.read");
    });

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/stock", (HttpContext ctx) =>
{
    var clientId = ctx.User.FindFirst("client_id")?.Value;
    return Results.Ok(new { Status = "Stock Healthy", Caller = clientId });
}).RequireAuthorization("CanReadInventory");

app.Run();