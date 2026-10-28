using Duende.IdentityServer.Models;

namespace IdentityServerHost;

public static class Config
{
    // 1. Capabilities (What intent is the client requesting?)
    public static IEnumerable<ApiScope> ApiScopes =>
        [
            new ApiScope(name: "inventory.read", displayName: "Read access to Inventory"),
            new ApiScope(name: "inventory.write", displayName: "Write access to Inventory")
        ];

    // 2. Logical APIs (Which microservice is this token meant for?)
    // Putting Scopes inside an ApiResource ensures that when a client requests "inventory.read",
    // Duende adds "https://inventory.company.internal" to the 'aud' (Audience) claim of the JWT!
    public static IEnumerable<ApiResource> ApiResources =>
        [
            new ApiResource(name: "https://inventory.company.internal", displayName: "Inventory Microservice")
            {
                Scopes = { "inventory.read", "inventory.write" },
                // These claims will be copied into Access Tokens meant for this API
                UserClaims = { "role", "department" }
            }
        ];

    // 3. The Calling Applications (Who is asking for the token?)
    public static IEnumerable<Client> Clients =>
        [
            new Client
            {
                ClientId = "warehouse-worker-daemon",
                ClientName = "Warehouse Background Sync Service",

                // No interactive human user is present -> Client Credentials Flow
                AllowedGrantTypes = GrantTypes.ClientCredentials,

                // In production, never store raw secrets in code; hashes are compared at runtime
                ClientSecrets = { new Secret("super-high-entropy-secret-value".Sha256()) },

                // Principle of Least Privilege: this daemon can only read, not write
                AllowedScopes = { "inventory.read" },
                
                AccessTokenLifetime = 3600 // 1 hour in seconds
            }
        ];
}