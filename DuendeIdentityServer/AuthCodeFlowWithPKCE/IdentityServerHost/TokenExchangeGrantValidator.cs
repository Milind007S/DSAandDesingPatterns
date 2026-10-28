using Duende.IdentityServer.Validation;

public class TokenExchangeGrantValidator : IExtensionGrantValidator
{
    private readonly ITokenValidator _tokenValidator;
    
    public TokenExchangeGrantValidator(ITokenValidator tokenValidator)
    {
        _tokenValidator = tokenValidator;
    }

    // The custom grant_type string the client will send in the POST request
    public string GrantType => "urn:ietf:params:oauth:grant-type:token-exchange";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        // 1. Grab the original token the client wants to exchange
        var subjectToken = context.Request.Raw.Get("subject_token");

        if (string.IsNullOrWhiteSpace(subjectToken))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "subject_token missing");
            return;
        }

        // 2. Validate the original token signature and expiration
        var validationResult = await _tokenValidator.ValidateAccessTokenAsync(subjectToken);
        if (validationResult.IsError)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "subject_token invalid");
            return;
        }

        // 3. Extract the original human user's ID ('sub')
        var subClaim = validationResult.Claims.FirstOrDefault(c => c.Type == "sub");
        if (subClaim == null)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "subject_token lacks sub");
            return;
        }

        // 4. Success! Tell Duende to issue a new token mapped to the original human user
        context.Result = new GrantValidationResult(
            subject: subClaim.Value, 
            authenticationMethod: "token-exchange", 
            customResponse: null);
    }
}