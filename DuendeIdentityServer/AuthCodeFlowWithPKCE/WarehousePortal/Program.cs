var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
    {
        // 1. Use a local cookie to remember the user AFTER they log in
        options.DefaultScheme = "Cookies";
        // 2. When an unauthenticated user hits a protected page, trigger OIDC redirect to Duende
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie("Cookies")
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = "https://localhost:5001"; // Duende URL

        options.ClientId = "warehouse-web-portal";
        options.ClientSecret = "portal-secret-hash";
        
        // Use Authorization Code Flow!
        options.ResponseType = "code";
        
        // Enabled by default in .NET! Generates code_verifier, encrypts it in the 
        // correlation state cookie, sends code_challenge, and redeems code_verifier.
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("inventory.read");

        // Saves the ID Token and Access Token inside the local encrypted auth cookie
        options.SaveTokens = true;
    });
