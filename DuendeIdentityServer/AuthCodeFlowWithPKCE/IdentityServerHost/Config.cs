using Duende.IdentityServer.Models;

namespace IdentityServerHost;

public static class Config
{
    public static IEnumerable<Client> Clients =>
        [
            new Client
            {
                ClientId = "warehouse-web-portal",
                ClientName = "Warehouse Staff Web Portal",

                // Interactive human login flow
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true, // Enabled by default in Duende for GrantTypes.Code!

                ClientSecrets = { new Secret("portal-secret-hash".Sha256()) },

                // Where Duende is allowed to redirect the browser after login
                RedirectUris = { "https://localhost:5002/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },

                // Notice we now request OIDC Identity Scopes ("openid", "profile") 
                // PLUS OAuth API Scopes ("inventory.read")
                AllowedScopes = { "openid", "profile", "inventory.read" }
            }
        ];

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(), // Emits the mandatory 'sub' (Subject ID) claim
        new IdentityResources.Profile() // Emits profile claims like 'name', 'given_name', 'website'
    ];
}