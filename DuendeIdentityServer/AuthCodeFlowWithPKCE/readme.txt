In OAuth/OIDC, direct server-to-server communication is called the Back-Channel (high trust).

Anything that travels through the user's browser address bar, browser history, or mobile OS deep-links is called the Front-Channel (low trust).


Authorization Code Flow with PKCE (Proof Key for Code Exchange):

    1. Front-Channel: Duende sends a temporary, one-time-use Authorization Code via the browser redirect.

    2. Back-Channel: Your app exchanges that code for the real tokens (ID Token + Access Token) over a direct POST /connect/token call.


How PKCE Stops Authorization Code Interception
    What if a malicious app on your phone intercepts that one-time Authorization Code from the Front-Channel redirect and tries to call /connect/token before the legitimate app does? PKCE stops this in three steps:

    Before redirecting the user to Duende, the Client app generates a random one-time secret in memory called the code_verifier.

    The Client hashes it with SHA-256 to create a code_challenge, and sends only the hash (code_challenge) to Duende in the initial browser redirect (/connect/authorize).

    When the Client exchanges the Authorization Code at /connect/token, it sends the raw, unhashed code_verifier. Duende hashes it with SHA-256 and checks if it matches the code_challenge from step 2.


**********************************************************************************************


Let’s wipe the slate clean and strip away the buzzwords.

    Imagine our Warehouse Web Portal (localhost:5002) is an Office Building, and Duende IdentityServer (localhost:5001) is the Central Security Desk across the street.

    Here is the core problem:
    The Office Building (Web Portal) does not have a database of usernames and passwords. Only the Central Security Desk (Duende) knows how to check passwords. However, once the Central Security Desk verifies who you are, the Office Building doesn't want to make you walk back across the street to the Security Desk on every single page click!

    That is why the Web Portal uses two different tools in Program.cs:

    AddOpenIdConnect("oidc") (The Messenger to the Security Desk): Used only once when you are not logged in yet. Its job is to take you across the street to Duende, handle the PKCE security check, and bring back your verified identity and tokens.

    AddCookie("Cookies") (The Local Wristband): Once oidc brings back your verified identity, "Cookies" prints a local encrypted session cookie for localhost:5002. For the next 8 hours, every time you click a page on the Web Portal, the Portal just checks your local wristband ("Cookies") in 1 millisecond without talking to Duende at all.

