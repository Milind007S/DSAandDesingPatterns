using System.Security.Claims;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

[Route("[controller]/[action]")]
public class ExternalController : Controller
{
    // STEP 1 & 2: The user clicks the button on your login page
    [HttpGet]
    public IActionResult Challenge(string provider, string returnUrl)
    {
        // 'provider' will be "AzureAD"
        
        var props = new AuthenticationProperties
        {
            // Hop 2: Tells the middleware to send the browser here after /signin-oidc
            RedirectUri = Url.Action(nameof(Callback)), 
            
            // Stash the original returnUrl (e.g., /connect/authorize?client_id=...) 
            // so we can resume the flow at the very end.
            Items = { { "returnUrl", returnUrl } }
        };

        // Hands execution over to the AddOpenIdConnect middleware.
        return Challenge(props, provider);
    }

    // STEP 8 & 9: The user is back from Microsoft, sitting in the temporary cookie
    [HttpGet]
    public async Task<IActionResult> Callback()
    {
        // 1. Read the temporary "idsrv.external" cookie the middleware just created
        var result = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        if (result?.Succeeded != true)
        {
            throw new Exception("External authentication error");
        }

        // 2. Grab their Microsoft ID and Email
        var externalUser = result.Principal;
        var subjectIdClaim = externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                             externalUser.FindFirst("sub");
        
        var provider = result.Properties.Items["scheme"];
        var providerUserId = subjectIdClaim.Value;

        // (Optional) Here is where you would look up the user in your SQL database
        // e.g., var user = _db.Users.SingleOrDefault(u => u.ExternalId == providerUserId);

        // 3. Issue the REAL Duende session cookie
        var duendeUser = new IdentityServerUser(providerUserId)
        {
            DisplayName = externalUser.Identity?.Name,
            IdentityProvider = provider
        };
        await HttpContext.SignInAsync(duendeUser);

        // 4. Delete the temporary Microsoft holding cookie
        await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        // 5. Resume the original flow (redirect back to /connect/authorize)
        var returnUrl = result.Properties.Items["returnUrl"];
        return Redirect(returnUrl);
    }
}