The Complete 6-Step Movie (From First Click to Logged In)

    Let’s trace exactly what happens when an unauthenticated user tries to open https://localhost:5002/dashboard:

    Step 1: The Door Check (AddCookie fails, AddOpenIdConnect steps in)
    Your browser requests GET https://localhost:5002/dashboard.

    The Portal checks DefaultScheme = "Cookies": "Does this browser have a valid Portal Session Cookie?" No.

    So the Portal triggers DefaultChallengeScheme = "oidc".

    Step 2: Preparing the PKCE Secret (AddOpenIdConnect before redirect)
    Before sending your browser to Duende, the Portal (oidc handler) creates a random secret password in memory:

    code_verifier = "secret_abc_123"

    It hashes that secret with SHA-256:

    code_challenge = "HASH_999"

    Where does the Portal save secret_abc_123 while you are gone? It locks secret_abc_123 inside a temporary, encrypted cookie (CorrelationCookie) on your browser, and responds with a 302 Redirect to Duende (localhost:5001), passing only code_challenge=HASH_999 in the URL.

    Step 3: Logging in at Duende
    Your browser lands on Duende (https://localhost:5001/Account/Login).

    Duende remembers that whoever started this login sent code_challenge=HASH_999.

    You type your username and password into Duende's login form. Duende verifies them against its database.

    Step 4: The Claim Ticket (code=AUTH_CODE_123)
    Duende does not put your real tokens in the browser URL (that’s unsafe). Instead, Duende puts a one-time claim ticket (code=AUTH_CODE_123) in the URL and redirects your browser back to https://localhost:5002/signin-oidc?code=AUTH_CODE_123.

    Step 5: The Secret Handshake (Back-Channel Token Exchange)
    Your browser arrives back at https://localhost:5002/signin-oidc?code=AUTH_CODE_123 and automatically hands the Portal that temporary encrypted cookie from Step 2.

    The Portal decrypts its temporary cookie, pulls out code_verifier = "secret_abc_123", and deletes the temporary cookie.

    Now, behind the scenes (Server-to-Server, invisible to the browser), the Portal calls Duende's POST /connect/token endpoint and says:

    "Here is my claim ticket (AUTH_CODE_123) AND here is the original unhashed secret (secret_abc_123)."

    Duende hashes secret_abc_123, sees it equals HASH_999 from Step 2, and replies directly to the Portal server with the ID_Token (who the user is) and Access_Token (keycard for InventoryApi).

    Step 6: Issuing the Local Wristband (AddCookie takes over!)
    Now the oidc handler's job is finished. It hands the user's identity and tokens over to the "Cookies" handler (AddCookie).

    The "Cookies" handler creates the permanent Portal Session Cookie (.AspNetCore.Cookies) and sends you to /dashboard.

    On your next 50 page clicks on the Portal, .AddCookie() reads that session cookie automatically.

******************************************************************************************

The C#/.NET Bridge: Federation and Upstream IdPs
    Right now, if an employee logs into Duende (IdentityServerHost), they have to type a username and password that lives in our local SQL Server database (using ASP.NET Core Identity).

    But what if the company is acquired, and HR says: "Everyone must now log in using their Microsoft Azure AD (Entra ID) corporate account"?

    You do NOT rewrite WarehousePortal (localhost:5002).
    Instead, you configure Duende IdentityServer (localhost:5001) to act as a Federation Gateway.

    WarehousePortal redirects the browser to Duende.

    Duende displays a button: "Log in with Microsoft".

    The user clicks it. Duende acts as the Client to Microsoft, redirects the browser to Microsoft's login page, and waits for Microsoft to send back the user's Microsoft identity.

    Duende receives the Microsoft identity, maps it to a local user record in our SQL database, and mints its own Duende tokens to send back to WarehousePortal.

    WarehousePortal has no idea Microsoft was even involved. It only trusts Duende!

    SEV-2 INCIDENT: A junior dev tried to implement the Federation feature today. They set up the AddOpenIdConnect Microsoft configuration above.

    However, they noticed that after a user successfully authenticated at Microsoft and redirected back to Duende, Duende just threw a 500 Server Error instead of redirecting the user back to WarehousePortal!

    The junior dev assumed Duende would automatically log the user in just because Microsoft said they were valid.

    Your single task right now:
    Look at the options.SignInScheme configured above.
    When Microsoft redirects the user back to Duende, Duende puts the user's Microsoft claims (like name and email) into a temporary holding cookie called idsrv.external.

    Why doesn't Duende just automatically issue tokens to the Portal immediately upon seeing this cookie? What missing C# step must you write inside Duende's ExternalLoginCallback controller method to move the user from that temporary idsrv.external cookie into a real, logged-in Duende user session?

        step-by-step redirect sequence.

        1. The Trigger:
        The user is sitting on your custom Duende Login Page. They click a button that points to your C# controller: GET /External/Challenge?provider=AzureAD&returnUrl=/connect/authorize...

        2. The Handoff (Controller → Middleware):
        Your C# controller says, "Hey ASP.NET Core Middleware, I need you to handle Microsoft. When you are done, send the user back to my Callback method." You do this by returning Challenge(props, "AzureAD").

        3. The Outbound Redirect:
        The Middleware takes over. It builds a massive URL containing your Microsoft client_id and PKCE secrets, and sends a 302 Redirect to the browser.

        4 & 5. The Microsoft Sandbox:
        The browser leaves your server entirely. The user logs in at Microsoft. Once successful, Microsoft sends a 302 Redirect back to your server's default middleware route: https://localhost:5001/signin-oidc.

        6. The Checkpoint (Middleware):
        Your C# code is not involved here. The middleware catches the request at /signin-oidc. It talks to Microsoft over the back-channel, verifies the cryptographic signatures, and extracts the claims (Name, Email). It puts these claims into the idsrv.external cookie and drops it in the user's browser.

        7. The Internal Bounce:
        The middleware remembers the instruction from Step 2 ("send the user back to my Callback method"). So, the middleware sends another 302 Redirect to the browser, saying: "Go to https://localhost:5001/External/Callback".

        8. The Translation (Controller):
        The browser hits your C# Callback() method. Now you are back in control. You read the idsrv.external cookie, verify the user belongs in your database, and issue the real Duende session cookie (HttpContext.SignInAsync). You then delete the temporary idsrv.external cookie.

        9. The Final Destination:
        Your C# code redirects the user one last time back to the original returnUrl (which is Duende's /connect/authorize endpoint). Duende finishes its own protocol and sends the user back to WarehousePortal as a fully logged-in human.

******************************************************************************************

The C#/.NET Bridge: When OIDC and Client Credentials Aren't Enough
    Let's look at a complex microservice scenario:

    The Browser logs into WarehousePortal and gets an access_token (Scope: portal.read).

    WarehousePortal passes that access_token to InventoryApi.

    Now, InventoryApi needs to call BillingApi (Scope: billing.write) on behalf of the user to log an inventory cost charge.

    The Problem:
    The token InventoryApi received only has scope="portal.read". If InventoryApi passes that exact same token to BillingApi, BillingApi will reject it (403 Forbidden) because it requires scope="billing.write".

    Furthermore, InventoryApi shouldn't just use a generic Machine-to-Machine (ClientCredentials) token, because BillingApi needs to know which human user triggered the charge!

    The Solution: Token Exchange (RFC 8693) via IExtensionGrantValidatorInventoryApi can take the incoming token, call Duende's POST /connect/token endpoint, and say: "I am the InventoryApi client. Here is a valid token from a human user. Please 'exchange' it for a brand new token that has scope=billing.write, but keep the human's sub claim attached!"

    💻 Code Implementation: Custom IExtensionGrantValidator

******************************************************************************************

The C#/.NET Bridge: Bearer Tokens vs. Proof-of-Possession
    Up until today, every token we have issued has been a Bearer Token.

    Bearer Token (Like Cash): Whoever holds the token (the "bearer") can use it. If a hacker steals your $100 bill, they can spend it at the store. If a hacker uses an XSS attack to steal your JWT access_token from the WarehousePortal and sends it to InventoryApi, the API accepts it blindly.

    The modern OAuth 2.1 defense against this is DPoP (Demonstrating Proof-of-Possession).

    DPoP Token (Like a Credit Card): You can't just hand the cashier a stolen credit card; you have to know the PIN or sign the receipt to prove you own it.

    How DPoP works under the hood in Duende:

    When WarehousePortal starts up, it generates its own private/public key pair in memory.

    When the Portal asks Duende for a token (POST /connect/token), it includes its public key.

    Duende mints the access_token and actually bakes a hash of the Portal's public key directly inside the JWT!

    When the Portal calls InventoryApi, it doesn't just send the token. It uses its private key to cryptographically sign the HTTP request itself (the URL, the HTTP method, and a timestamp), creating a DPoP header.

    InventoryApi receives the request. It checks the token, sees the baked-in public key hash, and uses it to verify the signature on the HTTP request.

    💻 Code Implementation: Enabling DPoP

        In WarehousePortal/Program.cs, the Duende.AccessTokenManagement library makes this incredibly easy. You just add one line:

        C#
        builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            // Generates the browser/client keys and handles all the HTTP signing automatically!
            options.DPoPJsonWebKey = /* dynamically generated RSA/EC key */;
        });
        And on InventoryApi/Program.cs, you tell the JwtBearer middleware to expect DPoP:

        C#
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = "https://localhost:5001";
                options.Audience = "https://inventory.company.internal";
            });
            
        // Adds the middleware that checks the DPoP HTTP signature against the JWT!
        builder.Services.AddDPoP();

        Chaos Engineering Injector #3: Network Interception
        🚨 SEV-1 INCIDENT: A rogue employee installs a network packet sniffer on the corporate Wi-Fi.

        They manage to intercept an HTTP request going from WarehousePortal to InventoryApi. They successfully steal the JWT access_token out of the Authorization header.

        They immediately open Postman on their own laptop, paste the stolen token into the header, and try to call GET https://localhost:5003/api/stock.

        Your single task for Day 9:
        Assuming we have fully implemented DPoP as shown above, why will InventoryApi reject the rogue employee's Postman request, even though the JWT access_token they pasted is mathematically valid and unexpired?

            The rogue employee is missing two critical things: the private key and the DPoP Proof Header.

            Here is exactly what happens when that Postman request hits the API:

            The API checks the JWT: InventoryApi opens up the stolen access_token. Inside, it finds a special claim called cnf (confirmation). This claim contains the hash of WarehousePortal's public key.

            The API demands Proof: Because of the cnf claim, the API says: "Okay, whoever is sending me this token MUST prove they own the matching private key."

            The API checks the HTTP Headers: The API looks for a second header in the HTTP request called DPoP. This header must contain a cryptographic signature of the exact request being made (e.g., "I am making a GET request to /api/stock at exactly 10:04 AM"), signed by the private key.

            The Hacker Fails: The hacker's packet sniffer only captured data in transit over the Wi-Fi. The private key never travels over the network! It lives safely locked inside the memory of the WarehousePortal server.

            Because the hacker doesn't have the private key, they cannot mathematically generate a valid DPoP signature header for their Postman request. InventoryApi rejects the stolen token with a 401 Unauthorized.

            Token theft over the wire is completely neutralized.